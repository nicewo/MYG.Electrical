using System.ComponentModel;
using System.Diagnostics;
using Serilog;

namespace MYG.Electrical.App.Services;

public sealed class ShellService(ILogger logger, IDialogService dialogs) : IShellService
{
    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            logger.Warning(ex, "Bağlantı açılamadı: {Url}", url);
            dialogs.ShowError($"Bağlantı açılamadı:\n{url}");
        }
    }
}
