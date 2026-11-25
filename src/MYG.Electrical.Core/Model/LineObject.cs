using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

public sealed class LineObject : DrawingObject, IRotatable
{
    public const double DefaultThicknessMm = 0.35;

    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Thickness { get; set; } = DefaultThicknessMm;

    public Point2D Start => new(X1, Y1);
    public Point2D End => new(X2, Y2);

    public override Point2D Anchor => new((X1 + X2) / 2, (Y1 + Y2) / 2);

    /// <summary>Çizgi kendi orta noktası etrafında 90° adımlarla döner; açı bilgisi tutulmaz.</summary>
    double IRotatable.Rotation
    {
        get => 0;
        set
        {
            var c = Anchor;
            var s = CoordinateTransform.LocalToGlobal(Start - c, c, value);
            var e = CoordinateTransform.LocalToGlobal(End - c, c, value);
            (X1, Y1, X2, Y2) = (s.X, s.Y, e.X, e.Y);
        }
    }

    public override void Move(double dx, double dy)
    {
        X1 += dx; Y1 += dy; X2 += dx; Y2 += dy;
    }

    public override DrawingObject CloneWithNewId() => new LineObject { X1 = X1, Y1 = Y1, X2 = X2, Y2 = Y2, Thickness = Thickness };
}
