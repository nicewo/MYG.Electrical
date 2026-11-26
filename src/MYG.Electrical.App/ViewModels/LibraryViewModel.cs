using System.Collections.ObjectModel;
using System.Windows.Media;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.App.Services;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Drawing;

namespace MYG.Electrical.App.ViewModels;

public enum LibraryMode
{
    Symbols,
    Devices,
}

/// <summary>Kütüphanedeki bir sembol veya cihaz.</summary>
public sealed class LibraryItemViewModel : ObservableObject
{
    private readonly ISymbolCatalog _catalog;
    private bool _isFavorite;

    public LibraryItemViewModel(SymbolDefinition symbol, ISymbolCatalog catalog, bool favorite)
    {
        _catalog = catalog;
        Symbol = symbol;
        Id = symbol.Id;
        Name = symbol.Name;
        Category = symbol.Category;
        Description = symbol.Description;
        Details = $"{symbol.Id} · {symbol.ReferencePrefix} · {symbol.ConnectionPoints.Count} pin{(symbol.IsUserDefined ? " · kullanıcı" : "")}";
        SearchText = string.Join(' ', [symbol.Name, symbol.Id, symbol.Category, symbol.Description, .. symbol.Keywords]);
        _isFavorite = favorite;
    }

    public LibraryItemViewModel(DeviceTemplate template, ISymbolCatalog catalog, bool favorite)
    {
        _catalog = catalog;
        Template = template;
        Symbol = catalog.Find(template.Symbol);
        Id = template.Id;
        Name = template.Name;
        Category = template.Category;
        Manufacturer = template.Manufacturer;
        Description = template.Description;
        var commercial = string.Join(" ", new[] { template.Manufacturer, template.Model, template.OrderNumber }.Where(s => s.Length > 0));
        Details = (commercial.Length > 0 ? commercial + " · " : "Genel şablon · ")
            + string.Join(", ", template.Properties.Take(3).Select(p => $"{p.Key}: {p.Value}"))
            + (template.Contacts.Count > 0 ? $" · kontaklar: {string.Join(", ", template.Contacts.Select(c => c.Display))}" : "");
        SearchText = string.Join(' ', [template.Name, template.Id, template.Category, template.Manufacturer, template.Model,
            template.OrderNumber, template.Description, .. template.Keywords]);
        _isFavorite = favorite;
    }

    public string Id { get; }
    public string Name { get; }
    public string Category { get; }
    public string Manufacturer { get; } = string.Empty;
    public string Description { get; }
    public string Details { get; }
    public string SearchText { get; }
    public SymbolDefinition? Symbol { get; }
    public DeviceTemplate? Template { get; }
    public bool IsDevice => Template is not null;

    public ImageSource? Preview => SymbolPreview.Create(Symbol, _catalog);

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }
}

/// <summary>Kütüphane ağacında kategori düğümü. Çocukları alt kategoriler ve öğelerdir.</summary>
public sealed class LibraryNodeViewModel(string name, bool expanded) : ObservableObject
{
    private bool _isExpanded = expanded;

    public string Name { get; } = name;
    public ObservableCollection<object> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public int Count => Children.Sum(c => c is LibraryNodeViewModel n ? n.Count : 1);
    public string Header => $"{Name} ({Count})";
}

/// <summary>
/// Sol paneldeki kütüphane: semboller ve cihazlar ayrı sekmeler, kategori ağacı, arama, üretici süzgeci,
/// favoriler ve son kullanılanlar, önizleme. Sürükle-bırak ve çift tıkla yerleştirme.
/// </summary>
public sealed class LibraryViewModel : ObservableObject
{
    public const string AllManufacturers = "Tüm üreticiler";
    private readonly ISymbolCatalog _catalog;
    private readonly DeviceLibrary _devices;
    private readonly AppSettingsService _settings;
    private readonly Action<LibraryItemViewModel> _place;
    private readonly List<LibraryItemViewModel> _symbolItems;
    private readonly List<LibraryItemViewModel> _deviceItems;
    private LibraryMode _mode = LibraryMode.Symbols;
    private string _search = string.Empty;
    private string _manufacturer = AllManufacturers;
    private LibraryItemViewModel? _selected;

    public LibraryViewModel(ISymbolCatalog catalog, DeviceLibrary devices, AppSettingsService settings, Action<LibraryItemViewModel> place)
    {
        _catalog = catalog;
        _devices = devices;
        _settings = settings;
        _place = place;
        _symbolItems = catalog.All.Select(s => new LibraryItemViewModel(s, catalog, settings.IsFavorite(s.Id))).ToList();
        _deviceItems = devices.All.Select(t => new LibraryItemViewModel(t, catalog, settings.IsFavorite(t.Id))).ToList();
        Manufacturers = [AllManufacturers, .. devices.Manufacturers];
        PlaceCommand = new RelayCommand(p => Place(p as LibraryItemViewModel ?? Selected), p => (p as LibraryItemViewModel ?? Selected) is not null);
        ToggleFavoriteCommand = new RelayCommand(p => ToggleFavorite(p as LibraryItemViewModel ?? Selected), p => (p as LibraryItemViewModel ?? Selected) is not null);
        ShowSymbolsCommand = new RelayCommand(() => Mode = LibraryMode.Symbols);
        ShowDevicesCommand = new RelayCommand(() => Mode = LibraryMode.Devices);
        Rebuild();
    }

