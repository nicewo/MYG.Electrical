using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>
/// Elektriksel birleşim noktası. Dört yöne bakan sanal pine sahiptir;
/// bu sayede T ve çoklu bağlantılar aynı geometrik kurallarla çözülür.
/// </summary>
public sealed class JunctionObject : DrawingObject
{
    public const double DefaultSizeMm = 1.2;

    public double X { get; set; }
    public double Y { get; set; }
    public double Size { get; set; } = DefaultSizeMm;
    public bool Visible { get; set; } = true;

    public override Point2D Anchor => new(X, Y);

    [JsonIgnore]
    public override bool AffectsWiring => true;

    public static readonly IReadOnlyList<PinDirection> PinDirections =
        [PinDirection.Left, PinDirection.Right, PinDirection.Up, PinDirection.Down];

    public IEnumerable<GlobalPin> GetGlobalPins()
    {
        var p = Anchor;
        foreach (var d in PinDirections)
        {
            yield return new GlobalPin(Id, d.ToString(), p, d);
        }
    }

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }

    public override DrawingObject CloneWithNewId() => new JunctionObject { X = X, Y = Y, Size = Size, Visible = Visible };
}
