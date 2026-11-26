using System.Globalization;
using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Reports;

/// <summary>Rapor tablosu (CSV / ekran).</summary>
public sealed record ReportTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

public enum ReportKind
{
    DeviceList,
    BillOfMaterials,
    TerminalPlan,
    CableList,
    PlcIoList,
}

/// <summary>Proje raporları: cihaz listesi, malzeme listesi (BOM), klemens planı, kablo listesi, PLC G/Ç listesi.</summary>
public static class ProjectReports
{
    public static string FileName(ReportKind kind) => kind switch
    {
        ReportKind.DeviceList => "Cihaz listesi",
        ReportKind.BillOfMaterials => "Malzeme listesi",
        ReportKind.TerminalPlan => "Klemens plani",
        ReportKind.CableList => "Kablo listesi",
        _ => "PLC GC listesi",
    };

    public static ReportTable Create(ReportKind kind, ProjectDocument doc, ProjectNetwork network, ISymbolCatalog catalog) => kind switch
    {
        ReportKind.DeviceList => DeviceList(doc, network, catalog),
        ReportKind.BillOfMaterials => BillOfMaterials(doc),
        ReportKind.TerminalPlan => TerminalPlan(doc, network, catalog),
        ReportKind.CableList => CableList(doc, network),
        _ => PlcIoList(doc, network, catalog),
    };

    /// <summary>Şemada yerleşimi olan cihazlar, etikete göre doğal sırayla (-K2, -K10).</summary>
    private static IEnumerable<DeviceRecord> PlacedDevices(ProjectDocument doc) =>
        doc.Devices.Where(r => DeviceRegistry.Placements(doc, r.Reference).Any()).OrderBy(r => r.Reference, NaturalComparer.Instance);

    public static ReportTable DeviceList(ProjectDocument doc, ProjectNetwork network, ISymbolCatalog catalog)
    {
        var rows = PlacedDevices(doc).Select(r =>
        {
            var parts = CrossReferences.Parts(network, catalog, r.Reference);
            return (IReadOnlyList<string>)
            [
                r.Reference, r.Description, r.Category, r.Manufacturer, r.Model, r.OrderNumber, r.Location,
                string.Join("; ", r.Properties.Select(p => $"{p.Key}={p.Value}")),
                string.Join(" ", parts.Select(p => p.Location.ToString())),
            ];
        }).ToList();
        return new("Cihaz listesi", ["Etiket", "Açıklama", "Kategori", "Üretici", "Model", "Sipariş no", "Yer", "Teknik veri", "Konumlar"], rows);
    }

    /// <summary>Malzeme listesi: aynı üretici + sipariş no/model + açıklama satırları adetle birleşir.</summary>
    public static ReportTable BillOfMaterials(ProjectDocument doc)
    {
        var rows = PlacedDevices(doc)
            .GroupBy(r => (r.Manufacturer.ToUpperInvariant(), (r.OrderNumber.Length > 0 ? r.OrderNumber : r.Model).ToUpperInvariant(), r.Description.ToUpperInvariant()))
            .OrderBy(g => g.First().Manufacturer, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.First().Description, StringComparer.CurrentCultureIgnoreCase)
            .Select((g, i) =>
            {
                var r = g.First();
                return (IReadOnlyList<string>)
                [
                    (i + 1).ToString(CultureInfo.InvariantCulture), g.Count().ToString(CultureInfo.InvariantCulture), r.Description,
                    r.Manufacturer, r.Model, r.OrderNumber, string.Join(", ", g.Select(x => x.Reference).Order(NaturalComparer.Instance)),
                ];
            }).ToList();
        return new("Malzeme listesi", ["Sıra", "Adet", "Açıklama", "Üretici", "Model", "Sipariş no", "Etiketler"], rows);
    }

    /// <summary>
    /// Klemens planı: klemens sembollerinin (X) her ucu için bağlı olduğu hedefler (etiket:pin), potansiyel ve kablo numarası.
    /// </summary>
    public static ReportTable TerminalPlan(ProjectDocument doc, ProjectNetwork network, ISymbolCatalog catalog)
    {
        var rows = new List<IReadOnlyList<string>>();
        var terminals = doc.Pages.SelectMany(p => p.Objects.OfType<DeviceObject>().Select(d => (Page: p, Device: d)))
            .Where(x => x.Device.SymbolId.StartsWith("iec.terminal", StringComparison.Ordinal))
            .OrderBy(x => x.Device.Reference, NaturalComparer.Instance)
            .ThenBy(x => doc.Pages.IndexOf(x.Page)).ThenBy(x => x.Device.X);
        foreach (var (page, d) in terminals)
        {
            var targets = d.ConnectionPoints.Select(cp => Targets(doc, network, new PinRef(d.Id, cp.Id))).ToList();
            var pinA = new PinRef(d.Id, d.ConnectionPoints.ElementAtOrDefault(0)?.Id ?? "");
            rows.Add(
            [
                d.Reference, string.Join("/", d.ConnectionPoints.Select(cp => d.PinLabel(cp.Id))),
                targets.ElementAtOrDefault(0) ?? "", targets.ElementAtOrDefault(1) ?? "",
                network.PotentialOf(pinA)?.Name ?? "", WireNumbers(page, network, d.Id),
                network.LocationOf(page, d.X).ToString(), d.Description,
            ]);
        }
        return new("Klemens planı", ["Klemens", "Uçlar", "Üst / iç hedef", "Alt / dış hedef", "Potansiyel", "Kablo no", "Konum", "Açıklama"], rows);
    }

