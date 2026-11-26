using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Rendering;

/// <summary>
/// Çerçeve ve antet çizimini renk şemasına uyarlar: siyah çizgiler çerçeve rengine, siyah metinler metin rengine
/// çevrilir. Diğer renkler (gri etiketler, logo) olduğu gibi kalır.
/// </summary>
internal sealed class FrameColorSink(ISceneSink inner, ColorScheme colors) : ISceneSink
{
    private StrokeStyle Map(StrokeStyle s) => s.Color == DrawingStyles.Black ? s with { Color = colors.Frame } : s;
    private StrokeStyle? Map(StrokeStyle? s) => s is { } v ? Map(v) : null;

    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) => inner.DrawLine(a, b, Map(stroke));
    public void DrawRectangle(Rect2D rect, StrokeStyle? stroke, uint? fill = null) => inner.DrawRectangle(rect, Map(stroke), fill);
    public void DrawEllipse(Point2D center, double rx, double ry, StrokeStyle? stroke, uint? fill = null) => inner.DrawEllipse(center, rx, ry, Map(stroke), fill);
    public void DrawArc(Point2D center, double radius, double start, double sweep, StrokeStyle stroke) => inner.DrawArc(center, radius, start, sweep, Map(stroke));
    public void DrawPolygon(IReadOnlyList<Point2D> points, bool closed, StrokeStyle? stroke, uint? fill = null) => inner.DrawPolygon(points, closed, Map(stroke), fill);

    public void DrawText(string text, Point2D position, double height, uint color, TextAlign align = TextAlign.Left, bool bold = false) =>
        inner.DrawText(text, position, height, color == DrawingStyles.Black ? colors.Text : color, align, bold);

    public void DrawImage(string path, Rect2D rect) => inner.DrawImage(path, rect);
    public bool DrawImageData(string cacheKey, byte[] data, Rect2D rect) => inner.DrawImageData(cacheKey, data, rect);
}
