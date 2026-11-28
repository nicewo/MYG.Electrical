using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Infrastructure.Serialization;

/// <summary>.myge (JSON) proje formatı okuma/yazma.</summary>
public static class ProjectSerializer
{
    private const string FormatVersionProperty = "formatVersion";

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Türkçe karakterler dosyada okunabilir kalsın (\u00FC yerine ü)
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        IgnoreReadOnlyProperties = true,
        // Tanınmayan alan varsa sessizce atılmaz, dosya açılmaz: aksi halde eski bir sürüm
        // yeni alanları içeren dosyayı açıp kaydettiğinde veri kaybolurdu.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    public static string Serialize(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.FormatVersion = ProjectDocument.CurrentFormatVersion;
        // Kullanılmayan (silinmiş resimlere ait) kaynaklar dosyaya yazılmaz.
        var snapshot = new ProjectDocument
        {
            FormatVersion = document.FormatVersion,
            Project = document.Project,
            Settings = document.Settings,
            Pages = document.Pages,
            Devices = document.Devices,
            Symbols = document.Symbols,
            Resources = document.UsedResources(),
        };
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static ProjectDocument Deserialize(string json) => DeserializeWithReport(json).Document;

    /// <summary>Dosyayı okur, eski sürümleri yükseltir, onarılabilir sorunları düzeltip uyarı olarak döndürür.</summary>
    public static ProjectLoadResult DeserializeWithReport(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProjectFormatException("Proje dosyası geçerli bir JSON değil (dosya yarım kalmış veya bozulmuş olabilir).", ex);
        }

        if (root is not JsonObject obj || obj[FormatVersionProperty] is not JsonValue versionNode
            || !versionNode.TryGetValue<int>(out var version))
        {
            throw new ProjectFormatException("Proje dosyasında zorunlu 'formatVersion' alanı bulunamadı.");
        }
        if (version > ProjectDocument.CurrentFormatVersion)
        {
            throw new ProjectFormatException(
                $"Bu dosya daha yeni bir MYG Electrical sürümüyle kaydedilmiş (format {version}). Programı güncelleyin.");
        }
        if (version < 1)
        {
            throw new ProjectFormatException($"Geçersiz format sürümü: {version}.");
        }

        Migrate(obj, version);
        var warnings = new List<string>();
        CheckObjectKinds(obj);
        RepairResourceData(obj, warnings);

        ProjectDocument doc;
        try
        {
            doc = obj.Deserialize<ProjectDocument>(Options)
                ?? throw new ProjectFormatException("Proje dosyası boş.");
        }
        catch (JsonException ex)
        {
            throw new ProjectFormatException($"Proje dosyası okunamadı ({ex.Path}): {ex.Message}", ex);
        }
        doc.FormatVersion = ProjectDocument.CurrentFormatVersion;
        warnings.AddRange(ProjectValidator.Repair(doc));
        return new ProjectLoadResult(doc, warnings);
    }

    /// <summary>Programın tanıdığı nesne türleri (DrawingObject üzerindeki JsonDerivedType kayıtları).</summary>
    public static IReadOnlySet<string> KnownObjectKinds { get; } = typeof(DrawingObject)
        .GetCustomAttributes(typeof(JsonDerivedTypeAttribute), inherit: false)
        .Cast<JsonDerivedTypeAttribute>()
        .Select(a => (string)a.TypeDiscriminator!)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Tanınmayan nesne türünde, nerede olduğunu söyleyen anlaşılır hata (nesne sessizce atılmaz).</summary>
    private static void CheckObjectKinds(JsonObject root)
    {
        var pages = root["pages"] as JsonArray ?? [];
        for (var p = 0; p < pages.Count; p++)
        {
            if (pages[p] is not JsonObject page || page["objects"] is not JsonArray objects)
            {
                continue;
            }
            for (var i = 0; i < objects.Count; i++)
            {
                if (objects[i] is null)
                {
                    continue;
                }
                var kind = objects[i] is JsonObject o && o["kind"] is JsonValue v && v.TryGetValue<string>(out var k) ? k : null;
                if (kind is null || !KnownObjectKinds.Contains(kind))
                {
                    var pageName = (page["name"] as JsonValue)?.ToString() ?? $"#{p + 1}";
                    throw new ProjectFormatException(
                        $"'{pageName}' sayfasının {i + 1}. nesnesi tanınmayan türde: '{kind ?? "(tür yok)"}'. " +
                        "Dosya daha yeni bir sürümden gelmiş veya elle bozulmuş olabilir; veri kaybını önlemek için açılmadı.");
                }
            }
        }
    }

    /// <summary>Bozuk base64 resim verisi tüm projeyi açılmaz yapmasın: o kaynak boşaltılır ve uyarı verilir.</summary>
    private static void RepairResourceData(JsonObject root, List<string> warnings)
    {
        foreach (var res in (root["resources"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (res["data"] is not JsonValue data || !data.TryGetValue<string>(out var b64))
            {
                continue;
            }
            var buffer = new byte[b64.Length];
            if (!Convert.TryFromBase64String(b64, buffer, out _))
            {
                res["data"] = string.Empty;
                warnings.Add($"'{res["fileName"]}' kaynağının verisi bozuk; resim yer tutucu ile gösterilecek.");
            }
        }
    }

    /// <summary>Eski format sürümlerini, nesneye çevirmeden önce JSON ağacı üzerinde güncel sürüme yükseltir.</summary>
    private static void Migrate(JsonObject root, int fromVersion)
    {
        if (fromVersion < 2)
        {
            // v1 → v2: junction'lar her zaman 4 yönlüydü; anlam korunsun diye "cross" olarak işaretlenir.
            foreach (var junction in EnumerateObjects(root).Where(o => (string?)o["kind"] == "junction"))
            {
                junction["shape"] ??= "cross";
                junction["rotation"] ??= 0;
            }
        }
        // v2 → v3: yalnızca ekleme (resources, image). Dönüşüm gerekmez. Not: 37afe6e derlemesi bir hata
        // nedeniyle resim içeren dosyaları "formatVersion": 2 olarak yazdı; bu dosyalar da olduğu gibi açılır.
        // v3 → v4: yalnızca ekleme (devices, settings, wireData, description, pinDesignations). Cihaz kayıtları
        // nesneye çevrildikten sonra ProjectValidator içinde etiketlerden üretilir (DeviceRegistry.EnsureRecords).
        // v4 → v5: yalnızca yeni nesne türleri (potential, pageLink). Dönüşüm gerekmez.
        // v5 → v6: cihaz kaydına templateId ve contacts eklendi (boş varsayılan). Dönüşüm gerekmez.
        // v6 → v7: devicePin nesnesi, dikdörtgende dashed, gömülü kullanıcı sembolleri (symbols). Dönüşüm gerekmez.
        // v7 → v8: gömülü sembolde etiket yeri (referencePosition, varsayılan right). Dönüşüm gerekmez.
        root[FormatVersionProperty] = ProjectDocument.CurrentFormatVersion;
    }

    private static IEnumerable<JsonObject> EnumerateObjects(JsonObject root) =>
        (root["pages"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .SelectMany(page => (page["objects"] as JsonArray ?? []).OfType<JsonObject>());
}
