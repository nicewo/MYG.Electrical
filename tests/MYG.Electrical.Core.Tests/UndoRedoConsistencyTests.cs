using System.Text.Json;
using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

/// <summary>Tüm düzenleme işlemlerinin geri alınabilirliği ve geri/ileri almadan sonra verinin tutarlılığı.</summary>
public class UndoRedoConsistencyTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;

    private static EditorSession NewSession() => new(ProjectDocument.CreateNew("Test"), Lib);

    /// <summary>Projenin tam anlık görüntüsü (sayfa sırası, nesneler, tüm alanlar).</summary>
    private static string Snapshot(EditorSession s) => JsonSerializer.Serialize(s.Document);

    private static void AssertWiresConsistent(EditorSession s) =>
        Assert.Equal(
            new AutoWireEngine().Compute(s.ActivePage.Objects).Select(w => (w.From, w.To)).OrderBy(x => x.ToString()),
            s.Wires.Select(w => (w.From, w.To)).OrderBy(x => x.ToString()));

    [Fact]
    public void Page_add_remove_and_move_are_undoable()
    {
        var s = NewSession();
        var initial = Snapshot(s);
        var start = s.Document.Pages.Count;

        var p = s.AddPage(PageType.Schematic, "Kumanda");
        s.MovePage(p, -1);
        Assert.Equal(1, s.Document.Pages.IndexOf(p));
        Assert.True(s.RemovePage(p));
        Assert.Equal(start, s.Document.Pages.Count);

        s.Undo(); // silme geri alındı → sayfa geri geldi ve aktif
        Assert.Same(p, s.ActivePage);
        Assert.Equal(1, s.Document.Pages.IndexOf(p));
        s.Undo(); // taşıma
        Assert.Equal(2, s.Document.Pages.IndexOf(p));
        s.Undo(); // ekleme
        Assert.DoesNotContain(p, s.Document.Pages);
        Assert.Contains(s.ActivePage, s.Document.Pages);
        Assert.Equal(initial, Snapshot(s));

        s.Redo();
        s.Redo();
        s.Redo();
        Assert.DoesNotContain(p, s.Document.Pages);
    }

    [Fact]
    public void Removing_page_keeps_earlier_history()
    {
        var s = NewSession();
        var line = new LineObject { X2 = 10 };
        s.AddObject(line);
        var extra = s.AddPage(PageType.Schematic);
        s.RemovePage(extra);

        Assert.True(s.History.CanUndo);
        s.Undo(); // sayfa silme
        s.Undo(); // sayfa ekleme
        s.Undo(); // çizgi ekleme
        Assert.DoesNotContain(line, s.Document.Pages.SelectMany(p => p.Objects));
    }

    [Fact]
    public void Undo_switches_to_page_where_change_happened()
    {
        var s = NewSession();
        var power = s.ActivePage;
        var coil = Lib.Find(SymbolIds.Coil)!;
        var k1 = s.PlaceSymbol(coil, new Point2D(50, 50));
        var control = s.AddPage(PageType.Schematic, "Kumanda");
        Assert.Same(control, s.ActivePage);
        s.ActivePage = power;
        s.Select(k1);
        s.MoveSelection(10, 0);
        s.ActivePage = control;

        s.Undo(); // taşıma güç sayfasındaydı

        Assert.Same(power, s.ActivePage);
        Assert.Equal(50, k1.X);
    }

    [Fact]
    public void Page_rename_is_undoable_and_returns_to_page()
    {
        var s = NewSession();
        var page = s.ActivePage;
        var other = s.Document.Pages.First(p => p != page);
        s.SetDocumentProperty("Sayfa adı", () => page.Name, v => page.Name = v, "Ana güç", page);
        s.ActivePage = other;

        s.Undo();

        Assert.Equal("Güç Devresi", page.Name);
        Assert.Same(page, s.ActivePage);
    }

    [Fact]
    public void Junction_place_rotate_and_shape_change_are_undoable()
    {
        var s = NewSession();
        var j = s.PlaceJunction(JunctionShape.Angle, new Point2D(50, 50));
        s.Select(j);
        s.RotateSelection();
        s.SetProperty(j, "Şekil", () => j.Shape, v => j.Shape = v, JunctionShape.Tee);

        s.Undo();
        Assert.Equal(JunctionShape.Angle, j.Shape);
        s.Undo();
        Assert.Equal(0, j.Rotation);
        s.Undo();
        Assert.Empty(s.ActivePage.Objects);
    }

    [Fact]
    public void Image_add_edit_delete_are_undoable()
    {
        var s = NewSession();
        var img = new ImageObject { Width = 40, Height = 20, SourceResourceId = "a", RenderResourceId = "a" };
        s.AddObject(img);
        s.SetProperty(img, "Resim ayarları", () => (img.Settings, img.Width, img.Height),
            v => (img.Settings, img.Width, img.Height) = v, (new ImageSettings("b", true, 0xFFFFFFFF, 20), 80.0, 40.0));
        s.Select(img);
        s.DeleteSelection();

        s.Undo();
        Assert.Contains(img, s.ActivePage.Objects);
        s.Undo();
        Assert.Equal("a", img.RenderResourceId);
        Assert.Equal(40, img.Width);
        s.Undo();
        Assert.DoesNotContain(img, s.ActivePage.Objects);
    }

    /// <summary>
    /// Rastgele (ama tekrarlanabilir) işlem dizisi: her adımdan sonra kablolar tutarlı olmalı; tümü geri alınınca
    /// proje başlangıçla, tümü ileri alınınca son halle birebir aynı olmalı.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Random_edit_sequence_undoes_and_redoes_exactly(int seed)
    {
        var rnd = new Random(seed);
        var s = NewSession();
        var initial = Snapshot(s);
        var symbols = Lib.All;
        double G(int max) => rnd.Next(2, max) * GeometryConstants.DefaultGridSpacingMm;

        for (var i = 0; i < 150; i++)
        {
            var objects = s.ActivePage.Objects;
            switch (rnd.Next(12))
            {
                case 0 or 1:
                    s.PlaceSymbol(symbols[rnd.Next(symbols.Count)], new Point2D(G(70), G(50)), rnd.Next(4) * 90);
                    break;
                case 2:
                    s.PlaceJunction((JunctionShape)rnd.Next(3), new Point2D(G(70), G(50)), rnd.Next(4) * 90);
                    break;
                case 3:
                    s.AddObject(new LineObject { X1 = G(70), Y1 = G(50), X2 = G(70), Y2 = G(50) });
                    break;
                case 4 when objects.Count > 0:
                    s.Select(objects[rnd.Next(objects.Count)]);
                    s.MoveSelection(G(4) - 10, G(4) - 10);
                    break;
                case 5 when objects.Count > 0:
                    s.Select(objects[rnd.Next(objects.Count)]);
                    s.RotateSelection();
                    break;
                case 6 when objects.Count > 0:
                    s.Select(objects[rnd.Next(objects.Count)]);
                    s.DeleteSelection();
                    break;
                case 7 when objects.Count > 0:
                    s.Select(objects[rnd.Next(objects.Count)]);
                    s.CopySelection();
                    s.Paste();
                    break;
                case 8 when objects.OfType<DeviceObject>().FirstOrDefault() is { } d:
                    s.SetProperty(d, "Tanım", () => d.Reference, v => d.Reference = v, $"-X{i}");
                    break;
                case 9:
                    s.AddPage(PageType.Schematic);
                    break;
                case 10 when s.Document.Pages.Count > 2:
                    s.RemovePage(s.Document.Pages[rnd.Next(s.Document.Pages.Count)]);
                    break;
                case 11:
                    s.ActivePage = s.Document.Pages[rnd.Next(s.Document.Pages.Count)];
                    break;
            }
            AssertWiresConsistent(s);
            Assert.Contains(s.ActivePage, s.Document.Pages);
        }
        var final = Snapshot(s);

        while (s.History.CanUndo)
        {
            s.Undo();
            AssertWiresConsistent(s);
            Assert.Contains(s.ActivePage, s.Document.Pages);
        }
        Assert.Equal(initial, Snapshot(s));

        while (s.History.CanRedo)
        {
            s.Redo();
            AssertWiresConsistent(s);
        }
        Assert.Equal(final, Snapshot(s));
    }
}
