namespace MYG.Electrical.Core.Geometry;

/// <summary>Milimetre cinsinden 2B nokta. Sayfa koordinat sistemi: orijin sol üst, Y aşağı doğru artar.</summary>
public readonly record struct Point2D(double X, double Y)
{
    public static readonly Point2D Zero = new(0, 0);

    public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2D operator *(Point2D a, double k) => new(a.X * k, a.Y * k);

    public double DistanceTo(Point2D other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override string ToString() => $"({X:0.##}; {Y:0.##})";
}
