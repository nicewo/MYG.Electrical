using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Pages;

public enum PageFormat
{
    A4,
    A3,
    A2,
    A1,
}

public enum PageOrientation
{
    Landscape,
    Portrait,
}

public enum PageType
{
    Cover,
    Schematic,
}

/// <summary>ISO 216 sayfa ölçüleri (mm, dikey yönelimde kısa × uzun kenar).</summary>
public static class PageSizes
{
    private static readonly Dictionary<PageFormat, (double Short, double Long)> Sizes = new()
    {
        [PageFormat.A4] = (210, 297),
        [PageFormat.A3] = (297, 420),
        [PageFormat.A2] = (420, 594),
        [PageFormat.A1] = (594, 841),
    };

    public static (double Width, double Height) Get(PageFormat format, PageOrientation orientation)
    {
        var (s, l) = Sizes[format];
        return orientation == PageOrientation.Landscape ? (l, s) : (s, l);
    }

    public static Rect2D GetBounds(PageFormat format, PageOrientation orientation)
    {
        var (w, h) = Get(format, orientation);
        return new Rect2D(0, 0, w, h);
    }
}
