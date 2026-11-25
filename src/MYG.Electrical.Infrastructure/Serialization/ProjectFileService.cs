using System.Text;
using MYG.Electrical.Core.Pages;
using Serilog;

namespace MYG.Electrical.Infrastructure.Serialization;

/// <summary>
/// Proje dosyalarını diske güvenli şekilde yazar/okur.
/// Kayıt sırası: JSON üret → geri okunabildiğini doğrula → geçici dosyaya yaz ve diske boşalt →
/// asıl dosyayı tek adımda değiştir (önceki sürüm <c>.bak</c> olarak saklanır). Hiçbir adımda yarım dosya oluşmaz.
/// </summary>
public sealed class ProjectFileService(ILogger logger)
{
    public const string BackupExtension = ".bak";
    private const int WriteBufferSize = 1 << 16;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public Task SaveAsync(ProjectDocument document, string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.Run(() => Save(document, path), ct);
    }

    /// <summary>Senkron kayıt (kapanış onayı gibi beklenmesi gereken akışlar için).</summary>
    public void Save(ProjectDocument document, string path)
    {
        var json = ProjectSerializer.Serialize(document);
        // Yazmadan önce doğrula: okunamayacak bir dosya asla diske gitmesin.
        _ = ProjectSerializer.Deserialize(json);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, WriteBufferSize, FileOptions.WriteThrough))
            {
                var bytes = Utf8NoBom.GetBytes(json);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temp, fullPath, fullPath + BackupExtension, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
        logger.Information("Proje kaydedildi: {Path} ({Pages} sayfa, {Bytes} bayt)", fullPath, document.Pages.Count, json.Length);
    }

    public async Task<ProjectLoadResult> OpenAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            throw new ProjectFormatException($"Dosya bulunamadı: {path}");
        }
        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var result = ProjectSerializer.DeserializeWithReport(json);
        logger.Information("Proje açıldı: {Path} ({Pages} sayfa)", path, result.Document.Pages.Count);
        foreach (var warning in result.Warnings)
        {
            logger.Warning("Açılış uyarısı: {Warning}", warning);
        }
        return result;
    }
}
