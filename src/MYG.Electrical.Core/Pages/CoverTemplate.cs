using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Rendering;

namespace MYG.Electrical.Core.Pages;

/// <summary>
/// Kapak şablonu: kapak sayfasının tasarımı (çizgiler, metinler, resimler ve proje bilgisinden dolan bilgi
/// alanları). Şablon bilgi değil yerleşim taşır: yeni projede alanlar o projenin bilgileriyle dolar.
/// Kullanıcı şablonları uygulama veri klasöründe saklanır; varsayılan şablon yeni projelerde kullanılır.
/// </summary>
public sealed class CoverTemplate
{
    /// <summary>Yerleşik şablonun adı (kullanıcı şablonu bu adı alamaz).</summary>
    public const string StandardName = "Standart";

    public string Name { get; set; } = StandardName;
    public PageFormat Format { get; set; } = PageFormat.A3;
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;
    public List<DrawingObject> Objects { get; set; } = [];

    /// <summary>Şablondaki resimlerin verisi (şablon tek dosya olarak taşınabilsin).</summary>
    public List<Resources.ProjectResource> Resources { get; set; } = [];

    /// <summary>Kapak sayfasının şu anki tasarımından şablon.</summary>
    public static CoverTemplate Capture(ProjectDocument doc, Page page, string name)
    {
        var objects = page.Objects.Select(o => o.CloneWithNewId()).ToList();
        var used = objects.OfType<ImageObject>().SelectMany(i => new[] { i.SourceResourceId, i.RenderResourceId }).ToHashSet(StringComparer.Ordinal);
        return new CoverTemplate
        {
            Name = name,
            Format = page.Format,
            Orientation = page.Orientation,
            Objects = objects,
            Resources = doc.Resources.Where(r => used.Contains(r.Id)).ToList(),
        };
    }

    /// <summary>Şablon nesnelerinin yeni kimlikli kopyaları; resim kaynakları projeye eklenir.</summary>
    public List<DrawingObject> Instantiate(ProjectDocument doc)
    {
        foreach (var r in Resources.Where(r => doc.FindResource(r.Id) is null))
        {
            doc.Resources.Add(r);
        }
        return Objects.Select(o => o.CloneWithNewId()).ToList();
    }

    /// <summary>Boş kapak sayfasına şablonu uygular (yeni proje, eski dosya dönüşümü).</summary>
    public void ApplyTo(ProjectDocument doc, Page page)
    {
        page.SetFormat(Format, Orientation);
        page.Objects.Clear();
        page.Objects.AddRange(Instantiate(doc));
    }

    /// <summary>
    /// Yerleşik "Standart" kapak: logo, firma, başlık, "ELEKTRİK PROJESİ" ve proje bilgileri tablosu
    /// (önceki sürümlerin otomatik kapağıyla aynı görünüm, ama her parçası düzenlenebilir).
    /// </summary>
    public static CoverTemplate Standard(PageFormat format = PageFormat.A3, PageOrientation orientation = PageOrientation.Landscape)
    {
        var page = Page.Create("Kapak", PageType.Cover, format, orientation);
        var frame = PageLayout.Frame(page);
        var area = new Rect2D(frame.Left, frame.Top, frame.Width, frame.Height - PageLayout.TitleBlockHeight);
        var cx = Math.Round(area.Center.X);
        var y = Math.Round(area.Top + area.Height * 0.12);
        const double row = 9;

        var objects = new List<DrawingObject>
        {
            new FieldObject { X = cx, Y = y + 20, Field = TitleBlockFields.Logo, Height = 40, Align = TextAlign.Center },
        };
        y += 40 + row;
        objects.Add(new FieldObject { X = cx, Y = y, Field = TitleBlockFields.Company, Height = 7, Align = TextAlign.Center, Bold = true });
        y += row * 2;
        objects.Add(new FieldObject { X = cx, Y = y, Field = TitleBlockFields.Title, Height = 12, Align = TextAlign.Center, Bold = true });
        y += 12 + row;
        objects.Add(new TextObject { X = cx, Y = y, Text = "ELEKTRİK PROJESİ", Height = 5.6, Align = TextAlign.Center });
        y += row * 2;

        (string Key, string Label)[] rows =
        [
            (TitleBlockFields.ProjectNumber, "Proje No"), (TitleBlockFields.Customer, "Müşteri"),
            (TitleBlockFields.PreparedBy, "Hazırlayan"), (TitleBlockFields.CheckedBy, "Kontrol Eden"),
            (TitleBlockFields.Date, "Tarih"), (TitleBlockFields.Revision, "Revizyon"),
        ];
        var tableW = Math.Min(area.Width * 0.6, 180);
        var labelW = Math.Round(tableW * 0.35);
        var left = Math.Round(cx - tableW / 2);
        var tableH = rows.Length * row;
        if (y + tableH > area.Bottom - row)
        {
            y = Math.Max(area.Top, area.Bottom - row - tableH);
        }
        objects.Add(new RectangleObject { X = left, Y = y, Width = tableW, Height = tableH, Thickness = 0.25 });
        objects.Add(new LineObject { X1 = left + labelW, Y1 = y, X2 = left + labelW, Y2 = y + tableH, Thickness = 0.25 });
        for (var i = 0; i < rows.Length; i++)
        {
            var top = y + i * row;
            if (i > 0)
            {
                objects.Add(new LineObject { X1 = left, Y1 = top, X2 = left + tableW, Y2 = top, Thickness = 0.25 });
            }
            var mid = top + row / 2;
            objects.Add(new TextObject { X = left + 2.4, Y = mid, Text = rows[i].Label, Height = 4 });
            objects.Add(new FieldObject { X = left + labelW + 2.4, Y = mid, Field = rows[i].Key, Label = rows[i].Label, Height = 4 });
        }
        return new CoverTemplate { Name = StandardName, Format = format, Orientation = orientation, Objects = objects };
    }
}
