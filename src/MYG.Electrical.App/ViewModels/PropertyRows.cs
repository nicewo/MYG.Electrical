using System.Globalization;
using MYG.Electrical.App.Mvvm;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Özellik panelindeki tek satır.</summary>
public abstract class PropertyRow(string label) : ObservableObject
{
    public string Label { get; } = label;

    /// <summary>Model değiştiğinde görüntülenen değeri yeniler.</summary>
    public abstract void Refresh();
}

public sealed class SectionRow(string title) : PropertyRow(title)
{
    public override void Refresh() { }
}

public sealed class TextRow(string label, Func<string> getter, Action<string>? setter) : PropertyRow(label)
{
    public bool IsReadOnly => setter is null;

    public string Value
    {
        get => getter();
        set
        {
            if (setter is not null && value != getter())
            {
                setter(value);
            }
            OnPropertyChanged();
        }
    }

    public override void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>Sayısal değer (mm, derece...). Geçersiz girişte eski değere döner.</summary>
public sealed class NumberRow(string label, Func<double> getter, Action<double>? setter, string format = "0.##") : PropertyRow(label)
{
    public bool IsReadOnly => setter is null;

    public string Value
    {
        get => getter().ToString(format, CultureInfo.CurrentCulture);
        set
        {
            var normalized = value.Trim().Replace(',', '.');
            if (setter is not null && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                && double.IsFinite(v) && Math.Abs(v - getter()) > double.Epsilon)
            {
                setter(v);
            }
            OnPropertyChanged();
        }
    }

    public override void Refresh() => OnPropertyChanged(nameof(Value));
}

public sealed class ChoiceRow(string label, IReadOnlyList<string> options, Func<string> getter, Action<string> setter) : PropertyRow(label)
{
    public IReadOnlyList<string> Options { get; } = options;

    public string Value
    {
        get => getter();
        set
        {
            if (value is not null && value != getter())
            {
                setter(value);
            }
            OnPropertyChanged();
        }
    }

    public override void Refresh() => OnPropertyChanged(nameof(Value));
}

public sealed class BoolRow(string label, Func<bool> getter, Action<bool> setter) : PropertyRow(label)
{
    public bool Value
    {
        get => getter();
        set
        {
            if (value != getter())
            {
                setter(value);
            }
            OnPropertyChanged();
        }
    }

    public override void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>Dosya yolu + Gözat düğmesi.</summary>
public sealed class FileRow(string label, Func<string> getter, Action<string> setter, Func<string?> browse) : PropertyRow(label)
{
    public string Value
    {
        get => getter();
        set
        {
            if (value != getter())
            {
                setter(value);
            }
            OnPropertyChanged();
        }
    }

    public RelayCommand BrowseCommand => new(() =>
    {
        if (browse() is { } path)
        {
            Value = path;
        }
    });

    public RelayCommand ClearCommand => new(() => Value = string.Empty);

    public override void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>Panelde komut düğmesi (ör. "Resim ayarları…").</summary>
public sealed class ButtonRow(string label, Action action) : PropertyRow(label)
{
    public RelayCommand Command { get; } = new(action);

    public override void Refresh() { }
}
