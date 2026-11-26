<div align="center">
  <a href="https://myusufgultekin.web.app/"><img src="./assets/myg-project-banner.svg" alt="MYG ELECTRICAL — MYG Tech" width="100%"></a>
  <p><a href="https://github.com/nicewo/MYG.Electrical/releases/latest"><img src="https://img.shields.io/github/v/release/nicewo/MYG.Electrical?style=for-the-badge&label=LATEST%20RELEASE" alt="Latest release"></a> <a href="https://github.com/nicewo/MYG.Electrical/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/nicewo/MYG.Electrical/build.yml?style=for-the-badge&label=BUILD" alt="Build"></a> <img src="https://img.shields.io/badge/WINDOWS-10%2F11-2563EB?style=for-the-badge&logo=windows&logoColor=white" alt="Windows"></p>
  <p><a href="https://myusufgultekin.web.app/">MYG TECH</a> · <a href="https://github.com/nicewo">GitHub Profile</a></p>
</div>

---

<p align="center">
  <img src="src/MYG.Electrical.App/Assets/myg-electrical.png" width="150" alt="MYG Electrical">
</p>

<h1 align="center">MYG Electrical</h1>

<p align="center">
  <b>Makine imalatı için Windows tabanlı IEC elektrik şeması çizim yazılımı</b><br>
  MYG Tech tarafından geliştirilmektedir.
</p>

<p align="center">
  <a href="https://github.com/nicewo/MYG.Electrical/releases/latest"><img alt="Son sürüm" src="https://img.shields.io/github/v/release/nicewo/MYG.Electrical?label=s%C3%BCr%C3%BCm"></a>
  <a href="https://github.com/nicewo/MYG.Electrical/actions/workflows/build.yml"><img alt="Derleme" src="https://github.com/nicewo/MYG.Electrical/actions/workflows/build.yml/badge.svg"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-1f6feb">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-512bd4">
</p>

<p align="center">
  <a href="https://github.com/nicewo/MYG.Electrical/releases/latest"><b>⬇ İndir (Setup.exe)</b></a> ·
  <a href="docs/KullanimKilavuzu.md">Kullanım kılavuzu</a> ·
  <a href="docs/KlavyeKisayollari.md">Kısayollar</a> ·
  <a href="https://myusufgultekin.web.app/">MYG Tech</a>
</p>

---

## MYG Electrical nedir?

MYG Electrical, makine imalatında kullanılan **kumanda ve güç devrelerini** çizmek, proje kapağı ve antetli
teknik sayfalar hazırlamak, çok sayfalı projeleri yönetmek ve **vektörel PDF** almak için geliştirilmiş,
sade ve hızlı bir Windows masaüstü uygulamasıdır.

EPLAN gibi büyük paketlerin tamamını kopyalamayı hedeflemez. Odak noktası, sahada ve atölyede en sık ihtiyaç
duyulan işi — IEC sembolleriyle temiz, okunur ve ölçekli bir şema çıkarmayı — az tıklamayla yapmaktır.
Öne çıkan özelliği, hizalanan cihazların pinlerini kendiliğinden bağlayan **geometrik otomatik kablolama**dır.

### Hangi ihtiyacı karşılar?

- Küçük ve orta ölçekli makine projelerinde, ağır bir CAD lisansı olmadan **hızlı şema** çıkarmak
- Pano imalatı ve servis için **antetli, sayfa numaralı, ölçekli PDF** üretmek
- Proje dosyasını **tek dosyada** (resimler dahil) taşımak, arşivlemek ve paylaşmak

### Kimler için?

Makine imalatçıları, pano üreticileri, otomasyon ve bakım teknisyenleri, elektrik-elektronik ve mekatronik
mühendisleri, teknik eğitim veren kurumlar ve öğrenciler.

## Ekran görüntüleri

Aşağıdaki görüntüler programın kendi ürettiği PDF çıktısıdır (`samples/Demo.myge`):

| Şema sayfası | Kapak sayfası |
|---|---|
| <img src="docs/images/pdf-sema.png" alt="Şema sayfası PDF çıktısı" width="520"> | <img src="docs/images/pdf-kapak.png" alt="Kapak sayfası PDF çıktısı" width="520"> |

## Temel özellikler (v0.3)

### Çizim
- Gerçek ölçü birimi **milimetre**; A4, A3, A2, A1 — yatay ve dikey
- 5 mm grid ve snap, imlece göre zoom, orta tuşla kaydırma
- Çizgi, dikdörtgen, metin; seçim, çoklu seçim, alan seçimi, sürükle-taşı, 90° döndürme
- Kes / kopyala / yapıştır, ok tuşlarıyla hassas kaydırma, sağ tık menüsü
- Sayfa işlemleri dahil **tam geri al / ileri al**

