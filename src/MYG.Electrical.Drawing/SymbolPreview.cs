using System.Windows.Media;
using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Drawing;

/// <summary>Kütüphane önizlemesi: sembolü vektörel bir resim olarak üretir (şemadaki çizimle aynı kompozisyon).</summary>
public static class SymbolPreview
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.Ordinal);

    public static ImageSource? Create(SymbolDefinition? symbol, ISymbolCatalog catalog)
    {
        if (symbol is null)
        {
            return null;
        }
        if (Cache.TryGetValue(symbol.Id, out var cached))
        {
            return cached;
        }
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Pin adları görünsün diye çerçeve: önizleme bütün sembolü kenar boşluğuyla gösterir
            var b = symbol.LocalBounds.Inflate(4);
            dc.DrawRectangle(Brushes.Transparent, null, WpfConvert.ToRect(b));
            var device = symbol.CreateDevice(0, 0, string.Empty);
            var composer = new SceneComposer(catalog);
            composer.ComposeObject(device, new WpfSceneSink(dc), new SceneComposerOptions { ShowConnectionPoints = false, ShowPinLabels = true });
        }
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        Cache[symbol.Id] = image;
        return image;
    }
}
