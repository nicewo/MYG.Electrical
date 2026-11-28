using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using MYG.Electrical.App.Localization;
using MYG.Electrical.App.Services;
using MYG.Electrical.App.ViewModels;
using MYG.Electrical.App.Views;
using MYG.Electrical.Infrastructure.Logging;
using MYG.Electrical.Infrastructure.Pdf;
using MYG.Electrical.Infrastructure.Serialization;
using MYG.Electrical.Symbols;
using Serilog;

namespace MYG.Electrical.App;

public partial class App : Application
{
    private ILogger _log = Serilog.Core.Logger.None;

    public App()
    {
        // Windows 11 Fluent koyu tema; kaynaklar (App.xaml) yüklenmeden önce ayarlanmalı.
#pragma warning disable WPF0001 // Fluent tema .NET 10'da deneysel olarak işaretli
        ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _log = LogSetup.CreateLogger();
        Log.Logger = _log;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _log.Fatal(args.ExceptionObject as Exception, "İşlenmeyen hata");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _log.Error(args.Exception, "Gözlemlenmeyen görev hatası");
            args.SetObserved();
        };
        _log.Information("MYG Electrical başlatıldı ({Version})", typeof(App).Assembly.GetName().Version);

        // Yerleşik IEC kütüphanesi + kullanıcı sembolleri (%AppData%\MYG.Electrical\Symbols\*.json)
        var userSymbols = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MYG.Electrical", "Symbols");
        var catalog = BuiltInSymbolLibrary.CreateCatalog(userSymbols);
        _log.Information("Sembol kütüphanesi: {Count} sembol", catalog.All.Count);
        foreach (var warning in catalog.Warnings)
        {
            _log.Warning("Sembol kütüphanesi: {Warning}", warning);
        }
        var settings = new AppSettingsService(_log);
        ApplyLanguage(settings.Settings.Language);
        var devices = BuiltInDeviceLibrary.Create(catalog, Path.Combine(AppSettingsService.AppDataFolder, "Devices"));
        _log.Information("Cihaz kütüphanesi: {Count} cihaz", devices.All.Count);
        foreach (var warning in devices.Warnings)
        {
            _log.Warning("Cihaz kütüphanesi: {Warning}", warning);
        }
        var dialogs = new DialogService();
        var updates = new UpdateService(_log);
        var shell = new ShellService(_log, dialogs);
        var vm = new MainViewModel(dialogs, new ProjectFileService(_log), new PdfExporter(catalog, _log), _log, catalog, updates, shell, devices, settings);
        var window = new MainWindow(vm);
        MainWindow = window;
        window.Show();

        // İnternet varsa GitHub'daki son sürümü arka planda denetle ve indir
        window.ContentRendered += (_, _) => _ = vm.CheckForUpdatesAsync(manual: false);

        var fileArg = e.Args.FirstOrDefault(a => a.EndsWith(Core.Pages.ProjectDocument.FileExtension, StringComparison.OrdinalIgnoreCase));
        if (fileArg is not null)
        {
            _ = vm.OpenProjectAsync(fileArg);
        }
    }

    /// <summary>Arayüz dilini uygular: .NET/WPF kültürü (sayı, tarih, standart iletişim kutuları) + panel menüleri.</summary>
    private void ApplyLanguage(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language == "en" ? "en-US" : "tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        if (language != "en" && !DockingLocalization.Apply(language))
        {
            _log.Warning("Panel menüleri çevrilemedi (AvalonDock iç yapısı değişmiş olabilir)");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.Information("MYG Electrical kapatıldı");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Error(e.Exception, "Arayüzde beklenmeyen hata");
        MessageBox.Show($"Beklenmeyen bir hata oluştu:\n{e.Exception.Message}\n\nAyrıntılar log dosyasına yazıldı:\n{LogSetup.LogDirectory}",
            "MYG Electrical", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
