using MYG.Electrical.Core.Geometry;

namespace MYG.Electrical.Core.AutoWire;

/// <summary>
/// Otomatik kablolama sonucu üretilen bağlantı. Kalıcı bir çizim nesnesi değildir;
/// her hesaplamada cihaz ve junction konumlarından yeniden üretilir.
/// </summary>
public sealed record WireConnection(Model.PinRef From, Model.PinRef To, Point2D Start, Point2D End)
{
    public bool IsHorizontal => Math.Abs(End.Y - Start.Y) <= Math.Abs(End.X - Start.X);
    public double Length => Start.DistanceTo(End);

    public bool Involves(Guid ownerId) => From.OwnerId == ownerId || To.OwnerId == ownerId;
}
