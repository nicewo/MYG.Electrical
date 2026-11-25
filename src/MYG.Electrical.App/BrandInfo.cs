namespace MYG.Electrical.App;

/// <summary>Ürün ve geliştirici kimliği: Hakkında penceresi, Yardım menüsü ve README ile aynı bilgiler.</summary>
public static class BrandInfo
{
    public const string ProductName = "MYG Electrical";
    public const string ProductDescription =
        "Makine imalatı için Windows tabanlı IEC elektrik şeması çizim yazılımı: semboller, otomatik kablolama, " +
        "çok sayfalı projeler ve vektörel PDF çıktısı.";

    public const string Brand = "MYG Tech";
    public const string DevelopedBy = "MYG Tech tarafından geliştirilmiştir.";
    public const string Developer = "Muhammed Yusuf Gültekin";
    public const string DeveloperTitle = "Software & Electronics Engineer";

    public const string WebsiteUrl = "https://myusufgultekin.web.app/";
    public const string GitHubProfileUrl = "https://github.com/nicewo";
    public const string RepositoryUrl = "https://github.com/nicewo/MYG.Electrical";
    public const string ReleasesUrl = RepositoryUrl + "/releases";
    public const string IssuesUrl = RepositoryUrl + "/issues";
    public const string UserGuideUrl = RepositoryUrl + "/blob/main/docs/KullanimKilavuzu.md";
    public const string ThirdPartyNoticesUrl = RepositoryUrl + "/blob/main/THIRD-PARTY-NOTICES.md";

    public const string LicenseNotice =
        "Ücretsiz dağıtılır; kaynak kodu GitHub'da herkese açıktır. Açık kaynak lisansı henüz belirlenmemiştir: " +
        "bir lisans dosyası yayımlanana kadar kodun kopyalanması, değiştirilmesi ve yeniden dağıtılması için " +
        "geliştiricinin izni gerekir.";

    /// <summary>Kurulum paketinde dağıtılan üçüncü taraf bileşenler (THIRD-PARTY-NOTICES.md ile aynı).</summary>
    public static readonly IReadOnlyList<(string Name, string License, string Url)> ThirdPartyComponents =
    [
        (".NET 10 Runtime ve WPF", "MIT", "https://github.com/dotnet/runtime"),
        ("PDFsharp 6.2", "MIT", "https://github.com/empira/PDFsharp"),
        ("Serilog / Serilog.Sinks.File", "Apache-2.0", "https://github.com/serilog/serilog"),
        ("Velopack", "MIT", "https://github.com/velopack/velopack"),
        ("Microsoft.Extensions.*.Abstractions", "MIT", "https://github.com/dotnet/runtime"),
        ("System.Security.Cryptography.Pkcs", "MIT", "https://github.com/dotnet/runtime"),
    ];
}
