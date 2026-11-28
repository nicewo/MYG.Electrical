using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Rendering;

namespace MYG.Electrical.Core.Model;

/// <summary>
/// Bilgi alanı: değeri proje bilgisinden (firma, proje adı, müşteri, tarih, logo...) okunan, taşınabilir ve
/// boyutlandırılabilir metin. Kapak sayfası tasarımının yapı taşıdır; şema sayfalarına da konabilir.
/// Anahtarlar <see cref="Pages.TitleBlockFields"/> içindedir.
/// </summary>
public sealed class FieldObject : DrawingObject
{
    public const double DefaultHeightMm = 4.0;

    /// <summary>Referans noktası: hizalamaya göre metnin sol / orta / sağ ucu, dikeyde ortası.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Alan anahtarı (ör. "company", "customer", "logo").</summary>
    public string Field { get; set; } = Pages.TitleBlockFields.Project;

    /// <summary>Değerin önüne yazılan etiket (ör. "Müşteri"). <see cref="ShowLabel"/> kapalıysa yazılmaz.</summary>
    public string Label { get; set; } = string.Empty;
    public bool ShowLabel { get; set; }

    /// <summary>Yazı yüksekliği (mm). Logo alanında logonun yüksekliği.</summary>
    public double Height { get; set; } = DefaultHeightMm;
    public TextAlign Align { get; set; }
    public bool Bold { get; set; }

    /// <summary>Gizli alan çıktıda yer almaz; editörde soluk gösterilir ki yeniden açılabilsin.</summary>
    public bool Visible { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLogo => Field == Pages.TitleBlockFields.Logo;

    /// <summary>Son çizimde yazılan metin (seçim çerçevesi ve isabet testi için; kaydedilmez).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? RenderedText { get; set; }

    public override Point2D Anchor => new(X, Y);

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }

    public override DrawingObject CloneWithNewId() => new FieldObject
    {
        X = X, Y = Y, Field = Field, Label = Label, ShowLabel = ShowLabel, Height = Height, Align = Align, Bold = Bold, Visible = Visible,
    };

    /// <summary>Çizilecek metin: "Etiket: değer" ya da yalnız değer.</summary>
    public string Compose(string value) => ShowLabel && Label.Length > 0 ? $"{Label}: {value}" : value;
}
