using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>
/// Bağlantı noktasının şekli (EPLAN "bağlantı tanımı noktası" mantığı). Hangi yönlere kablo çıkabileceğini belirler.
/// </summary>
public enum JunctionShape
{
    /// <summary>Dirsek (90° dönüş): iki dik yön. Rotation 0 = ┌ (Sağ + Aşağı).</summary>
    Angle,

    /// <summary>T bağlantı: üç yön. Rotation 0 = ┬ (Sol + Sağ + Aşağı).</summary>
    Tee,

    /// <summary>Çapraz düğüm: dört yön. Kesişen yatay ve dikey hattı elektriksel olarak birleştirir.</summary>
    Cross,
}

/// <summary>
/// Elektriksel bağlantı noktası (dirsek, T, çapraz). Yalnızca şekline ve yönüne göre aktif olan yönlerde sanal pin
/// üretir; böylece kesişen ama bağlanmaması gereken hatlar birleşmez.
/// </summary>
public sealed class JunctionObject : DrawingObject, IRotatable
{
    public const double DefaultSizeMm = 1.2;

    private static readonly IReadOnlyDictionary<JunctionShape, PinDirection[]> BaseDirections =
        new Dictionary<JunctionShape, PinDirection[]>
        {
            [JunctionShape.Angle] = [PinDirection.Right, PinDirection.Down],
            [JunctionShape.Tee] = [PinDirection.Left, PinDirection.Right, PinDirection.Down],
            [JunctionShape.Cross] = [PinDirection.Left, PinDirection.Right, PinDirection.Up, PinDirection.Down],
        };

    private double _rotation;

    public double X { get; set; }
    public double Y { get; set; }
    public JunctionShape Shape { get; set; } = JunctionShape.Tee;

    /// <summary>Şeklin yönü: saat yönünde 0/90/180/270. Dirsek ve T'nin dört varyantını seçer.</summary>
    public double Rotation
    {
        get => _rotation;
        set => _rotation = CoordinateTransform.NormalizeRotation(value);
    }

    public double Size { get; set; } = DefaultSizeMm;

    /// <summary>Nokta işareti görünür mü (dirsekte nokta hiç çizilmez).</summary>
    public bool Visible { get; set; } = true;

    public override Point2D Anchor => new(X, Y);

    [JsonIgnore]
    public override bool AffectsWiring => true;

    /// <summary>Dirsekte nokta işareti çizilmez (EPLAN'daki gibi).</summary>
    [JsonIgnore]
    public bool ShowsDot => Visible && Shape != JunctionShape.Angle;

    /// <summary>Şekil ve dönüşe göre aktif yönler.</summary>
    [JsonIgnore]
    public IReadOnlyList<PinDirection> ActiveDirections =>
        BaseDirections[Shape].Select(d => CoordinateTransform.RotateDirection(d, Rotation)).ToArray();

    public IEnumerable<GlobalPin> GetGlobalPins()
    {
        var p = Anchor;
        foreach (var d in ActiveDirections)
        {
            yield return new GlobalPin(Id, d.ToString(), p, d);
        }
    }

    /// <summary>Verilen yön kümesini karşılayan şekil + dönüş (ör. {Sol, Aşağı} → Dirsek 90°). Yoksa null.</summary>
    public static (JunctionShape Shape, double Rotation)? FromDirections(IReadOnlyCollection<PinDirection> directions)
    {
        foreach (var (shape, baseDirs) in BaseDirections)
        {
            if (baseDirs.Length != directions.Count)
            {
                continue;
            }
            for (var rot = 0; rot < (int)GeometryConstants.FullTurn; rot += (int)GeometryConstants.RightAngle)
            {
                if (baseDirs.Select(d => CoordinateTransform.RotateDirection(d, rot)).ToHashSet().SetEquals(directions))
                {
                    return (shape, rot);
                }
            }
        }
        return null;
    }

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }

    public override DrawingObject CloneWithNewId() =>
        new JunctionObject { X = X, Y = Y, Shape = Shape, Rotation = Rotation, Size = Size, Visible = Visible };
}
