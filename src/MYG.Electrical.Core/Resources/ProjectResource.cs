using System.Security.Cryptography;

namespace MYG.Electrical.Core.Resources;

/// <summary>
/// Projeye gömülü kaynak dosya (resim). Eklenen resim bu sayede .myge dosyasının içine kopyalanır;
/// orijinal dosya silinse veya taşınsa da proje bozulmaz. JSON'da <see cref="Data"/> base64 olarak saklanır.
/// </summary>
public sealed class ProjectResource
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Kullanıcının seçtiği orijinal dosya adı (bilgi amaçlı).</summary>
    public string FileName { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];

    /// <summary>İçerikten türetilen kimlik: aynı dosya iki kez eklenirse tek kopya saklanır.</summary>
    public static string CreateId(ReadOnlySpan<byte> data) =>
        "res-" + Convert.ToHexStringLower(SHA256.HashData(data))[..16];
}
