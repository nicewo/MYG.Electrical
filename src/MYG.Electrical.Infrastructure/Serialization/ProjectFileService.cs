using System.Text;
using MYG.Electrical.Core.Pages;
using Serilog;

namespace MYG.Electrical.Infrastructure.Serialization;

/// <summary>Proje dosyalarını diske güvenli şekilde yazar/okur.</summary>
public sealed class ProjectFileService(ILogger logger)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task SaveAsync(ProjectDocument document, string path, CancellationToken ct = default)
    {
        var json = ProjectSerializer.Serialize(document);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);

        // Önce geçici dosyaya yaz, sonra değiştir: yarım kalmış kayıt eski dosyayı bozmaz.
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(temp, json, Utf8NoBom, ct).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
        logger.Information("Proje kaydedildi: {Path} ({Pages} sayfa)", path, document.Pages.Count);
    }

    /// <summary>Senkron kayıt (UI'da kapanış onayı gibi beklenmesi gereken akışlar için).</summary>
    public void Save(ProjectDocument document, string path)
    {
        var json = ProjectSerializer.Serialize(document);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, json, Utf8NoBom);
        File.Move(temp, path, overwrite: true);
        logger.Information("Proje kaydedildi: {Path} ({Pages} sayfa)", path, document.Pages.Count);
    }

    public async Task<ProjectDocument> OpenAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            throw new ProjectFormatException($"Dosya bulunamadı: {path}");
        }
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var doc = ProjectSerializer.Deserialize(json);
        logger.Information("Proje açıldı: {Path} ({Pages} sayfa)", path, doc.Pages.Count);
        return doc;
    }
}
