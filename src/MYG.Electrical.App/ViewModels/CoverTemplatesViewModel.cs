using System.Collections.ObjectModel;
using System.IO;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.App.Services;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Library;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Listede bir kapak şablonu.</summary>
public sealed class CoverTemplateItem(string name, bool isDefault)
{
    public string Name { get; } = name;
    public bool IsDefault { get; } = isDefault;
    public bool IsBuiltIn => CoverTemplateStore.IsStandard(Name);

    public string Display => (IsDefault ? "★ " : "    ") + Name + (IsBuiltIn ? "  (yerleşik)" : string.Empty);
}

/// <summary>
/// Kapak şablonları penceresi: etkin kapağı şablon olarak kaydet, şablonu kapağa uygula, yeni projelerin
/// varsayılan şablonunu seç, şablon sil.
/// </summary>
public sealed class CoverTemplatesViewModel : ObservableObject
{
    private readonly CoverTemplateStore _store;
    private readonly AppSettingsService _settings;
    private readonly Func<EditorSession> _session;
    private readonly IDialogService _dialogs;
    private CoverTemplateItem? _selected;
    private string _newName = string.Empty;
    private bool _makeDefault = true;
    private string _status = string.Empty;

    public CoverTemplatesViewModel(CoverTemplateStore store, AppSettingsService settings, Func<EditorSession> session, IDialogService dialogs)
    {
        _store = store;
        _settings = settings;
        _session = session;
        _dialogs = dialogs;
        SaveCommand = new RelayCommand(SaveCurrent, () => !string.IsNullOrWhiteSpace(NewName));
        ApplyCommand = new RelayCommand(Apply, () => Selected is not null);
        SetDefaultCommand = new RelayCommand(SetDefault, () => Selected is { IsDefault: false });
        DeleteCommand = new RelayCommand(Delete, () => Selected is { IsBuiltIn: false });
        OpenFolderCommand = new RelayCommand(() =>
        {
            Directory.CreateDirectory(_store.Folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_store.Folder) { UseShellExecute = true });
        });
        Reload(DefaultName);
    }

    public ObservableCollection<CoverTemplateItem> Items { get; } = [];

    public CoverTemplateItem? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public string NewName
    {
        get => _newName;
        set => SetField(ref _newName, value);
    }

    /// <summary>Kaydedilen şablon yeni projelerin varsayılanı olsun.</summary>
    public bool MakeDefault
    {
        get => _makeDefault;
        set => SetField(ref _makeDefault, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string CoverPageText => CoverPage() is { } p
        ? $"Etkin kapak: \"{p.Name}\" (sayfa {_session().Document.Pages.IndexOf(p) + 1})"
        : "Projede kapak sayfası yok: uygulanınca başa eklenir.";

    public RelayCommand SaveCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand SetDefaultCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    private string DefaultName => string.IsNullOrWhiteSpace(_settings.Settings.DefaultCoverTemplate) || !_store.Exists(_settings.Settings.DefaultCoverTemplate)
        ? CoverTemplate.StandardName
        : _settings.Settings.DefaultCoverTemplate;

    private void Reload(string? select)
    {
        Items.Clear();
        foreach (var name in _store.Names())
        {
            Items.Add(new CoverTemplateItem(name, string.Equals(name, DefaultName, StringComparison.CurrentCultureIgnoreCase)));
        }
        Selected = Items.FirstOrDefault(i => string.Equals(i.Name, select, StringComparison.CurrentCultureIgnoreCase)) ?? Items.FirstOrDefault();
    }

    /// <summary>Etkin sayfa kapaksa o, değilse projenin ilk kapak sayfası.</summary>
    private Page? CoverPage()
    {
        var s = _session();
        return s.ActivePage.Type == PageType.Cover ? s.ActivePage : s.Document.Pages.FirstOrDefault(p => p.Type == PageType.Cover);
    }

    private void SaveCurrent()
    {
        var name = NewName.Trim();
        var page = CoverPage();
        if (page is null)
        {
            Status = "Projede kapak sayfası yok; önce Sayfa → Yeni kapak sayfası ekleyin.";
            return;
        }
        try
        {
            CoverTemplateStore.ValidateName(name);
            if (_store.Exists(name) && !_dialogs.Confirm($"'{name}' şablonu zaten var. Üzerine yazılsın mı?"))
            {
                return;
            }
            _store.Save(CoverTemplate.Capture(_session().Document, page, name));
            if (MakeDefault)
            {
                _settings.Settings.DefaultCoverTemplate = name;
                _settings.Save();
            }
            Status = MakeDefault ? $"'{name}' kaydedildi; yeni projeler bu kapakla başlayacak." : $"'{name}' kaydedildi.";
            NewName = string.Empty;
            Reload(name);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            Status = ex.Message;
        }
    }

    private void Apply()
    {
        if (Selected is null)
        {
            return;
        }
        var (template, error) = _store.Load(Selected.Name);
        var session = _session();
        var page = CoverPage();
        if (page is null)
        {
            page = session.AddPage(PageType.Cover);
        }
        else if (page.Objects.Count > 0 && !_dialogs.Confirm($"\"{page.Name}\" sayfasındaki kapak tasarımı '{template.Name}' şablonuyla değiştirilecek (Geri Al ile dönülebilir). Devam edilsin mi?"))
        {
            return;
        }
        session.ApplyCoverTemplate(template, page);
        Status = error ?? $"'{template.Name}' kapağa uygulandı. Bilgiler proje bilgisinden doldu.";
        OnPropertyChanged(nameof(CoverPageText));
    }

    private void SetDefault()
    {
        if (Selected is null)
        {
            return;
        }
        _settings.Settings.DefaultCoverTemplate = Selected.IsBuiltIn ? string.Empty : Selected.Name;
        _settings.Save();
        Status = $"Yeni projeler '{Selected.Name}' kapağıyla başlayacak.";
        Reload(Selected.Name);
    }

    private void Delete()
    {
        if (Selected is not { IsBuiltIn: false } item || !_dialogs.Confirm($"'{item.Name}' kapak şablonu silinsin mi? (Projelerdeki kapaklar etkilenmez.)"))
        {
            return;
        }
        try
        {
            _store.Delete(item.Name);
            if (item.IsDefault)
            {
                _settings.Settings.DefaultCoverTemplate = string.Empty;
                _settings.Save();
            }
            Status = $"'{item.Name}' silindi.";
            Reload(null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = ex.Message;
        }
    }
}
