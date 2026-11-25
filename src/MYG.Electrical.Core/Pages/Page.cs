using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Pages;

/// <summary>Projedeki tek çizim sayfası.</summary>
public sealed class Page
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; } = "Yeni Sayfa";
    public PageType Type { get; set; } = PageType.Schematic;
    public PageFormat Format { get; set; } = PageFormat.A3;
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 297;

    /// <summary>Antet alanlarında sayfaya özel değerler (proje değerlerini ezer).</summary>
    public Dictionary<string, string> TitleBlock { get; set; } = [];

    public List<DrawingObject> Objects { get; set; } = [];

    /// <summary>Bu sayfadaki kablolara girilen bilgiler (numara, renk, kesit...).</summary>
    public List<Devices.WireData> WireData { get; set; } = [];

    public Devices.WireData? FindWireData(PinRef a, PinRef b) => WireData.FirstOrDefault(w => w.Connects(a, b));

    [JsonIgnore]
    public Rect2D Bounds => new(0, 0, Width, Height);

    public static Page Create(string name, PageType type, PageFormat format = PageFormat.A3,
        PageOrientation orientation = PageOrientation.Landscape)
    {
        var page = new Page { Name = name, Type = type };
        page.SetFormat(format, orientation);
        return page;
    }

    public void SetFormat(PageFormat format, PageOrientation orientation)
    {
        Format = format;
        Orientation = orientation;
        (Width, Height) = PageSizes.Get(format, orientation);
    }

    public DrawingObject? Find(Guid id) => Objects.FirstOrDefault(o => o.Id == id);

    public static string NewId() => "page-" + Guid.NewGuid().ToString("N")[..8];
}
