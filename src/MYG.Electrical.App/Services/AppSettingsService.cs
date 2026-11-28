using System.IO;
using System.Text.Json;
using MYG.Electrical.Core.Rendering;
using Serilog;

namespace MYG.Electrical.App.Services;

/// <summary>Kullanıcıya özel uygulama ayarları (%AppData%\MYG.Electrical\settings.json).</summary>
public sealed class AppSettings
{
    /// <summary>Arayüz dili ("tr" varsayılan; "en" = İngilizce).</summary>
    public string Language { get; set; } = "tr";

    public List<string> Favorites { get; set; } = [];
    public List<string> Recent { get; set; } = [];

    /// <summary>Renk ayarları, ad → "#RRGGBB" (ColorScheme özellik adları: L1, PE, Symbol...).</summary>
    public Dictionary<string, string> Colors { get; set; } = [];
}

/// <summary>Ayarları okur/yazar; dosya bozuksa varsayılanlarla devam eder (günlüğe yazar).</summary>
public sealed class AppSettingsService
{
    public const int MaxRecent = 15;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly ILogger _log;

    public AppSettingsService(ILogger log, string? path = null)
    {
        _log = log;
        Path = path ?? System.IO.Path.Combine(AppDataFolder, "settings.json");
        Settings = Load();
    }

    public static string AppDataFolder =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MYG.Electrical");

    public string Path { get; }
    public AppSettings Settings { get; }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Json) ?? new AppSettings();
                s.Favorites ??= [];
                s.Recent ??= [];
                s.Colors ??= [];
                return s;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Ayar dosyası okunamadı, varsayılanlar kullanılıyor: {Path}", Path);
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(Settings, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Ayar dosyası yazılamadı: {Path}", Path);
        }
    }

    public bool IsFavorite(string id) => Settings.Favorites.Contains(id);

    public void ToggleFavorite(string id)
    {
        if (!Settings.Favorites.Remove(id))
        {
            Settings.Favorites.Add(id);
        }
        Save();
    }

    public void AddRecent(string id)
    {
        Settings.Recent.Remove(id);
        Settings.Recent.Insert(0, id);
        if (Settings.Recent.Count > MaxRecent)
        {
            Settings.Recent.RemoveRange(MaxRecent, Settings.Recent.Count - MaxRecent);
        }
        Save();
    }

    /// <summary>Ayarlardaki renklerle varsayılan şemanın birleşimi (geçersiz değerler atlanır).</summary>
    public ColorScheme GetColorScheme()
    {
        var scheme = ColorScheme.Default;
        foreach (var (name, hex) in Settings.Colors)
        {
            if (ColorScheme.TryParseHex(hex, out var c) && ColorSchemeProperties.TrySet(scheme, name, c, out var updated))
            {
                scheme = updated;
            }
        }
        return scheme;
    }

    public void SetColorScheme(ColorScheme scheme)
    {
        Settings.Colors = ColorSchemeProperties.All.ToDictionary(p => p.Name, p => ColorScheme.ToHex(p.Get(scheme)));
        Save();
    }
}
