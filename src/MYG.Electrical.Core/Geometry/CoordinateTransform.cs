using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Geometry;

/// <summary>
/// Tüm lokal → global koordinat dönüşümlerinin merkezi.
/// Dönüş açısı saat yönünde derece cinsindendir ve 90°'nin katlarına yuvarlanır (Y ekseni aşağı doğru).
/// </summary>
public static class CoordinateTransform
{
    /// <summary>Açıyı 0, 90, 180, 270 değerlerinden birine normalize eder.</summary>
    public static int NormalizeRotation(double degrees)
    {
        var quarterTurns = (int)Math.Round(degrees / GeometryConstants.RightAngle);
        var normalized = (quarterTurns % 4 + 4) % 4;
        return normalized * (int)GeometryConstants.RightAngle;
    }

    /// <summary>Lokal noktayı (orijin etrafında) döndürür ve ölçekler; ardından ekleme noktasına taşır.</summary>
    public static Point2D LocalToGlobal(Point2D local, Point2D origin, double rotationDegrees, double scale = 1.0)
    {
        var rotated = Rotate(local * scale, rotationDegrees);
        return origin + rotated;
    }

    /// <summary>Noktayı orijin etrafında saat yönünde döndürür (Y aşağı ekseninde görsel saat yönü).</summary>
    public static Point2D Rotate(Point2D p, double rotationDegrees) => NormalizeRotation(rotationDegrees) switch
    {
        90 => new Point2D(-p.Y, p.X),
        180 => new Point2D(-p.X, -p.Y),
        270 => new Point2D(p.Y, -p.X),
        _ => p,
    };

    /// <summary>Bağlantı noktası yönünü sembol dönüşüne göre döndürür.</summary>
    public static PinDirection RotateDirection(PinDirection direction, double rotationDegrees)
    {
        var steps = NormalizeRotation(rotationDegrees) / (int)GeometryConstants.RightAngle;
        var result = direction;
        for (var i = 0; i < steps; i++)
        {
            result = result switch
            {
                PinDirection.Right => PinDirection.Down,
                PinDirection.Down => PinDirection.Left,
                PinDirection.Left => PinDirection.Up,
                _ => PinDirection.Right,
            };
        }
        return result;
    }

    /// <summary>Yerel dikdörtgenin dönüştürülmüş global sınırlarını hesaplar.</summary>
    public static Rect2D TransformBounds(Rect2D local, Point2D origin, double rotationDegrees, double scale = 1.0)
    {
        Point2D[] corners =
        [
            new(local.Left, local.Top), new(local.Right, local.Top),
            new(local.Right, local.Bottom), new(local.Left, local.Bottom),
        ];
        return Rect2D.FromPoints(corners.Select(c => LocalToGlobal(c, origin, rotationDegrees, scale)));
    }

    /// <summary>Değeri en yakın grid adımına yuvarlar.</summary>
    public static double Snap(double value, double gridSpacing) =>
        gridSpacing <= 0 ? value : Math.Round(value / gridSpacing) * gridSpacing;

    public static Point2D Snap(Point2D p, double gridSpacing) => new(Snap(p.X, gridSpacing), Snap(p.Y, gridSpacing));

    /// <summary>
    /// Sürükleme vektörünü, tutulan nesnenin referans noktası (<paramref name="snapOrigin"/>) grid'e oturacak şekilde
    /// düzeltir. Grid dışında kalmış bir nesne de ilk sürüklemede hizaya gelir.
    /// </summary>
    public static Point2D SnapDelta(Point2D snapOrigin, Point2D rawDelta, double gridSpacing) =>
        Snap(snapOrigin + rawDelta, gridSpacing) - snapOrigin;

    /// <summary>Noktanın bir doğru parçasına en kısa uzaklığı.</summary>
    public static double DistanceToSegment(Point2D p, Point2D a, Point2D b)
    {
        var ab = b - a;
        var lengthSquared = ab.X * ab.X + ab.Y * ab.Y;
        if (lengthSquared <= double.Epsilon)
        {
            return p.DistanceTo(a);
        }
        var t = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        return p.DistanceTo(a + ab * t);
    }
}
