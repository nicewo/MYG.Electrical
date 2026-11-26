using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Drc;

public enum DrcSeverity
{
    Error,
    Warning,
    Info,
}

/// <summary>Bir tasarım kuralı bulgusu. <see cref="ObjectIds"/> çizimde vurgulanır, <see cref="Page"/> gezinme içindir.</summary>
public sealed record DrcFinding(DrcSeverity Severity, string Code, string Message, Page? Page, IReadOnlyList<Guid> ObjectIds, string Location)
{
    public string SeverityText => Severity switch
    {
        DrcSeverity.Error => "Hata",
        DrcSeverity.Warning => "Uyarı",
        _ => "Bilgi",
    };
}

/// <summary>
/// Tasarım kuralları denetimi (DRC). Şemanın tutarlılığını kontrol eder: bağlantısız pinler, boşta kalan
/// bağlantı kolları, çift etiketler, tanımsız sembol/pin, aynı bağlantı tanımının iki kez kullanılması, eksik cihaz
/// bilgisi, potansiyel çakışmaları (kısa devre), kontak kapasitesi ve eşi olmayan sayfa bağlantıları.
/// <para><b>DRC elektriksel güvenlik veya mevzuat uygunluğu sertifikası değildir.</b> Kesit, koruma seçimi,
/// selektivite, gerilim düşümü ve standartlara uygunluk yetkin bir mühendis tarafından ayrıca doğrulanmalıdır.</para>
/// </summary>
public static class DrcEngine
{
    public const string Disclaimer =
        "DRC şemanın tutarlılığını denetler; elektriksel güvenlik veya mevzuat uygunluğu (IEC 60204-1, EN 61439 vb.) sertifikası değildir. " +
        "Kesit, koruma seçimi, selektivite ve gerilim düşümü yetkin bir mühendis tarafından ayrıca doğrulanmalıdır.";

    public static IReadOnlyList<DrcFinding> Run(ProjectDocument doc, ProjectNetwork network, ISymbolCatalog catalog)
    {
        var findings = new List<DrcFinding>();
        var connected = new HashSet<PinRef>();
        foreach (var w in network.Wires.Values.SelectMany(x => x))
        {
            connected.Add(w.From);
            connected.Add(w.To);
        }

        CheckObjects(doc, network, catalog, connected, findings);
        CheckReferences(doc, network, catalog, findings);
        CheckPotentials(network, findings);
        CheckPageLinks(doc, network, findings);

        return findings
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.Page is null ? int.MaxValue : doc.Pages.IndexOf(f.Page))
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static string Loc(ProjectNetwork n, Page page, DrawingObject o) => n.LocationOf(page, o.Anchor.X).ToString();

    private static void CheckObjects(ProjectDocument doc, ProjectNetwork network, ISymbolCatalog catalog, HashSet<PinRef> connected, List<DrcFinding> f)
    {
        foreach (var page in doc.Pages)
        {
            foreach (var obj in page.Objects)
            {
                switch (obj)
                {
                    case DeviceObject d:
                        CheckDevice(page, d, network, catalog, connected, f);
                        break;
                    case JunctionObject j:
                        var arms = j.GetGlobalPins().ToList();
                        var used = arms.Count(a => connected.Contains(a.Ref));
                        if (used < arms.Count)
                        {
                            f.Add(new(used <= 1 ? DrcSeverity.Error : DrcSeverity.Warning, "DRC-WIRE-01",
                                used <= 1
                                    ? "Bağlantı noktası boşta: en fazla bir kolu bağlı (kablo havada kalıyor)."
                                    : $"Bağlantı noktasının {arms.Count - used} kolu boşta; şekli veya yönü yanlış olabilir.",
                                page, [j.Id], Loc(network, page, j)));
                        }
                        break;
                    case PotentialObject p when !connected.Contains(p.Pin.Ref):
                        f.Add(new(DrcSeverity.Warning, "DRC-PIN-02", $"'{p.Name}' potansiyeli hiçbir kabloya bağlı değil.", page, [p.Id], Loc(network, page, p)));
                        break;
                    case DevicePinObject dp:
                        f.Add(new(DrcSeverity.Info, "DRC-DEV-01",
                            $"'{dp.Name}' tasarım pini henüz bir cihaza dönüştürülmedi (kutu ve pinleri seçip 'Seçimden cihaz oluştur').",
                            page, [dp.Id], Loc(network, page, dp)));
                        break;
                    case PageLinkObject l when !connected.Contains(l.Pin.Ref):
                        f.Add(new(DrcSeverity.Warning, "DRC-PIN-02", $"'{l.Name}' sayfa bağlantısı bu sayfada hiçbir kabloya bağlı değil.", page, [l.Id], Loc(network, page, l)));
                        break;
                }
            }
        }
    }

