namespace MYG.Electrical.Core.Pages;

/// <summary>.myge dosyasının kök nesnesi.</summary>
public sealed class ProjectDocument
{
    /// <summary>
    /// Yazılan format sürümü.
    /// <list type="bullet">
    /// <item>1: ilk sürüm (junction'lar 4 yollu).</item>
    /// <item>2: junction şekli (dirsek/T/çapraz) ve yönü.</item>
    /// <item>3: gömülü kaynaklar (<see cref="Resources"/>) ve resim nesnesi.</item>
    /// <item>4: proje cihazları (<see cref="Devices"/>), proje ayarları, kablo verisi (Page.WireData),
    /// yerleşimde açıklama ve pin adı.</item>
    /// <item>5: potansiyel (<c>potential</c>) ve sayfa bağlantısı (<c>pageLink</c>) nesneleri.</item>
    /// <item>6: proje cihazında cihaz kütüphanesi kimliği (<c>templateId</c>) ve kontak kapasitesi (<c>contacts</c>).</item>
    /// <item>7: tasarım pini (<c>devicePin</c>), kesikli dikdörtgen, projeye gömülü kullanıcı sembolleri (<c>symbols</c>).</item>
    /// <item>8: gömülü sembolde etiket bloğunun yeri (<c>referencePosition</c>: right, left, top).</item>
    /// <item>9: tasarlanabilir kapak: bilgi alanı (<c>field</c>), metinde hizalama ve kalınlık. Kapak artık
    /// otomatik çizilmez; eski dosyalardaki boş kapak sayfalarına Standart şablon yerleştirilir.</item>
    /// </list>
    /// Yeni bir alan veya nesne türü eklendiğinde bu değer artırılmalı ve
    /// ProjectSerializer.Migrate içine dönüşüm adımı yazılmalıdır.
    /// </summary>
    public const int CurrentFormatVersion = 9;
    public const string FileExtension = ".myge";

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public ProjectInfo Project { get; set; } = new();
    /// <summary>Numaralandırma şablonları. JSON adı editör ayarlarıyla karışmasın diye "projectSettings".</summary>
    [System.Text.Json.Serialization.JsonPropertyName("projectSettings")]
    public ProjectSettings Settings { get; set; } = new();
    public List<Page> Pages { get; set; } = [];

    /// <summary>Proje cihazları (etiket başına bir kayıt). Yerleşimler sayfalardaki DeviceObject'lerdir.</summary>
    public List<Devices.DeviceRecord> Devices { get; set; } = [];

    /// <summary>Projede kullanılan kullanıcı sembolleri (başka bilgisayarda da çizilebilsin diye).</summary>
    public List<Symbols.SymbolDefinition> Symbols { get; set; } = [];

    /// <summary>Projeye gömülü dosyalar (resimler). JSON'da base64.</summary>
    public List<Resources.ProjectResource> Resources { get; set; } = [];

    public Resources.ProjectResource? FindResource(string id) => Resources.FirstOrDefault(r => r.Id == id);

    /// <summary>Veriyi projeye kopyalar; aynı içerik zaten varsa mevcut kaynağı döndürür.</summary>
    public Resources.ProjectResource AddResource(byte[] data, string fileName)
    {
        var id = MYG.Electrical.Core.Resources.ProjectResource.CreateId(data);
        if (FindResource(id) is { } existing)
        {
            return existing;
        }
        var resource = new Resources.ProjectResource { Id = id, FileName = fileName, Data = data };
        Resources.Add(resource);
        return resource;
    }

    /// <summary>
    /// Sayfalardaki nesnelerin kullandığı kaynaklar. Kayıtta yalnızca bunlar yazılır; bellekteki liste
    /// değiştirilmez (geri alınan bir silme işlemi resmini kaybetmesin diye).
    /// </summary>
    public List<Resources.ProjectResource> UsedResources()
    {
        var used = Pages.SelectMany(p => p.Objects).OfType<Model.ImageObject>()
            .SelectMany(i => new[] { i.SourceResourceId, i.RenderResourceId })
            .ToHashSet(StringComparer.Ordinal);
        return Resources.Where(r => used.Contains(r.Id)).ToList();
    }

    /// <summary>Kapak + bir şema sayfası içeren yeni proje. Kapak, verilen şablonla (yoksa Standart) başlar.</summary>
    public static ProjectDocument CreateNew(string name = "Yeni Proje", CoverTemplate? cover = null)
    {
        var doc = new ProjectDocument { Project = new ProjectInfo { Name = name, MachineName = name } };
        var coverPage = Page.Create("Kapak", PageType.Cover);
        (cover ?? CoverTemplate.Standard()).ApplyTo(doc, coverPage);
        doc.Pages.Add(coverPage);
        doc.Pages.Add(Page.Create("Güç Devresi", PageType.Schematic));
        return doc;
    }
}
