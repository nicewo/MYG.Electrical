using System.Windows;
using System.Windows.Media;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Drawing;

/// <summary>Çekirdek geometri tipleri ↔ WPF tipleri dönüşümleri.</summary>
public static class WpfConvert
{
    public static Point ToPoint(Point2D p) => new(p.X, p.Y);
    public static Point2D ToPoint2D(Point p) => new(p.X, p.Y);
    public static Rect ToRect(Rect2D r) => new(r.X, r.Y, Math.Max(0, r.Width), Math.Max(0, r.Height));

    public static Color ToColor(uint argb) =>
        Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    /// <summary>Dünya (mm) → ekran dönüşümünü WPF matrisine çevirir.</summary>
    public static Matrix ToMatrix(ViewTransform view) => new(view.Scale, 0, 0, view.Scale, view.OffsetX, view.OffsetY);
}