    private static void CheckDevice(Page page, DeviceObject d, ProjectNetwork network, ISymbolCatalog catalog, HashSet<PinRef> connected, List<DrcFinding> f)
    {
        var loc = Loc(network, page, d);
        var name = string.IsNullOrWhiteSpace(d.Reference) ? d.SymbolId : d.Reference;
        if (catalog.Find(d.SymbolId) is not { } def)
        {
            f.Add(new(DrcSeverity.Error, "DRC-SYM-01", $"{name}: '{d.SymbolId}' sembolü kütüphanede yok (tanımsız pinler).", page, [d.Id], loc));
            return;
        }
        var symbolPins = def.ConnectionPoints.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var unknown = d.ConnectionPoints.Where(p => !symbolPins.Contains(p.Id)).Select(p => p.Id).ToList();
        if (unknown.Count > 0)
        {
            f.Add(new(DrcSeverity.Warning, "DRC-SYM-02", $"{name}: sembolde tanımsız pin(ler) {string.Join(", ", unknown)} (sembol kütüphanesi değişmiş olabilir).", page, [d.Id], loc));
        }
        if (def.Role != SymbolRole.Graphic && string.IsNullOrWhiteSpace(d.Reference))
        {
            f.Add(new(DrcSeverity.Error, "DRC-TAG-02", $"'{def.Name}' sembolünün etiketi yok.", page, [d.Id], loc));
        }

        var open = d.ConnectionPoints.Where(p => !connected.Contains(new PinRef(d.Id, p.Id))).ToList();
        if (open.Count == 0)
        {
            return;
        }
        // Çok kanallı cihazlarda (PLC, analizör) kullanılmayan G/Ç kanalları olağandır: bilgi olarak bildirilir.
        var optional = open.Where(p => p.Function is PinFunction.Input or PinFunction.Output or PinFunction.Communication
            or PinFunction.Shield or PinFunction.ProtectiveEarth).ToList();
        var required = open.Except(optional).ToList();
        if (required.Count > 0)
        {
            f.Add(new(DrcSeverity.Warning, "DRC-PIN-01",
                $"{name}: bağlantısız pin {string.Join(", ", required.Select(p => d.PinLabel(p.Id)))}.", page, [d.Id], loc));
        }
        if (optional.Count > 0)
        {
            f.Add(new(DrcSeverity.Info, "DRC-PIN-03",
                $"{name}: kullanılmayan G/Ç / haberleşme / PE ucu {string.Join(", ", optional.Select(p => d.PinLabel(p.Id)))}.", page, [d.Id], loc));
        }
    }

