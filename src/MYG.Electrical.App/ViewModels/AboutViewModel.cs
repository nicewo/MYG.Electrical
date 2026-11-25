using MYG.Electrical.App.Mvvm;
using MYG.Electrical.App.Services;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Hakkında penceresi. Sürüm sabit yazılmaz: kurulu Velopack sürümünden veya derleme sürümünden okunur.</summary>
public sealed class AboutViewModel(string version, bool isInstalled, IShellService shell, Action checkForUpdates)
{
    public string ProductName => BrandInfo.ProductName;
    public string Version => version;
    public string VersionLine => isInstalled
        ? $"Sürüm {version} · GitHub Releases ile otomatik güncellenir"
        : $"Sürüm {version} · geliştirme derlemesi (otomatik güncelleme kapalı)";
    public string Description => BrandInfo.ProductDescription;
    public string DevelopedBy => BrandInfo.DevelopedBy;
    public string Developer => BrandInfo.Developer;
    public string DeveloperTitle => BrandInfo.DeveloperTitle;
    public string LicenseNotice => BrandInfo.LicenseNotice;
    public string Website => BrandInfo.WebsiteUrl.Replace("https://", string.Empty).TrimEnd('/');
    public string Repository => BrandInfo.RepositoryUrl.Replace("https://", string.Empty);

    public IEnumerable<string> Components =>
        BrandInfo.ThirdPartyComponents.Select(c => $"{c.Name} — {c.License}");

    public RelayCommand OpenWebsiteCommand { get; } = new(() => shell.OpenUrl(BrandInfo.WebsiteUrl));
    public RelayCommand OpenRepositoryCommand { get; } = new(() => shell.OpenUrl(BrandInfo.RepositoryUrl));
    public RelayCommand OpenReleasesCommand { get; } = new(() => shell.OpenUrl(BrandInfo.ReleasesUrl));
    public RelayCommand OpenNoticesCommand { get; } = new(() => shell.OpenUrl(BrandInfo.ThirdPartyNoticesUrl));
    public RelayCommand CheckUpdatesCommand { get; } = new(checkForUpdates);
}