### Sembol ve cihaz kütüphanesi
- **113 IEC 60617 sembolü**, 10 kategori: koruma (sigorta, MCB 1P–4P, motor koruma şalteri, termik, kaçak akım,
  parafudr, ayırıcı), kontaktör ve röle (bobinler, zaman röleleri, NO/NC/CO ve zamanlı kontaklar, faz sırası ve
  güvenlik rölesi, SSR), motor ve sürücü (motorlar, frekans inverteri güç + kumanda + haberleşme, yumuşak yolverici,
  fren, pompa, fan), PLC ve otomasyon (CPU, DI/DO/AI/AO modülleri, tek kanal G/Ç, HMI, switch, RS485), sensör ve saha
  (endüktif/kapasitif/optik sensörler, limit switch, basınç/seviye/akış şalteri, termostat, PT100, termokupl,
  transmitter, enkoder, kapı ve emniyet switchi), kumanda (butonlar, seçici, lamba, buzzer, korna), klemens ve
  konnektör, ölçüm (analizör, akım trafosu, sayaç), enerji (güç kaynağı, 24 VDC dağıtım, trafo, priz) ve diğerleri.
- Semboller **JSON**: kendi sembolünüzü `%AppData%\MYG.Electrical\Symbols\*.json` dosyasına ekleyin, kod değişmez
  ([biçim](docs/SymbolLibrary.md)). Pinlerin işlevi (güç, bobin, NO/NC, giriş/çıkış, besleme, PE...) ve iç
  bağlantıları tanımlıdır.
- **Cihaz kütüphanesi** sembolden ayrıdır: 63 genel şablon (kontaktörler, sigortalar, 22 kW / 2.2 kW sürücüler,
  PLC, Delta HMI, analizör, akım trafoları, güç kaynakları, sensörler, butonlar...). Kendi ürünlerinizi
  `%AppData%\MYG.Electrical\Devices\*.json` ile ekleyin.
- Kütüphane panelinde kategori ağacı, çok kelimeli arama, üretici süzgeci, favoriler, son kullanılanlar,
  önizleme; çift tık veya **sürükle-bırak** ile yerleştirme.

### Cihazlar (EPLAN mantığı)
- **Çok parçalı cihaz:** aynı etiketi taşıyan bobin, ana kontaklar ve yardımcı kontaklar farklı sayfalarda
  olabilir. Eklenen yardımcı kontak sıradaki numarayı alır (13-14 → 23-24 → 33-34).
- **Çapraz referans:** bobin etiketinin altında kontak aynası (`NO 13-14 /5.3`), kontağın altında bobinin
  `/sayfa.kolon` konumu.
- **Cihaz özellikleri** (çift tık): genel, elektriksel veri, pinler, üretici, kontaklar (kullanılan / boş /
  kapasite dışı), çapraz referanslar, dokümantasyon.
- **Kendi PLC / cihazını çiz:** kesikli cihaz kutusu ve güç/giriş/çıkış pinleri; pin adları EPLAN gibi kendiliğinden
  artar (X0 → X1, I0.7 → I1.0, Delta AS X0.15 → X1.0). "Seçimden cihaz oluştur" ile cihaz (ör. Delta AS228T)
  kütüphaneye ve sol listeye eklenir, projeye de gömülür. Çok sayfalı PLC'lerde parçalar birbirinin
  `/sayfa.kolon` konumunu gösterir.
- **IEC 81346 etiketleri** proje şablonuyla (`-{prefix}{n}`, `={page}-{prefix}{n}`...) ve toplu yeniden
  numaralandırma.

### Otomatik kablolama
- Aynı hatta bulunan ve **birbirine bakan** pinler kendiliğinden düz kabloyla bağlanır
- **Karşılıklı en yakın pin** kuralı: araya giren uygun pin varsa uzaktaki pine kablo çekilmez
- Cihaz taşındıkça kablolar canlı güncellenir; kablolar dosyada saklanmaz, her seferinde yeniden üretilir
- EPLAN tarzı bağlantı noktaları: **dirsek** (90° dönüş), yönlü **T** ve bilerek seçilen **çapraz** düğüm;
  kesişen hatlar yanlışlıkla birleşmez
- Yerleştirirken dirsek/T yönü çevredeki pinlere göre otomatik önerilir

