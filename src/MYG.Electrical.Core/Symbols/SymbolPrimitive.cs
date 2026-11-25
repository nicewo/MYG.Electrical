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
[JsonDerivedType(typeof(PolygonPrimitive), "polygon")]
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

/// <summary>Çokgen / çoklu çizgi: oklar, diyot üçgeni, toprak işareti gibi şekiller. Noktalar [x0, y0, x1, y1, ...].</summary>
public sealed record PolygonPrimitive(IReadOnlyList<double> Points) : SymbolPrimitive
{
    /// <summary>
    /// Açık çoklu çizgi (son nokta ilkine bağlanmaz). Varsayılan false = kapalı çokgen; varsayılan değerler
    /// JSON'a yazılmadığı için bayrak "false = olağan durum" olacak şekilde seçildi.
    /// </summary>
    public bool Open { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Closed => !Open;
    public bool Filled { get; init; }

    public IEnumerable<Point2D> Vertices()
    {
        for (var i = 0; i + 1 < Points.Count; i += 2)
        {
            yield return new Point2D(Points[i], Points[i + 1]);
        }
    }

    public override IEnumerable<Point2D> ExtentPoints() => Vertices();

    /// <summary>Değer eşitliği (nokta listesi içerik olarak karşılaştırılır).</summary>
    public bool Equals(PolygonPrimitive? other) =>
        other is not null && base.Equals(other) && Open == other.Open && Filled == other.Filled
        && Points.SequenceEqual(other.Points);

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(base.GetHashCode());
        h.Add(Open);
        h.Add(Filled);
        foreach (var v in Points)
        {
            h.Add(v);
        }
        return h.ToHashCode();
    }
}
