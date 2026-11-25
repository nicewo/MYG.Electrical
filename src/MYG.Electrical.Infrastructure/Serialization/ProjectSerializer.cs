using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    public static string Serialize(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.FormatVersion = ProjectDocument.CurrentFormatVersion;
        return JsonSerializer.Serialize(document, Options);
    }

    public static ProjectDocument Deserialize(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProjectFormatException("Proje dosyası geçerli bir JSON değil.", ex);
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

        // Sürüm 1 güncel formattır; ileride eski sürümler için dönüşüm adımları burada uygulanır.

        try
        {
            var doc = obj.Deserialize<ProjectDocument>(Options)
                ?? throw new ProjectFormatException("Proje dosyası boş.");
            doc.FormatVersion = ProjectDocument.CurrentFormatVersion;
            return doc;
        }
        catch (JsonException ex)
        {
            throw new ProjectFormatException($"Proje dosyası okunamadı: {ex.Message}", ex);
        }
    }
}
