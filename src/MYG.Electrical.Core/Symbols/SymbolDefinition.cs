using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Symbols;

/// <summary>
/// Kütüphanedeki sembol tanımı (veriye dayalı, JSON). Orijin = sembolün ekleme noktası; ölçüler mm.
/// </summary>
public sealed class SymbolDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Kategori yolu, "/" ile ayrılır: "Koruma/Otomatik sigortalar".</summary>
    public string Category { get; init; } = "Genel";

    public string Description { get; init; } = string.Empty;

    /// <summary>Arama için ek anahtar kelimeler (ör. "MCB", "B16", "kesici").</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Yeni cihazlarda kullanılacak tanım harfi (IEC 81346-2): K, F, Q, M, S, B, X, H...</summary>
    public string ReferencePrefix { get; init; } = "X";

    public SymbolRole Role { get; init; } = SymbolRole.Standalone;

    /// <summary>Etiket bloğunun yeri (sağ, sol, üst).</summary>
    public ReferencePosition ReferencePosition { get; init; } = ReferencePosition.Right;

    public IReadOnlyList<SymbolPrimitive> Primitives { get; init; } = [];

    [JsonPropertyName("pins")]
    public IReadOnlyList<ConnectionPoint> ConnectionPoints { get; init; } = [];

    /// <summary>Pinler arası iletken yollar (potansiyel ve kısa devre analizi).</summary>
    public IReadOnlyList<InternalConnection> InternalConnections { get; init; } = [];

    /// <summary>Yeni cihaza kopyalanacak varsayılan elektriksel özellikler (ör. "Güç": "", "Gerilim": "400V").</summary>
    public IReadOnlyDictionary<string, string> DefaultProperties { get; init; } = new Dictionary<string, string>();

    /// <summary>Sembolün geldiği kütüphane adı ve sürümü (ör. "MYG IEC 60617 1.0.0"); dosyada sembol başına saklanmaz.</summary>
    [JsonIgnore]
    public string Library { get; internal set; } = string.Empty;

    /// <summary>Yerleşik kütüphane mi, kullanıcı klasöründen mi yüklendi.</summary>
    [JsonIgnore]
    public bool IsUserDefined { get; internal set; }

    /// <summary>Lokal sınırlar (bağlantı noktaları dahil).</summary>
    [JsonIgnore]
    public Rect2D LocalBounds => Rect2D.FromPoints(
        Primitives.SelectMany(p => p.ExtentPoints())
            .Concat(ConnectionPoints.Select(c => new Point2D(c.X, c.Y))));

    /// <summary>Kategori yolunun ilk düzeyi (kütüphane ağacı).</summary>
    [JsonIgnore]
    public string TopCategory => Category.Split('/')[0];

    /// <summary>Bu sembolden yeni bir cihaz nesnesi oluşturur.</summary>
    public DeviceObject CreateDevice(double x, double y, string reference) => new()
    {
        SymbolId = Id,
        X = x,
        Y = y,
        Reference = reference,
        ConnectionPoints = ConnectionPoints.Select(c => c.Clone()).ToList(),
    };
}
