using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Symbols;

/// <summary>
/// Kullanıcı sembollerinin projeye gömülmesi: çizimden oluşturulan cihazlar (Delta AS228T kutusu gibi) yalnızca
/// o bilgisayarın kütüphanesinde olsaydı, proje başka bilgisayarda açılınca çizilemezdi. Kaydederken projede
/// kullanılan kullanıcı sembolleri dosyaya yazılır, açarken katalogda yoksa eklenir.
/// </summary>
public static class ProjectSymbols
{
    /// <summary>Projede kullanılan kullanıcı sembollerini <see cref="ProjectDocument.Symbols"/> listesine yazar.</summary>
    public static void Embed(ProjectDocument doc, ISymbolCatalog catalog)
    {
        var used = doc.Pages.SelectMany(p => p.Objects.OfType<DeviceObject>()).Select(d => d.SymbolId).ToHashSet(StringComparer.Ordinal);
        var embedded = new List<SymbolDefinition>();
        foreach (var id in used.Order(StringComparer.Ordinal))
        {
            if (catalog.Find(id) is { IsUserDefined: true } def)
            {
                embedded.Add(def);
            }
            else if (catalog.Find(id) is null && doc.Symbols.FirstOrDefault(s => s.Id == id) is { } kept)
            {
                embedded.Add(kept); // katalogda yok ama dosyadan gelmişti: kaybolmasın
            }
        }
        doc.Symbols = embedded;
    }

    /// <summary>Dosyadaki gömülü sembollerden katalogda olmayanları ekler. Eklenen sayısını döndürür.</summary>
    public static int Register(ProjectDocument doc, SymbolCatalog catalog)
    {
        var added = 0;
        foreach (var def in doc.Symbols)
        {
            if (catalog.Find(def.Id) is null)
            {
                def.IsUserDefined = true;
                if (def.Library.Length == 0)
                {
                    def.Library = $"Proje: {doc.Project.Name}";
                }
                if (catalog.AddOrReplace(def))
                {
                    added++;
                }
            }
        }
        return added;
    }
}
