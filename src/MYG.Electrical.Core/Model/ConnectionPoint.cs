namespace MYG.Electrical.Core.Model;

/// <summary>Sembolün lokal koordinat sisteminde tanımlı bağlantı noktası (mm).</summary>
public sealed class ConnectionPoint
{
    public string Id { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public PinDirection Direction { get; set; }

    public ConnectionPoint() { }

    public ConnectionPoint(string id, double x, double y, PinDirection direction)
    {
        Id = id;
        X = x;
        Y = y;
        Direction = direction;
    }

    public ConnectionPoint Clone() => new(Id, X, Y, Direction);
}
