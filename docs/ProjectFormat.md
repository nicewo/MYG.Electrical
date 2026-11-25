# .myge Proje Formatı

UTF-8 JSON, camelCase alan adları, enum değerleri metin. Uzantı: `.myge`.

```json
{
  "formatVersion": 3,
  "project": {
    "name": "Machine01",
    "company": "MYG TECH",
    "revision": "A",
    "machineName": "Machine01",
    "projectNumber": "P-001",
    "customer": "",
    "preparedBy": "",
    "checkedBy": "",
    "date": "25.11.2026",
    "logoPath": ""
  },
  "pages": [
    {
      "id": "page-001",
      "name": "Güç Devresi",
      "type": "schematic",
      "format": "a3",
      "orientation": "landscape",
      "width": 420,
      "height": 297,
      "titleBlock": { "pageTitle": "Ana güç" },
      "objects": [
        {
          "kind": "device",
          "id": "3f1c…",
          "symbolId": "iec.contactor.3p",
          "x": 100, "y": 60, "rotation": 0, "scale": 1,
          "reference": "-K1",
          "connectionPoints": [ { "id": "1", "x": -10, "y": -10, "direction": "up" } ]
        },
        { "kind": "junction", "id": "…", "x": 50, "y": 40, "shape": "tee", "rotation": 270, "size": 1.2, "visible": true },
        { "kind": "line", "id": "…", "x1": 0, "y1": 0, "x2": 10, "y2": 0, "thickness": 0.35 },
        { "kind": "rectangle", "id": "…", "x": 10, "y": 10, "width": 30, "height": 20, "thickness": 0.35 },
        { "kind": "text", "id": "…", "x": 5, "y": 5, "text": "Metin", "height": 3.5, "rotation": 0 },
        { "kind": "symbol", "id": "…", "symbolId": "iec.lamp", "x": 0, "y": 0, "rotation": 0, "scale": 1 },
        {
          "kind": "image", "id": "…", "x": 30, "y": 20, "width": 60, "height": 25,
          "sourceResourceId": "res-3fa1c0d2e4b59a71",
          "renderResourceId": "res-81b0e6c3d2a4f5e9",
          "transparencyEnabled": true, "transparentColor": 4294967295, "tolerance": 12
        }
      ]
    }
  ],
  "resources": [
    { "id": "res-3fa1c0d2e4b59a71", "fileName": "logo.jpg", "data": "/9j/4AAQSkZJRgABAQ…" },
    { "id": "res-81b0e6c3d2a4f5e9", "fileName": "logo-saydam.png", "data": "iVBORw0KGgoAAAANSU…" }
  ]
}
```

## Kurallar

- `formatVersion` **zorunludur**. Yoksa, 1'den küçükse veya programın desteklediğinden büyükse dosya açılmaz ve
  kullanıcıya anlaşılır mesaj gösterilir.
- Tüm ölçüler mm, açılar derece (saat yönü; 0/90/180/270).
- `objects[].kind` nesne türünü belirler: `line`, `rectangle`, `text`, `symbol`, `device`, `junction`.
- Cihazın `connectionPoints` değerleri **sembolün lokal koordinatlarındadır**; global konum ve yön her seferinde
  `x`, `y`, `rotation`, `scale` ile hesaplanır.
- **Kablolar dosyada saklanmaz.** Açılışta cihaz ve junction konumlarından yeniden üretilir.
- `objects[].kind = "image"`: resim. **Resim verisi dosyaya gömülür** (`resources[].data`, base64); kaynak dosya
  silinse veya taşınsa da proje bozulmaz. Kaynak kimliği içerikten (SHA-256) türetilir, aynı resim bir kez saklanır.
- Saydamlık tahribatsızdır: `sourceResourceId` orijinal, `renderResourceId` saydamlığı uygulanmış PNG'dir
  (saydamlık kapalıysa ikisi aynıdır). `transparentColor` ARGB tamsayı, `tolerance` 0–100.
- Kayıtta yalnızca nesnelerin kullandığı kaynaklar yazılır; silinmiş resimlerin verisi dosyada kalmaz.
- `titleBlock` sayfaya özel antet değerleridir; boş alanlar proje değerini kullanır.
- Hesaplanan (salt okunur) özellikler yazılmaz; okurken alan adları büyük/küçük harf duyarsızdır.
- **Tanınmayan alan içeren dosya açılmaz.** Eski bir program sürümü yeni alanları sessizce atıp dosyayı
  geri kaydederse veri kaybolurdu; bunun yerine hangi alanın tanınmadığını söyleyen bir hata verilir.
- Programın desteklediğinden **yeni** bir `formatVersion` taşıyan dosya da açılmaz ("programı güncelleyin").
- Kayıt önce geçici dosyaya yazılır, sonra asıl dosyanın yerine taşınır (yarım kayıt eski dosyayı bozmaz).

## Sürüm geçmişi

| Sürüm | Değişiklik |
|---|---|
| 1 | İlk format. Junction'lar her zaman 4 yönlü. |
| 2 | Junction'a `shape` (`angle`/`tee`/`cross`) ve `rotation` eklendi. v1 dosyalardaki junction'lar açılırken `cross` olarak yükseltilir (eski davranış korunur). |
| 3 | `resources` (gömülü dosyalar) ve `image` nesnesi eklendi. v2 dosyalar değişiklik gerektirmeden açılır. |

> **Bilinen hata (37afe6e):** Bu commit'teki derleme, sürüm sabiti 2'de kaldığı için resim içeren dosyaları
> `"formatVersion": 2` olarak yazdı. Bu dosyalar güncel sürümde sorunsuz açılır ve bir sonraki kayıtta 3 olur.
> Regresyon testleri (`FormatCompatibilityTests`) sürüm numarasını sabite değil sabit değere (3) göre kontrol eder.

## Sürüm yükseltme

Format değiştiğinde `ProjectDocument.CurrentFormatVersion` artırılır ve eski sürümler için dönüşüm adımı
`ProjectSerializer.Deserialize` içinde, nesneye çevirmeden önce JSON ağacı üzerinde uygulanır.
