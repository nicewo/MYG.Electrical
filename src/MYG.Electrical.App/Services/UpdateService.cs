using System.IO;
using System.Net.Http;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace MYG.Electrical.App.Services;

/// <summary>
/// GitHub Releases üzerinden otomatik güncelleme. İnternet yoksa veya uygulama kurulum dışı
/// (Visual Studio / dotnet run) çalışıyorsa sessizce hiçbir şey yapmaz.
/// </summary>
public sealed class UpdateService(ILogger logger)
{
    public const string RepositoryUrl = "https://github.com/nicewo/MYG.Electrical";

    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
    private UpdateInfo? _pending;

    /// <summary>Kurulu (Setup.exe ile) çalışıyor mu? Değilse güncelleme denetlenmez.</summary>
    public bool IsInstalled => _manager.IsInstalled;

    public string CurrentVersion =>
        _manager.CurrentVersion?.ToString() ?? typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "?";

    /// <summary>İndirilmiş ve uygulanmayı bekleyen sürüm.</summary>
    public string? PendingVersion => _pending?.TargetFullRelease.Version.ToString();

    /// <summary>Yeni sürüm varsa arka planda indirir. İndirildiyse true.</summary>
    public async Task<bool> CheckAndDownloadAsync(CancellationToken ct = default)
    {
        if (!IsInstalled)
        {
            logger.Debug("Güncelleme denetimi atlandı: uygulama kurulum dışı çalışıyor");
            return false;
        }
        try
        {
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                logger.Information("Güncel sürüm kullanılıyor ({Version})", CurrentVersion);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            logger.Information("Yeni sürüm bulundu: {Version}, indiriliyor", info.TargetFullRelease.Version);
            await _manager.DownloadUpdatesAsync(info, cancelToken: ct).ConfigureAwait(false);
            _pending = info;
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
        {
            // İnternet yok / GitHub'a ulaşılamıyor: sonraki açılışta tekrar denenir.
            logger.Information(ex, "Güncelleme denetlenemedi (bağlantı yok olabilir)");
            return false;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Güncelleme sırasında beklenmeyen hata");
            return false;
        }
    }

    /// <summary>İndirilen sürümü kurar ve uygulamayı yeniden başlatır.</summary>
    public void ApplyAndRestart()
    {
        if (_pending is not null)
        {
            logger.Information("Güncelleme uygulanıyor: {Version}", PendingVersion);
            _manager.ApplyUpdatesAndRestart(_pending);
        }
    }

    /// <summary>Kullanıcı "sonra" dediyse: uygulama kapanınca güncelleme kurulur.</summary>
    public void ApplyOnExit()
    {
        if (_pending is not null)
        {
            logger.Information("Güncelleme kapanışta uygulanacak: {Version}", PendingVersion);
            _manager.WaitExitThenApplyUpdates(_pending, silent: true, restart: false);
        }
    }
}