Ayrıntı: [docs/AutoWiring.md](docs/AutoWiring.md)

### Potansiyeller, renkli kablolar ve kablo numaraları
- **Potansiyel** nesnesi (L1, L2, L3, N, PE, +24V, 0V, sinyal, haberleşme): adı, türü ve gerilimi kabloya geçer;
  sigorta ve klemenslerden, kontak ve kesicilerden (anahtarlı) ilerler, bobin ve yüklerde durur.
- Kablolar potansiyel rengiyle çizilir (L1 kırmızı, L2 kahverengi, L3 siyah, N mavi, PE yeşil, +24V kırmızı,
  0V koyu mavi, sinyal camgöbeği, haberleşme mor). Renkler **Araçlar → Renk ayarları**ndan değişir; renk tek
  başına anlam taşımaz, ad her zaman yazılır. PDF isteğe göre siyah-beyaz.
- **Sayfa bağlantısı** (kesme noktası): aynı adlı bağlantılar sayfalar arasında birleşir, karşı ucun
  `/sayfa.kolon` referansı yazılır.
- **Kablo bilgisi:** kabloya tıklayıp numara, damar rengi, kesit, kablo tipi, açıklama girin.
- **Otomatik kablo numaralandırma:** iletken (net) başına tek numara, potansiyele bağlı hatlarda potansiyel adı,
  kilitli numaralar korunur.

### Denetim ve raporlar
- **DRC (F7):** bağlantısız pin, boşta kalan bağlantı kolu, çift etiket, tanımsız sembol/pin, tekrar eden
  kontak/klemens numarası, eksik cihaz bilgisi, potansiyel çakışması ve **kısa devre**, kontak kapasitesi,
  eşi olmayan sayfa bağlantısı. Bulgular alt panelde listelenir ve çizimde kırmızı işaretlenir.
  **DRC elektriksel güvenlik veya mevzuat uygunluğu sertifikası değildir.**
- **Raporlar:** cihaz listesi, malzeme listesi (BOM), klemens planı, kablo listesi, PLC G/Ç listesi —
  tek Excel dosyası (.xlsx) veya ayrı CSV.
- Alt panel: DRC, mesajlar, çapraz referanslar, proje bilgisi.

### Proje ve sayfa yönetimi
- Kapak ve şema sayfaları, sayfa ekleme/silme/sıralama
- Kapak sayfası ve düzenlenebilir antet: firma, logo, makine adı, proje no, müşteri, hazırlayan,
  kontrol eden, tarih, revizyon, sayfa `n / N`, 0–9 kolon numaraları
- `.myge` JSON proje dosyası; güvenli kayıt (doğrulama + `.bak` yedeği), açılışta onarım

### Görsel ekleme
- PNG, JPG, BMP, GIF, TIFF; resim **proje dosyasına gömülür**
- Renk seçici ile **saydamlık** ve 0–100 **hassasiyet** (yakın renkleri de siler); PDF'te de saydam

### PDF çıktısı
- Tek sayfa veya tüm proje, **vektörel** (ekran görüntüsü değil), gerçek kağıt ölçüsünde
- Kapak ve antet dahil; sayfa dışına taşan nesneler için uyarı

### Kurulum ve otomatik güncelleme
- GitHub Releases üzerinden **Setup.exe** veya **Portable.zip**; .NET kurulumu gerekmez
- Açılışta yeni sürüm denetimi, arka planda (yalnızca değişen kısmı) indirme, onaylı yeniden başlatma

## Sistem gereksinimleri

| | |
|---|---|
| İşletim sistemi | Windows 10 veya 11, 64 bit |
| Disk | yaklaşık 250 MB |
| Ekran | en az 1280×720 önerilir |
| .NET | gerekmez (pakete dahil) |
| İnternet | yalnızca güncelleme denetimi için; çevrimdışı çalışır |

## İndir ve kur

