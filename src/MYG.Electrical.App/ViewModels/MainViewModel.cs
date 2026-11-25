using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.App.Services;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Drawing;
using MYG.Electrical.Infrastructure.Logging;
using MYG.Electrical.Infrastructure.Pdf;
using MYG.Electrical.Infrastructure.Serialization;
using Serilog;

namespace MYG.Electrical.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly ProjectFileService _files;
    private readonly PdfExporter _pdf;
    private readonly ILogger _log;
    private readonly ISymbolCatalog _catalog;
    private readonly UpdateService _updates;
    private readonly IShellService _shell;
    private bool _checkingUpdates;

    private EditorSession _session;
    private string? _filePath;
    private PageItemViewModel? _selectedPage;
    private SymbolDefinition? _selectedSymbol;
    private EditorTool _activeTool = EditorTool.Select;
    private ImageObject? _pendingImage;
    private string _symbolFilter = string.Empty;
    private string _cursorText = "X 0  Y 0";
    private string _zoomText = "100 %";
    private string _statusMessage = "Hazır";

    public MainViewModel(IDialogService dialogs, ProjectFileService files, PdfExporter pdf, ILogger log, ISymbolCatalog catalog,
        UpdateService updates, IShellService shell)
    {
        _shell = shell;
        _updates = updates;
        _dialogs = dialogs;
        _files = files;
        _pdf = pdf;
        _log = log;
        _catalog = catalog;
        Properties = new PropertiesViewModel(dialogs, EditImage, () => Canvas?.InvalidateFileImages());

        SymbolsView = CollectionViewSource.GetDefaultView(catalog.All);
        SymbolsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SymbolDefinition.Category)));
        SymbolsView.Filter = o => o is SymbolDefinition s
            && (string.IsNullOrWhiteSpace(_symbolFilter) || s.Name.Contains(_symbolFilter, StringComparison.CurrentCultureIgnoreCase));

        _session = CreateSession(ProjectDocument.CreateNew());
        AttachSession(_session);

        NewCommand = new RelayCommand(NewProject);
        OpenCommand = new RelayCommand(() => _ = OpenProjectAsync(null));
        SaveCommand = new RelayCommand(() => Save(saveAs: false));
        SaveAsCommand = new RelayCommand(() => Save(saveAs: true));
        ExportPdfCommand = new RelayCommand(() => ExportPdf(activePageOnly: false));
        ExportPagePdfCommand = new RelayCommand(() => ExportPdf(activePageOnly: true));
        UndoCommand = new RelayCommand(() => Session.Undo(), () => Session.History.CanUndo);
        RedoCommand = new RelayCommand(() => Session.Redo(), () => Session.History.CanRedo);
        DeleteCommand = new RelayCommand(() => Session.DeleteSelection(), HasSelection);
        CopyCommand = new RelayCommand(() => Session.CopySelection(), HasSelection);
        CutCommand = new RelayCommand(() => { Session.CopySelection(); Session.DeleteSelection(); }, HasSelection);
        PasteCommand = new RelayCommand(() => Session.Paste(), () => Session.CanPaste);
        SelectAllCommand = new RelayCommand(() => Session.SelectAll());
        RotateCommand = new RelayCommand(Rotate, () => ActiveTool.IsPlacementTool() || HasSelection());
        NudgeCommand = new RelayCommand(p => Nudge(p as string), _ => HasSelection());
        ZoomInCommand = new RelayCommand(() => Canvas?.ZoomIn());
        ZoomOutCommand = new RelayCommand(() => Canvas?.ZoomOut());
        ZoomFitCommand = new RelayCommand(() => Canvas?.ZoomToFit());
        SetToolCommand = new RelayCommand(p => ActiveTool = p is EditorTool t ? t : Enum.Parse<EditorTool>(p?.ToString() ?? nameof(EditorTool.Select)));
        InsertImageCommand = new RelayCommand(InsertImage);
        AddSchematicPageCommand = new RelayCommand(() => Session.AddPage(PageType.Schematic));
        AddCoverPageCommand = new RelayCommand(() => Session.AddPage(PageType.Cover));
        DeletePageCommand = new RelayCommand(DeleteActivePage, () => Session.Document.Pages.Count > 1);
        MovePageUpCommand = new RelayCommand(() => Session.MovePage(Session.ActivePage, -1));
        MovePageDownCommand = new RelayCommand(() => Session.MovePage(Session.ActivePage, +1));
        NextPageCommand = new RelayCommand(() => StepPage(+1));
        PreviousPageCommand = new RelayCommand(() => StepPage(-1));
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        CheckUpdatesCommand = new RelayCommand(() => _ = CheckForUpdatesAsync(manual: true), () => !_checkingUpdates);
        AboutCommand = new RelayCommand(() => _dialogs.ShowAbout(
            new AboutViewModel(_updates.CurrentVersion, _updates.IsInstalled, _shell, () => _ = CheckForUpdatesAsync(manual: true))));
        ShortcutsCommand = new RelayCommand(() => _dialogs.ShowShortcuts());
        UserGuideCommand = new RelayCommand(() => _shell.OpenUrl(BrandInfo.UserGuideUrl));
        SourceCodeCommand = new RelayCommand(() => _shell.OpenUrl(BrandInfo.RepositoryUrl));
        WebsiteCommand = new RelayCommand(() => _shell.OpenUrl(BrandInfo.WebsiteUrl));
        ReportIssueCommand = new RelayCommand(() => _shell.OpenUrl(BrandInfo.IssuesUrl));
        OpenSampleCommand = new RelayCommand(() => _ = OpenSampleAsync(), () => File.Exists(SamplePath));
    }

    // ---------- Durum ----------

    public PropertiesViewModel Properties { get; }
    public ObservableCollection<PageItemViewModel> Pages { get; } = [];
    public ObservableCollection<ProjectNodeViewModel> ProjectTree { get; } = [];
    public ICollectionView SymbolsView { get; }

    /// <summary>View tarafından atanır.</summary>
    public ICanvasController? Canvas { get; set; }

    public EditorSession Session
    {
        get => _session;
        private set
        {
            if (!ReferenceEquals(_session, value))
            {
                _session = value;
                AttachSession(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(GridVisible));
                OnPropertyChanged(nameof(SnapEnabled));
                OnPropertyChanged(nameof(ShowConnectionPoints));
            }
        }
    }

    public string Title
    {
        get
        {
            var name = _filePath is null ? Session.Document.Project.Name : Path.GetFileName(_filePath);
            return $"{name}{(Session.IsDirty ? " *" : string.Empty)} — MYG Electrical";
        }
    }

    public PageItemViewModel? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (SetField(ref _selectedPage, value) && value is not null)
            {
                Session.ActivePage = value.Page;
            }
        }
    }

    public SymbolDefinition? SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            if (SetField(ref _selectedSymbol, value) && value is not null)
            {
                ActiveTool = EditorTool.Symbol;
            }
        }
    }

    /// <summary>Resim aracıyla yerleştirilmeyi bekleyen resim.</summary>
    public ImageObject? PendingImage
    {
        get => _pendingImage;
        private set => SetField(ref _pendingImage, value);
    }

    public EditorTool ActiveTool
    {
        get => _activeTool;
        set
        {
            if (SetField(ref _activeTool, value))
            {
                StatusMessage = ToolHint(value);
                if (value != EditorTool.Symbol && _selectedSymbol is not null)
                {
                    // Aynı sembol kütüphaneden tekrar seçilebilsin
                    _selectedSymbol = null;
                    OnPropertyChanged(nameof(SelectedSymbol));
                }
            }
        }
    }

    public string SymbolFilter
    {
        get => _symbolFilter;
        set
        {
            if (SetField(ref _symbolFilter, value))
            {
                SymbolsView.Refresh();
            }
        }
    }

    public bool GridVisible
    {
        get => Session.Settings.GridVisible;
        set
        {
            Session.Settings.GridVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GridText));
            Canvas?.RefreshAll();
        }
    }

    public bool SnapEnabled
    {
        get => Session.Settings.SnapEnabled;
        set
        {
            Session.Settings.SnapEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GridText));
        }
    }

    public bool ShowConnectionPoints
    {
        get => Session.Settings.ShowConnectionPoints;
        set
        {
            Session.Settings.ShowConnectionPoints = value;
            OnPropertyChanged();
            Canvas?.RefreshAll();
        }
    }

    public string CursorText
    {
        get => _cursorText;
        private set => SetField(ref _cursorText, value);
    }

    public string ZoomText
    {
        get => _zoomText;
        private set => SetField(ref _zoomText, value);
    }

    public string GridText => string.Create(CultureInfo.CurrentCulture,
        $"Grid {Session.Settings.GridSpacing:0.#} mm · {(GridVisible ? "görünür" : "gizli")} · Snap {(SnapEnabled ? "açık" : "kapalı")}");

    public string ActivePageText
    {
        get
        {
            var doc = Session.Document;
            var page = Session.ActivePage;
            return $"Sayfa {doc.Pages.IndexOf(page) + 1}/{doc.Pages.Count}: {page.Name} ({page.Format})";
        }
    }

    public string WireCountText => $"{Session.Wires.Count} kablo";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    // ---------- Komutlar ----------

    public RelayCommand NewCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportPagePdfCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand CutCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand RotateCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ZoomFitCommand { get; }
    public RelayCommand SetToolCommand { get; }
    public RelayCommand InsertImageCommand { get; }
    public RelayCommand NudgeCommand { get; }
    public RelayCommand AddSchematicPageCommand { get; }
    public RelayCommand AddCoverPageCommand { get; }
    public RelayCommand DeletePageCommand { get; }
    public RelayCommand MovePageUpCommand { get; }
    public RelayCommand MovePageDownCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand CheckUpdatesCommand { get; }

    public string VersionText => "v" + _updates.CurrentVersion;
    public RelayCommand AboutCommand { get; }
    public RelayCommand ShortcutsCommand { get; }
    public RelayCommand UserGuideCommand { get; }
    public RelayCommand SourceCodeCommand { get; }
    public RelayCommand WebsiteCommand { get; }
    public RelayCommand ReportIssueCommand { get; }
    public RelayCommand OpenSampleCommand { get; }

    /// <summary>Kurulumla gelen örnek proje.</summary>
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "Samples", "Demo.myge");

    // ---------- Canvas geri bildirimleri ----------

    public void OnCursorMoved(Point2D world) =>
        CursorText = string.Create(CultureInfo.CurrentCulture, $"X {world.X:0.0}  Y {world.Y:0.0} mm");

    public void OnViewChanged(ViewTransform view) =>
        ZoomText = string.Create(CultureInfo.CurrentCulture, $"{view.Zoom * 100:0} %");

    // ---------- Proje işlemleri ----------

    /// <summary>Kapatma/yeni/açma öncesi kaydedilmemiş değişiklik kontrolü. false → işlem iptal.</summary>
    public bool ConfirmDiscardChanges()
    {
        if (!Session.IsDirty)
        {
            return true;
        }
        return _dialogs.ConfirmSaveChanges(Session.Document.Project.Name) switch
        {
            true => Save(saveAs: false),
            false => true,
            null => false,
        };
    }

    private void NewProject()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }
        _filePath = null;
        Session = CreateSession(ProjectDocument.CreateNew());
        StatusMessage = "Yeni proje oluşturuldu";
    }

    public async Task OpenProjectAsync(string? path)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }
        path ??= _dialogs.OpenProjectFile();
        if (path is null)
        {
            return;
        }
        try
        {
            var result = await _files.OpenAsync(path);
            _filePath = path;
            Session = CreateSession(result.Document);
            StatusMessage = $"Açıldı: {Path.GetFileName(path)}";
            if (result.Warnings.Count > 0)
            {
                Session.IsDirty = true; // onarılan veriler kaydedilsin
                OnPropertyChanged(nameof(Title));
                _dialogs.ShowInfo("Proje açıldı, ancak bazı sorunlar onarıldı:\n\n• " + string.Join("\n• ", result.Warnings.Take(10)));
            }
        }
        catch (ProjectFormatException ex)
        {
            _log.Warning(ex, "Proje açılamadı: {Path}", path);
            _dialogs.ShowError(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error(ex, "Proje dosyası okunamadı: {Path}", path);
            _dialogs.ShowError($"Dosya okunamadı:\n{ex.Message}");
        }
    }

    private bool Save(bool saveAs)
    {
        var path = _filePath;
        if (saveAs || path is null)
        {
            path = _dialogs.SaveProjectFile(Session.Document.Project.Name);
            if (path is null)
            {
                return false;
            }
        }
        try
        {
            _files.Save(Session.Document, path);
            _filePath = path;
            Session.IsDirty = false;
            OnPropertyChanged(nameof(Title));
            StatusMessage = $"Kaydedildi: {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error(ex, "Proje kaydedilemedi: {Path}", path);
            _dialogs.ShowError($"Proje kaydedilemedi:\n{ex.Message}");
            return false;
        }
    }

    private void ExportPdf(bool activePageOnly)
    {
        var doc = Session.Document;
        var suggested = activePageOnly ? $"{doc.Project.Name} - {Session.ActivePage.Name}" : doc.Project.Name;
        if (_dialogs.SavePdfFile(suggested) is not { } path)
        {
            return;
        }
        try
        {
            var options = new PdfExportOptions
            {
                PageIndices = activePageOnly ? [doc.Pages.IndexOf(Session.ActivePage)] : null,
            };
            var warnings = _pdf.Export(doc, path, options);
            StatusMessage = $"PDF oluşturuldu: {Path.GetFileName(path)}";
            if (warnings.Count > 0)
            {
                _dialogs.ShowInfo("PDF oluşturuldu, ancak:\n\n• " + string.Join("\n• ", warnings));
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _log.Error(ex, "PDF oluşturulamadı: {Path}", path);
            _dialogs.ShowError($"PDF oluşturulamadı:\n{ex.Message}");
        }
    }

    // ---------- Resim ----------

    /// <summary>Ekle → Resim: dosya seç, boyut/saydamlık ayarla, projeye kopyala, tıklayarak yerleştir.</summary>
    private void InsertImage()
    {
        if (_dialogs.OpenImageFile() is not { } path)
        {
            return;
        }
        byte[] data;
        ImageDialogViewModel dialog;
        try
        {
            data = File.ReadAllBytes(path);
            dialog = new ImageDialogViewModel(data, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            _log.Warning(ex, "Resim açılamadı: {Path}", path);
            _dialogs.ShowError($"Resim açılamadı:\n{ex.Message}");
            return;
        }
        if (!_dialogs.ShowImageDialog(dialog))
        {
            return;
        }

        var doc = Session.Document;
        var source = doc.AddResource(data, dialog.FileName);
        var render = dialog.TransparencyEnabled
            ? doc.AddResource(dialog.BuildRenderData(), Path.GetFileNameWithoutExtension(dialog.FileName) + "-saydam.png")
            : source;
        PendingImage = new ImageObject
        {
            Width = dialog.WidthMm,
            Height = dialog.HeightMm,
            SourceResourceId = source.Id,
            RenderResourceId = render.Id,
            TransparencyEnabled = dialog.TransparencyEnabled,
            TransparentColor = dialog.TransparentColor,
            Tolerance = dialog.Tolerance,
        };
        ActiveTool = EditorTool.Image;
        StatusMessage = "Resim: sol üst köşesini yerleştirmek için tıklayın (Esc: iptal)";
    }

    /// <summary>Seçili resmin boyut ve saydamlık ayarlarını değiştirir (tek geri alma adımı).</summary>
    private void EditImage(ImageObject image)
    {
        var doc = Session.Document;
        if (doc.FindResource(image.SourceResourceId) is not { } source)
        {
            _dialogs.ShowError("Resmin kaynak verisi projede bulunamadı.");
            return;
        }
        var dialog = new ImageDialogViewModel(source.Data, source.FileName, image);
        if (!_dialogs.ShowImageDialog(dialog))
        {
            return;
        }
        var render = dialog.TransparencyEnabled
            ? doc.AddResource(dialog.BuildRenderData(), Path.GetFileNameWithoutExtension(source.FileName) + "-saydam.png")
            : source;
        var settings = new ImageSettings(render.Id, dialog.TransparencyEnabled, dialog.TransparentColor, dialog.Tolerance);
        Session.SetProperty(image, "Resim ayarları",
            () => (image.Settings, image.Width, image.Height),
            v => (image.Settings, image.Width, image.Height) = v,
            (settings, dialog.WidthMm, dialog.HeightMm));
    }

    private void DeleteActivePage()
    {
        var page = Session.ActivePage;
        if (page.Objects.Count > 0
            && !_dialogs.Confirm($"'{page.Name}' sayfası ve üzerindeki {page.Objects.Count} nesne silinecek (Ctrl+Z ile geri alınabilir). Devam edilsin mi?"))
        {
            return;
        }
        Session.RemovePage(page);
    }

    private void StepPage(int delta)
    {
        var pages = Session.Document.Pages;
        var index = pages.IndexOf(Session.ActivePage) + delta;
        if (index >= 0 && index < pages.Count)
        {
            Session.ActivePage = pages[index];
        }
    }

    // ---------- Güncelleme ----------

    /// <summary>
    /// GitHub'daki son sürümü denetler; yeniyse arka planda indirir ve kullanıcıya şimdi mi yoksa kapanışta mı
    /// kurulacağını sorar (kaydedilmemiş çalışma kaybolmasın diye zorla yeniden başlatılmaz).
    /// </summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (_checkingUpdates)
        {
            return;
        }
        if (!_updates.IsInstalled)
        {
            if (manual)
            {
                _dialogs.ShowInfo("Otomatik güncelleme yalnızca GitHub'daki Setup.exe ile kurulmuş sürümde çalışır.\n" +
                                  $"Şu an geliştirme derlemesi çalışıyor ({VersionText}).");
            }
            return;
        }

        _checkingUpdates = true;
        StatusMessage = "Güncelleme denetleniyor…";
        try
        {
            if (!await _updates.CheckAndDownloadAsync())
            {
                StatusMessage = $"Güncel sürüm: {VersionText}";
                if (manual)
                {
                    _dialogs.ShowInfo($"En güncel sürümü kullanıyorsunuz ({VersionText}).\n(İnternet yoksa denetlenemez; ayrıntı log dosyasında.)");
                }
                return;
            }

            StatusMessage = $"Yeni sürüm indirildi: v{_updates.PendingVersion}";
            if (_dialogs.Confirm($"MYG Electrical v{_updates.PendingVersion} indirildi (şu an {VersionText}).\n\n" +
                                 "Şimdi kurulup program yeniden başlatılsın mı?\nHayır derseniz program kapanınca kurulur."))
            {
                if (ConfirmDiscardChanges())
                {
                    _updates.ApplyAndRestart();
                    return;
                }
            }
            _updates.ApplyOnExit();
        }
        finally
        {
            _checkingUpdates = false;
        }
    }

    /// <summary>
    /// Örnek projeyi kaydedilmemiş yeni proje olarak açar: kaydederken konum sorulur, böylece program klasörüne
    /// (güncellemede silinir) yazılmaz.
    /// </summary>
    private async Task OpenSampleAsync()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }
        try
        {
            var result = await _files.OpenAsync(SamplePath);
            _filePath = null;
            Session = CreateSession(result.Document);
            StatusMessage = "Örnek proje açıldı (kaydederken yeni konum seçin)";
        }
        catch (Exception ex) when (ex is ProjectFormatException or IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Örnek proje açılamadı");
            _dialogs.ShowError($"Örnek proje açılamadı:\n{ex.Message}");
        }
    }

    private static void OpenLogFolder()
    {
        Directory.CreateDirectory(LogSetup.LogDirectory);
        Process.Start(new ProcessStartInfo(LogSetup.LogDirectory) { UseShellExecute = true });
    }

    /// <summary>
    /// Ok tuşlarıyla seçimi kaydırır. Parametre: "Left", "Right", "Up", "Down"; sonuna ":fine" eklenirse 1 mm,
    /// aksi halde bir grid adımı.
    /// </summary>
    private void Nudge(string? direction)
    {
        if (string.IsNullOrEmpty(direction))
        {
            return;
        }
        const double fineStepMm = 1.0;
        var parts = direction.Split(':');
        var step = parts.Length > 1 && parts[1] == "fine" ? fineStepMm : Session.Settings.GridSpacing;
        var (dx, dy) = parts[0] switch
        {
            "Left" => (-step, 0.0),
            "Right" => (step, 0.0),
            "Up" => (0.0, -step),
            _ => (0.0, step),
        };
        Session.MoveSelection(dx, dy);
    }

    /// <summary>R: yerleştirme aracındaysa önizlemeyi, değilse seçimi döndürür.</summary>
    private void Rotate()
    {
        if (ActiveTool.IsPlacementTool())
        {
            Canvas?.RotatePlacement();
        }
        else
        {
            Session.RotateSelection();
        }
    }

    private bool HasSelection() => Session.SelectedObjects.Count > 0;

    // ---------- Oturum bağlama ----------

    private EditorSession CreateSession(ProjectDocument doc)
    {
        var session = new EditorSession(doc, _catalog);
        session.SceneChanged += (_, _) => OnDocumentChanged();
        session.HistoryChanged += (_, _) => OnPropertyChanged(nameof(Title));
        session.WiresChanged += (_, _) => OnPropertyChanged(nameof(WireCountText));
        session.PagesChanged += (_, _) => RebuildPages();
        session.ActivePageChanged += (_, _) => SyncSelectedPage();
        return session;
    }

    /// <summary>Yeni oturumu panellere bağlar (alan zaten atanmış olmalı).</summary>
    private void AttachSession(EditorSession session)
    {
        Properties.Attach(session);
        RebuildPages();
        OnPropertyChanged(nameof(GridText));
        OnPropertyChanged(nameof(WireCountText));
    }

    private void OnDocumentChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActivePageText));
        foreach (var p in Pages)
        {
            p.Refresh();
        }
    }

    private void RebuildPages()
    {
        Pages.Clear();
        var pages = Session.Document.Pages;
        for (var i = 0; i < pages.Count; i++)
        {
            Pages.Add(new PageItemViewModel(pages[i], i + 1));
        }
        ProjectTree.Clear();
        ProjectTree.Add(new ProjectNodeViewModel(Session.Document.Project.Name, Pages));
        SyncSelectedPage();
        OnPropertyChanged(nameof(Title));
    }

    private void SyncSelectedPage()
    {
        _selectedPage = Pages.FirstOrDefault(p => ReferenceEquals(p.Page, Session.ActivePage));
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(ActivePageText));
        OnPropertyChanged(nameof(WireCountText));
    }

    private static string ToolHint(EditorTool tool) => tool switch
    {
        EditorTool.Line => "Çizgi: başlangıç ve bitiş noktasına tıklayın (Esc: iptal)",
        EditorTool.Rectangle => "Dikdörtgen: iki köşeye tıklayın (Esc: iptal)",
        EditorTool.Text => "Metin: yerleştirmek için tıklayın, içeriği sağ panelden düzenleyin",
        EditorTool.Symbol => "Sembol: tıklayarak yerleştirin · R: döndür · sağ tık / Esc: bitir",
        EditorTool.Angle => "Dirsek: 90° dönüş noktası · yön otomatik önerilir, R ile değiştirin",
        EditorTool.Tee => "T bağlantı: 3 yollu düğüm · yön otomatik önerilir, R ile değiştirin",
        EditorTool.Image => "Resim: sol üst köşesini yerleştirmek için tıklayın (Esc: iptal)",
        EditorTool.Cross => "Çapraz düğüm: kesişen yatay ve dikey hattı BİRLEŞTİRİR · yalnızca gerçekten bağlıysa kullanın",
        _ => "Seç: tıkla / sürükle · Ctrl+tık çoklu seçim · R döndür · Del sil",
    };
}
