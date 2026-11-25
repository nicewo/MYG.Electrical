namespace MYG.Electrical.Core.Pages;

/// <summary>.myge dosyasının kök nesnesi.</summary>
public sealed class ProjectDocument
{
    /// <summary>
    /// Format sürümü. 1: ilk sürüm (junction'lar 4 yollu). 2: junction şekli (dirsek/T/çapraz) ve yönü eklendi.
    /// </summary>
    public const int CurrentFormatVersion = 2;
    public const string FileExtension = ".myge";

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public ProjectInfo Project { get; set; } = new();
    public List<Page> Pages { get; set; } = [];

    /// <summary>Kapak + bir şema sayfası içeren yeni proje.</summary>
    public static ProjectDocument CreateNew(string name = "Yeni Proje")
    {
        var doc = new ProjectDocument { Project = new ProjectInfo { Name = name, MachineName = name } };
        doc.Pages.Add(Page.Create("Kapak", PageType.Cover));
        doc.Pages.Add(Page.Create("Güç Devresi", PageType.Schematic));
        return doc;
    }
}
