using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Pages;

/// <summary>
/// Açılan projede eksik/hatalı verileri onarır ve kullanıcıya gösterilecek uyarıları üretir.
/// Onarılamayacak durumlar (tanınmayan nesne türü, bozuk JSON) serileştirici tarafından hata olarak bildirilir.
/// </summary>
public static class ProjectValidator
{
    public static IReadOnlyList<string> Repair(ProjectDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var warnings = new List<string>();

        doc.Project ??= new ProjectInfo();
        doc.Pages ??= [];
        doc.Resources ??= [];
        doc.Settings ??= new ProjectSettings();
        doc.Settings.ReferenceFormat = string.IsNullOrWhiteSpace(doc.Settings.ReferenceFormat)
            ? ProjectSettings.DefaultReferenceFormat : doc.Settings.ReferenceFormat;
        doc.Settings.WireNumberFormat = string.IsNullOrWhiteSpace(doc.Settings.WireNumberFormat)
            ? ProjectSettings.DefaultWireNumberFormat : doc.Settings.WireNumberFormat;
        doc.Devices ??= [];
        doc.Devices.RemoveAll(d => d is null);
        foreach (var d in doc.Devices)
        {
            d.Reference ??= string.Empty;
            d.Description ??= string.Empty;
            d.Category ??= string.Empty;
            d.Manufacturer ??= string.Empty;
            d.Model ??= string.Empty;
            d.OrderNumber ??= string.Empty;
            d.Location ??= string.Empty;
            d.Datasheet ??= string.Empty;
            d.LibraryVersion ??= string.Empty;
            d.Notes ??= string.Empty;
            d.Properties ??= [];
        }
        var seenDevices = new HashSet<string>(Devices.DeviceRegistry.ReferenceComparer);
        var duplicates = doc.Devices.Where(d => string.IsNullOrWhiteSpace(d.Reference) || !seenDevices.Add(d.Reference)).ToList();
        foreach (var dup in duplicates)
        {
            doc.Devices.Remove(dup);
            if (!string.IsNullOrWhiteSpace(dup.Reference))
            {
                warnings.Add($"'{dup.Reference}' cihaz kaydı iki kez vardı; ikincisi atlandı.");
            }
        }

        // Kaynaklar: tekrar eden kimlikler ve boş veriler
        var seenResources = new HashSet<string>(StringComparer.Ordinal);
        doc.Resources.RemoveAll(r => r is null || !seenResources.Add(r.Id));
        foreach (var r in doc.Resources.Where(r => r.Data is null || r.Data.Length == 0))
        {
            r.Data ??= [];
            warnings.Add($"'{r.FileName}' kaynağının verisi boş veya bozuk; resim yer tutucu ile gösterilecek.");
        }

        var seenPages = new HashSet<string>(StringComparer.Ordinal);
        var seenObjects = new HashSet<Guid>();
        foreach (var page in doc.Pages.ToList())
        {
            if (page is null)
            {
                doc.Pages.Remove(page!);
                continue;
            }
            page.Name ??= "Sayfa";
            page.Objects ??= [];
            page.TitleBlock ??= [];
            page.WireData ??= [];
            page.WireData.RemoveAll(w => w is null);
            if (string.IsNullOrWhiteSpace(page.Id) || !seenPages.Add(page.Id))
            {
                page.Id = Page.NewId();
                seenPages.Add(page.Id);
                warnings.Add($"'{page.Name}' sayfasının kimliği tekrar ediyordu; yeni kimlik verildi.");
            }
            if (!IsValidSize(page.Width) || !IsValidSize(page.Height))
            {
                page.SetFormat(page.Format, page.Orientation);
                warnings.Add($"'{page.Name}' sayfasının ölçüleri geçersizdi; {page.Format} ölçülerine ayarlandı.");
            }

            var nulls = page.Objects.RemoveAll(o => o is null);
            if (nulls > 0)
            {
                warnings.Add($"'{page.Name}' sayfasında {nulls} boş nesne kaydı atlandı.");
            }

            foreach (var obj in page.Objects)
            {
                if (obj.Id == Guid.Empty || !seenObjects.Add(obj.Id))
                {
                    obj.Id = Guid.NewGuid();
                    seenObjects.Add(obj.Id);
                    warnings.Add($"'{page.Name}' sayfasında kimliği tekrar eden bir nesneye yeni kimlik verildi.");
                }
                switch (obj)
                {
                    case DeviceObject d:
                        d.ConnectionPoints ??= [];
                        d.Reference ??= string.Empty;
                        d.Description ??= string.Empty;
                        d.PinDesignations ??= [];
                        break;
                    case TextObject t:
                        t.Text ??= string.Empty;
                        break;
                    case ImageObject img:
                        var id = string.IsNullOrEmpty(img.RenderResourceId) ? img.SourceResourceId : img.RenderResourceId;
                        if (doc.FindResource(id) is null)
                        {
                            warnings.Add($"'{page.Name}' sayfasındaki bir resmin verisi projede yok; yer tutucu gösterilecek.");
                        }
                        break;
                }
            }
        }

        if (doc.Pages.Count == 0)
        {
            doc.Pages.Add(Page.Create("Sayfa 1", PageType.Schematic));
            warnings.Add("Projede sayfa yoktu; boş bir şema sayfası eklendi.");
        }

        // v3 ve öncesi dosyalarda cihaz kaydı yoktu: her etiket için kayıt oluşturulur (uyarı gerekmez).
        Devices.DeviceRegistry.EnsureRecords(doc);
        return warnings;
    }

    private static bool IsValidSize(double v) => double.IsFinite(v) && v > 0;
}
