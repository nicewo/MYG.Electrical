namespace MYG.Electrical.Core.Devices;

/// <summary>
/// Projedeki somut cihaz (EPLAN "cihaz"). Anahtarı etikettir: aynı etiketi taşıyan bütün sembol yerleşimleri
/// (<see cref="Model.DeviceObject.Reference"/>) bu cihazın parçalarıdır (bobin, kontaklar, G/Ç kanalları...).
/// </summary>
public sealed class DeviceRecord
{
    /// <summary>IEC 81346 etiketi, ör. "-K1". Proje genelinde benzersiz.</summary>
    public string Reference { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Kütüphane kategorisi (ör. "Kontaktör ve Röle/Kontaktörler").</summary>
    public string Category { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>Model / tip adı (ör. "LC1D09BD").</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Sipariş (parça) numarası; malzeme listesinde kullanılır.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Montaj yeri (IEC 81346 "+" yeri), ör. "+PANO1".</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Teknik doküman / datasheet yolu veya bağlantısı.</summary>
    public string Datasheet { get; set; } = string.Empty;

    /// <summary>Cihazın oluşturulduğu kütüphane ve sürümü (ör. "MYG IEC 60617 1.0.0").</summary>
    public string LibraryVersion { get; set; } = string.Empty;

    /// <summary>Elektriksel ve diğer teknik özellikler (Gerilim, Akım, Güç, Bobin gerilimi...).</summary>
    public Dictionary<string, string> Properties { get; set; } = [];

    public string Notes { get; set; } = string.Empty;

    /// <summary>Oluşturulduğu cihaz kütüphanesi kaydının kimliği (yoksa boş).</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Cihazın kontak kapasitesi (şablondan). Boşsa kapasite bilinmiyor; kullanılan kontaklar yine listelenir.</summary>
    public List<ContactSlot> Contacts { get; set; } = [];

    /// <summary>Üretici/model gibi ticari bilgilerin hiçbiri girilmemiş mi (DRC "eksik bilgi").</summary>
    public bool HasCommercialData => Manufacturer.Length > 0 || Model.Length > 0 || OrderNumber.Length > 0;

    public DeviceRecord Clone() => new()
    {
        Reference = Reference,
        Description = Description,
        Category = Category,
        Manufacturer = Manufacturer,
        Model = Model,
        OrderNumber = OrderNumber,
        Location = Location,
        Datasheet = Datasheet,
        LibraryVersion = LibraryVersion,
        Properties = new Dictionary<string, string>(Properties),
        Notes = Notes,
        TemplateId = TemplateId,
        Contacts = Contacts.Select(c => c.Clone()).ToList(),
    };
}
