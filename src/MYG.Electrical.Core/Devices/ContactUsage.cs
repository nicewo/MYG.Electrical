using MYG.Electrical.Core.Network;

namespace MYG.Electrical.Core.Devices;

/// <summary>Bir kontak yuvasının durumu: hangi parça (yerleşim) kullanıyor; boşsa null.</summary>
public sealed record ContactSlotUsage(ContactSlot Slot, DevicePart? UsedBy)
{
    public bool IsUsed => UsedBy is not null;
}

/// <summary>Cihazın kontak kapasitesine göre kullanılan / boş kontaklar ve kapasite dışı kullanımlar.</summary>
public static class ContactUsage
{
    public static (IReadOnlyList<ContactSlotUsage> Slots, IReadOnlyList<DevicePart> Unassigned) Compute(DeviceRecord record, IReadOnlyList<DevicePart> parts)
    {
        var contactParts = parts.Where(p => p.Role == Symbols.SymbolRole.Contact).ToList();
        var remaining = new List<DevicePart>(contactParts);
        var result = new List<ContactSlotUsage>();
        foreach (var slot in record.Contacts)
        {
            var user = remaining.FirstOrDefault(p => Covers(p, slot));
            if (user is not null && !IsMultiSlotPart(user))
            {
                remaining.Remove(user);
            }
            result.Add(new ContactSlotUsage(slot, user));
        }
        // Birden çok yuvayı karşılayan parça (ör. 3 kutuplu ana kontak) kullanıldıysa listeden düşer.
        remaining.RemoveAll(p => result.Any(r => ReferenceEquals(r.UsedBy, p)));
        return (result, record.Contacts.Count == 0 ? [] : remaining);
    }

    private static bool Covers(DevicePart part, ContactSlot slot)
    {
        var labels = part.Placement.ConnectionPoints.Select(p => part.Placement.PinLabel(p.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return slot.Pins.Count > 0 && slot.Pins.All(labels.Contains);
    }

    private static bool IsMultiSlotPart(DevicePart part) =>
        part.Placement.ConnectionPoints.Count(p => p.Function == Model.PinFunction.Power) > 2;
}
