# Sembol kütüphanesi (JSON)

MYG Electrical sembolleri koddan değil JSON dosyalarından okur. Yeni sembol eklemek için programı yeniden derlemek gerekmez.

## Nerede?

| Kaynak | Konum | Not |
|---|---|---|
| Yerleşik IEC 60617 kütüphanesi | `src/MYG.Electrical.Symbols/Library/*.json` (uygulamaya gömülü) | `tools/symbolgen/generate.py` ile üretilir |
| Kullanıcı sembolleri | `%AppData%\MYG.Electrical\Symbols\*.json` | Program açılışında okunur |

Kullanıcı dosyası yerleşik bir sembolün kimliğini (`id`) yeniden tanımlarsa o sembol **atlanır** ve günlüğe uyarı yazılır. Böylece mevcut projelerdeki semboller değişmez. Bozuk bir sembol yalnızca kendisini düşürür; dosyanın geri kalanı yüklenir.

## Dosya biçimi

```json
{
  "formatVersion": 1,
  "library": "Benim Kütüphanem",
  "version": "1.0.0",
  "symbols": [
    {
      "id": "user.valve.5-2",
      "name": "5/2 valf bobini",
      "category": "Valfler/Pnömatik",
      "description": "Tek bobinli 5/2 yön valfi",
      "keywords": ["valf", "festo"],
      "referencePrefix": "Y",
      "role": "standalone",
      "primitives": [
        { "kind": "rect", "x": -5, "y": -3, "width": 10, "height": 6 },
        { "kind": "line", "x1": -5, "y1": 3, "x2": 5, "y2": -3 },
        { "kind": "line", "x1": 0, "y1": -10, "x2": 0, "y2": -3 },
        { "kind": "line", "x1": 0, "y1": 3, "x2": 0, "y2": 10 }
      ],
      "pins": [
        { "id": "1", "x": 0, "y": -10, "direction": "up", "function": "coil" },
        { "id": "2", "x": 0, "y": 10, "direction": "down", "function": "coil" }
      ],
      "internalConnections": [],
      "defaultProperties": { "Gerilim": "24VDC" }
    }
  ]
}
```

### Kurallar

- Ölçüler **mm**, Y ekseni **aşağı**, orijin sembolün ekleme noktasıdır.
- Pinler **5 mm grid** üzerinde olmalıdır; aksi halde otomatik bağlantı çalışmaz.
- `id` proje dosyasına yazılır. Yayımlanmış bir kimliği değiştirmeyin; yeni sürüm için yeni kimlik verin.
- `category` "/" ile alt kategori alır: `"Koruma/Otomatik sigortalar"`.

### Şekiller (`primitives`)

| kind | Alanlar |
|---|---|
| `line` | `x1, y1, x2, y2`, `dashed` |
| `rect` | `x, y, width, height`, `filled`, `dashed` |
| `circle` | `centerX, centerY, radius`, `filled` |
| `arc` | `centerX, centerY, radius, startAngle, sweepAngle` (derece, 0° = +X, saat yönü) |
| `polygon` | `points: [x0, y0, x1, y1, ...]`, `open` (true = açık çoklu çizgi), `filled` |
| `text` | `x, y, text, height` (ortalanmış) |

### Pin alanları

- `direction`: `up`, `down`, `left`, `right` (kablonun çıktığı yön).
- `function`: `general`, `power`, `coil`, `contactNo`, `contactNc`, `contactCommon`, `supplyPositive`, `supplyNegative`, `neutral`, `protectiveEarth`, `input`, `output`, `communication`, `shield`, `terminal`.

### Rol (`role`)

| Rol | Anlamı |
|---|---|
| `standalone` | Tek parçalı cihaz (sigorta, motor, sensör). |
| `main` | Ana fonksiyon (bobin, PLC CPU, sürücü güç bölümü). |
| `contact` | Aynı etiketli ana cihaza ait parça (kontak, kontaktör ana kontakları, G/Ç kanalı). |
| `graphic` | Etiket almayan işaret (toprak, ekran). `referencePrefix` boş olmalıdır. |

### Etiket yeri (`referencePosition`)

Etiket (ör. `-K1`), işlev metni ve çapraz referanslar (kontak aynası, `/2.3`) sembolün neresine yazılır:

| Değer | Kullanım |
|---|---|
| `right` (varsayılan) | Sembolün sağı. |
| `left` | Sembolün solu, sağa hizalı. EPLAN'daki gibi dikey bobin, kontak, klemens şeridi. |
| `top` | Sembolün üstü, ortalı. Yan yana dizilen dar modüller. |
| `inSymbol` | Dışarıya yazılmaz; sembol kendi metin alanlarıyla gösterir (EPLAN PLC kutusu). |

Metin şekillerinde (`text`) şu alanlar yerleşimin değeriyle doldurulur: `{Reference}` (etiket), `{Description}`
(işlev metni), `{Pin:1}` (pinin bağlantı tanımı, ör. PLC adresi `I2.0`), `{CrossReference}` (çapraz referans).
Örnek: `samples/KamaraFirin.myge` içindeki `user.kamara-plc-giris-t` PLC kanal kutusu.

### İç bağlantılar (`internalConnections`)

`{ "a": "13", "b": "14" }` iki pin arasında iletken yol tanımlar. `kind` verilmezse `switched` kabul edilir (kontak, kesici). Klemens, sigorta ve köprü için `"kind": "permanent"` kullanın. Potansiyel her iki türden de geçer. Kısa devre kontrolü yalnız `permanent` bağlantıları dikkate alır.

## Yerleşik kütüphaneyi güncellemek

```
python3 tools/symbolgen/generate.py
```

Betik id tekrarını, pin tekrarını, grid dışı pinleri ve tanımsız iç bağlantıları üretim sırasında reddeder. Testler (`SymbolCatalogTests`) ilk sürümdeki 14 sembolün geometrisinin değişmediğini de doğrular.
