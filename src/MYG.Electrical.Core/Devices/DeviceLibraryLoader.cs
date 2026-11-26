using System.Text.Json;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Devices;

/// <summary>
/// Cihaz kütüphanesi JSON okuyucu: <c>{ "formatVersion": 1, "library": "...", "version": "...", "devices": [...] }</c>.
/// Bozuk kayıt atlanır ve uyarı verilir; ana sembolü katalogda olmayan cihaz da atlanır.
/// </summary>
public static class DeviceLibraryLoader
{
    public static IReadOnlyList<DeviceTemplate> Read(Stream json, string source, ICollection<string> warnings,
        ISymbolCatalog? catalog = null, bool userDefined = false)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            warnings.Add($"{source}: JSON okunamadı ({ex.Message}).");
            return [];
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
            {
                warnings.Add($"{source}: 'devices' dizisi bulunamadı.");
                return [];
            }
            var library = string.Join(' ', new[] { Str(root, "library"), Str(root, "version") }.Where(s => s.Length > 0));
            var result = new List<DeviceTemplate>();
            var index = 0;
            foreach (var e in devices.EnumerateArray())
            {
                index++;
                DeviceTemplate? t;
                try
                {
                    t = e.Deserialize<DeviceTemplate>(SymbolLibraryLoader.JsonOptions);
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
                {
                    warnings.Add($"{source}: {index}. cihaz okunamadı ({ex.Message}).");
                    continue;
                }
                if (t is null)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(t.Id) || string.IsNullOrWhiteSpace(t.Name))
                {
                    warnings.Add($"{source}: {index}. cihazın kimliği veya adı boş.");
                    continue;
                }
                if (catalog is not null && catalog.Find(t.Symbol) is null)
                {
                    warnings.Add($"{source}: '{t.Id}' atlandı: '{t.Symbol}' sembolü kütüphanede yok.");
                    continue;
                }
                var missingPart = catalog is null ? null : t.Parts.FirstOrDefault(p => catalog.Find(p) is null);
                if (missingPart is not null)
                {
                    warnings.Add($"{source}: '{t.Id}' atlandı: '{missingPart}' parça sembolü kütüphanede yok.");
                    continue;
                }
                t.Library = library;
                t.IsUserDefined = userDefined;
                result.Add(t);
            }
            return result;
        }
    }

    public static IReadOnlyList<DeviceTemplate> ReadDirectory(string directory, ICollection<string> warnings, ISymbolCatalog? catalog)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }
        var result = new List<DeviceTemplate>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var stream = File.OpenRead(file);
                result.AddRange(Read(stream, Path.GetFileName(file), warnings, catalog, userDefined: true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"{Path.GetFileName(file)}: okunamadı ({ex.Message}).");
            }
        }
        return result;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}

/// <summary>Cihaz kütüphanesi (yerleşik + kullanıcı). Yerleşik kimlikler kullanıcı dosyasıyla ezilemez.</summary>
public sealed class DeviceLibrary
{
    private readonly Dictionary<string, DeviceTemplate> _byId = new(StringComparer.Ordinal);

    public DeviceLibrary(IEnumerable<DeviceTemplate> templates, IEnumerable<string>? warnings = null)
    {
        var w = new List<string>(warnings ?? []);
        var all = new List<DeviceTemplate>();
        foreach (var t in templates)
        {
            if (_byId.TryAdd(t.Id, t))
            {
                all.Add(t);
            }
            else
            {
                w.Add($"'{t.Id}' cihaz kimliği zaten tanımlı; atlandı.");
            }
        }
        _all = all;
        Warnings = w;
    }

    private readonly List<DeviceTemplate> _all;

    public IReadOnlyList<DeviceTemplate> All => _all;

    /// <summary>Kullanıcı cihazı ekler veya aynı kimlikli kullanıcı cihazını değiştirir; yerleşik olanı değiştirmez.</summary>
    public bool AddOrReplace(DeviceTemplate template)
    {
        if (_byId.TryGetValue(template.Id, out var existing))
        {
            if (!existing.IsUserDefined)
            {
                return false;
            }
            _all[_all.IndexOf(existing)] = template;
        }
        else
        {
            _all.Add(template);
        }
        _byId[template.Id] = template;
        return true;
    }
    public IReadOnlyList<string> Warnings { get; }
    public DeviceTemplate? Find(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<string> Manufacturers =>
        All.Select(t => t.Manufacturer).Where(m => m.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();

    public static DeviceLibrary Empty { get; } = new([]);
}
