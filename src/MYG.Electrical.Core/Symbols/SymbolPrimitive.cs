using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Symbols;

/// <summary>Sembol grafiğini oluşturan vektörel temel şekil (lokal mm koordinatları).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LinePrimitive), "line")]
[JsonDerivedType(typeof(RectPrimitive), "rect")]
[JsonDerivedType(typeof(CirclePrimitive), "circle")]
[JsonDerivedType(typeof(ArcPrimitive), "arc")]
[JsonDerivedType(typeof(TextPrimitive), "text")]
public abstract record SymbolPrimitive
{
    /// <summary>Kesikli çizgi (mekanik bağlantı gösterimi vb.).</summary>
    public bool Dashed { get; init; }

    /// <summary>Şeklin kapsadığı lokal noktalar (sınır hesaplaması için).</summary>
    public abstract IEnumerable<Point2D> ExtentPoints();
}

public sealed record LinePrimitive(double X1, double Y1, double X2, double Y2) : SymbolPrimitive
{
    public override IEnumerable<Point2D> ExtentPoints() => [new(X1, Y1), new(X2, Y2)];
}

public sealed record RectPrimitive(double X, double Y, double Width, double Height) : SymbolPrimitive
{
    public bool Filled { get; init; }
    public override IEnumerable<Point2D> ExtentPoints() => [new(X, Y), new(X + Width, Y + Height)];
}

public sealed record CirclePrimitive(double CenterX, double CenterY, double Radius) : SymbolPrimitive
{
    public bool Filled { get; init; }
    public override IEnumerable<Point2D> ExtentPoints() =>
        [new(CenterX - Radius, CenterY - Radius), new(CenterX + Radius, CenterY + Radius)];
}

/// <summary>Yay: açılar derece, 0° = +X ekseni, pozitif yön saat yönü (Y aşağı).</summary>
public sealed record ArcPrimitive(double CenterX, double CenterY, double Radius, double StartAngle, double SweepAngle) : SymbolPrimitive
{
    public override IEnumerable<Point2D> ExtentPoints() =>
        [new(CenterX - Radius, CenterY - Radius), new(CenterX + Radius, CenterY + Radius)];

    public Point2D PointAt(double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        return new Point2D(CenterX + Radius * Math.Cos(rad), CenterY + Radius * Math.Sin(rad));
    }
}

public sealed record TextPrimitive(double X, double Y, string Text, double Height) : SymbolPrimitive
{
    public override IEnumerable<Point2D> ExtentPoints() =>
        [new(X - Text.Length * Height * GeometryConstants.AverageCharWidthRatio / 2, Y - Height / 2),
         new(X + Text.Length * Height * GeometryConstants.AverageCharWidthRatio / 2, Y + Height / 2)];
}
