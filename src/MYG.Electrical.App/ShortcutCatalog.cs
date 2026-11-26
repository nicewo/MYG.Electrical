namespace MYG.Electrical.App;

/// <summary>Uygulamadaki klavye ve fare kısayolları (Yardım → Klavye kısayolları ve docs/KlavyeKisayollari.md).</summary>
public static class ShortcutCatalog
{
    public static readonly IReadOnlyList<(string Group, string Keys, string Action)> All =
    [
        ("Dosya", "Ctrl+N", "Yeni proje"),
        ("Dosya", "Ctrl+O", "Proje aç"),
        ("Dosya", "Ctrl+S", "Kaydet"),
        ("Dosya", "Ctrl+Shift+S", "Farklı kaydet"),
        ("Dosya", "Ctrl+P", "Tüm projeyi PDF'e aktar"),
        ("Düzen", "Ctrl+Z / Ctrl+Y", "Geri al / İleri al"),
        ("Düzen", "Ctrl+X / Ctrl+C / Ctrl+V", "Kes / Kopyala / Yapıştır"),
        ("Düzen", "Ctrl+A", "Tümünü seç"),
        ("Düzen", "Del", "Seçimi sil"),
        ("Düzen", "R", "Seçimi veya yerleştirme önizlemesini 90° döndür"),
        ("Düzen", "← → ↑ ↓", "Seçimi bir grid adımı kaydır"),
        ("Düzen", "Shift + ok tuşları", "Seçimi 1 mm kaydır"),
        ("Araçlar", "S", "Seç"),
        ("Araçlar", "L", "Çizgi"),
        ("Araçlar", "T", "Metin"),
        ("Araçlar", "D", "Dirsek (90° bağlantı noktası)"),
        ("Araçlar", "J", "T bağlantı"),
        ("Araçlar", "Esc", "İşlemi iptal et, seçim aracına dön"),
        ("Görünüm", "Fare tekerleği", "İmlecin olduğu noktaya yakınlaştır / uzaklaştır"),
        ("Görünüm", "Orta tuş + sürükle", "Kaydır (pan)"),
        ("Görünüm", "F", "Sayfaya sığdır"),
        ("Görünüm", "Ctrl+Num+ / Ctrl+Num−", "Yakınlaştır / Uzaklaştır"),
        ("Sayfa", "PgUp / PgDn", "Önceki / sonraki sayfa"),
        ("Fare", "Ctrl + tık", "Seçime ekle / çıkar"),
        ("Fare", "Boş alanda sürükle", "Alan seçimi"),
        ("Fare", "Sağ tık", "Seç aracında bağlam menüsü, diğer araçlarda iptal"),
        ("Fare", "Cihaza çift tık", "Cihaz özellikleri penceresi"),
        ("Fare", "Kabloya tık", "Kablo bilgisi (numara, renk, kesit) sağ panelde"),
        ("Fare", "Kütüphaneden sürükle", "Sembol veya cihazı bırakılan noktaya yerleştir"),
        ("Proje", "F7", "Tasarım kuralları denetimi (DRC)"),
    ];
}
