namespace MYG.Electrical.Core.Pages;

/// <summary>Proje ve kapak bilgileri.</summary>
public sealed class ProjectInfo
{
    public string Name { get; set; } = "Yeni Proje";
    public string Company { get; set; } = "MYG TECH";
    public string Revision { get; set; } = "A";
    public string MachineName { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
    public string CheckedBy { get; set; } = string.Empty;
    public string Date { get; set; } = DateTime.Today.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Firma logosu dosya yolu (PNG/JPG). Boş olabilir.</summary>
    public string LogoPath { get; set; } = string.Empty;
}
