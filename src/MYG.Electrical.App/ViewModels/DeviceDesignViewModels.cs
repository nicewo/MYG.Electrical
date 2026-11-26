using MYG.Electrical.App.Mvvm;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Seçim listeleri için ad + değer.</summary>
public sealed record Choice<T>(string Text, T Value)
{
    public override string ToString() => Text;
}

/// <summary>Bağlantı adı penceresi (pin aracı): ad, işlev, sıralama biçimi, sonraki pinleri sormadan numarala.</summary>
public sealed class PinNameViewModel : ObservableObject
{
    private string _name;
    private Choice<PinFunction> _function;
    private Choice<PinSequence> _sequence;

    public PinNameViewModel(string name, PinFunction function, PinSequence sequence, bool autoContinue)
    {
        _name = name;
        _function = AllFunctions.FirstOrDefault(f => f.Value == function) ?? AllFunctions[0];
        _sequence = AllSequences.FirstOrDefault(s => s.Value == sequence) ?? AllSequences[0];
        AutoContinue = autoContinue;
    }

    public static IReadOnlyList<Choice<PinFunction>> AllFunctions { get; } =
    [
        new("Giriş (DI / AI)", PinFunction.Input),
        new("Çıkış (DO / AO)", PinFunction.Output),
        new("Güç + (L+, 24V, L)", PinFunction.SupplyPositive),
        new("Güç − (M, 0V, N)", PinFunction.SupplyNegative),
        new("Güç / faz (L1, U...)", PinFunction.Power),
        new("PE", PinFunction.ProtectiveEarth),
        new("Haberleşme (A, B, ETH)", PinFunction.Communication),
        new("Ortak uç (COM)", PinFunction.ContactCommon),
        new("Genel", PinFunction.General),
    ];

    public static IReadOnlyList<Choice<PinSequence>> AllSequences { get; } =
    [
        new("Otomatik (addan)", PinSequence.Auto),
        new("Ondalık: X0, X1 … X9, X10", PinSequence.Decimal),
        new("Sekizli: X0 … X7, X10 (Delta DVP)", PinSequence.Octal),
        new("Bayt.bit: I0.0 … I0.7, I1.0 (Siemens)", PinSequence.Byte8),
        new("Word.bit: X0.0 … X0.15, X1.0 (Delta AS)", PinSequence.Word16),
    ];

    // WPF statik özelliğe Binding ile bağlanamaz: örnek özellikler
    public IReadOnlyList<Choice<PinFunction>> Functions => AllFunctions;
    public IReadOnlyList<Choice<PinSequence>> Sequences => AllSequences;

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                OnPropertyChanged(nameof(Preview));
            }
        }
    }

    public Choice<PinFunction> Function
    {
        get => _function;
        set => SetField(ref _function, value);
    }

    public Choice<PinSequence> Sequence
    {
        get => _sequence;
        set
        {
            if (SetField(ref _sequence, value))
            {
                OnPropertyChanged(nameof(Preview));
            }
        }
    }

    public string Description { get; set; } = string.Empty;

    public bool AutoContinue { get; set; }

    /// <summary>"Sonraki: X1, X2, X3 …"</summary>
    public string Preview => string.IsNullOrWhiteSpace(_name)
        ? string.Empty
        : "Sonraki pinler: " + string.Join(", ", PinNaming.Sequence(_name.Trim(), 5, _sequence.Value).Skip(1)) + " …";
}

/// <summary>"Seçimden cihaz oluştur" penceresi.</summary>
public sealed class CreateDeviceViewModel : ObservableObject
{
    public CreateDeviceViewModel(IReadOnlyList<string> categories, int pinCount, int shapeCount)
    {
        Categories = categories;
        Summary = $"{pinCount} pin ve {shapeCount} çizim öğesi (çizgi, kutu, metin) cihaza alınacak.";
    }

    public IReadOnlyList<string> Categories { get; }
    public string Summary { get; }

    public static IReadOnlyList<Choice<SymbolRole>> AllRoles { get; } =
    [
        new("Cihazın tamamı (tek parça)", SymbolRole.Standalone),
        new("Çok sayfalı cihazın ana parçası (CPU, ana kutu)", SymbolRole.Main),
        new("Aynı etiketli cihazın ek parçası (başka sayfadaki G/Ç kutusu)", SymbolRole.Contact),
    ];

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "PLC ve Otomasyon/Kullanıcı";
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string Prefix { get; set; } = "A";
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<Choice<SymbolRole>> Roles => AllRoles;
    public Choice<SymbolRole> Role { get; set; } = AllRoles[0];
    public bool ReplaceDrawing { get; set; } = true;

    public string? Validate() =>
        string.IsNullOrWhiteSpace(Name) ? "Cihaz adını yazın (ör. Delta AS228T)."
        : string.IsNullOrWhiteSpace(Prefix) || !Prefix.Trim().All(char.IsLetter) ? "Tanım harfi bir veya birkaç harf olmalı (PLC için A, röle için K)."
        : null;

    public DeviceBuildOptions ToOptions() => new()
    {
        Name = Name.Trim(),
        Category = string.IsNullOrWhiteSpace(Category) ? "Kullanıcı" : Category.Trim(),
        Manufacturer = Manufacturer.Trim(),
        Model = string.IsNullOrWhiteSpace(Model) ? Name.Trim() : Model.Trim(),
        OrderNumber = OrderNumber.Trim(),
        ReferencePrefix = Prefix.Trim().ToUpperInvariant(),
        Description = Description.Trim(),
        Role = Role.Value,
    };
}
