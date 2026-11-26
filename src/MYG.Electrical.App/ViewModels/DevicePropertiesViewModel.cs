using System.Collections.ObjectModel;
using MYG.Electrical.App.Mvvm;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.App.ViewModels;

/// <summary>Anahtar/değer satırı (elektriksel özellikler).</summary>
public sealed class KeyValueRow(string key, string value) : ObservableObject
{
    private string _key = key;
    private string _value = value;

    public string Key
    {
        get => _key;
        set => SetField(ref _key, value);
    }

    public string Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }
}

/// <summary>Pin satırı: sembol pini, şemada görünen adı (düzenlenebilir), işlevi ve bağlı olduğu potansiyel.</summary>
public sealed class PinRow(string pinId, string label, string function, string connection) : ObservableObject
{
    private string _label = label;

    public string PinId { get; } = pinId;
    public string Function { get; } = function;
    public string Connection { get; } = connection;

    public string Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }
}

/// <summary>Salt okunur tablo satırı (kontaklar, çapraz referanslar).</summary>
public sealed record InfoRow(string First, string Second, string Third, bool Warning = false);

/// <summary>
/// Cihaz özellikleri penceresi (çift tık). Sekmeler: Genel, Elektriksel veri, Bağlantılar/Pinler, Üretici,
/// Kontaklar, Çapraz referanslar, Dokümantasyon. Tamam → tek geri alma adımı.
/// </summary>
public sealed class DevicePropertiesViewModel : ObservableObject
{
    private readonly EditorSession _session;
    private readonly DeviceObject _placement;
    private readonly DeviceRecord _record;

    public DevicePropertiesViewModel(EditorSession session, DeviceObject placement)
    {
        _session = session;
        _placement = placement;
        var catalog = session.Catalog;
        Symbol = catalog.Find(placement.SymbolId);
        var existing = DeviceRegistry.Find(session.Document, placement.Reference);
        _record = existing?.Clone() ?? DeviceRegistry.CreateRecord(placement.Reference, Symbol);

        Reference = placement.Reference;
        Description = _record.Description;
        Category = _record.Category;
        Location = _record.Location;
        Function = placement.Description;
        Manufacturer = _record.Manufacturer;
        Model = _record.Model;
        OrderNumber = _record.OrderNumber;
        Datasheet = _record.Datasheet;
        Notes = _record.Notes;

        foreach (var (k, v) in _record.Properties)
        {
            Properties.Add(new KeyValueRow(k, v));
        }
        foreach (var cp in placement.ConnectionPoints)
        {
            var pin = new PinRef(placement.Id, cp.Id);
            var potential = session.Network.PotentialOf(pin);
            var connected = session.Network.NetOf(pin) is { } net && session.Network.PinsInNet(net).Skip(1).Any();
            var conn = potential is not null ? $"{potential.Name} ({potential.Voltage})".Replace(" ()", "", StringComparison.Ordinal)
                : connected ? "Bağlı" : "Bağlantısız";
            Pins.Add(new PinRow(cp.Id, placement.PinLabel(cp.Id), FunctionName(cp.Function) + (string.IsNullOrEmpty(cp.Description) ? "" : $" · {cp.Description}"), conn));
        }

        var parts = CrossReferences.Parts(session.Network, catalog, placement.Reference);
        foreach (var p in parts)
        {
            var self = ReferenceEquals(p.Placement, placement) ? " (bu)" : "";
            CrossRefs.Add(new InfoRow(p.Location.ToString(), (p.Symbol?.Name ?? p.Placement.SymbolId) + self,
                $"{p.Paths}{(p.Placement.Description.Length > 0 ? " · " + p.Placement.Description : "")}"));
        }
        if (_record.Contacts.Count > 0)
        {
            var (slots, unassigned) = ContactUsage.Compute(_record, parts);
            foreach (var u in slots)
            {
                Contacts.Add(new InfoRow(u.Slot.Display, u.IsUsed ? "Kullanılıyor" : "Boş", u.UsedBy?.Location.ToString() ?? ""));
            }
            foreach (var x in unassigned)
            {
                Contacts.Add(new InfoRow($"{x.KindText} {x.Paths}", "Kapasite dışı", x.Location.ToString(), Warning: true));
            }
            ContactSummary = $"{slots.Count(s => s.IsUsed)} / {slots.Count} kontak kullanılıyor" + (unassigned.Count > 0 ? $" · {unassigned.Count} kapasite dışı" : "");
        }
        else
        {
            foreach (var p in parts.Where(p => p.Role == SymbolRole.Contact))
            {
                Contacts.Add(new InfoRow($"{p.KindText} {p.Paths}", "Kullanılıyor", p.Location.ToString()));
            }
            ContactSummary = "Kontak kapasitesi tanımsız (cihaz kütüphanesinden yerleştirilen cihazlarda bilinir).";
        }

        AddPropertyCommand = new RelayCommand(() => Properties.Add(new KeyValueRow("Yeni özellik", "")));
        RemovePropertyCommand = new RelayCommand(p =>
        {
            if (p is KeyValueRow row)
            {
                Properties.Remove(row);
            }
        });
    }