    private static void CheckReferences(ProjectDocument doc, ProjectNetwork network, ISymbolCatalog catalog, List<DrcFinding> f)
    {
        var groups = doc.Pages
            .SelectMany(p => p.Objects.OfType<DeviceObject>().Where(d => !string.IsNullOrWhiteSpace(d.Reference)))
            .GroupBy(d => d.Reference, DeviceRegistry.ReferenceComparer);
        foreach (var g in groups)
        {
            var parts = CrossReferences.Parts(network, catalog, g.Key);
            var first = parts.FirstOrDefault();
            var mains = parts.Where(p => p.Role is SymbolRole.Main or SymbolRole.Standalone).ToList();
            if (mains.Count > 1)
            {
                f.Add(new(DrcSeverity.Error, "DRC-TAG-01",
                    $"{g.Key}: etiket {mains.Count} ayrı cihazda kullanılmış ({string.Join(", ", mains.Select(m => $"{m.Symbol?.Name} {m.Location}"))}). Etiket benzersiz olmalı.",
                    mains[0].Page, mains.Select(m => m.Placement.Id).ToList(), mains[0].Location.ToString()));
            }
            var contacts = parts.Where(p => p.Role == SymbolRole.Contact).ToList();
            if (contacts.Count > 0 && mains.Count == 0)
            {
                f.Add(new(DrcSeverity.Warning, "DRC-TAG-03", $"{g.Key}: kontak/parça var ama ana fonksiyonu (bobin, cihaz) şemada yok.",
                    contacts[0].Page, contacts.Select(c => c.Placement.Id).ToList(), contacts[0].Location.ToString()));
            }

            // Aynı bağlantı tanımı (ör. 13) aynı cihazın iki parçasında: yanlış klemens / kontak numarası
            var labels = parts.SelectMany(p => p.Placement.ConnectionPoints.Select(cp => (Part: p, Label: p.Placement.PinLabel(cp.Id))))
                .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Select(y => y.Part.Placement.Id).Distinct().Count() > 1)
                .ToList();
            foreach (var dup in labels)
            {
                var ps = dup.Select(x => x.Part).DistinctBy(p => p.Placement.Id).ToList();
                // Hiç ad verilmemiş (varsayılan) pinler: ör. PLC tek kanal sembolleri adres girilmeden çoğaltılmış
                var allDefault = dup.All(x => x.Part.Placement.ConnectionPoints.Any(cp => cp.Id == dup.Key && !x.Part.Placement.PinDesignations.ContainsKey(cp.Id)));
                f.Add(new(allDefault ? DrcSeverity.Warning : DrcSeverity.Error, "DRC-TERM-01",
                    $"{g.Key}: '{dup.Key}' bağlantı tanımı birden fazla parçada ({string.Join(", ", ps.Select(p => p.Location))}). Kontak/klemens numarası tekrar ediyor.",
                    ps[0].Page, ps.Select(p => p.Placement.Id).ToList(), ps[0].Location.ToString()));
            }

            var record = DeviceRegistry.Find(doc, g.Key);
            if (record is null)
            {
                continue;
            }
            if (first?.Symbol is { Role: not SymbolRole.Graphic } && !record.HasCommercialData && record.Description.Length == 0)
            {
                f.Add(new(DrcSeverity.Info, "DRC-INFO-01", $"{g.Key}: üretici/model/sipariş numarası ve açıklama girilmemiş (malzeme listesi eksik kalır).",
                    first.Page, [first.Placement.Id], first.Location.ToString()));
            }
            if (record.Contacts.Count > 0)
            {
                var (slots, unassigned) = ContactUsage.Compute(record, parts);
                foreach (var x in unassigned)
                {
                    f.Add(new(DrcSeverity.Error, "DRC-CONT-02",
                        $"{g.Key}: {x.KindText} {x.Paths} kontağı cihazın kontak kapasitesinde yok ({string.Join(", ", record.Contacts.Select(c => c.Display))}).",
                        x.Page, [x.Placement.Id], x.Location.ToString()));
                }
                var free = slots.Where(s => !s.IsUsed && s.Slot.Kind != ContactKind.Power).ToList();
                if (free.Count > 0 && first is not null)
                {
                    f.Add(new(DrcSeverity.Info, "DRC-CONT-01", $"{g.Key}: kullanılmayan kontak {string.Join(", ", free.Select(s => s.Slot.Display))}.",
                        first.Page, [first.Placement.Id], first.Location.ToString()));
                }
            }
        }

        foreach (var orphan in doc.Devices.Where(r => !DeviceRegistry.Placements(doc, r.Reference).Any()))
        {
            f.Add(new(DrcSeverity.Info, "DRC-TAG-04", $"{orphan.Reference}: cihaz kaydı var ama şemada yerleşimi yok.", null, [], ""));
        }
    }

    private static void CheckPotentials(ProjectNetwork network, List<DrcFinding> f)
    {
        foreach (var (sources, direct) in network.PotentialConflicts())
        {
            var names = string.Join(" ↔ ", sources.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase));
            var first = sources[0];
            network.PageOf.TryGetValue(first.Id, out var page);
            f.Add(direct
                ? new(DrcSeverity.Error, "DRC-POT-01", $"Kısa devre: {names} potansiyelleri aynı iletkende birleşiyor.", page,
                    sources.Select(s => s.Id).ToList(), network.LocationOf(first)?.ToString() ?? "")
                : new(DrcSeverity.Warning, "DRC-POT-02", $"Olası kısa devre: {names} potansiyelleri bir kontak/kesici kapanınca birleşir.", page,
                    sources.Select(s => s.Id).ToList(), network.LocationOf(first)?.ToString() ?? ""));
        }
    }

    private static void CheckPageLinks(ProjectDocument doc, ProjectNetwork network, List<DrcFinding> f)
    {
        foreach (var page in doc.Pages)
        {
            foreach (var link in page.Objects.OfType<PageLinkObject>())
            {
                if (string.IsNullOrWhiteSpace(link.Name))
                {
                    f.Add(new(DrcSeverity.Error, "DRC-LINK-02", "Sayfa bağlantısının adı yok.", page, [link.Id], Loc(network, page, link)));
                }
                else if (network.LinkTargets(link).Count == 0)
                {
                    f.Add(new(DrcSeverity.Warning, "DRC-LINK-01", $"'{link.Name}' sayfa bağlantısının karşı ucu yok (aynı adlı başka bağlantı bulunamadı).",
                        page, [link.Id], Loc(network, page, link)));
                }
            }
        }
    }
}
