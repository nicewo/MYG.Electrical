# MYG Electrical Kullanım Kılavuzu

Bu kılavuz programın **şu anki sürümünde çalışan** özelliklerini anlatır. Planlanan özellikler için
[Yol haritası](Roadmap.md).

## 1. Kurulum ve güncelleme

1. [Son sürüm](https://github.com/nicewo/MYG.Electrical/releases/latest) sayfasından
   `MYG.Electrical-win-Setup.exe` dosyasını indirip çalıştırın. Yönetici izni gerekmez; program
   `%LocalAppData%\MYG.Electrical` altına kurulur, Başlat menüsüne ve masaüstüne kısayol eklenir.
2. Program imzasız olduğu için Windows SmartScreen uyarı verebilir: **Ek bilgi → Yine de çalıştır**.
3. Program her açılışta internet varsa yeni sürümü denetler, arka planda indirir ve
   **şimdi yeniden başlatılsın mı?** diye sorar. *Hayır* derseniz program kapanınca kurulur.
   Elle denetleme: **Yardım → Güncellemeleri denetle**. Kullanılan sürüm sağ alttaki durum çubuğunda yazar.

## 2. Ekran düzeni

| Bölüm | İçerik |
|---|---|
| Üst menü | Dosya, Düzen, Görünüm, Ekle, Çizim, Sayfa, Araçlar, Yardım |
| Araç çubuğu | Seç, çizgi, dikdörtgen, metin, sembol, dirsek, T, çapraz, resim, sil, döndür, geri/ileri al, yakınlaştırma, grid, snap |
| Sol panel | Proje ağacı, sayfalar, IEC sembol kütüphanesi (aranabilir) |
| Orta | Çizim alanı: beyaz sayfa, grid, antet |
| Sağ panel | Seçili nesnenin özellikleri; seçim yoksa sayfa ve proje (kapak/antet) bilgileri |
| Durum çubuğu | İmleç koordinatı (mm), zoom, grid/snap, kablo sayısı, aktif sayfa, sürüm |

## 3. Proje ve sayfalar

- **Yeni proje** (Ctrl+N) bir kapak ve bir şema sayfasıyla başlar.
- **Sayfa ekleme:** Sayfa menüsü veya sol paneldeki `+`. Kapak sayfası başa eklenir.
- **Sıra, silme:** Sol paneldeki ▲ ▼ ✕ düğmeleri. Sayfa silme dahil bütün sayfa işlemleri Ctrl+Z ile geri alınır.
- **Sayfa ayarları:** Çizim alanında boş bir yere tıklayın (seçim olmasın); sağ panelde sayfa adı, türü
  (kapak/şema), formatı (A4–A1) ve yönelimi (yatay/dikey) değiştirilir.
- **Kapak ve antet:** Aynı panelde proje adı, firma, makine adı, proje no, müşteri, hazırlayan, kontrol eden,
  tarih, revizyon ve logo. Bu bilgiler kapak sayfasına ve her sayfanın antedine otomatik yazılır.
- **Örnek proje:** Yardım → Örnek projeyi aç. Örnek, kaydedilmemiş yeni proje olarak açılır.

## 4. Çizim

- **Grid ve snap:** 5 mm grid; Görünüm menüsünden veya araç çubuğundan açılıp kapatılır.
- **Çizgi / dikdörtgen:** Aracı seçin, başlangıç ve bitiş noktasına tıklayın.
- **Metin:** T ile tıklayın; içeriği ve yüksekliği sağ panelden düzenleyin.
- **Seçme ve taşıma:** Tıklayın veya alan seçin; sürükleyin ya da ok tuşlarını kullanın.
- **Döndürme:** R (90° adımlar). Sağ tık menüsünde de vardır.
- **Kopyala / yapıştır:** Yapıştırılan cihazlar yeni tanım numarası alır (-K1 → -K2).

## 4a. Paneller

Proje gezgini (sayfalar), Kütüphane, Özellikler, DRC, Mesajlar, Çapraz referanslar ve Proje bilgisi
Visual Studio'daki gibi **yerleştirilebilir panellerdir**:

- Başlıktaki **×** paneli gizler; **Görünüm → Paneller** menüsünden tekrar açılır.
- Başlıktan **sürükleyin**: ekranda çıkan hedeflere bırakarak sola/sağa/alta yerleştirin, başka panelle aynı
  sekme grubuna koyun veya boşa bırakıp **ayrı pencere** yapın (ikinci monitöre taşınabilir).
- Başlıktaki **raptiye** paneli kenara gizler (üstüne gelince açılır).
- Panel başlığına sağ tıklayınca çıkan menü: **Kayan** (serbest pencere), **Ayrı pencere**, **Yerleştir**,
  **Sekmeli belge olarak yerleştir**, **Otomatik gizle**, **Gizle**.
- Düzen kapanışta kaydedilir ve sonraki açılışta aynen gelir. **Görünüm → Paneller → Varsayılan düzene dön**
  ilk haline getirir.

Proje gezgini, projeyi ve sayfaları tek ağaçta gösterir; üstteki + ▲ ▼ ✕ düğmeleri sayfa ekler, taşır, siler.
Sayfaya tıklayınca açılır.

## 5. Semboller ve cihazlar

Sol alttaki **Kütüphane** iki sekmelidir:

- **Semboller:** 113 IEC 60617 sembolü (genel grafik). Kategori ağacından veya aramadan bulun.
- **Cihazlar:** ürün bilgili şablonlar (ör. "Kontaktör 9 A, 24 VDC bobin, 1NO+1NC", "Frekans inverteri 22 kW").
  Yerleştirince proje cihazı bu bilgilerle (elektriksel veri, kontak kapasitesi) oluşur. Üretici süzgeci vardır.

Yerleştirme: öğeye **çift tıklayın** ve çizimde tıklayın (R ile döndürün, sağ tık/Esc ile bitirin) ya da öğeyi
çizim alanına **sürükleyip bırakın**. Alttaki önizlemede **★ Favori** ile favorilere ekleyin; son kullanılanlar
otomatik listelenir. Arama birden çok kelimeyi birlikte arar ("kontaktör 24").

Her cihaz IEC 81346 etiketi alır (`-K1`, `-F1`, `-M1`…). Şablon proje özelliklerinde **Etiket şablonu**
alanındadır (`-{prefix}{n}`, sayfa numaralı: `={page}-{prefix}{n}`). **Proje → Cihazları yeniden
numaralandır** bütün etiketleri sayfa ve kolon sırasıyla yeniden verir.

**Çok parçalı cihaz:** bir kontağın etiketini bobininkiyle aynı yapın (sağ panel → Tanım). Kontak o cihazın
parçası olur, sıradaki numarayı alır (13-14, 23-24...). Bobinin altında kontak aynası, kontağın altında
bobinin `/sayfa.kolon` konumu yazılır.

**Cihaz özellikleri:** cihaza çift tıklayın. Sekmeler: Genel (etiket, açıklama, yer, işlev metni), Elektriksel
veri, Bağlantılar/Pinler (görünen pin adı düzenlenebilir), Üretici, Kontaklar (kullanılan/boş), Çapraz
referanslar, Dokümantasyon (datasheet, notlar). Tamam tek adımda geri alınabilir.

**Kendi sembol ve cihazlarınız:** `%AppData%\MYG.Electrical\Symbols\` ve `...\Devices\` klasörlerine JSON
dosyası koyun ve programı yeniden açın (biçim: `docs/SymbolLibrary.md`).

## 5a. Kendi PLC'nizi / cihazınızı çizmek (EPLAN tarzı)

Kütüphanede olmayan bir PLC'yi (ör. **Delta AS228T**) sayfada çizip kütüphaneye ekleyebilirsiniz:

1. **Ekle → Cihaz / PLC tasarımı → Cihaz kutusu** (veya araç çubuğundaki kesikli kutu): iki köşeye tıklayın.
2. **Giriş pini** aracını seçin, kutunun üst kenarının 5 mm üstüne tıklayın. Bağlantı adı penceresi açılır:
   `X0.0` yazın, Enter. Sonraki tıklamalarda pin adları **kendiliğinden artar**: X0.1, X0.2 … X0.15, X1.0
   (Delta AS: 16 bit). `I0.0` yazarsanız I0.7'den sonra I1.0 (Siemens), `X0` yazarsanız X1, X2, X3 gelir.
   Sıralama biçimi pencerede seçilebilir (ondalık, sekizli X0…X7, X10, bayt.bit, word.bit).
3. **Çıkış pini** (kutunun altına, kablo aşağı çıkar), **Güç + / Güç −** (L+, M), PE ve haberleşme pinleri aynı
   şekilde. R ile yön değişir. Pin aracını yeniden seçince ad penceresi tekrar açılır.
4. İsterseniz kutunun içine metin yazın (ör. "AS228T").
5. Kutuyu ve pinleri **alan seçimiyle** seçin → **Seçimden cihaz oluştur**: ad (Delta AS228T), kategori
   (PLC ve Otomasyon/Delta), üretici, model, tanım harfi (A).
6. Cihaz sol listedeki **Cihazlar** sekmesinde seçtiğiniz kategoride görünür; çift tık veya sürükle-bırakla
   istediğiniz sayfaya konur. Çizim de (işaretliyse) yeni cihazla değiştirilir, bağlı kablolar yerinde kalır.

Kayıt yeri: `%AppData%\MYG.Electrical\Symbols\kullanici.json` ve `...\Devices\kullanici.json`. Proje
kaydedilirken kullanılan kullanıcı cihazları **projeye de gömülür**; proje başka bilgisayarda açılınca çizilir.

**Birden fazla sayfaya yayılan PLC:** girişleri ve çıkışları ayrı kutular olarak çizin. Birini "ana parça",
diğerini "ek parça" olarak oluşturun; yerleştirdikten sonra ek parçaya ana parçanın etiketini verin (-A1).
Ana parçanın etiketinin altında diğer parçaların, ek parçanın altında ana parçanın `/sayfa.kolon` konumu yazılır.

## 5b. Potansiyeller, sayfa bağlantıları, kablo numaraları

- **Ekle → Potansiyel** (L1, L2, L3, N, PE, +24V, 0V, diğer): hattın başına koyun. Ad ve tür bağlı kablolara geçer;
  kablolar türün rengiyle çizilir. Sigorta/klemens ve kontaklardan geçer, bobin ve yüklerde durur.
- **Ekle → Sayfa bağlantısı:** hattı başka sayfaya taşır. Diğer sayfaya **aynı adla** ikinci bir bağlantı koyun;
  yanlarında karşı ucun `/sayfa.kolon` referansı yazar.
- **Kablo bilgisi:** Seç aracıyla kabloya tıklayın; sağ panelde numara, damar rengi, kesit, kablo tipi, açıklama.
  "Numara kilitli" işaretliyse otomatik numaralandırma dokunmaz.
- **Proje → Kabloları numaralandır:** her iletkene (net) bir numara; potansiyele doğrudan bağlı hatlar
  potansiyelin adını alır. Şablon: proje özelliklerinde **Kablo no şablonu** (`{n}`, `{page}{column}`...).
- **Araçlar → Renk ayarları:** potansiyel, sembol, çerçeve, metin, seçim ve hata renkleri. Renk yalnızca görsel
  yardımdır. **Dosya → PDF'i siyah-beyaz aktar** baskı için.

## 5c. DRC ve raporlar

- **Proje → DRC (F7):** sonuçlar alt paneldeki DRC sekmesinde; hatalar kırmızı, uyarılar turuncu. Satıra çift
  tıklayınca ilgili sayfa açılır ve nesneler seçilir; hatalı/uyarılı nesneler çizimde kırmızı çerçevelenir.
  **DRC elektriksel güvenlik veya mevzuat uygunluğu sertifikası değildir**; kesit, koruma seçimi ve
  standartlara uygunluk ayrıca doğrulanmalıdır.
- **Raporlar → Tüm raporlar (Excel):** cihaz listesi, malzeme listesi, klemens planı, kablo listesi ve PLC G/Ç
  listesi tek .xlsx dosyasında (her biri ayrı sayfa). Tek tek CSV de alınabilir.
- Alt paneldeki **Mesajlar**, **Çapraz referanslar** ve **Proje bilgisi** sekmeleri; panel **Görünüm → Alt
  panel** ile gizlenir.

## 6. Otomatik kablolama

Kablolar elle çizilmez. İki pin **aynı yatay veya dikey hatta** ve **birbirine bakıyorsa** otomatik bağlanır;
arada daha yakın uygun bir pin varsa uzaktakine bağlanmaz. Cihaz taşındıkça kablolar anında güncellenir.

Kablonun dönmesi veya dallanması için **bağlantı noktaları** kullanılır:

| Araç | Kısayol | Ne işe yarar |
|---|---|---|
| Dirsek | D | Kablonun 90° dönmesi (┌ ┐ └ ┘). Noktası çizilmez. |
| T bağlantı | J | Bir hattan dal almak (┬ ┤ ┴ ├). Kapalı tarafından gelen hat bağlanmaz. |
| Çapraz | araç çubuğu | Kesişen yatay ve dikey hattı gerçekten birleştirmek için (┼). |

Dirsek veya T yerleştirirken program, çevredeki pinlere bakarak uygun yönü **otomatik önerir**; R ile
değiştirebilirsiniz. Bağlantı noktası olmayan kesişmelerde hatlar birbirine bağlanmaz.

## 7. Resim ekleme

**Ekle → Resim…** veya araç çubuğundaki resim düğmesi:

1. Dosyayı seçin (PNG, JPG, BMP, GIF, TIFF). Resim **projenin içine kopyalanır**; orijinal dosya
   silinse de proje bozulmaz.
2. Boyutu mm olarak girin (en-boy oranı korunabilir).
3. **Saydamlık** kutusunu işaretleyip önizlemede saydam olacak renge tıklayın. **Hassasiyet** (0–100)
   arttıkça o renge yakın tonlar da silinir. Kenarlarda açık renk hale kalırsa 15–25 deneyin.
4. Tamam'dan sonra sayfada resmin sol üst köşesine tıklayın.

Sonradan değiştirmek için resmi seçip sağ paneldeki **Resim ayarları…** düğmesine basın.

## 8. Kaydetme ve açma

- Proje tek bir `.myge` dosyasıdır (resimler dahil).
- Her kayıtta önceki sürüm `<dosya>.myge.bak` olarak saklanır. Kayıt herhangi bir nedenle yarım kalırsa
  mevcut dosya değişmez.
- Açılışta onarılan sorunlar (ör. eksik resim) bir liste halinde gösterilir.

## 9. PDF çıktısı

- **Dosya → Tüm projeyi PDF'e aktar** (Ctrl+P) veya **Aktif sayfayı PDF'e aktar**.
- Çıktı vektöreldir, sayfalar gerçek ölçüsünde (A4–A1) ve kapak/antetle birlikte basılır.
- Sayfa sınırının dışına taşan nesneler için uyarı verilir.

## 10. Sorun giderme

- **Loglar:** Araçlar → Log klasörünü aç (`%LocalAppData%\MYG.Electrical\logs`).
- **Hata bildirimi ve öneriler:** Yardım → Hata bildir / öneri (GitHub Issues). Mümkünse son log dosyasını ekleyin.
