using System.Text.Json.Serialization;
using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>Bağlantı noktaları olan elektrik cihazı (kontaktör, sigorta, motor...).</summary>
public sealed class DeviceObject : SymbolObject
{
    /// <summary>Cihaz tanımlayıcısı, ör. -K1, -F3.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Bu yerleşimin işlev metni (ör. PLC kanalı "Konveyör çalışıyor"). Cihaz açıklamasından ayrıdır.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Pinlerin şemada görünen adları (bağlantı tanımı). Anahtar sembol pin kimliği, değer görünen ad:
    /// ör. ikinci NO kontak için {"13": "23", "14": "24"}, PLC kanalı için {"1": "I0.3"}.
    /// Kablo bağlantıları pin kimliğiyle tutulduğu için yeniden adlandırma bağlantıyı bozmaz.
    /// </summary>
    public Dictionary<string, string> PinDesignations { get; set; } = [];

    /// <summary>Pinin şemada görünen adı.</summary>
    public string PinLabel(string pinId) =>
        PinDesignations.TryGetValue(pinId, out var label) && !string.IsNullOrWhiteSpace(label) ? label : pinId;

    /// <summary>Sembolün lokal koordinatlarındaki bağlantı noktaları.</summary>
    public List<ConnectionPoint> ConnectionPoints { get; set; } = [];

    [JsonIgnore]
    public override bool AffectsWiring => true;

    /// <summary>Bağlantı noktalarının güncel global konum ve yönleri.</summary>
    public IEnumerable<GlobalPin> GetGlobalPins()
    {
        foreach (var cp in ConnectionPoints)
        {
            var p = LocalToGlobal(new Point2D(cp.X, cp.Y));
            yield return new GlobalPin(Id, cp.Id, p, CoordinateTransform.RotateDirection(cp.Direction, Rotation));
        }
    }

    public override DrawingObject CloneWithNewId() => new DeviceObject
    {
        SymbolId = SymbolId,
        X = X,
        Y = Y,
        Rotation = Rotation,
        Scale = Scale,
        Reference = Reference,
        Description = Description,
        PinDesignations = new Dictionary<string, string>(PinDesignations),
        ConnectionPoints = ConnectionPoints.Select(c => c.Clone()).ToList(),
    };
}
