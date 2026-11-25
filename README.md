<p align="center"><img src="src/MYG.Electrical.App/Assets/myg-electrical.png" width="160" alt="MYG Electrical"></p>

# MYG Electrical

Makine imalatı için sade, hızlı bir **IEC elektrik şeması çizim programı** (Windows, WPF, .NET 10).

EPLAN'ın tamamını kopyalamayı değil; kumanda ve güç devrelerini çizmeyi, proje kapağı hazırlamayı,
sayfaları yönetmeyi ve vektörel PDF almayı hedefler. Öne çıkan özelliği **EPLAN benzeri geometrik
otomatik kablolama**dır: hizalı ve birbirine bakan pinler kendiliğinden bağlanır.

![durum](https://github.com/nicewo/MYG.Electrical/actions/workflows/build.yml/badge.svg)

## Özellikler (V0.1)

- Çok sayfalı proje (kapak + şema sayfaları), A4/A3/A2/A1, yatay/dikey
- Gerçek ölçü (mm) koordinat sistemi, grid, snap, imlece göre zoom, orta tuşla pan
- Çizgi, dikdörtgen, metin; seçme, çoklu seçim (Ctrl / alan seçimi), taşıma, 90° döndürme, kes/kopyala/yapıştır, sil
- Komut tabanlı Geri al / İleri al
- IEC sembol kütüphanesi: kontaktör, röle, sigorta, otomatik sigorta, motor, buton, acil stop, limit switch, sensör, klemens, lamba, bobin, NO/NC kontak
- Otomatik cihaz tanımı: `-K1`, `-K2`, `-F1`…
- **AutoWireEngine**: karşılıklı en yakın pin kuralı, tolerans 0,5 mm
- EPLAN tarzı bağlantı noktaları: **dirsek** (90° dönüş), yönlü **T** ve bilerek seçilen **çapraz** düğüm; kesişen hatlar yanlışlıkla birleşmez
- Resim ekleme: resim projeye gömülür; renk seçiciyle saydamlık ve 0–100 hassasiyet (yakın renkleri de siler), PDF'te de saydam
- Kapak sayfası ve düzenlenebilir antet (firma, logo, proje no, müşteri, hazırlayan, kontrol, tarih, revizyon)
- `.myge` JSON proje dosyası (`formatVersion` zorunlu)
- GitHub Releases'tan kurulum ve otomatik güncelleme (Velopack)
- Vektörel PDF (tek sayfa / tüm proje, istenirse farklı kağıt boyutuna ölçekli)

## İndir ve kur

1. [Son sürüm (Releases)](https://github.com/nicewo/MYG.Electrical/releases/latest) sayfasından
   **`MYG.Electrical-win-Setup.exe`** dosyasını indirip çalıştırın. Yönetici izni gerekmez; program
   `%LocalAppData%\MYG.Electrical` altına kurulur ve Başlat menüsüne eklenir. .NET kurulu olması gerekmez.
2. İmzasız olduğu için Windows SmartScreen "Windows bilgisayarınızı korudu" diyebilir:
   **Ek bilgi → Yine de çalıştır**.
3. Kurulumsuz kullanmak için `MYG.Electrical-win-Portable.zip` da var.

**Otomatik güncelleme:** Program her açılışta internet varsa GitHub'daki son sürümü denetler, yeniyse arka planda
indirir ve "şimdi yeniden başlatılsın mı?" diye sorar (Hayır → program kapanınca kurulur). Elle denetlemek için:
Yardım → Güncellemeleri denetle. İnternet yoksa sessizce atlanır.

**Yayın süreci:** `main` dalına her push'ta GitHub Actions derler, testleri çalıştırır ve testler geçerse
`v0.2.<çalıştırma no>` sürümünü Release olarak yayınlar (Velopack: kurulum dosyası + delta güncelleme paketi).

## Geliştirme

Gereksinim: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
git clone https://github.com/nicewo/MYG.Electrical.git
cd MYG.Electrical
dotnet build
dotnet test --solution MYG.Electrical.slnx
dotnet run --project src/MYG.Electrical.App
```

Geliştirme derlemesinde (`dotnet run`) otomatik güncelleme devre dışıdır; yalnızca Setup.exe ile kurulan sürümde çalışır.

Örnek proje: `samples/Demo.myge` (Dosya → Aç).

## Kısayollar

| Tuş | İşlem |
|---|---|
| S / L / T | Seç / Çizgi / Metin aracı |
| D / J | Dirsek / T bağlantı aracı |
| R | Seçimi veya yerleştirme önizlemesini 90° döndür |
| Del | Sil |
| Ctrl+Z / Ctrl+Y | Geri al / İleri al |
| Ctrl+C / X / V | Kopyala / Kes / Yapıştır |
| F | Sayfaya sığdır |
| Tekerlek / Orta tuş | Yakınlaştır / Kaydır |
| ← → ↑ ↓ / Shift+ok | Seçimi bir grid adımı / 1 mm kaydır |
| Sağ tık | Seç aracında bağlam menüsü (kes, kopyala, yapıştır, sil, döndür); başka araçta iptal |
| Esc | İşlemi iptal et, seçim aracına dön |
| PgUp / PgDn | Önceki / sonraki sayfa |

## Proje yapısı

```
src/
  MYG.Electrical.App             WPF arayüz (MVVM)
  MYG.Electrical.Core            Model, geometri, AutoWireEngine, düzenleme oturumu, sahne kompozisyonu (WPF bağımsız)
  MYG.Electrical.Drawing         DrawingVisual tabanlı çizim alanı ve WPF renderer
  MYG.Electrical.Symbols         Yerleşik IEC sembol kütüphanesi
  MYG.Electrical.Infrastructure  JSON kayıt/açma, PDF (PDFsharp), loglama (Serilog)
tests/
  MYG.Electrical.Core.Tests          AutoWireEngine, geometri, düzenleme, semboller
  MYG.Electrical.Serialization.Tests .myge format ve PDF çıktısı
  MYG.Electrical.Drawing.Tests       WPF renderer (Windows)
docs/
  Architecture.md · ProjectFormat.md · AutoWiring.md · Roadmap.md
```

Loglar: `%LOCALAPPDATA%\MYG.Electrical\logs`

## Lisans notu

PDF için **PDFsharp 6.2.x (MIT)** kullanılır. PDFsharp 6.5 ve sonrası AGPL-3.0 lisansına geçtiği için
bilinçli olarak 6.2 serisine sabitlenmiştir (`Directory.Packages.props`).
