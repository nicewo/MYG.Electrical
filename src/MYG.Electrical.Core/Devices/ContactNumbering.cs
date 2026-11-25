using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Devices;

/// <summary>
/// Yardımcı kontak numaralandırması (EN 50005 / IEC 60947-5-1): iki haneli numarada ilk hane sıra (1, 2, 3...),
/// ikinci hane işlev (1-2 NC, 3-4 NO, 5-6 / 7-8 zamanlı). Aynı cihaza ikinci NO kontak eklenince 23-24,
/// üçüncüsü 33-34 olur. Güç kontakları (1-2, 3-4, 5-6) ve 9x (termik) numaraları değişmez.
/// </summary>
public static class ContactNumbering
{
    public static bool IsAuxiliaryContactPin(ConnectionPoint pin) =>
        pin.Function is PinFunction.ContactNo or PinFunction.ContactNc or PinFunction.ContactCommon
        && pin.Id.Length == 2 && pin.Id[0] is >= '1' and <= '8' && pin.Id[1] is >= '1' and <= '8';

    /// <summary>Yerleşimin yardımcı kontaklarının şu anki sıra hanesi; yardımcı kontağı yoksa null.</summary>
    public static int? OrdinalOf(DeviceObject placement)
    {
        var pin = placement.ConnectionPoints.FirstOrDefault(IsAuxiliaryContactPin);
        if (pin is null)
        {
            return null;
        }
        var label = placement.PinLabel(pin.Id);
        return label.Length == 2 && char.IsDigit(label[0]) ? label[0] - '0' : null;
    }

    /// <summary>
    /// Yerleşim <paramref name="reference"/> etiketli cihaza katılırsa kullanılacak pin adları. Sıra hanesi başka bir
    /// parçayla çakışmıyorsa mevcut adlar döner (değişiklik yok).
    /// </summary>
    public static Dictionary<string, string> DesignationsFor(ProjectDocument doc, DeviceObject placement, string reference)
    {
        var current = new Dictionary<string, string>(placement.PinDesignations);
        var auxPins = placement.ConnectionPoints.Where(IsAuxiliaryContactPin).ToList();
        if (auxPins.Count == 0 || string.IsNullOrWhiteSpace(reference))
        {
            return current;
        }
        var used = DeviceRegistry.Placements(doc, reference)
            .Where(x => !ReferenceEquals(x.Placement, placement))
            .Select(x => OrdinalOf(x.Placement))
            .OfType<int>()
            .ToHashSet();
        var mine = OrdinalOf(placement);
        if (mine is { } m && !used.Contains(m))
        {
            return current;
        }
        var next = Enumerable.Range(1, 8).FirstOrDefault(i => !used.Contains(i));
        if (next == 0)
        {
            return current; // 8'den fazla yardımcı kontak: elle numaralandırılır, DRC uyarır
        }
        foreach (var pin in auxPins)
        {
            var designation = $"{next}{pin.Id[1]}";
            if (designation == pin.Id)
            {
                current.Remove(pin.Id);
            }
            else
            {
                current[pin.Id] = designation;
            }
        }
        return current;
    }

    public static bool SameDesignations(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);
}
