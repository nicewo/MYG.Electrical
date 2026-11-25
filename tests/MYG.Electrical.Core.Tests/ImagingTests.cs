using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Imaging;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class ImagingTests
{
    /// <summary>BGRA32 piksel dizisi oluşturur (her renk ARGB).</summary>
    private static byte[] Pixels(params uint[] argb) => argb.SelectMany(c => new[]
    {
        (byte)c, (byte)(c >> 8), (byte)(c >> 16), (byte)(c >> 24),
    }).ToArray();

    private static bool IsTransparent(byte[] bgra, int index) => bgra[index * 4 + 3] == 0;

    [Fact]
    public void Zero_tolerance_removes_only_exact_color()
    {
        var px = Pixels(0xFFFFFFFF, 0xFFFEFEFE, 0xFF000000);

        var removed = ColorKey.Apply(px, 0xFFFFFFFF, 0);

        Assert.Equal(1, removed);
        Assert.True(IsTransparent(px, 0));
        Assert.False(IsTransparent(px, 1));
        Assert.False(IsTransparent(px, 2));
    }

    [Fact]
    public void Tolerance_removes_near_colors_proportionally()
    {
        // Beyaza yakın gri tonlar: 250 (çok yakın), 200 (orta), 0 (siyah)
        var px = Pixels(0xFFFAFAFA, 0xFFC8C8C8, 0xFF000000);

        ColorKey.Apply(px, 0xFFFFFFFF, 5);

        Assert.True(IsTransparent(px, 0));   // uzaklık ≈ %2
        Assert.False(IsTransparent(px, 1));  // uzaklık ≈ %22
        Assert.False(IsTransparent(px, 2));

        var px2 = Pixels(0xFFFAFAFA, 0xFFC8C8C8, 0xFF000000);
        ColorKey.Apply(px2, 0xFFFFFFFF, 30);
        Assert.True(IsTransparent(px2, 1));
        Assert.False(IsTransparent(px2, 2));
    }

    [Fact]
    public void Full_tolerance_removes_everything()
    {
        var px = Pixels(0xFFFFFFFF, 0xFF123456, 0xFF000000);
        Assert.Equal(3, ColorKey.Apply(px, 0xFFFFFFFF, 100));
    }

    [Fact]
    public void Out_of_range_tolerance_is_clamped()
    {
        var px = Pixels(0xFFFFFFFF, 0xFF000000);
        Assert.Equal(1, ColorKey.Apply(px, 0xFFFFFFFF, -50));
        var px2 = Pixels(0xFFFFFFFF, 0xFF000000);
        Assert.Equal(2, ColorKey.Apply(px2, 0xFFFFFFFF, 500));
    }

    [Fact]
    public void Alpha_of_key_color_is_ignored()
    {
        var px = Pixels(0xFF00FF00);
        Assert.Equal(1, ColorKey.Apply(px, 0x0000FF00, 0)); // anahtarın alfası 0 olsa da RGB eşleşir
    }

    [Fact]
    public void Pixel_is_read_back_as_argb()
    {
        var px = Pixels(0xFF112233, 0x80445566, 0xFF778899, 0xFFAABBCC);
        Assert.Equal(0x80445566u, ColorKey.PixelAt(px, 2, 1, 0));
        Assert.Equal(0xFFAABBCCu, ColorKey.PixelAt(px, 2, 1, 1));
    }

    [Fact]
    public void Invalid_buffer_length_is_rejected() =>
        Assert.Throws<ArgumentException>(() => ColorKey.Apply(new byte[5], 0, 0));

    [Fact]
    public void Same_data_is_stored_once()
    {
        var doc = new ProjectDocument();
        var a = doc.AddResource([1, 2, 3], "a.png");
        var b = doc.AddResource([1, 2, 3], "kopya.png");
        var c = doc.AddResource([9], "c.png");

        Assert.Same(a, b);
        Assert.NotEqual(a.Id, c.Id);
        Assert.Equal(2, doc.Resources.Count);
        Assert.StartsWith("res-", a.Id);
    }

    [Fact]
    public void Only_referenced_resources_are_used()
    {
        var doc = ProjectDocument.CreateNew();
        var src = doc.AddResource([1], "src.png");
        var render = doc.AddResource([2], "saydam.png");
        doc.AddResource([3], "eski.png");
        doc.Pages[1].Objects.Add(new ImageObject { SourceResourceId = src.Id, RenderResourceId = render.Id });

        var used = doc.UsedResources();

        Assert.Equal(2, used.Count);
        Assert.Equal(3, doc.Resources.Count); // bellekteki liste değişmez (geri alma için)
    }

    [Fact]
    public void Image_is_selectable_movable_and_copy_keeps_resource()
    {
        var session = new EditorSession(ProjectDocument.CreateNew(), BuiltInSymbolLibrary.Instance);
        var img = new ImageObject { X = 10, Y = 10, Width = 40, Height = 20, SourceResourceId = "res-a", RenderResourceId = "res-b" };
        session.AddObject(img);

        Assert.Same(img, session.HitTest(new Point2D(30, 20), 0.5));
        session.MoveSelection(5, 5);
        Assert.Equal(15, img.X);

        session.CopySelection();
        session.Paste();
        var copy = Assert.IsType<ImageObject>(Assert.Single(session.SelectedObjects));
        Assert.NotEqual(img.Id, copy.Id);
        Assert.Equal("res-b", copy.RenderResourceId);
    }

    [Fact]
    public void Image_settings_change_is_single_undo_step()
    {
        var session = new EditorSession(ProjectDocument.CreateNew(), BuiltInSymbolLibrary.Instance);
        var img = new ImageObject { Width = 40, Height = 20, RenderResourceId = "res-a" };
        session.AddObject(img);

        session.SetProperty(img, "Resim ayarları",
            () => (img.Settings, img.Width, img.Height),
            v => (img.Settings, img.Width, img.Height) = v,
            (new ImageSettings("res-b", true, 0xFFFFFFFF, 25), 80.0, 40.0));
        Assert.True(img.TransparencyEnabled);
        Assert.Equal("res-b", img.RenderResourceId);
        Assert.Equal(80, img.Width);

        session.Undo();
        Assert.False(img.TransparencyEnabled);
        Assert.Equal("res-a", img.RenderResourceId);
        Assert.Equal(40, img.Width);
    }
}
