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
    /// </list>
    /// Yeni bir alan veya nesne türü eklendiğinde bu değer artırılmalı ve
    /// ProjectSerializer.Migrate içine dönüşüm adımı yazılmalıdır.
    /// </summary>
    public const int CurrentFormatVersion = 3;
    public const string FileExtension = ".myge";

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public ProjectInfo Project { get; set; } = new();
    public List<Page> Pages { get; set; } = [];

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

    /// <summary>Kapak + bir şema sayfası içeren yeni proje.</summary>
    public static ProjectDocument CreateNew(string name = "Yeni Proje")
    {
        var doc = new ProjectDocument { Project = new ProjectInfo { Name = name, MachineName = name } };
        doc.Pages.Add(Page.Create("Kapak", PageType.Cover));
        doc.Pages.Add(Page.Create("Güç Devresi", PageType.Schematic));
        return doc;
    }
}