    public ObservableCollection<object> Roots { get; } = [];
    public IReadOnlyList<string> Manufacturers { get; }
    public RelayCommand PlaceCommand { get; }
    public RelayCommand ToggleFavoriteCommand { get; }
    public RelayCommand ShowSymbolsCommand { get; }
    public RelayCommand ShowDevicesCommand { get; }

    public int SymbolCount => _symbolItems.Count;
    public int DeviceCount => _deviceItems.Count;

    public LibraryMode Mode
    {
        get => _mode;
        set
        {
            if (SetField(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsDeviceMode));
                OnPropertyChanged(nameof(IsSymbolMode));
                Selected = null;
                Rebuild();
            }
        }
    }

    public bool IsDeviceMode
    {
        get => _mode == LibraryMode.Devices;
        set
        {
            if (value)
            {
                Mode = LibraryMode.Devices;
            }
        }
    }

    public bool IsSymbolMode
    {
        get => _mode == LibraryMode.Symbols;
        set
        {
            if (value)
            {
                Mode = LibraryMode.Symbols;
            }
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            if (SetField(ref _search, value))
            {
                Rebuild();
            }
        }
    }

    public string Manufacturer
    {
        get => _manufacturer;
        set
        {
            if (SetField(ref _manufacturer, value ?? AllManufacturers))
            {
                Rebuild();
            }
        }
    }

    public LibraryItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => _selected is not null;

    public LibraryItemViewModel? FindItem(string id) =>
        _symbolItems.FirstOrDefault(i => i.Id == id) ?? _deviceItems.FirstOrDefault(i => i.Id == id);

    public void Place(LibraryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }
        _settings.AddRecent(item.Id);
        _place(item);
        Rebuild(keepExpansion: true);
    }

    private void ToggleFavorite(LibraryItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }
        _settings.ToggleFavorite(item.Id);
        item.IsFavorite = _settings.IsFavorite(item.Id);
        Rebuild(keepExpansion: true);
    }

    private bool Matches(LibraryItemViewModel i)
    {
        if (_mode == LibraryMode.Devices && _manufacturer != AllManufacturers
            && !string.Equals(i.Manufacturer, _manufacturer, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(_search))
        {
            return true;
        }
        // Her kelime ayrı aranır: "kontaktör 24" → adında/anahtar kelimesinde ikisi de geçenler
        return _search.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => i.SearchText.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }

    private void Rebuild(bool keepExpansion = false)
    {
        var expanded = keepExpansion
            ? Roots.OfType<LibraryNodeViewModel>().SelectMany(Flatten).Where(n => n.IsExpanded).Select(n => n.Name).ToHashSet()
            : null;
        Roots.Clear();
        var items = (_mode == LibraryMode.Symbols ? _symbolItems : _deviceItems).Where(Matches).ToList();
        var searching = !string.IsNullOrWhiteSpace(_search);

        if (!searching)
        {
            var favorites = items.Where(i => i.IsFavorite).ToList();
            if (favorites.Count > 0)
            {
                var fav = new LibraryNodeViewModel("★ Favoriler", expanded?.Contains("★ Favoriler") ?? true);
                favorites.ForEach(fav.Children.Add);
                Roots.Add(fav);
            }
            var recent = _settings.Settings.Recent.Select(id => items.FirstOrDefault(i => i.Id == id)).OfType<LibraryItemViewModel>().ToList();
            if (recent.Count > 0)
            {
                var rec = new LibraryNodeViewModel("Son kullanılanlar", expanded?.Contains("Son kullanılanlar") ?? false);
                recent.ForEach(rec.Children.Add);
                Roots.Add(rec);
            }
        }

        foreach (var top in items.GroupBy(i => i.Category.Split('/')[0]))
        {
            var node = new LibraryNodeViewModel(top.Key, searching || (expanded?.Contains(top.Key) ?? false));
            foreach (var sub in top.GroupBy(i => i.Category.Contains('/') ? i.Category[(i.Category.IndexOf('/') + 1)..] : string.Empty))
            {
                if (sub.Key.Length == 0)
                {
                    foreach (var i in sub)
                    {
                        node.Children.Add(i);
                    }
                    continue;
                }
                var subNode = new LibraryNodeViewModel(sub.Key, searching || (expanded?.Contains(sub.Key) ?? false));
                foreach (var i in sub)
                {
                    subNode.Children.Add(i);
                }
                node.Children.Add(subNode);
            }
            Roots.Add(node);
        }
        OnPropertyChanged(nameof(ResultText));
    }

    public string ResultText
    {
        get
        {
            var count = (_mode == LibraryMode.Symbols ? _symbolItems : _deviceItems).Count(Matches);
            return _mode == LibraryMode.Symbols ? $"{count} sembol" : $"{count} cihaz";
        }
    }

    private static IEnumerable<LibraryNodeViewModel> Flatten(LibraryNodeViewModel n) =>
        new[] { n }.Concat(n.Children.OfType<LibraryNodeViewModel>().SelectMany(Flatten));
}
