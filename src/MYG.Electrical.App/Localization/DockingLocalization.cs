using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace MYG.Electrical.App.Localization;

/// <summary>
/// Panel kütüphanesinin (AvalonDock) menü ve ipucu metinlerini programın diline çevirir. AvalonDock'ta Türkçe
/// yoktur ve derleme imzalı olduğu için uydu kaynak eklenemez; bu yüzden kütüphanenin kaynak yöneticisi, kendi
/// çeviri tablosundan okuyan bir yöneticiyle değiştirilir (tabloda olmayan anahtar özgün metne düşer).
/// Uygulama açılırken, hiçbir pencere oluşturulmadan çağrılmalıdır.
/// </summary>
public static class DockingLocalization
{
    /// <summary>Dil kodu → (AvalonDock kaynak anahtarı → metin). Yeni dil = yeni tablo.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Languages { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["tr"] = new Dictionary<string, string>
            {
                ["Anchorable_Float"] = "Kayan",
                ["Anchorable_DetachToWindow"] = "Ayrı pencere",
                ["Anchorable_Dock"] = "Yerleştir",
                ["Anchorable_DockAsDocument"] = "Sekmeli belge olarak yerleştir",
                ["Anchorable_AutoHide"] = "Otomatik gizle",
                ["Anchorable_Hide"] = "Gizle",
                ["Anchorable_BtnClose_Hint"] = "Gizle",
                ["Anchorable_BtnAutoHide_Hint"] = "Otomatik gizle",
                ["Anchorable_CxMenu_Hint"] = "Pencere konumu",
                ["Document_Float"] = "Kayan",
                ["Document_DockAsDocument"] = "Sekmeli belge olarak yerleştir",
                ["Document_Close"] = "Kapat",
                ["Document_CloseAll"] = "Tümünü kapat",
                ["Document_CloseAllButThis"] = "Bu hariç tümünü kapat",
                ["Document_NewHorizontalTabGroup"] = "Yeni yatay sekme grubu",
                ["Document_NewVerticalTabGroup"] = "Yeni dikey sekme grubu",
                ["Document_MoveToNextTabGroup"] = "Sonraki sekme grubuna taşı",
                ["Document_MoveToPreviousTabGroup"] = "Önceki sekme grubuna taşı",
                ["Document_BtnPinned_Hint"] = "Sabitle / sabitlemeyi kaldır",
                ["Document_CxMenu_Hint"] = "Pencere konumu",
                ["Window_Maximize"] = "Ekranı kapla",
                ["Window_Restore"] = "Önceki boyut",
                ["Active_ToolWindows"] = "Açık paneller",
                ["Active_Files"] = "Açık belgeler",
            },
        };

    /// <summary>Çeviriyi uygular. Başarılıysa true; kütüphane iç yapısı değişmişse false (metinler İngilizce kalır).</summary>
    public static bool Apply(string language)
    {
        if (!Languages.TryGetValue(language, out var table))
        {
            return false; // İngilizce: özgün metinler
        }
        var resources = typeof(AvalonDock.DockingManager).Assembly.GetType("AvalonDock.Properties.Resources");
        var field = resources?.GetField("resourceMan", BindingFlags.NonPublic | BindingFlags.Static);
        var property = resources?.GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (field is null || property?.GetValue(null) is not ResourceManager original)
        {
            return false;
        }
        field.SetValue(null, new TableResourceManager(table, original));
        return true;
    }

    /// <summary>Önce tablodan, yoksa özgün kaynaktan okuyan kaynak yöneticisi.</summary>
    private sealed class TableResourceManager(IReadOnlyDictionary<string, string> table, ResourceManager fallback) : ResourceManager
    {
        public override string? GetString(string name) => GetString(name, null);

        public override string? GetString(string name, CultureInfo? culture) =>
            table.TryGetValue(name, out var text) ? text : fallback.GetString(name, culture);

        public override object? GetObject(string name) => GetObject(name, null);

        public override object? GetObject(string name, CultureInfo? culture) =>
            table.TryGetValue(name, out var text) ? text : fallback.GetObject(name, culture);

        public override ResourceSet? GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) =>
            fallback.GetResourceSet(culture, createIfNotExists, tryParents);
    }
}
