using Serilog;

namespace MYG.Electrical.Infrastructure.Logging;

public static class LogSetup
{
    private const int RetainedLogFiles = 14;

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MYG.Electrical", "logs");

    /// <summary>Günlük dönen dosya loglayıcı oluşturur: %LOCALAPPDATA%\MYG.Electrical\logs</summary>
    public static ILogger CreateLogger()
    {
        Directory.CreateDirectory(LogDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(LogDirectory, "myg-electrical-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedLogFiles,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
