using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Devices;

/// <summary>Kontak türü (cihazın kontak kapasitesi ve kullanım takibi).</summary>
public enum ContactKind
{
    No,
    Nc,
    Changeover,
    Power,
}

/// <summary>Cihazın sahip olduğu bir kontak: türü ve pin adları (ör. NO 13-14, CO 11-12-14).</summary>
public sealed class ContactSlot
{
    public ContactKind Kind { get; set; }
    public List<string> Pins { get; set; } = [];

    public string Display => $"{KindText(Kind)} {string.Join("-", Pins)}";

    public static string KindText(ContactKind k) => k switch
    {
        ContactKind.No => "NO",
        ContactKind.Nc => "NC",
        ContactKind.Changeover => "CO",
        _ => "Güç",
    };

    public ContactSlot Clone() => new() { Kind = Kind, Pins = [.. Pins] };
}

/// <summary>
/// Cihaz kütüphanesi kaydı (sembolden ayrı). Ticari bilgiler, varsayılan özellikler, ana sembol ve kontak
/// kapasitesi. Yerleştirildiğinde ana sembol konur ve proje cihazı bu bilgilerle doldurulur.
/// </summary>
public sealed class DeviceTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Kütüphane ağacındaki yol ("/" ile).</summary>
    public string Category { get; init; } = "Genel";

    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Datasheet { get; init; } = string.Empty;
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Yerleştirilecek ana sembol (bobin, sürücü güç bölümü, PLC CPU...).</summary>
    public required string Symbol { get; init; }

    /// <summary>Aynı etiketle ayrıca yerleştirilebilecek parça sembolleri (kontaklar, G/Ç, kumanda klemensleri).</summary>
    public IReadOnlyList<string> Parts { get; init; } = [];

    /// <summary>Kontak kapasitesi (kullanılan/boş takibi için).</summary>
    public IReadOnlyList<ContactSlot> Contacts { get; init; } = [];

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    /// <summary>Kütüphane adı ve sürümü (yükleyici doldurur).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Library { get; internal set; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsUserDefined { get; internal set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string TopCategory => Category.Split('/')[0];

    /// <summary>Şablondan proje cihazı kaydı.</summary>
    public DeviceRecord CreateRecord(string reference, SymbolDefinition? symbol)
    {
        var record = DeviceRegistry.CreateRecord(reference, symbol);
        record.TemplateId = Id;
        record.Description = Description.Length > 0 ? Description : Name;
        record.Category = Category;
        record.Manufacturer = Manufacturer;
        record.Model = Model;
        record.OrderNumber = OrderNumber;
        record.Datasheet = Datasheet;
        record.LibraryVersion = Library;
        foreach (var (k, v) in Properties)
        {
            record.Properties[k] = v;
        }
        record.Contacts = Contacts.Select(c => c.Clone()).ToList();
        return record;
    }
}
