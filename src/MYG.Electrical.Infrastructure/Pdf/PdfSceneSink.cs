using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Rendering;
using PdfSharp.Drawing;
using Serilog;

namespace MYG.Electrical.Infrastructure.Pdf;

/// <summary>Sahne şekillerini PDFsharp XGraphics üzerine vektörel olarak çizer. Koordinatlar mm.</summary>
internal sealed class PdfSceneSink(XGraphics gfx, ILogger logger) : ISceneSink
{
    private static readonly double[] DashPattern = [4, 3];
    private readonly Dictionary<(double Size, bool Bold), XFont> _fonts = [];
    private readonly Dictionary<string, XImage> _images = [];

    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) =>
        gfx.DrawLine(Pen(stroke), a.X, a.Y, b.X, b.Y);

    public void DrawRectangle(Rect2D rect, StrokeStyle? stroke, uint? fill = null)
    {
        var pen = stroke is { } s ? Pen(s) : null;
        var brush = fill is { } f ? new XSolidBrush(Color(f)) : null;
        if (pen is null && brush is null)
        {
            return;
        }
        gfx.DrawRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
    }

    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle? stroke, uint? fill = null)
    {
        var pen = stroke is { } s ? Pen(s) : null;
        var brush = fill is { } f ? new XSolidBrush(Color(f)) : null;
        if (pen is null && brush is null)
        {
            return;
        }
        gfx.DrawEllipse(pen, brush, center.X - radiusX, center.Y - radiusY, 2 * radiusX, 2 * radiusY);
    }

    public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, StrokeStyle stroke) =>
        gfx.DrawArc(Pen(stroke), center.X - radius, center.Y - radius, 2 * radius, 2 * radius, startAngle, sweepAngle);

    public void DrawText(string text, Point2D position, double height, uint color, TextAlign align = TextAlign.Left, bool bold = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var format = align switch
        {
            TextAlign.Center => XStringFormats.Center,
            TextAlign.Right => XStringFormats.CenterRight,
            _ => XStringFormats.CenterLeft,
        };
        gfx.DrawString(text, Font(height, bold), new XSolidBrush(Color(color)), new XPoint(position.X, position.Y), format);
    }

    public void DrawImage(string path, Rect2D rect)
    {
        if (!File.Exists(path))
        {
            logger.Warning("Logo dosyası bulunamadı: {Path}", path);
            return;
        }
        try
        {
            using var image = XImage.FromFile(path);
            // En-boy oranını koruyarak dikdörtgene sığdır
            var ratio = Math.Min(rect.Width / image.PointWidth, rect.Height / image.PointHeight);
            var w = image.PointWidth * ratio;
            var h = image.PointHeight * ratio;
            gfx.DrawImage(image, rect.X + (rect.Width - w) / 2, rect.Y + (rect.Height - h) / 2, w, h);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            logger.Warning(ex, "Logo PDF'e eklenemedi: {Path}", path);
        }
    }

    public bool DrawImageData(string cacheKey, byte[] data, Rect2D rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return true;
        }
        try
        {
            if (!_images.TryGetValue(cacheKey, out var image))
            {
                // PDFsharp akışı belge kaydedilene kadar okuyabilir: akış açık bırakılır, belge ile birlikte atılır.
                image = XImage.FromStream(new MemoryStream(data, writable: false));
                _images[cacheKey] = image;
            }
            gfx.DrawImage(image, rect.X, rect.Y, rect.Width, rect.Height);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            logger.Warning(ex, "Resim PDF'e eklenemedi: {Resource}", cacheKey);
            return false;
        }
    }

    private XFont Font(double height, bool bold)
    {
        var key = (Math.Round(height, 3), bold);
        if (!_fonts.TryGetValue(key, out var font))
        {
            font = new XFont(PdfFontResolver.FamilyName, height, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
            _fonts[key] = font;
        }
        return font;
    }

    private static XPen Pen(StrokeStyle s)
    {
        var pen = new XPen(Color(s.Color), s.Thickness)
        {
            LineCap = XLineCap.Round,
            LineJoin = XLineJoin.Round,
        };
        if (s.Dashed)
        {
            // Ekranla aynı desen: çizgi kalınlığının 4 katı dolu, 3 katı boş
            pen.DashPattern = DashPattern;
        }
        return pen;
    }

    private static XColor Color(uint argb) =>
        XColor.FromArgb((int)(argb >> 24), (int)((argb >> 16) & 0xFF), (int)((argb >> 8) & 0xFF), (int)(argb & 0xFF));
}
