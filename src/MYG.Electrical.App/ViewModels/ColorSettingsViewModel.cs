using System.Collections.ObjectModel;
using System.Windows.Media;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.Core.Rendering;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Renk ayarı satırı: ad, "#RRGGBB" ve önizleme.</summary>
public sealed class ColorRow : ObservableObject
{
    private string _hex;

    public ColorRow(ColorSchemeProperties.Entry entry, uint color)
    {
        Entry = entry;
        _hex = ColorScheme.ToHex(color);
    }

    public ColorSchemeProperties.Entry Entry { get; }
    public string Label => Entry.Label;

    public string Hex
    {
        get => _hex;
        set
        {
            if (SetField(ref _hex, value))
            {
                OnPropertyChanged(nameof(Swatch));
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    public bool IsValid => ColorScheme.TryParseHex(_hex, out _);

    public Brush Swatch
    {
        get
        {
            if (!ColorScheme.TryParseHex(_hex, out var c))
            {
                return Brushes.Transparent;
            }
            var b = new SolidColorBrush(Color.FromArgb((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c));
            b.Freeze();
            return b;
        }
    }
}

/// <summary>
/// Renk ayarları: potansiyel, sembol, çerçeve, metin, seçim ve hata renkleri. Renk yalnızca görsel yardımdır;
/// potansiyelin adı ve türü ayrıca gösterilir (renk körlüğü ve siyah-beyaz baskı için).
/// </summary>
public sealed class ColorSettingsViewModel : ObservableObject
{
    public ColorSettingsViewModel(ColorScheme current)
    {
        Load(current);
        ResetCommand = new RelayCommand(() => Load(ColorScheme.Default));
    }

    public ObservableCollection<ColorRow> Rows { get; } = [];
    public RelayCommand ResetCommand { get; }
    public ColorScheme Result { get; private set; } = ColorScheme.Default;

    private void Load(ColorScheme scheme)
    {
        Rows.Clear();
        foreach (var e in ColorSchemeProperties.All)
        {
            Rows.Add(new ColorRow(e, e.Get(scheme)));
        }
    }

    /// <summary>Geçersiz değer varsa hata metni, yoksa null (sonuç <see cref="Result"/>).</summary>
    public string? Apply()
    {
        var scheme = ColorScheme.Default;
        foreach (var row in Rows)
        {
            if (!ColorScheme.TryParseHex(row.Hex, out var c))
            {
                return $"'{row.Label}' için geçersiz renk: {row.Hex} (#RRGGBB biçiminde girin).";
            }
            scheme = row.Entry.Set(scheme, c);
        }
        Result = scheme;
        return null;
    }
}
