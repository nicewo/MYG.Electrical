using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Symbols;

/// <summary>
/// Yerleşik IEC 60617 sembol kütüphanesi. Tanımlar derlemeye gömülü <c>Library/*.json</c> dosyalarından okunur;
/// dosyalar <c>tools/symbolgen/generate.py</c> ile üretilir. Sembol kimlikleri proje dosyalarında saklandığı için
/// değiştirilmez (bkz. <see cref="SymbolIds"/>).
/// </summary>
public sealed class BuiltInSymbolLibrary : ISymbolCatalog
{
    private const string ResourcePrefix = "MYG.Electrical.Symbols.Library.";
    private readonly Dictionary<string, SymbolDefinition> _byId;

    public static BuiltInSymbolLibrary Instance { get; } = new();

    public BuiltInSymbolLibrary()
    {
        var warnings = new List<string>();
        var asm = typeof(BuiltInSymbolLibrary).Assembly;
        var all = new List<SymbolDefinition>();
        foreach (var name in asm.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            all.AddRange(SymbolLibraryLoader.Read(stream, name[ResourcePrefix.Length..], warnings));
        }
        All = all;
        Warnings = warnings;
        _byId = new Dictionary<string, SymbolDefinition>(StringComparer.Ordinal);
        foreach (var s in all)
        {
            if (!_byId.TryAdd(s.Id, s))
            {
                warnings.Add($"Yerleşik kütüphanede '{s.Id}' iki kez tanımlı.");
            }
        }
    }

    public IReadOnlyList<SymbolDefinition> All { get; }

    /// <summary>Yerleşik kütüphanenin yüklenmesinde oluşan uyarılar (normalde boş; testler bunu doğrular).</summary>
    public IReadOnlyList<string> Warnings { get; }

    public SymbolDefinition? Find(string symbolId) => _byId.GetValueOrDefault(symbolId);

    /// <summary>Yerleşik + kullanıcı klasöründeki sembollerden birleşik katalog oluşturur.</summary>
    public static SymbolCatalog CreateCatalog(string? userSymbolDirectory)
    {
        var warnings = new List<string>(Instance.Warnings);
        var user = userSymbolDirectory is null ? [] : SymbolLibraryLoader.ReadDirectory(userSymbolDirectory, warnings);
        return new SymbolCatalog(Instance.All, user, warnings);
    }
}
