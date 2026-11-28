using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Editing;

/// <summary>
/// Fareyle boyutlandırma (seçim çerçevesinin sağ alt tutamacı): metin ve bilgi alanında yazı boyu, resimde
/// en/boy oranı korunarak boyut, dikdörtgende genişlik/yükseklik. Sol üst köşe sabit kalır.
/// </summary>
public static class ObjectResize
{
    private const double MinTextHeight = 0.5;
    private const double MaxTextHeight = 200;
    private const double MinSize = 1;

    public static bool CanResize(DrawingObject obj) => obj is TextObject or FieldObject or ImageObject or RectangleObject;

    /// <summary>Boyutla ilgili değerlerin kopyası (geri alma için).</summary>
    public static double[] Capture(DrawingObject obj) => obj switch
    {
        TextObject t => [t.X, t.Y, t.Height],
        FieldObject f => [f.X, f.Y, f.Height],
        ImageObject i => [i.X, i.Y, i.Width, i.Height],
        RectangleObject r => [r.X, r.Y, r.Width, r.Height],
        _ => [],
    };

    public static void Restore(DrawingObject obj, double[] v)
    {
        switch (obj)
        {
            case TextObject t:
                (t.X, t.Y, t.Height) = (v[0], v[1], v[2]);
                break;
            case FieldObject f:
                (f.X, f.Y, f.Height) = (v[0], v[1], v[2]);
                break;
            case ImageObject i:
                (i.X, i.Y, i.Width, i.Height) = (v[0], v[1], v[2], v[3]);
                break;
            case RectangleObject r:
                (r.X, r.Y, r.Width, r.Height) = (v[0], v[1], v[2], v[3]);
                break;
        }
    }

    /// <summary>Başlangıçtaki sınırın sağ alt köşesi <paramref name="corner"/> noktasına çekilmiş gibi boyutlandırır.</summary>
    public static void Apply(DrawingObject obj, double[] original, Rect2D originalBounds, Point2D corner)
    {
        Restore(obj, original);
        var left = originalBounds.Left;
        var top = originalBounds.Top;
        switch (obj)
        {
            case RectangleObject r:
                r.Width = Math.Max(MinSize, corner.X - r.X);
                r.Height = Math.Max(MinSize, corner.Y - r.Y);
                return;
            case ImageObject i:
                var k = Math.Max((corner.X - i.X) / Math.Max(i.Width, MinSize), (corner.Y - i.Y) / Math.Max(i.Height, MinSize));
                k = Math.Max(k, MinSize / Math.Min(i.Width, i.Height));
                i.Width *= k;
                i.Height *= k;
                return;
        }
        // Metin / bilgi alanı: ölçek, sınırın yüksekliğinden; referans noktası sol üst köşeye göre ölçeklenir
        var height = Math.Max(originalBounds.Height, 0.1);
        var oldText = original[2];
        var scale = Math.Clamp((corner.Y - top) / height, MinTextHeight / oldText, MaxTextHeight / oldText);
        var x = left + (original[0] - left) * scale;
        var y = top + (original[1] - top) * scale;
        Restore(obj, [x, y, oldText * scale]);
    }
}
