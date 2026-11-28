using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

public sealed class TextObject : DrawingObject, IRotatable
{
    public const double DefaultHeightMm = 3.5;

    public double X { get; set; }
    public double Y { get; set; }
    public string Text { get; set; } = "Metin";
    public double Height { get; set; } = DefaultHeightMm;
    public double Rotation { get; set; }

    /// <summary>Hizalama: (X, Y) metnin sol ucu, ortası ya da sağ ucu.</summary>
    public Rendering.TextAlign Align { get; set; }
    public bool Bold { get; set; }

    public override Point2D Anchor => new(X, Y);

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }

    public override DrawingObject CloneWithNewId() => new TextObject { X = X, Y = Y, Text = Text, Height = Height, Rotation = Rotation, Align = Align, Bold = Bold };
}
