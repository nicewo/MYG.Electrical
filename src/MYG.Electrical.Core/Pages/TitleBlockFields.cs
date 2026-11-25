namespace MYG.Electrical.Core.Pages;

/// <summary>Antet alanı anahtarları ve değer çözümleme.</summary>
public static class TitleBlockFields
{
    public const string Company = "company";
    public const string Project = "project";
    public const string Machine = "machine";
    public const string ProjectNumber = "projectNumber";
    public const string Customer = "customer";
    public const string PreparedBy = "preparedBy";
    public const string CheckedBy = "checkedBy";
    public const string Date = "date";
    public const string Revision = "revision";
    public const string PageTitle = "pageTitle";

    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        (Company, "Firma"),
        (Project, "Proje"),
        (Machine, "Makine"),
        (ProjectNumber, "Proje No"),
        (Customer, "Müşteri"),
        (PreparedBy, "Hazırlayan"),
        (CheckedBy, "Kontrol"),
        (Date, "Tarih"),
        (Revision, "Rev."),
        (PageTitle, "Sayfa Başlığı"),
    ];

    /// <summary>Sayfaya özel değer varsa onu, yoksa proje değerini döndürür.</summary>
    public static string Resolve(ProjectInfo info, Page page, string key)
    {
        if (page.TitleBlock.TryGetValue(key, out var overridden) && !string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }
        return key switch
        {
            Company => info.Company,
            Project => info.Name,
            Machine => info.MachineName,
            ProjectNumber => info.ProjectNumber,
            Customer => info.Customer,
            PreparedBy => info.PreparedBy,
            CheckedBy => info.CheckedBy,
            Date => info.Date,
            Revision => info.Revision,
            PageTitle => page.Name,
            _ => string.Empty,
        };
    }
}
