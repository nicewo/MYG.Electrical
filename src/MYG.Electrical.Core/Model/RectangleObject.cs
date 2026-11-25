using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

public sealed class RectangleObject : DrawingObject, IRotatable
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Thickness { get; set; } = LineObject.DefaultThicknessMm;

    public Rect2D Bounds => new(X, Y, Width, Height);
    public override Point2D Anchor => Bounds.Center;
    public override Point2D SnapPoint => new(X, Y);

    /// <summary>Dikdörtgen merkezi etrafında döner (90° → en/boy yer değiştirir).</summary>
    double IRotatable.Rotation
    {
        get => 0;
        set
        {
            if (CoordinateTransform.NormalizeRotation(value) % 180 == 0)
            {
                return;
            }
            var c = Anchor;
            (Width, Height) = (Height, Width);
            X = c.X - Width / 2;
            Y = c.Y - Height / 2;
        }
    }

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }

    public override DrawingObject CloneWithNewId() => new RectangleObject { X = X, Y = Y, Width = Width, Height = Height, Thickness = Thickness };
}
