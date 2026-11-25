using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.Model;

/// <summary>Sayfa koordinatlarına dönüştürülmüş bağlantı noktası.</summary>
public readonly record struct GlobalPin(Guid OwnerId, string PinId, Point2D Position, PinDirection Direction)
{
    public PinRef Ref => new(OwnerId, PinId);
}

/// <summary>Bir bağlantı noktasının kimliği (sahip nesne + pin).</summary>
public readonly record struct PinRef(Guid OwnerId, string PinId);
