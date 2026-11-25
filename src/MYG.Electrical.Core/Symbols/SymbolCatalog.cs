namespace MYG.Electrical.Core.Symbols;

/// <summary>
/// Birden fazla kaynaktan (yerleşik + kullanıcı) birleştirilmiş sembol kataloğu.
/// Yerleşik kimlikler korunur: kullanıcı kütüphanesi aynı kimliği tanımlarsa atlanır (mevcut projeler bozulmasın).
/// </summary>
public sealed class SymbolCatalog : ISymbolCatalog
{
    private readonly Dictionary<string, SymbolDefinition> _byId;

    public SymbolCatalog(IEnumerable<SymbolDefinition> builtIn, IEnumerable<SymbolDefinition>? user = null, IEnumerable<string>? warnings = null)
    {
        var warn = new List<string>(warnings ?? []);
        _byId = new Dictionary<string, SymbolDefinition>(StringComparer.Ordinal);
        var all = new List<SymbolDefinition>();
        foreach (var s in builtIn.Concat(user ?? []))
        {
            if (!_byId.TryAdd(s.Id, s))
            {
                warn.Add($"'{s.Id}' kimliği zaten tanımlı; {(s.IsUserDefined ? "kullanıcı sembolü" : "sembol")} atlandı.");
                continue;
            }
            all.Add(s);
        }
        All = all;
        Warnings = warn;
    }

    public IReadOnlyList<SymbolDefinition> All { get; }

    /// <summary>Yükleme sırasında oluşan uyarılar (atlanan semboller, bozuk dosyalar).</summary>
    public IReadOnlyList<string> Warnings { get; }

    public SymbolDefinition? Find(string symbolId) => _byId.GetValueOrDefault(symbolId);

    /// <summary>Kategori yolları (ağaç için), sıralı ve tekil.</summary>
    public IReadOnlyList<string> Categories => All.Select(s => s.Category).Distinct(StringComparer.Ordinal).ToList();
}