1. **[Son sürüm](https://github.com/nicewo/MYG.Electrical/releases/latest)** sayfasından
   `MYG.Electrical-win-Setup.exe` dosyasını indirip çalıştırın. Yönetici izni gerekmez
   (`%LocalAppData%\MYG.Electrical` altına kurulur).
2. Program imzasız olduğu için Windows SmartScreen uyarı verebilir: **Ek bilgi → Yine de çalıştır**.
3. Kurulumsuz kullanım için `MYG.Electrical-win-Portable.zip`.

Program açılışta internet varsa güncellemeyi kendisi denetler. Elle: **Yardım → Güncellemeleri denetle**.

## Örnek proje

Programda **Yardım → Örnek projeyi aç**, ya da depodaki
[`samples/PompaIstasyonu.myge`](samples/PompaIstasyonu.myge): 7 sayfalık pompa istasyonu — 22 kW ve 2.2 kW
sürücüler, kontaktörle yol verilen motor (motor koruma, termik), faz sırası rölesi, akım trafoları ve enerji
analizörü, 24 VDC güç kaynağı ve dağıtım, start/stop/acil stop kumandası, PLC CPU + DI/DO modülleri, kapı ve
basınç şalteri, endüktif sensör, Delta HMI; potansiyeller, sayfa bağlantıları ve numaralı kablolarla.
Daha basit eski örnek: [`samples/Demo.myge`](samples/Demo.myge) (format 3, açılırken yükseltilir).

## Geliştirme mimarisi

```
src/
  MYG.Electrical.App             WPF arayüz (MVVM), güncelleme, Hakkında
  MYG.Electrical.Core            Model, geometri, AutoWireEngine, düzenleme oturumu, sahne kompozisyonu (WPF bağımsız)
  MYG.Electrical.Drawing         DrawingVisual tabanlı çizim alanı ve WPF renderer
  MYG.Electrical.Symbols         Yerleşik IEC sembol kütüphanesi
  MYG.Electrical.Infrastructure  .myge kayıt/açma, PDF (PDFsharp), log (Serilog)
tests/                           xUnit v3 — Core, serileştirme/PDF, WPF renderer
docs/                            Mimari, proje formatı, otomatik kablolama, kılavuz, yol haritası
```

- C# / .NET 10 / WPF, MVVM; Core katmanı WPF'e bağımlı değildir
- Ekran ve PDF aynı sahne kompozisyonunu kullanır (`ISceneSink`), bu yüzden çıktı ekranla aynıdır
- Her push'ta GitHub Actions derler ve 180+ testi çalıştırır; `main`'de uygulama dosyaları (src/, samples/,
  derleme ayarları) değiştiyse paketleyip Release yayınlar (Velopack). Yalnızca belge/görsel değişikliğinde
  release çıkmaz; gerekirse Actions → build → *Run workflow* ile elle yayınlanır.

Kaynaktan derleme (Windows, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)):

```powershell
git clone https://github.com/nicewo/MYG.Electrical.git
cd MYG.Electrical
dotnet build
dotnet test --solution MYG.Electrical.slnx
dotnet run --project src/MYG.Electrical.App
```

Ayrıntılı belgeler: [Mimari](docs/Architecture.md) · [Proje formatı](docs/ProjectFormat.md) ·
[Otomatik kablolama](docs/AutoWiring.md) · [Cihaz ve potansiyel mimarisi](docs/DeviceArchitecture.md) ·
[Sembol kütüphanesi biçimi](docs/SymbolLibrary.md) · [Son inceleme raporu](docs/Review-2026-11-25.md)

## Yol haritası

Planlanan başlıca konular (henüz **yok**): grafik sembol editörü, L/Z (dik açılı) otomatik kablo yönlendirme,
klemens şeridi ve kablo (çok damarlı) nesneleri, PLC adres atama sihirbazı, kayan paneller, doğrudan yazdırma.
Güncel liste: [docs/Roadmap.md](docs/Roadmap.md).

## Geliştirici

**MYG Electrical**, MYG Tech bünyesinde bağımsız olarak geliştirilen bir yazılım ürünüdür.

**Muhammed Yusuf Gültekin** — Software & Electronics Engineer
🌐 [myusufgultekin.web.app](https://myusufgultekin.web.app/) · GitHub: [@nicewo](https://github.com/nicewo)

## İletişim ve katkı

- Hata bildirimi ve öneriler: [GitHub Issues](https://github.com/nicewo/MYG.Electrical/issues)
  (mümkünse **Araçlar → Log klasörünü aç** ile son log dosyasını ekleyin)
- Kod katkısı için önce bir issue açıp konuyu konuşalım; ardından pull request gönderebilirsiniz.
  Pull request'lerde derleme ve testler otomatik çalışır.

## Lisans

MYG Electrical **ücretsiz** indirilip kullanılabilir ve kaynak kodu herkese açıktır. Ancak **henüz bir açık
kaynak lisansı seçilmemiştir**: depoda `LICENSE` dosyası yayımlanana kadar kodun kopyalanması,
değiştirilmesi ve yeniden dağıtılması için geliştiricinin izni gerekir.

Kullanılan üçüncü taraf bileşenler ve lisansları: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

© MYG Tech — Muhammed Yusuf Gültekin
