namespace MYG.Electrical.Core.Geometry;

/// <summary>Eksen hizalı dikdörtgen (mm).</summary>
public readonly record struct Rect2D(double X, double Y, double Width, double Height)
{
    public static readonly Rect2D Empty = new(0, 0, 0, 0);

    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public Point2D Center => new(X + Width / 2, Y + Height / 2);

    public static Rect2D FromPoints(Point2D a, Point2D b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new Rect2D(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    public static Rect2D FromPoints(IEnumerable<Point2D> points)
    {
        var minX = double.MaxValue; var minY = double.MaxValue;
        var maxX = double.MinValue; var maxY = double.MinValue;
        var any = false;
        foreach (var p in points)
        {
            any = true;
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
        }
        return any ? new Rect2D(minX, minY, maxX - minX, maxY - minY) : Empty;
    }

    public bool Contains(Point2D p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    public bool Contains(Rect2D r) => r.Left >= Left && r.Right <= Right && r.Top >= Top && r.Bottom <= Bottom;

    public bool IntersectsWith(Rect2D r) => r.Left <= Right && r.Right >= Left && r.Top <= Bottom && r.Bottom >= Top;

    public Rect2D Inflate(double amount) => new(X - amount, Y - amount, Width + 2 * amount, Height + 2 * amount);

    public Rect2D Union(Rect2D other) => FromPoints([new(Left, Top), new(Right, Bottom), new(other.Left, other.Top), new(other.Right, other.Bottom)]);
}
