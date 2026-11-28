using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Network;

/// <summary>Çok parçalı bir cihazın bir parçası (bobin, kontak...) ve şemadaki yeri.</summary>
public sealed record DevicePart(Page Page, DeviceObject Placement, SymbolDefinition? Symbol, PageLocation Location)
{
    public SymbolRole Role => Symbol?.Role ?? SymbolRole.Standalone;

    /// <summary>Kontak mı (NO/NC/CO/güç kontağı); PLC G/Ç kutusu gibi diğer parçalar adıyla listelenir.</summary>
    public bool IsContact => Placement.ConnectionPoints.Any(p => p.Function is PinFunction.ContactNo or PinFunction.ContactNc
        or PinFunction.ContactCommon) || (Role == SymbolRole.Contact && Placement.ConnectionPoints.Any(p => p.Function == PinFunction.Power));

    /// <summary>Kontak türü kısaltması: NO, NC, CO, güç veya sembol adı.</summary>
    public string KindText
    {
        get
        {
            var functions = Placement.ConnectionPoints.Select(p => p.Function).ToHashSet();
            if (functions.Contains(PinFunction.ContactCommon))
            {
                return "CO";
            }
            if (functions.Contains(PinFunction.Power) && Role == SymbolRole.Contact)
            {
                return "Güç";
            }
            if (functions.Contains(PinFunction.ContactNc))
            {
                return "NC";
            }
            if (functions.Contains(PinFunction.ContactNo))
            {
                return "NO";
            }
            return Symbol?.Name ?? Placement.SymbolId;
        }
    }

    /// <summary>Kontak yolları, ör. "13-14" veya "1-2 3-4 5-6" (iç bağlantılardan, görünen pin adlarıyla).</summary>
    public string Paths
    {
        get
        {
            var conns = Symbol?.InternalConnections ?? [];
            return conns.Count == 0
                ? string.Join(" ", Placement.ConnectionPoints.Select(p => Placement.PinLabel(p.Id)))
                : string.Join(" ", conns.Select(c => $"{Placement.PinLabel(c.A)}-{Placement.PinLabel(c.B)}"));
        }
    }
}

/// <summary>EPLAN tarzı çapraz referanslar: bobin altında kontak aynası, kontağın yanında bobinin yeri.</summary>
public static class CrossReferences
{
    /// <summary>Etiketin bütün parçaları, sayfa ve kolon sırasıyla.</summary>
    public static IReadOnlyList<DevicePart> Parts(ProjectNetwork network, ISymbolCatalog catalog, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return [];
        }
        return DeviceRegistry.Placements(network.Document, reference)
            .Select(x => new DevicePart(x.Page, x.Placement, catalog.Find(x.Placement.SymbolId), network.LocationOf(x.Page, x.Placement.X)))
            .OrderBy(p => p.Location.PageNumber).ThenBy(p => p.Location.Column).ThenBy(p => p.Placement.Y)
            .ToList();
    }

    /// <summary>Ana fonksiyonun (bobin) altında yazılacak kontak aynası satırları: "NO 13-14 /2.5".</summary>
    public static IReadOnlyList<string> ContactMirror(ProjectNetwork network, ISymbolCatalog catalog, DeviceObject main)
    {
        return Parts(network, catalog, main.Reference)
            .Where(p => !ReferenceEquals(p.Placement, main) && p.Role == SymbolRole.Contact)
            .Select(p => p.IsContact ? $"{p.KindText} {p.Paths} {p.Location}" : $"{PartName(p)} {p.Location}")
            .ToList();
    }

    /// <summary>Kontak olmayan parçanın aynadaki adı: PLC kanalında adres (I0.3), yoksa sembol adı.</summary>
    private static string PartName(DevicePart p) =>
        p.Placement.PinDesignations.Count > 0
            ? string.Join(" ", p.Placement.ConnectionPoints.Select(cp => p.Placement.PinLabel(cp.Id)))
            : p.Symbol?.Name ?? p.Placement.SymbolId;

    /// <summary>Kontağın yanında yazılacak ana fonksiyon konum(lar)ı: "/2.3".</summary>
    public static string MainLocation(ProjectNetwork network, ISymbolCatalog catalog, DeviceObject contact) =>
        string.Join(" ", Parts(network, catalog, contact.Reference)
            .Where(p => !ReferenceEquals(p.Placement, contact) && p.Role is SymbolRole.Main or SymbolRole.Standalone)
            .Select(p => p.Location.ToString()));
}
