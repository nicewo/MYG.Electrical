using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Symbols;

/// <summary>Kütüphanedeki sembol tanımı. Orijin = sembolün ekleme noktası.</summary>
public sealed class SymbolDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "Genel";

    /// <summary>Yeni cihazlarda kullanılacak tanım ön eki (IEC 81346): K, F, Q, M, S, B, X, H.</summary>
    public string ReferencePrefix { get; init; } = "X";

    public IReadOnlyList<SymbolPrimitive> Primitives { get; init; } = [];
    public IReadOnlyList<ConnectionPoint> ConnectionPoints { get; init; } = [];

    /// <summary>Lokal sınırlar (bağlantı noktaları dahil).</summary>
    public Rect2D LocalBounds => Rect2D.FromPoints(
        Primitives.SelectMany(p => p.ExtentPoints())
            .Concat(ConnectionPoints.Select(c => new Point2D(c.X, c.Y))));

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
