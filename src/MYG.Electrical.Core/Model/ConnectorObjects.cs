using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>Potansiyel türü. Ekran rengi buna göre seçilir; ad ve gerilim ayrıca saklanır (renk tek başına anlam taşımaz).</summary>
public enum PotentialKind
{
    Other,
    L1,
    L2,
    L3,
    N,
    PE,
    DcPositive,
    DcNegative,
    Signal,
    Communication,
}

/// <summary>Tek bağlantı noktalı, döndürülebilir yardımcı nesne (potansiyel, sayfa bağlantısı).</summary>
public abstract class SinglePinObject : DrawingObject, IRotatable
{
    public const string PinId = "1";

    private double _rotation;

    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Saat yönünde 0/90/180/270.</summary>
    public double Rotation
    {
        get => _rotation;
        set => _rotation = CoordinateTransform.NormalizeRotation(value);
    }

    /// <summary>Ad (potansiyel adı veya sayfa bağlantısı adı).</summary>
    public string Name { get; set; } = string.Empty;

    public override Point2D Anchor => new(X, Y);

    [JsonIgnore]
    public override bool AffectsWiring => true;

    /// <summary>Dönüş 0'da kablonun çıktığı yön.</summary>
    [JsonIgnore]
    protected abstract PinDirection BaseDirection { get; }

    [JsonIgnore]
    public PinDirection Direction => CoordinateTransform.RotateDirection(BaseDirection, Rotation);

    public GlobalPin Pin => new(Id, PinId, Anchor, Direction);

    public IEnumerable<GlobalPin> GetGlobalPins()
    {
        yield return Pin;
    }

    public Point2D LocalToGlobal(Point2D local) => CoordinateTransform.LocalToGlobal(local, Anchor, Rotation);

    public override void Move(double dx, double dy)
    {
        X += dx; Y += dy;
    }
}

/// <summary>
/// Potansiyel tanımı (EPLAN "potansiyel bağlantı noktası"): hattın başına konur, bağlı olduğu bütün iletkenlere
/// adını (L1, +24V...), türünü ve gerilimini verir. Dönüş 0'da kablo aşağı çıkar.
/// </summary>
public sealed class PotentialObject : SinglePinObject
{
    public PotentialKind PotentialKind { get; set; } = PotentialKind.Other;

    /// <summary>Gerilim bilgisi, ör. "400VAC", "24VDC".</summary>
    public string Voltage { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    protected override PinDirection BaseDirection => PinDirection.Down;

    public override DrawingObject CloneWithNewId() => new PotentialObject
    {
        X = X, Y = Y, Rotation = Rotation, Name = Name, PotentialKind = PotentialKind, Voltage = Voltage, Description = Description,
    };

    /// <summary>Yaygın potansiyeller için hazır ayarlar (ad, tür, gerilim).</summary>
    public static IReadOnlyList<(string Name, PotentialKind Kind, string Voltage)> Presets { get; } =
    [
        ("L1", PotentialKind.L1, "400VAC"),
        ("L2", PotentialKind.L2, "400VAC"),
        ("L3", PotentialKind.L3, "400VAC"),
        ("N", PotentialKind.N, "230VAC"),
        ("PE", PotentialKind.PE, ""),
        ("+24V", PotentialKind.DcPositive, "24VDC"),
        ("0V", PotentialKind.DcNegative, "24VDC"),
    ];

    /// <summary>Ad verilmişse türü tahmin eder (L1 → L1, +24V → DC+, 0V/M → DC−...).</summary>
    public static PotentialKind GuessKind(string name)
    {
        var n = name.Trim().ToUpperInvariant();
        return n switch
        {
            "L1" or "L" => PotentialKind.L1,
            "L2" => PotentialKind.L2,
            "L3" => PotentialKind.L3,
            "N" => PotentialKind.N,
            "PE" or "PEN" => PotentialKind.PE,
            "0V" or "M" or "-24V" or "GND" or "0VDC" => PotentialKind.DcNegative,
            _ when n.StartsWith('+') || n.StartsWith("L+", StringComparison.Ordinal) || n.EndsWith("VDC", StringComparison.Ordinal) => PotentialKind.DcPositive,
            _ => PotentialKind.Other,
        };
    }
}

/// <summary>
/// Sayfalar arası bağlantı (kesme noktası). Projede aynı adı taşıyan bağlantılar aynı iletkendir; yanında
/// karşı tarafın sayfa.kolon referansı gösterilir. Dönüş 0'da kablo soldan gelir (ok sağa, sonraki sayfaya bakar).
/// </summary>
public sealed class PageLinkObject : SinglePinObject
{
    public string Description { get; set; } = string.Empty;

    protected override PinDirection BaseDirection => PinDirection.Left;

    public override DrawingObject CloneWithNewId() => new PageLinkObject
    {
        X = X, Y = Y, Rotation = Rotation, Name = Name, Description = Description,
    };
}

/// <summary>
/// Cihaz tasarımı sırasında çizime konan bağlantı noktası (EPLAN "PLC bağlantı noktası" benzeri). Bir kutu ve
/// çizgilerle birlikte seçilip "Cihaz oluştur" ile kütüphaneye kaydedilir; sembolün pini olur.
/// <see cref="SinglePinObject.Name"/> bağlantı tanımıdır (X0, I0.0, L+...). Dönüş 0'da kablo yukarı çıkar,
/// kısa uç çizgisi aşağıya (kutunun içine) uzanır.
/// </summary>
public sealed class DevicePinObject : SinglePinObject
{
    /// <summary>Pin ucunun kutuya doğru uzunluğu (mm).</summary>
    public const double LeadLength = 5.0;

    public PinFunction Function { get; set; } = PinFunction.General;

    public string Description { get; set; } = string.Empty;

    protected override PinDirection BaseDirection => PinDirection.Up;

    /// <summary>Uç çizgisinin kutu tarafındaki ucu.</summary>
    [JsonIgnore]
    public Point2D InnerEnd => LocalToGlobal(new Point2D(0, LeadLength));

    public override DrawingObject CloneWithNewId() => new DevicePinObject
    {
        X = X, Y = Y, Rotation = Rotation, Name = Name, Function = Function, Description = Description,
    };

    /// <summary>İşleve göre varsayılan yön: çıkışlar aşağı, diğerleri yukarı.</summary>
    public static double DefaultRotation(PinFunction f) => f == PinFunction.Output ? 180 : 0;
}
