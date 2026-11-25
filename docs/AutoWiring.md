# AutoWireEngine — Geometrik Otomatik Kablolama

Elektriksel simülasyon veya pin adı eşleştirmesi **değildir**. Yalnızca bağlantı noktalarının
konumu ve yönü kullanılır. Pin kimlikleri (`A1`, `13`…) sadece tanımlama içindir.

## Bağlantı kuralı

İki pin ancak şu koşulların **hepsi** sağlanırsa bağlanır:

1. **Aynı hat:** yatay bağlantıda `|Ya − Yb| ≤ tolerans`, dikey bağlantıda `|Xa − Xb| ≤ tolerans`.
2. **Uygun yönler:** `Right ↔ Left` (yatay) veya `Down ↔ Up` (dikey).
3. **Birbirine bakma:** `Right` pinin karşısındaki `Left` pin sağda; `Down` pinin karşısındaki `Up` pin altta olmalı.
4. **Karşılıklı en yakın:** A'nın baktığı yöndeki en yakın uygun pin B **ve** B'nin baktığı yöndeki en yakın uygun pin A olmalı.
5. Aynı cihazın pinleri birbirine bağlanmaz.

Tolerans varsayılan **0,5 mm**'dir ve yalnızca kayan nokta farklarını karşılamak içindir.

## Neden "karşılıklı en yakın"?

```
K1 ─R      L─ K2 (sağ pini yok)      L─ K3
```

- K1.R için en yakın uygun pin K2.L → aday.
- K2.L için en yakın uygun pin K1.R → **karşılıklı** → bağlanır.
- K3.L için en yakın uygun (sağa bakan) pin K1.R'dir, ama K1.R'nin en yakını K2.L olduğu için eşleşme karşılıklı değildir → **K3.L boşta kalır**. K1–K3 arasında asla çizgi çizilmez.

## Junction

`JunctionObject` aynı noktada dört yöne (`Left`, `Right`, `Up`, `Down`) bakan sanal pinlere sahiptir. Böylece
T ve dörtlü bağlantılar ayrı bir kural gerektirmeden aynı algoritmayla çözülür:

```
L1 ─────●───── L2
        │
        M
```

## Yönlendirme

V0.1: **Direct Routing** — iki pin arasında tek düz çizgi. L/Z yönlendirme ve engelden kaçma kapsam dışıdır.

## Ne zaman çalışır?

Cihaz/junction ekleme, taşıma, döndürme, silme ve geri al/ileri al sonrası `EditorSession` yalnızca **aktif sayfa**
için `Compute` çağırır. Çizgi, dikdörtgen, metin değişikliklerinde çalışmaz. Sonuçlar kalıcı değildir; eski
kablolar birikmez.

## Algoritma ve performans

1. Sayfadaki cihaz ve junction pinleri global koordinata çevrilir (`DeviceObject.GetGlobalPins`).
2. Yatay pinler Y'ye, dikey pinler X'e göre sıralanıp tolerans içindeki ardışık değerler aynı **hat kümesine** alınır.
3. Her küme içinde kaynak pinlerin (Right/Down) ve hedef pinlerin (Left/Up) en yakın adayları bulunur; karşılıklı olanlar bağlanır.
4. Eşit uzaklıkta deterministik sonuç için sahip kimliği + pin kimliği sırası kullanılır.

Karmaşıklık: sıralama `O(n log n)` + küme içi `O(k²)`. 400 cihazlık (800 pin) bir sayfa testte birkaç ms'de çözülür.

## Testler

`tests/MYG.Electrical.Core.Tests/AutoWireEngineTests.cs` şartnamedeki 10 zorunlu senaryoyu ve ek olarak
T/dörtlü junction, bara zinciri, aynı cihaz pinleri, kontaktör–motor 3 faz ve performans testlerini içerir.
