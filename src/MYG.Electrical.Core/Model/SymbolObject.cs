using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>Kütüphaneden yerleştirilmiş sembol örneği (bağlantı noktası olmayan, dekoratif kullanım).</summary>
public class SymbolObject : DrawingObject, IRotatable
{
    public string SymbolId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }

    private double _rotation;

    /// <summary>Saat yönünde derece; 0/90/180/270.</summary>
    public double Rotation
    {
        get => _rotation;
        set => _rotation = CoordinateTransform.NormalizeRotation(value);
    }

    public double Scale { get; set; } = 1.0;

    public Point2D Position => new(X, Y);
    public override Point2D Anchor => Position;

    public Point2D LocalToGlobal(Point2D local) => CoordinateTransform.LocalToGlobal(local, Position, Rotation, Scale);

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }

    public override DrawingObject CloneWithNewId() => new SymbolObject { SymbolId = SymbolId, X = X, Y = Y, Rotation = Rotation, Scale = Scale };
}
