# Mimari

## Katmanlar

```
App (WPF, MVVM) ──► Drawing (WPF renderer) ──► Core
      │                                        ▲
      ├──► Infrastructure (JSON, PDF, log) ────┤
      └──► Symbols (IEC kütüphanesi) ──────────┘
```

| Proje | Sorumluluk | WPF bağımlılığı |
|---|---|---|
| **Core** | Model (`DrawingObject` ve türevleri, `Page`, `ProjectDocument`), geometri (`Point2D`, `Rect2D`, `CoordinateTransform`, `ViewTransform`), `AutoWireEngine`, `EditorSession` (seçim, geri al, kablo önbelleği), `SceneComposer` / `PageFrameComposer` | Yok |
| **Symbols** | `BuiltInSymbolLibrary` (`ISymbolCatalog`), vektörel IEC sembol tanımları | Yok |
| **Infrastructure** | `ProjectSerializer`, `ProjectFileService`, `PdfExporter` (PDFsharp), `LogSetup` (Serilog) | Yok |
| **Drawing** | `DrawingCanvas` (DrawingVisual), `WpfSceneSink`, `WpfConvert` | Var |
| **App** | Pencere, paneller, ViewModel'ler, diyaloglar, tema | Var |

## Temel kararlar

- **Ölçü birimi mm.** Tüm model koordinatları milimetredir; Y ekseni aşağı doğru artar (sayfa sol üst = 0,0).
- **Ekran koordinatı yalnızca render aşamasında.** `ViewTransform` (Core) dünya↔ekran dönüşümünü yapar;
  `DrawingCanvas` bunu dünya katmanına tek bir `MatrixTransform` olarak uygular. Seçim/önizleme katmanı ekran
  koordinatında çizilir ki çizgi kalınlığı zoomdan bağımsız 1 px kalsın.
- **Koordinat dönüşümleri tek yerde.** `CoordinateTransform`: lokal→global, 90° adımlı dönüş, pin yönü dönüşü, snap.
- **Hedeften bağımsız sahne kompozisyonu.** `SceneComposer` nesneleri temel şekillere (çizgi, dikdörtgen, elips,
  yay, metin, resim) çevirir ve `ISceneSink`'e gönderir. `WpfSceneSink` ekrana, `PdfSceneSink` PDF'e çizer.
  Böylece PDF ekran görüntüsü değildir ama ekranla birebir aynıdır.
- **Nesne bazlı yeniden çizim.** Her nesnenin kendi `DrawingVisual`'ı vardır. `EditorSession.SceneChanged`
  değişen nesne kimliklerini bildirir; yalnızca onlar yeniden çizilir. Kablolar ayrı bir görselde tutulur.
- **Resimler projeye gömülür.** `ProjectDocument.Resources` içerik-adresli (SHA-256) kaynakları tutar. Saydamlık
  `Core.Imaging.ColorKey` ile platformdan bağımsız uygulanır; çözme/kodlama `IImageCodec` arkasındadır
  (`WpfImageCodec`). PDF işlenmiş PNG'yi alfa maskesiyle gömer.
- **Kablolar kalıcı nesne değildir.** `AutoWireEngine` cihaz/junction konumlarından her değişiklikte yeniden
  üretir (bkz. [AutoWiring.md](AutoWiring.md)).
- **Komut tabanlı geri alma.** Her düzenleme `IEditCommand`'dır. Sürükleme sırasında `PreviewMove` canlı
  önizleme yapar, bırakınca tek bir `MoveObjectsCommand` geçmişe yazılır.

## Veri akışı (cihaz taşıma)

1. `DrawingCanvas` fare hareketini dünya koordinatına çevirir, grid'e yuvarlar.
2. `EditorSession.PreviewMove` nesneleri taşır → `SceneChanged(ids)` → yalnızca o görseller yeniden çizilir.
3. Taşınan nesne kablolamayı etkiliyorsa yalnızca **aktif sayfa** için `AutoWireEngine.Compute` çalışır → `WiresChanged`.
4. Fare bırakılınca `CommitMove` toplam vektörü tek geri alma adımı olarak kaydeder.

## Bağımlılıklar

Yalnızca: PDFsharp 6.2.x (MIT), Serilog + Serilog.Sinks.File, xunit.v3 (test). MVVM için harici kütüphane
kullanılmaz (`ObservableObject`, `RelayCommand` projede).
