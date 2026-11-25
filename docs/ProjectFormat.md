# .myge Proje Formatı

UTF-8 JSON, camelCase alan adları, enum değerleri metin. Uzantı: `.myge`.

```json
{
  "formatVersion": 2,
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
        { "kind": "symbol", "id": "…", "symbolId": "iec.lamp", "x": 0, "y": 0, "rotation": 0, "scale": 1 }
      ]
    }
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
- `titleBlock` sayfaya özel antet değerleridir; boş alanlar proje değerini kullanır.
- Hesaplanan (salt okunur) özellikler yazılmaz; okurken alan adları büyük/küçük harf duyarsızdır.
- Kayıt önce geçici dosyaya yazılır, sonra asıl dosyanın yerine taşınır (yarım kayıt eski dosyayı bozmaz).

## Sürüm geçmişi

| Sürüm | Değişiklik |
|---|---|
| 1 | İlk format. Junction'lar her zaman 4 yönlü. |
| 2 | Junction'a `shape` (`angle`/`tee`/`cross`) ve `rotation` eklendi. v1 dosyalardaki junction'lar açılırken `cross` olarak yükseltilir (eski davranış korunur). |

## Sürüm yükseltme

Format değiştiğinde `ProjectDocument.CurrentFormatVersion` artırılır ve eski sürümler için dönüşüm adımı
`ProjectSerializer.Deserialize` içinde, nesneye çevirmeden önce JSON ağacı üzerinde uygulanır.
