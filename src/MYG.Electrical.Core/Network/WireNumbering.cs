using System.Globalization;
using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Network;

/// <summary>
/// EPLAN tarzı otomatik kablo numaralandırma: aynı iletken (net) üzerindeki bütün kablolar aynı numarayı alır.
/// Potansiyel kaynağına doğrudan bağlı netler potansiyelin adını alır (L1, +24V...). Elle kilitlenmiş numaralar
/// korunur ve netin geri kalanına yayılır. Sıra: sayfa, kolon, yukarıdan aşağı, soldan sağa.
/// </summary>
public static class WireNumbering
{
    /// <summary>Numaralandırma sonrası sayfa başına yeni kablo verisi listeleri (değişmeyen sayfalar dahil edilmez).</summary>
    public static IReadOnlyDictionary<Page, List<WireData>> Plan(ProjectDocument doc, ProjectNetwork network, bool renumberAll)
    {
        var format = string.IsNullOrWhiteSpace(doc.Settings.WireNumberFormat) ? ProjectSettings.DefaultWireNumberFormat : doc.Settings.WireNumberFormat;
        var newLists = doc.Pages.ToDictionary(p => p, p => p.WireData.Select(w => w.Clone()).ToList());

        var ordered = doc.Pages.SelectMany((page, pageIndex) =>
                (network.Wires.TryGetValue(page, out var ws) ? ws : [])
                .Select(w => (Page: page, PageIndex: pageIndex, Wire: w, Column: Rendering.PageLayout.ColumnAt(page, Math.Min(w.Start.X, w.End.X)))))
            .OrderBy(x => x.PageIndex).ThenBy(x => x.Column)
            .ThenBy(x => Math.Min(x.Wire.Start.Y, x.Wire.End.Y)).ThenBy(x => Math.Min(x.Wire.Start.X, x.Wire.End.X))
            .ToList();

        var byNet = ordered.GroupBy(x => network.NetOf(x.Wire.From) ?? -1).Where(g => g.Key >= 0).ToList();

        // Sayaç, mevcut sayısal numaraların en büyüğünden devam eder (yalnızca eksikler numaralanırken).
        var counter = 0;
        if (!renumberAll)
        {
            foreach (var w in newLists.Values.SelectMany(l => l))
            {
                if (int.TryParse(w.Number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    counter = Math.Max(counter, n);
                }
            }
        }

        foreach (var net in byNet)
        {
            var entries = net.Select(x => (x.Page, x.Wire, Data: Find(newLists[x.Page], x.Wire))).ToList();
            var locked = entries.Select(e => e.Data).FirstOrDefault(d => d is { NumberLocked: true, Number.Length: > 0 });
            var existing = entries.Select(e => e.Data).FirstOrDefault(d => d is { Number.Length: > 0 });
            var potential = network.NetSources(net.Key).Select(p => p.Name.Trim()).FirstOrDefault(n => n.Length > 0);

            string number;
            if (locked is not null)
            {
                number = locked.Number;
            }
            else if (potential is not null)
            {
                number = potential;
            }
            else if (!renumberAll && existing is not null)
            {
                number = existing.Number;
            }
            else
            {
                var first = net.First();
                number = format
                    .Replace("{n}", (++counter).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{page}", (first.PageIndex + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{column}", first.Column.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }

            foreach (var (page, wire, data) in entries)
            {
                if (data is { NumberLocked: true })
                {
                    continue;
                }
                if (data is null)
                {
                    newLists[page].Add(new WireData { A = wire.From, B = wire.To, Number = number });
                }
                else
                {
                    data.Number = number;
                }
            }
        }

        return newLists.Where(kv => !SameNumbers(kv.Key.WireData, kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static WireData? Find(List<WireData> list, WireConnection w) => list.FirstOrDefault(d => d.Connects(w.From, w.To));

    private static bool SameNumbers(List<WireData> a, List<WireData> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Connects(p.Second.A, p.Second.B) && p.First.Number == p.Second.Number);
}