    /// <summary>Kablo listesi: her kablo bir satır (numara, kaynak, hedef, potansiyel, renk, kesit, tip).</summary>
    public static ReportTable CableList(ProjectDocument doc, ProjectNetwork network)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var page in doc.Pages)
        {
            if (!network.Wires.TryGetValue(page, out var wires))
            {
                continue;
            }
            foreach (var w in wires.OrderBy(w => PageLayoutColumn(page, w)).ThenBy(w => Math.Min(w.Start.Y, w.End.Y)))
            {
                var data = page.FindWireData(w.From, w.To);
                var potential = network.PotentialOf(w);
                rows.Add(
                [
                    data?.Number ?? "", EndName(doc, w.From), EndName(doc, w.To), potential?.Name ?? "", data?.Color ?? "",
                    data?.CrossSection ?? "", data?.CableType ?? "", network.LocationOf(page, Math.Min(w.Start.X, w.End.X)).ToString(),
                    data?.Description ?? "",
                ]);
            }
        }
        return new("Kablo listesi", ["Kablo no", "Kaynak", "Hedef", "Potansiyel", "Renk", "Kesit", "Kablo tipi", "Konum", "Açıklama"], rows);
    }

    /// <summary>PLC G/Ç listesi: PLC parçalarının (A) giriş/çıkış pinleri, adres, işlev metni ve bağlı saha cihazı.</summary>
    public static ReportTable PlcIoList(ProjectDocument doc, ProjectNetwork network, ISymbolCatalog catalog)
    {
        var rows = new List<IReadOnlyList<string>>();
        var plcParts = doc.Pages.SelectMany(p => p.Objects.OfType<DeviceObject>().Select(d => (Page: p, Device: d)))
            .Where(x => x.Device.SymbolId.StartsWith("iec.plc.", StringComparison.Ordinal))
            .OrderBy(x => x.Device.Reference, NaturalComparer.Instance).ThenBy(x => doc.Pages.IndexOf(x.Page)).ThenBy(x => x.Device.X);
        foreach (var (page, d) in plcParts)
        {
            foreach (var cp in d.ConnectionPoints.Where(c => c.Function is PinFunction.Input or PinFunction.Output))
            {
                var pin = new PinRef(d.Id, cp.Id);
                var connected = network.NetOf(pin) is not null && network.Wires.Values.SelectMany(x => x).Any(w => w.From == pin || w.To == pin);
                rows.Add(
                [
                    d.Reference, d.PinLabel(cp.Id), cp.Function == PinFunction.Input ? "Giriş" : "Çıkış", d.Description,
                    connected ? Targets(doc, network, pin) : "", network.PotentialOf(pin)?.Name ?? "",
                    network.LocationOf(page, d.X).ToString(),
                ]);
            }
        }
        return new("PLC G/Ç listesi", ["PLC", "Adres / uç", "Tür", "İşlev", "Saha cihazı", "Potansiyel", "Konum"], rows);
    }

    private static int PageLayoutColumn(Page page, WireConnection w) => Rendering.PageLayout.ColumnAt(page, Math.Min(w.Start.X, w.End.X));

    /// <summary>Bir pinin bağlı olduğu diğer cihaz pinleri (aynı net, klemensler dahil): "-K1:A1, -S2:14".</summary>
    private static string Targets(ProjectDocument doc, ProjectNetwork network, PinRef pin)
    {
        if (network.NetOf(pin) is not { } net)
        {
            return string.Empty;
        }
        var names = network.PinsInNet(net)
            .Where(p => p.OwnerId != pin.OwnerId)
            .Select(p => (Pin: p, Obj: network.PageOf.TryGetValue(p.OwnerId, out var pg) ? pg.Find(p.OwnerId) : null))
            .Where(x => x.Obj is DeviceObject)
            .Select(x => EndName(doc, x.Pin))
            .Distinct()
            .Order(NaturalComparer.Instance)
            .ToList();
        return string.Join(", ", names);
    }

    private static string WireNumbers(Page page, ProjectNetwork network, Guid owner) =>
        string.Join(" ", (network.Wires.TryGetValue(page, out var ws) ? ws : [])
            .Where(w => w.Involves(owner))
            .Select(w => page.FindWireData(w.From, w.To)?.Number)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct());

    private static string EndName(ProjectDocument doc, PinRef pin)
    {
        var obj = doc.Pages.Select(p => p.Find(pin.OwnerId)).FirstOrDefault(o => o is not null);
        return obj switch
        {
            DeviceObject d => $"{d.Reference}:{d.PinLabel(pin.PinId)}",
            PotentialObject p => p.Name,
            PageLinkObject l => $"→{l.Name}",
            JunctionObject => "düğüm",
            _ => pin.PinId,
        };
    }
}

/// <summary>"-K2" &lt; "-K10" sıralaması (sayılar sayı olarak karşılaştırılır).</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static NaturalComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return string.Compare(x, y, StringComparison.Ordinal);
        }
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
            {
                var si = i;
                var sj = j;
                while (i < x.Length && char.IsDigit(x[i])) { i++; }
                while (j < y.Length && char.IsDigit(y[j])) { j++; }
                var a = x.AsSpan(si, i - si).TrimStart('0');
                var b = y.AsSpan(sj, j - sj).TrimStart('0');
                var c = a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.SequenceCompareTo(b);
                if (c != 0)
                {
                    return c;
                }
            }
            else
            {
                var c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (c != 0)
                {
                    return c;
                }
                i++;
                j++;
            }
        }
        return (x.Length - i).CompareTo(y.Length - j);
    }
}
