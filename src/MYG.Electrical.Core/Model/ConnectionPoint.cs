using System.Text.Json.Serialization;

namespace MYG.Electrical.Core.Model;

/// <summary>Sembolün lokal koordinat sisteminde tanımlı bağlantı noktası (mm).</summary>
public sealed class ConnectionPoint
{
    /// <summary>Pin adı / numarası (A1, 13, 1/L1, X0...).</summary>
    public string Id { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public PinDirection Direction { get; set; }

    /// <summary>Elektriksel fonksiyon.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PinFunction Function { get; set; }

    /// <summary>Açıklama / adres (ör. "I0.0", "+24V besleme").</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    public ConnectionPoint() { }

    public ConnectionPoint(string id, double x, double y, PinDirection direction, PinFunction function = PinFunction.General)
    {
        Id = id;
        X = x;
        Y = y;
        Direction = direction;
        Function = function;
    }

    public ConnectionPoint Clone() => new(Id, X, Y, Direction, Function) { Description = Description };
}
