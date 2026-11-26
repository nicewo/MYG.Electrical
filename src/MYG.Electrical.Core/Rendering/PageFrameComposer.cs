using System.Globalization;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Rendering;

/// <summary>Sayfa çerçevesi, kolon numaraları, antet ve kapak sayfası içeriğini çizer.</summary>
public static class PageFrameComposer
{
    private const double LabelHeight = 1.8;
    private const double ValueHeight = 3.2;
    private const double CellPadding = 1.2;
    private const double CoverTitleHeight = 12.0;
    private const double CoverCompanyHeight = 7.0;
    private const double CoverFieldHeight = 4.0;
    private const double CoverRowHeight = 9.0;
    private const double CoverLogoSize = 40.0;

    public static void Compose(ProjectInfo info, Page page, int pageNumber, int pageCount, ISceneSink sink, ColorScheme? colors = null)
    {
        if (colors is not null && !ReferenceEquals(colors, ColorScheme.Monochrome))
        {
            sink = new FrameColorSink(sink, colors);
        }
        var frame = PageLayout.Frame(page);
        sink.DrawRectangle(frame, DrawingStyles.Frame);

        if (page.Type == PageType.Cover)
        {
            ComposeCover(info, frame, sink);
        }
        else
        {
            ComposeColumns(frame, sink);
        }
        ComposeTitleBlock(info, page, pageNumber, pageCount, sink);
    }

    private static void ComposeColumns(Rect2D frame, ISceneSink sink)
    {
        var strip = new Rect2D(frame.Left, frame.Top, frame.Width, PageLayout.ColumnStripHeight);
        sink.DrawLine(new(strip.Left, strip.Bottom), new(strip.Right, strip.Bottom), DrawingStyles.Thin);
        var w = strip.Width / PageLayout.ColumnCount;
        for (var i = 0; i < PageLayout.ColumnCount; i++)
        {
            var x = strip.Left + i * w;
            if (i > 0)
            {
                sink.DrawLine(new(x, strip.Top), new(x, strip.Bottom), DrawingStyles.Thin);
            }
            sink.DrawText(i.ToString(CultureInfo.InvariantCulture), new(x + w / 2, strip.Center.Y), LabelHeight + 0.6, DrawingStyles.DarkGray, TextAlign.Center);
        }
    }

    private static void ComposeTitleBlock(ProjectInfo info, Page page, int pageNumber, int pageCount, ISceneSink sink)
    {
        var tb = PageLayout.TitleBlock(page);
        sink.DrawRectangle(tb, DrawingStyles.Frame);

        string F(string key) => TitleBlockFields.Resolve(info, page, key);
        var pageText = string.Create(CultureInfo.InvariantCulture, $"{pageNumber} / {pageCount}");

        // (ağırlık, üst hücre, alt hücre) — alt hücre null ise tek satırlık büyük hücre
        (double Weight, (string Label, string Value) Top, (string Label, string Value)? Bottom)[] columns =
        [
            (3.0, ("Firma", F(TitleBlockFields.Company)), null),
            (4.0, ("Proje", F(TitleBlockFields.Project)), ("Makine", F(TitleBlockFields.Machine))),
            (5.0, ("Sayfa Başlığı", F(TitleBlockFields.PageTitle)), null),
            (3.0, ("Müşteri", F(TitleBlockFields.Customer)), ("Proje No", F(TitleBlockFields.ProjectNumber))),
            (3.0, ("Hazırlayan", F(TitleBlockFields.PreparedBy)), ("Kontrol", F(TitleBlockFields.CheckedBy))),
            (2.2, ("Tarih", F(TitleBlockFields.Date)), ("Rev.", F(TitleBlockFields.Revision))),
            (1.6, ("Sayfa", pageText), null),
        ];

        var totalWeight = columns.Sum(c => c.Weight);
        var x = tb.Left;
        foreach (var (weight, top, bottom) in columns)
        {
            var w = tb.Width * weight / totalWeight;
            if (x > tb.Left)
            {
                sink.DrawLine(new(x, tb.Top), new(x, tb.Bottom), DrawingStyles.Thin);
            }
            if (bottom is { } b)
            {
                var half = tb.Height / 2;
                sink.DrawLine(new(x, tb.Top + half), new(x + w, tb.Top + half), DrawingStyles.Thin);
                Cell(new Rect2D(x, tb.Top, w, half), top, ValueHeight, sink);
                Cell(new Rect2D(x, tb.Top + half, w, half), b, ValueHeight, sink);
            }
            else
            {
                Cell(new Rect2D(x, tb.Top, w, tb.Height), top, ValueHeight * 1.4, sink);
            }
            x += w;
        }
    }

