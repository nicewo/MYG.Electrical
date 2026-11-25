using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Rendering;

public enum TextAlign
{
    Left,
    Center,
    Right,
}

/// <summary>Çizgi stili. Renk ARGB, kalınlık mm.</summary>
public readonly record struct StrokeStyle(uint Color, double Thickness, bool Dashed = false);

/// <summary>
/// Çizim hedefi soyutlaması. WPF (DrawingContext) ve PDF (XGraphics) ayrı ayrı uygular;
/// tüm koordinatlar sayfa mm'sidir. Metinde Y değeri satırın dikey ortasıdır.
/// </summary>
public interface ISceneSink
{
    void DrawLine(Point2D a, Point2D b, StrokeStyle stroke);
    void DrawRectangle(Rect2D rect, StrokeStyle? stroke, uint? fill = null);
    void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle? stroke, uint? fill = null);

    /// <summary>Açılar derece; 0° = +X, pozitif yön saat yönü (Y aşağı).</summary>
    void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, StrokeStyle stroke);

    /// <summary>Çokgen veya açık çoklu çizgi.</summary>
    void DrawPolygon(IReadOnlyList<Point2D> points, bool closed, StrokeStyle? stroke, uint? fill = null);

    void DrawText(string text, Point2D position, double height, uint color, TextAlign align = TextAlign.Left, bool bold = false);

    /// <summary>Dosyadan resim çizer (en-boy oranı korunur); dosya yoksa sessizce atlar.</summary>
    void DrawImage(string path, Rect2D rect);

    /// <summary>
    /// Bellekteki resmi dikdörtgene gerilerek çizer. <paramref name="cacheKey"/> aynı veri için sabit olmalıdır
    /// (kaynak kimliği); hedef çözülmüş resmi bu anahtarla önbelleğe alabilir.
    /// </summary>
    /// <returns>Resim çözülemediyse false (çağıran yer tutucu çizer).</returns>
    bool DrawImageData(string cacheKey, byte[] data, Rect2D rect);
}
