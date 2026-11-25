using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Devices;

/// <summary>Etiket ↔ cihaz kaydı ↔ sembol yerleşimleri ilişkisi.</summary>
public static class DeviceRegistry
{
    public static readonly StringComparer ReferenceComparer = StringComparer.OrdinalIgnoreCase;

    public static DeviceRecord? Find(ProjectDocument doc, string reference) =>
        string.IsNullOrWhiteSpace(reference) ? null : doc.Devices.FirstOrDefault(d => ReferenceComparer.Equals(d.Reference, reference));

    /// <summary>Bir cihazın bütün yerleşimleri (sayfa sırasıyla).</summary>
    public static IEnumerable<(Page Page, DeviceObject Placement)> Placements(ProjectDocument doc, string reference) =>
        doc.Pages.SelectMany(p => p.Objects.OfType<DeviceObject>()
            .Where(d => ReferenceComparer.Equals(d.Reference, reference))
            .Select(d => (p, d)));

    /// <summary>
    /// Yerleşimi olup kaydı olmayan her etiket için kayıt oluşturur (v3 dosyalarının yükseltilmesi, eski
    /// sürümle düzenlenmiş dosyalar). Oluşturulan kayıt sayısını döndürür. Var olan kayıtlara dokunmaz.
    /// </summary>
    public static int EnsureRecords(ProjectDocument doc, ISymbolCatalog? catalog = null)
    {
        var known = new HashSet<string>(doc.Devices.Select(d => d.Reference), ReferenceComparer);
        var created = 0;
        foreach (var placement in doc.Pages.SelectMany(p => p.Objects).OfType<DeviceObject>())
        {
            if (string.IsNullOrWhiteSpace(placement.Reference) || !known.Add(placement.Reference))
            {
                continue;
            }
            doc.Devices.Add(CreateRecord(placement.Reference, catalog?.Find(placement.SymbolId)));
            created++;
        }
        return created;
    }

    /// <summary>Sembolden yeni cihaz kaydı (kategori, varsayılan özellikler, kütüphane sürümü).</summary>
    public static DeviceRecord CreateRecord(string reference, SymbolDefinition? symbol) => new()
    {
        Reference = reference,
        Category = symbol?.Category ?? string.Empty,
        LibraryVersion = symbol?.Library ?? string.Empty,
        Properties = symbol is null ? [] : new Dictionary<string, string>(symbol.DefaultProperties),
    };

    /// <summary>Projede kullanılan etiketler (yerleşimler + kayıtlar).</summary>
    public static IEnumerable<string> UsedReferences(ProjectDocument doc) =>
        doc.Pages.SelectMany(p => p.Objects).OfType<DeviceObject>().Select(d => d.Reference)
            .Concat(doc.Devices.Select(d => d.Reference))
            .Where(r => !string.IsNullOrWhiteSpace(r));
}
