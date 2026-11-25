using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Devices;

/// <summary>
/// Kullanıcının bir kabloya girdiği bilgiler. Kablolar geometriden yeniden üretildiği için veri, kablonun iki
/// ucundaki pinlere bağlı saklanır (yön önemsiz). Bağlantı var olduğu sürece veri kablonun üzerinde kalır.
/// </summary>
public sealed class WireData
{
    public PinRef A { get; set; }
    public PinRef B { get; set; }

    /// <summary>Kablo numarası (otomatik veya elle).</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Damar rengi (ör. "BK", "BU", "GNYE"); ekran rengini değil fiziksel rengi anlatır.</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Kesit, ör. "1,5 mm²".</summary>
    public string CrossSection { get; set; } = string.Empty;

    /// <summary>Kablo tipi / adı, ör. "H07V-K" veya "-W1 4x2,5".</summary>
    public string CableType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Numara elle mi girildi (otomatik numaralandırma bunu değiştirmez).</summary>
    public bool NumberLocked { get; set; }

    public bool Connects(PinRef x, PinRef y) => (A == x && B == y) || (A == y && B == x);

    public bool IsEmpty => Number.Length == 0 && Color.Length == 0 && CrossSection.Length == 0
        && CableType.Length == 0 && Description.Length == 0;

    public WireData Clone() => new()
    {
        A = A, B = B, Number = Number, Color = Color, CrossSection = CrossSection, CableType = CableType,
        Description = Description, NumberLocked = NumberLocked,
    };
}
