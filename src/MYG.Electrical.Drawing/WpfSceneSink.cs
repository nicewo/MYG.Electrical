using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Rendering;

namespace MYG.Electrical.Drawing;

/// <summary>
/// Sahne şekillerini WPF DrawingContext'e çizer. Koordinatlar mm'dir; ekran dönüşümü
/// görselin üst katmanındaki matris ile render aşamasında uygulanır.
/// </summary>
public sealed class WpfSceneSink(DrawingContext dc) : ISceneSink
{
    private static readonly Typeface Regular = new(new FontFamily("Segoe UI, Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI, Arial"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly DashStyle Dash = CreateDash();
    private static readonly Dictionary<StrokeStyle, Pen> Pens = [];
    private static readonly Dictionary<uint, Brush> Brushes = [];
    private static readonly Dictionary<string, BitmapSource?> Images = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gömülü kaynaklar: anahtar içerikten türetildiği için önbellek hiç bayatlamaz.</summary>
    private static readonly Dictionary<string, BitmapSource?> DataImages = new(StringComparer.Ordinal);
    private const double PixelsPerDip = 1.0;

    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) =>
        dc.DrawLine(GetPen(stroke), WpfConvert.ToPoint(a), WpfConvert.ToPoint(b));

    public void DrawRectangle(Rect2D rect, StrokeStyle? stroke, uint? fill = null) =>
        dc.DrawRectangle(fill is { } f ? GetBrush(f) : null, stroke is { } s ? GetPen(s) : null, WpfConvert.ToRect(rect));

    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle? stroke, uint? fill = null) =>
        dc.DrawEllipse(fill is { } f ? GetBrush(f) : null, stroke is { } s ? GetPen(s) : null, WpfConvert.ToPoint(center), radiusX, radiusY);

    public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, StrokeStyle stroke)
    {
        static Point At(Point2D c, double r, double deg)
        {
            var rad = deg * Math.PI / 180.0;
            return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(center, radius, startAngle), isFilled: false, isClosed: false);
            ctx.ArcTo(At(center, radius, startAngle + sweepAngle), new Size(radius, radius), 0,
                Math.Abs(sweepAngle) > 180, sweepAngle >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, GetPen(stroke), geometry);
    }

    public void DrawText(string text, Point2D position, double height, uint color, TextAlign align = TextAlign.Left, bool bold = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            bold ? Bold : Regular, height, GetBrush(color), PixelsPerDip);
        var x = align switch
        {
            TextAlign.Center => position.X - ft.WidthIncludingTrailingWhitespace / 2,
            TextAlign.Right => position.X - ft.WidthIncludingTrailingWhitespace,
            _ => position.X,
        };
        dc.DrawText(ft, new Point(x, position.Y - ft.Height / 2));
    }

    public void DrawImage(string path, Rect2D rect)
    {
        if (LoadImage(path) is not { } img || img.Width <= 0 || img.Height <= 0)
        {
            return;
        }
        var ratio = Math.Min(rect.Width / img.Width, rect.Height / img.Height);
        var w = img.Width * ratio;
        var h = img.Height * ratio;
        dc.DrawImage(img, new Rect(rect.X + (rect.Width - w) / 2, rect.Y + (rect.Height - h) / 2, w, h));
    }

    public void DrawImageData(string cacheKey, byte[] data, Rect2D rect)
    {
        if (!DataImages.TryGetValue(cacheKey, out var img))
        {
            try
            {
                img = WpfImageCodec.LoadBitmap(data);
            }
            catch (Exception ex) when (ex is System.IO.IOException or NotSupportedException or InvalidOperationException or ArgumentException)
            {
                img = null;
            }
            DataImages[cacheKey] = img;
        }
        if (img is not null && rect.Width > 0 && rect.Height > 0)
        {
            dc.DrawImage(img, WpfConvert.ToRect(rect));
        }
    }

    /// <summary>Logo dosyası değiştiğinde önbelleği temizlemek için.</summary>
    public static void ClearImageCache() => Images.Clear();

    private static BitmapSource? LoadImage(string path)
    {
        if (Images.TryGetValue(path, out var cached))
        {
            return cached;
        }
        BitmapSource? result = null;
        if (System.IO.File.Exists(path))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(System.IO.Path.GetFullPath(path));
                bmp.EndInit();
                bmp.Freeze();
                result = bmp;
            }
            catch (Exception ex) when (ex is System.IO.IOException or NotSupportedException or UriFormatException or InvalidOperationException)
            {
                result = null;
            }
        }
        Images[path] = result;
        return result;
    }

    public static Pen GetPen(StrokeStyle s)
    {
        if (!Pens.TryGetValue(s, out var pen))
        {
            pen = new Pen(GetBrush(s.Color), s.Thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
                DashStyle = s.Dashed ? Dash : DashStyles.Solid,
            };
            pen.Freeze();
            Pens[s] = pen;
        }
        return pen;
    }

    public static Brush GetBrush(uint argb)
    {
        if (!Brushes.TryGetValue(argb, out var brush))
        {
            brush = new SolidColorBrush(WpfConvert.ToColor(argb));
            brush.Freeze();
            Brushes[argb] = brush;
        }
        return brush;
    }

    private static DashStyle CreateDash()
    {
        var d = new DashStyle([4.0, 3.0], 0);
        d.Freeze();
        return d;
    }
}
