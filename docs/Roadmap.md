# Yol Haritası

## V0.1 (bu sürüm)

- [x] Solution ve katmanlar, derlenebilir uygulama
- [x] Koyu tema WPF arayüz, menü, araç çubuğu, paneller, durum çubuğu
- [x] Çizim motoru: mm koordinat, A4–A1, grid, snap, zoom (imlece göre), pan (orta tuş)
- [x] Çizgi, dikdörtgen, metin; seçim, çoklu seçim, taşıma, döndürme, kopyalama, silme, geri al/ileri al
- [x] IEC sembol kütüphanesi ve lokal bağlantı noktaları
- [x] AutoWireEngine (karşılıklı en yakın pin, junction, tolerans)
- [x] `.myge` JSON kaydet/aç, çok sayfa yönetimi
- [x] Kapak sayfası, düzenlenebilir antet, vektörel çok sayfalı PDF

## V0.2 (devam ediyor)

- [x] Dirsek, yönlü T ve çapraz bağlantı noktaları; yerleştirirken otomatik yön önerisi (format v2)
- [x] Sembol ve junction'ı yerleştirmeden önce R ile döndürme
- [x] Hakkında penceresi, Yardım menüsü (kılavuz, kısayollar, örnek proje, bağlantılar), kullanım kılavuzu
- [x] GitHub Releases + otomatik güncelleme (Velopack)
- [x] Resim ekleme: projeye gömülü kaynak, renk seçici ile saydamlık + hassasiyet, PDF'te alfa kanalı (format v3)

## V0.3 — IEC cihaz kütüphanesi, potansiyeller, DRC (tamamlandı)

- [x] JSON sembol kütüphanesi (113 IEC 60617 sembolü), kullanıcı sembol klasörü (format v4–v6)
- [x] Cihaz kütüphanesi (sembolden ayrı), proje cihazı kaydı, çok parçalı cihazlar, kontak numaralandırma
- [x] Kütüphane paneli: ağaç, arama, üretici süzgeci, favoriler, son kullanılanlar, önizleme, sürükle-bırak
- [x] Cihaz özellikleri penceresi (7 sekme), IEC 81346 etiket şablonu ve yeniden numaralandırma
- [x] Potansiyel ve sayfa bağlantısı nesneleri, proje geneli net analizi, renkli kablolar, renk ayarları
- [x] Kablo bilgisi ve otomatik kablo numaralandırma
- [x] Çapraz referanslar (kontak aynası, /sayfa.kolon)
- [x] DRC (kısa devre dahil), alt panel (DRC, mesajlar, çapraz referanslar, proje bilgisi)
- [x] Raporlar: cihaz listesi, BOM, klemens planı, kablo listesi, PLC G/Ç (xlsx, CSV)

## Sonraki adaylar

- [ ] L/Z (90°) otomatik kablo yönlendirme (dirsekleri kendisi koyan kablo aracı)
- [ ] Grafik sembol editörü (JSON üretir)
- [ ] Klemens şeridi, çok damarlı kablo nesnesi ve kablo planı
- [x] Kayan/ayrılabilir paneller (VS tarzı, v0.2.17)
- [x] Tamamen Türkçe arayüz: panel menüleri dahil, tr-TR kültürü (v0.2.18)
- [ ] Çok dilli arayüz (aşağıya bakın)
- [ ] Sayfa şablonları, özelleştirilebilir antet düzeni
- [ ] Ayna (flip) ve serbest açılı döndürme
- [ ] Doğrudan yazdırma, PDF içinde sayfa yer imleri
- [ ] Son açılan dosyalar, otomatik yedekleme
- [ ] `.myge` dosya ilişkilendirmesi ve kurulum paketi (MSIX)

## Çok dilli arayüz planı

Altyapı hazır: `settings.json` içinde `"Language": "tr"` (varsayılan) okunur, .NET/WPF kültürü buna göre kurulur,
panel kütüphanesinin (AvalonDock) metinleri `App/Localization/DockingLocalization.cs` içindeki dil tablosundan
gelir. Sonraki adımlar:

1. Arayüz metinlerini `App/Localization/Strings.resx` (Türkçe, ana dil) dosyasına taşımak; XAML'de
   `{loc:Tr Key=Menu_File}` biçiminde bir işaretleme uzantısıyla okumak.
2. `Strings.en.resx` ile İngilizce çeviri; `DockingLocalization` için de `"en"` = özgün metinler.
3. **Ayarlar → Dil** menüsü (değişiklik yeniden başlatınca uygulanır).
4. Rapor/PDF başlıkları ve DRC iletileri için aynı kaynak dosyası; proje dosyası dilden bağımsız kalır.
