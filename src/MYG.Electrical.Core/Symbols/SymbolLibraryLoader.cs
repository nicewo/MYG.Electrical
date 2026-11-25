using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MYG.Electrical.Core.Symbols;

/// <summary>
/// JSON sembol kütüphanesi okuyucu. Dosya biçimi:
/// <c>{ "formatVersion": 1, "library": "...", "version": "1.0.0", "symbols": [ {...}, ... ] }</c>.
/// Bozuk bir sembol tüm dosyayı düşürmez: atlanır ve uyarı olarak raporlanır.
/// Yeni sembol eklemek için kod değişikliği gerekmez; JSON dosyası yeterlidir.
/// </summary>
public static class SymbolLibraryLoader
{
    public const int SupportedFormatVersion = 1;

    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }

    /// <summary>Tek bir kütüphane dosyasını okur. <paramref name="source"/> uyarı mesajlarında görünür.</summary>
    public static IReadOnlyList<SymbolDefinition> Read(Stream json, string source, ICollection<string> warnings, bool userDefined = false)
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
            if (root.ValueKind != JsonValueKind.Object || !TryGet(root, "symbols", out var symbols) || symbols.ValueKind != JsonValueKind.Array)
            {
                warnings.Add($"{source}: 'symbols' dizisi bulunamadı.");
                return [];
            }
            if (TryGet(root, "formatVersion", out var fv) && fv.TryGetInt32(out var version) && version > SupportedFormatVersion)
            {
                warnings.Add($"{source}: kütüphane biçimi {version} bu sürümden yeni (desteklenen {SupportedFormatVersion}); yine de okunmaya çalışılıyor.");
            }
            var library = string.Join(' ', new[] { Str(root, "library"), Str(root, "version") }.Where(s => s.Length > 0));

            var result = new List<SymbolDefinition>();
            var index = 0;
            foreach (var element in symbols.EnumerateArray())
            {
                index++;
                SymbolDefinition? def;
                try
                {
                    def = element.Deserialize<SymbolDefinition>(JsonOptions);
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
                {
                    warnings.Add($"{source}: {index}. sembol okunamadı ({ex.Message}).");
                    continue;
                }
                if (def is null)
                {
                    continue;
                }
                var problem = Validate(def);
                if (problem is not null)
                {
                    warnings.Add($"{source}: '{def.Id}' atlandı: {problem}");
                    continue;
                }
                def.Library = library;
                def.IsUserDefined = userDefined;
                result.Add(def);
            }
            return result;
        }
    }

    /// <summary>Sembol tanımının yapısal doğruluğu; sorun yoksa null.</summary>
    public static string? Validate(SymbolDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Id))
        {
            return "kimlik (id) boş.";
        }
        if (string.IsNullOrWhiteSpace(def.Name))
        {
            return "ad (name) boş.";
        }
        if (def.ConnectionPoints.Count == 0)
        {
            return "en az bir pin gerekli.";
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in def.ConnectionPoints)
        {
            if (string.IsNullOrWhiteSpace(p.Id))
            {
                return "pin kimliği boş.";
            }
            if (!ids.Add(p.Id))
            {
                return $"pin '{p.Id}' birden fazla tanımlı.";
            }
            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
            {
                return $"pin '{p.Id}' koordinatı geçersiz.";
            }
        }
        foreach (var c in def.InternalConnections)
        {
            if (!ids.Contains(c.A) || !ids.Contains(c.B))
            {
                return $"iç bağlantı {c.A}-{c.B} tanımsız pine işaret ediyor.";
            }
        }
        return null;
    }

    /// <summary>Bir klasördeki tüm *.json kütüphanelerini okur (kullanıcı sembolleri).</summary>
    public static IReadOnlyList<SymbolDefinition> ReadDirectory(string directory, ICollection<string> warnings)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }
        var result = new List<SymbolDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var stream = File.OpenRead(file);
                result.AddRange(Read(stream, Path.GetFileName(file), warnings, userDefined: true));
            }
            catch (IOException ex)
            {
                warnings.Add($"{Path.GetFileName(file)}: okunamadı ({ex.Message}).");
            }
            catch (UnauthorizedAccessException ex)
            {
                warnings.Add($"{Path.GetFileName(file)}: erişim yok ({ex.Message}).");
            }
        }
        return result;
    }

    private static bool TryGet(JsonElement e, string name, out JsonElement value)
    {
        foreach (var p in e.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string Str(JsonElement e, string name) =>
        TryGet(e, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
