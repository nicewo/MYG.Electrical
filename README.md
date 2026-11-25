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
- Vektörel PDF (tek sayfa / tüm proje, istenirse farklı kağıt boyutuna ölçekli)

## Kurulum ve çalıştırma

Gereksinim: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
git clone https://github.com/nicewo/MYG.Electrical.git
cd MYG.Electrical
dotnet build
dotnet test --solution MYG.Electrical.slnx
dotnet run --project src/MYG.Electrical.App
```

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
| Esc / Sağ tık | İşlemi iptal et, seçim aracına dön |
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