    private static void Cell(Rect2D r, (string Label, string Value) field, double valueHeight, ISceneSink sink)
    {
        sink.DrawText(field.Label, new(r.Left + CellPadding, r.Top + CellPadding + LabelHeight / 2), LabelHeight, DrawingStyles.MidGray);
        var maxChars = Math.Max(1, (int)((r.Width - 2 * CellPadding) / (valueHeight * Geometry.GeometryConstants.AverageCharWidthRatio)));
        var value = field.Value.Length > maxChars ? field.Value[..maxChars] : field.Value;
        sink.DrawText(value, new(r.Left + CellPadding, r.Top + (r.Height + LabelHeight) / 2 + CellPadding / 2), valueHeight, DrawingStyles.Black);
    }

    private static void ComposeCover(ProjectInfo info, Rect2D frame, ISceneSink sink)
    {
        var area = new Rect2D(frame.Left, frame.Top, frame.Width, frame.Height - PageLayout.TitleBlockHeight);
        var cx = area.Center.X;
        var y = area.Top + area.Height * 0.12;

        if (!string.IsNullOrWhiteSpace(info.LogoPath))
        {
            sink.DrawImage(info.LogoPath, new Rect2D(cx - CoverLogoSize / 2, y, CoverLogoSize, CoverLogoSize));
            y += CoverLogoSize + CoverRowHeight;
        }

        sink.DrawText(info.Company, new(cx, y), CoverCompanyHeight, DrawingStyles.DarkGray, TextAlign.Center, bold: true);
        y += CoverRowHeight * 2;
        sink.DrawText(string.IsNullOrWhiteSpace(info.MachineName) ? info.Name : info.MachineName, new(cx, y), CoverTitleHeight, DrawingStyles.Black, TextAlign.Center, bold: true);
        y += CoverTitleHeight + CoverRowHeight;
        sink.DrawText("ELEKTRİK PROJESİ", new(cx, y), CoverCompanyHeight * 0.8, DrawingStyles.MidGray, TextAlign.Center);
        y += CoverRowHeight * 2;

        (string Label, string Value)[] rows =
        [
            ("Proje No", info.ProjectNumber),
            ("Müşteri", info.Customer),
            ("Hazırlayan", info.PreparedBy),
            ("Kontrol Eden", info.CheckedBy),
            ("Tarih", info.Date),
            ("Revizyon", info.Revision),
        ];

        var tableW = Math.Min(area.Width * 0.6, 180);
        var labelW = tableW * 0.35;
        var left = cx - tableW / 2;
        var tableH = rows.Length * CoverRowHeight;
        if (y + tableH > area.Bottom - CoverRowHeight)
        {
            y = Math.Max(area.Top, area.Bottom - CoverRowHeight - tableH);
        }
        sink.DrawRectangle(new Rect2D(left, y, tableW, tableH), DrawingStyles.Thin);
        sink.DrawLine(new(left + labelW, y), new(left + labelW, y + tableH), DrawingStyles.Thin);
        for (var i = 0; i < rows.Length; i++)
        {
            var rowTop = y + i * CoverRowHeight;
            if (i > 0)
            {
                sink.DrawLine(new(left, rowTop), new(left + tableW, rowTop), DrawingStyles.Thin);
            }
            var mid = rowTop + CoverRowHeight / 2;
            sink.DrawText(rows[i].Label, new(left + CellPadding * 2, mid), CoverFieldHeight, DrawingStyles.MidGray);
            sink.DrawText(rows[i].Value, new(left + labelW + CellPadding * 2, mid), CoverFieldHeight, DrawingStyles.Black);
        }
    }
}
