using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.AutoWire;

/// <summary>
/// Dirsek/T yerleştirirken imlecin bulunduğu noktaya en uygun yönü önerir:
/// her dönüş için, aktif yönlerin kaçında o yöne bakan bir karşı pin olduğu sayılır.
/// Çapraz (4 yollu) şekil asla otomatik önerilmez; kullanıcı açıkça seçmelidir.
/// </summary>
public static class JunctionPlacement
{
    public static double SuggestRotation(IEnumerable<DrawingObject> objects, Point2D point, JunctionShape shape,
        double currentRotation, double tolerance = GeometryConstants.ConnectionToleranceMm)
    {
        var available = FacingDirections(objects, point, tolerance);
        var probe = new JunctionObject { Shape = shape };
        var bestRotation = CoordinateTransform.NormalizeRotation(currentRotation);
        probe.Rotation = bestRotation;
        var bestScore = Score(probe, available);

        for (var rot = 0; rot < (int)GeometryConstants.FullTurn; rot += (int)GeometryConstants.RightAngle)
        {
            probe.Rotation = rot;
            var score = Score(probe, available);
            if (score > bestScore)
            {
                bestScore = score;
                bestRotation = rot;
            }
        }
        return bestRotation;
    }

    /// <summary>Noktadan bakıldığında, o yönde aynı hatta ve noktaya doğru bakan bir pin bulunan yönler.</summary>
    public static IReadOnlySet<PinDirection> FacingDirections(IEnumerable<DrawingObject> objects, Point2D point, double tolerance)
    {
        var result = new HashSet<PinDirection>();
        foreach (var pin in AutoWireEngine.CollectPins(objects))
        {
            var dx = pin.Position.X - point.X;
            var dy = pin.Position.Y - point.Y;
            var direction = pin.Direction switch
            {
                PinDirection.Left when Math.Abs(dy) <= tolerance && dx > tolerance => PinDirection.Right,
                PinDirection.Right when Math.Abs(dy) <= tolerance && dx < -tolerance => PinDirection.Left,
                PinDirection.Up when Math.Abs(dx) <= tolerance && dy > tolerance => PinDirection.Down,
                PinDirection.Down when Math.Abs(dx) <= tolerance && dy < -tolerance => PinDirection.Up,
                _ => (PinDirection?)null,
            };
            if (direction is { } d)
            {
                result.Add(d);
            }
        }
        return result;
    }

    private static int Score(JunctionObject probe, IReadOnlySet<PinDirection> available) =>
        probe.ActiveDirections.Count(available.Contains);
}