    public string Title => $"Cihaz özellikleri — {Reference}";
    public SymbolDefinition? Symbol { get; }
    public string SymbolName => Symbol?.Name ?? _placement.SymbolId;
    public string LibraryVersion => _record.LibraryVersion.Length > 0 ? _record.LibraryVersion : "—";
    public string TemplateId => _record.TemplateId.Length > 0 ? _record.TemplateId : "— (sembolden oluşturuldu)";
    public string ContactSummary { get; }

    public string Reference { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public string Location { get; set; }
    public string Function { get; set; }
    public string Manufacturer { get; set; }
    public string Model { get; set; }
    public string OrderNumber { get; set; }
    public string Datasheet { get; set; }
    public string Notes { get; set; }

    public ObservableCollection<KeyValueRow> Properties { get; } = [];
    public ObservableCollection<PinRow> Pins { get; } = [];
    public ObservableCollection<InfoRow> Contacts { get; } = [];
    public ObservableCollection<InfoRow> CrossRefs { get; } = [];
    public RelayCommand AddPropertyCommand { get; }
    public RelayCommand RemovePropertyCommand { get; }

    /// <summary>Değişiklikleri uygular. Etiket değiştiyse önce yeniden adlandırma yapılır (ayrı geri alma adımı).</summary>
    public string? Apply()
    {
        var newReference = Reference.Trim();
        if (newReference.Length == 0 && Symbol?.Role != SymbolRole.Graphic)
        {
            return "Etiket boş olamaz.";
        }
        if (!string.Equals(newReference, _placement.Reference, StringComparison.Ordinal))
        {
            _session.RenameReference(_placement, newReference);
        }
        if (newReference.Length == 0)
        {
            return null;
        }
        var keys = Properties.Select(p => p.Key.Trim()).Where(k => k.Length > 0).ToList();
        if (keys.Count != keys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return "Elektriksel özelliklerde aynı ad iki kez var.";
        }

        var baseRecord = DeviceRegistry.Find(_session.Document, newReference)?.Clone() ?? _record.Clone();
        baseRecord.Reference = newReference;
        baseRecord.Description = Description.Trim();
        baseRecord.Category = Category.Trim();
        baseRecord.Location = Location.Trim();
        baseRecord.Manufacturer = Manufacturer.Trim();
        baseRecord.Model = Model.Trim();
        baseRecord.OrderNumber = OrderNumber.Trim();
        baseRecord.Datasheet = Datasheet.Trim();
        baseRecord.Notes = Notes;
        baseRecord.Properties = Properties.Where(p => p.Key.Trim().Length > 0).ToDictionary(p => p.Key.Trim(), p => p.Value.Trim());
        var designations = Pins.ToDictionary(p => p.PinId, p => p.Label.Trim());
        _session.UpdateDevice(_placement, baseRecord, Function.Trim(), designations);
        return null;
    }

    public static string FunctionName(PinFunction f) => f switch
    {
        PinFunction.Power => "Güç",
        PinFunction.Coil => "Bobin",
        PinFunction.ContactNo => "NO kontak",
        PinFunction.ContactNc => "NC kontak",
        PinFunction.ContactCommon => "Ortak uç",
        PinFunction.SupplyPositive => "Besleme +",
        PinFunction.SupplyNegative => "Besleme −",
        PinFunction.Neutral => "Nötr",
        PinFunction.ProtectiveEarth => "PE",
        PinFunction.Input => "Giriş",
        PinFunction.Output => "Çıkış",
        PinFunction.Communication => "Haberleşme",
        PinFunction.Shield => "Ekran",
        PinFunction.Terminal => "Klemens",
        _ => "Genel",
    };
}
