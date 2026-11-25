# Cihaz, Sembol, Potansiyel ve DRC Mimarisi

Bu belge v0.3 geliştirmesinin (IEC cihaz kütüphanesi, çok parçalı cihazlar, renkli kablolar, DRC) mimari
kararlarını anlatır. Mevcut mimari korunur; yeni sistem üzerine eklenir.

## 1. Mevcut durum (v0.2)

| Konu | v0.2'de durum | Eksik |
|---|---|---|
| Sembol | `SymbolDefinition` C# kodunda (`BuiltInSymbolLibrary`), 14 sembol | Veriye dayalı değil, pin fonksiyonu ve iç bağlantı yok |
| Cihaz | `DeviceObject` = yerleştirilmiş sembol + `Reference` (-K1) + pin kopyası | Üretici/model/elektriksel veri yok, çok parçalı cihaz yok |
| Kablo | `AutoWireEngine` hizalı ve birbirine bakan pinleri bağlar; kablolar kalıcı değil | Potansiyel, renk, numara, kesit, sayfalar arası bağlantı yok |
| Kontrol | Yok | DRC, raporlar |

Güçlü yanlar korunur: geometrik otomatik kablolama (dirsek/T/çapraz), geri alma, sahne kompozisyonu
(ekran = PDF), format sürümleme.

## 2. Katmanlar

```
Sembol kütüphanesi (JSON)          Cihaz kütüphanesi (JSON)
  SymbolDefinition                   DeviceTemplate
   ├ PinDefinition (fonksiyon)        ├ üretici, model, kategori
   ├ iç bağlantılar                   ├ ana sembol + parça sembolleri
   └ rol (ana fonksiyon / kontak)     └ kontak listesi, varsayılan özellikler
            │                                   │
            ▼                                   ▼
Proje ─ Sayfa ─ DeviceObject (yerleşim) ── Reference (etiket) ──► DeviceRecord (proje cihazı)
                PotentialObject, PageLinkObject, JunctionObject
                                │
                                ▼
            AutoWireEngine (sayfa) → NetAnalyzer (proje) → Net / Potansiyel / kablo rengi / numara
                                │
                                ▼
                         DrcEngine, raporlar
```

### Sembol ≠ cihaz

- **Sembol** genel IEC grafiğidir (ör. "NO kontak"). Veriye dayalıdır: `Symbols/Library/*.json`
  (gömülü) ve `%AppData%\MYG.Electrical\Symbols\*.json` (kullanıcı). Yeni sembol için kod değişmez.
- **Cihaz şablonu** üretici/model bilgisi taşıyan bileşendir (ör. "Kontaktör 3P 24 VDC 1NO+1NC").
  Hangi sembollerden oluştuğunu ve kontaklarını tanımlar.
- **Proje cihazı (`DeviceRecord`)** projedeki somut cihazdır; anahtarı etikettir (`-K1`). Etiket, üretici,
  model, açıklama, elektriksel özellikler, doküman yolu burada tutulur.

### Çok parçalı cihaz

EPLAN'daki gibi: aynı etiketi taşıyan bütün yerleşimler (`DeviceObject.Reference`) **aynı cihazın
parçalarıdır**. Bobin bir sayfada, ana kontaklar başka sayfada, yardımcı kontaklar üçüncü sayfada olabilir.
Sembolün rolü (`SymbolRole`): `Standalone`, `Main` (bobin, PLC CPU...), `Contact` (yardımcı/ana kontak).
Aynı etiketle iki `Main`/`Standalone` yerleşim → DRC hatası (çift etiket).

### Pinler

`ConnectionPoint` (sembol pini ve yerleşim kopyası) genişletilir: `Function` (güç, bobin, NO/NC kontak,
sinyal giriş/çıkış, besleme +/−, PE, N, haberleşme, ekran...) ve `Description`. Sembolde **iç bağlantılar**
(`InternalConnection`): klemens 1–2 kalıcı, sigorta/kontak açılıp kapanan. Potansiyel adı ve rengi açılıp
kapanan iç bağlantılardan da geçer (ör. +24V butondan sonra da +24V rengindedir); kısa devre kontrolü
yalnızca kalıcı bağlantıları kullanır.

### Potansiyel ve net

- `PotentialObject`: bir hattın başına konan potansiyel tanımı (L1, L2, L3, N, PE, +24V, 0V, sinyal,
  haberleşme). Tek pinlidir, otomatik kablolamaya katılır. Tür ve gerilim bilgisi taşır.
- `PageLinkObject`: sayfalar arası bağlantı oku. Aynı adlı oklar aynı neti oluşturur ve karşı sayfa/kolon
  referansını gösterir.
- `NetAnalyzer` (proje geneli): pinler düğüm; kablolar, junction'lar, kalıcı iç bağlantılar ve sayfa okları
  birleştirir → **net**. Açılıp kapanan iç bağlantılar da eklenerek → **potansiyel grubu** (renk ve ad).
- Kablo rengi = potansiyel türünün ekran rengi. **Renk tek başına anlam taşımaz**: potansiyel adı, tür ve
  gerilim ayrı veri olarak tutulur (DC + ve AC L1 aynı renkte olabilir).

### Kablo verisi

Kablolar geometrik olarak üretilmeye devam eder. Kullanıcının girdiği kablo verisi (numara, renk, kesit,
kablo tipi, açıklama) sayfada `WireData` olarak **iki uç pine** (`PinRef` çifti) bağlı saklanır; bağlantı
var olduğu sürece veri korunur. Kablo numarası EPLAN gibi açık bir komutla üretilir (net başına tek numara;
adlandırılmış potansiyellerde potansiyel adı).

### Çapraz referans

Sayfa 10 kolona bölünmüştür (`PageLayout`). Konum `sayfa.kolon` olarak yazılır (ör. `/3.4`).
Ana fonksiyonun (bobin) altında kontak aynası (kontak pinleri ve konumları), kontağın yanında ana
fonksiyonun konumu çizilir.

### Numaralandırma

IEC 81346-2 sınıf harfleri sembolde (`ReferencePrefix`): A, B, C, E, F, G, K, M, P, Q, R, S, T, U, V, W, X.
Proje ayarında biçim şablonu (`-{prefix}{n}`) değiştirilebilir. Etiket proje genelinde benzersizdir;
silinen numaralar yeniden kullanılmaz (en büyük + 1).

### DRC

`DrcEngine` (Core) bulgu listesi üretir: bağlantısız pin, boşta junction kolu, çift etiket, eksik cihaz
bilgisi, potansiyel çakışması (iki farklı potansiyel aynı nette = olası kısa devre), kullanılmayan kontak,
tanımsız pin. Bulgular alt panelde listelenir ve çizimde kırmızı vurgulanır. **DRC elektriksel güvenlik
veya mevzuat uygunluğu sertifikası değildir.**

## 3. Format

Sürüm 4: `devices`, sayfalarda `wireData`, nesne türleri `potential` ve `pageLink`, pinlerde `function`.
v3 → v4 dönüşümü mevcut `DeviceObject` etiketlerinden `DeviceRecord` üretir; çizim ve bağlantılar aynen kalır.
