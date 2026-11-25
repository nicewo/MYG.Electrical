using System.Windows.Media;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Drawing;

namespace MYG.Electrical.Drawing.Tests;

public class WpfConvertTests
{
    [Fact]
    public void Argb_is_converted_to_wpf_color()
    {
        var c = WpfConvert.ToColor(0x80FF4020);
        Assert.Equal(Color.FromArgb(0x80, 0xFF, 0x40, 0x20), c);
    }

    [Fact]
    public void View_matrix_matches_view_transform()
    {
        var view = new ViewTransform();
        view.Pan(15, 25);
        view.ZoomAt(new Point2D(100, 80), 1.7);
        var world = new Point2D(42, 17);

        var expected = view.WorldToScreen(world);
        var actual = WpfConvert.ToMatrix(view).Transform(WpfConvert.ToPoint(world));

        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }

    [Fact]
    public void Pens_are_cached_and_frozen() => Sta.Run(() =>
    {
        var a = WpfSceneSink.GetPen(DrawingStyles.Wire);
        var b = WpfSceneSink.GetPen(DrawingStyles.Wire);
        Assert.Same(a, b);
        Assert.True(a.IsFrozen);
        Assert.Equal(DrawingStyles.WireLine, a.Thickness);
    });

    [Fact]
    public void Scene_can_be_rendered_into_drawing_group() => Sta.Run(() =>
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            var sink = new WpfSceneSink(dc);
            var doc = Core.Pages.ProjectDocument.CreateNew();
            PageFrameComposer.Compose(doc.Project, doc.Pages[1], 2, 2, sink);
            var composer = new SceneComposer(Symbols.BuiltInSymbolLibrary.Instance);
            foreach (var def in Symbols.BuiltInSymbolLibrary.Instance.All)
            {
                composer.ComposeObject(def.CreateDevice(100, 100, "-X1"), sink, SceneComposerOptions.Editor);
            }
        }
        Assert.True(group.Bounds.Width > 300); // çerçeve A3 genişliğine yakın
    });
}
