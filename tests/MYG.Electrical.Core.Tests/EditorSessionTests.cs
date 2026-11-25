using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class EditorSessionTests
{
    private static EditorSession NewSession() => new(ProjectDocument.CreateNew("Test"), BuiltInSymbolLibrary.Instance);


    [Fact]
    public void New_session_starts_on_first_schematic_page()
    {
        var s = NewSession();
        Assert.Equal(PageType.Schematic, s.ActivePage.Type);
    }

    [Fact]
    public void Placing_symbols_assigns_incrementing_references()
    {
        var s = NewSession();
        var coil = BuiltInSymbolLibrary.Instance.Find(SymbolIds.Coil)!;

        var k1 = s.PlaceSymbol(coil, new Point2D(50, 50));
        var k2 = s.PlaceSymbol(coil, new Point2D(100, 50));

        Assert.Equal("-K1", k1.Reference);
        Assert.Equal("-K2", k2.Reference);
        Assert.Equal(2, k1.ConnectionPoints.Count);
    }

    [Fact]
    public void Placing_aligned_devices_creates_wire_and_undo_removes_it()
    {
        var s = NewSession();
        var coil = BuiltInSymbolLibrary.Instance.Find(SymbolIds.Coil)!;
        s.PlaceSymbol(coil, new Point2D(50, 50));
        s.PlaceSymbol(coil, new Point2D(50, 100));
        Assert.Single(s.Wires);

        s.Undo();
        Assert.Empty(s.Wires);
        Assert.Single(s.ActivePage.Objects);

        s.Redo();
        Assert.Single(s.Wires);
    }

    [Fact]
    public void Move_and_rotate_are_undoable_and_rewire()
    {
        var s = NewSession();
        var coil = BuiltInSymbolLibrary.Instance.Find(SymbolIds.Coil)!;
        s.PlaceSymbol(coil, new Point2D(50, 50));
        var lower = s.PlaceSymbol(coil, new Point2D(50, 100));
        Assert.Single(s.Wires);

        s.Select(lower);
        s.MoveSelection(5, 0);
        Assert.Empty(s.Wires);
        s.Undo();
        Assert.Single(s.Wires);

        s.Select(lower);
        s.RotateSelection();
        Assert.Equal(90, lower.Rotation);
        Assert.Empty(s.Wires);
        s.Undo();
        Assert.Equal(0, lower.Rotation);
        Assert.Single(s.Wires);
    }

    [Fact]
    public void Drag_preview_commits_single_undo_step()
    {
        var s = NewSession();
        var line = new LineObject { X1 = 0, Y1 = 0, X2 = 10, Y2 = 0 };
        s.AddObject(line);
        s.History.Clear();

        s.PreviewMove([line], 5, 0);
        s.PreviewMove([line], 5, 5);
        s.CommitMove([line], 10, 5);
        Assert.Equal(10, line.X1);

        s.Undo();
        Assert.Equal(0, line.X1);
        Assert.Equal(0, line.Y1);
        Assert.False(s.History.CanUndo);
    }

    [Fact]
    public void Delete_selection_removes_device_and_its_wire()
    {
        var s = NewSession();
        var coil = BuiltInSymbolLibrary.Instance.Find(SymbolIds.Coil)!;
        s.PlaceSymbol(coil, new Point2D(50, 50));
        var lower = s.PlaceSymbol(coil, new Point2D(50, 100));

        s.Select(lower);
        s.DeleteSelection();

        Assert.Empty(s.Wires);
        Assert.Single(s.ActivePage.Objects);
        s.Undo();
        Assert.Single(s.Wires);
    }

    [Fact]
    public void Copy_paste_creates_new_ids_and_references()
    {
        var s = NewSession();
        var coil = BuiltInSymbolLibrary.Instance.Find(SymbolIds.Coil)!;
        var k1 = s.PlaceSymbol(coil, new Point2D(50, 50));

        s.Select(k1);
        s.CopySelection();
        s.Paste();

        var pasted = Assert.IsType<DeviceObject>(Assert.Single(s.SelectedObjects));
        Assert.NotEqual(k1.Id, pasted.Id);
        Assert.Equal("-K2", pasted.Reference);
        Assert.Equal(2, s.ActivePage.Objects.Count);
    }

    [Fact]
    public void Set_property_is_undoable()
    {
        var s = NewSession();
        var t = new TextObject { Text = "A" };
        s.AddObject(t);

        s.SetProperty(t, "Metin", () => t.Text, v => t.Text = v, "B");
        Assert.Equal("B", t.Text);
        s.Undo();
        Assert.Equal("A", t.Text);
    }

    [Fact]
    public void Hit_test_finds_topmost_object_and_rect_selection()
    {
        var s = NewSession();
        var line = new LineObject { X1 = 0, Y1 = 0, X2 = 100, Y2 = 0 };
        var rect = new RectangleObject { X = 40, Y = -10, Width = 20, Height = 20 };
        s.AddObject(line);
        s.AddObject(rect);

        Assert.Same(rect, s.HitTest(new Point2D(40, 0), 1));
        Assert.Same(line, s.HitTest(new Point2D(10, 0.3), 1));
        Assert.Null(s.HitTest(new Point2D(50, 50), 1));

        var inside = s.ObjectsInside(new Rect2D(30, -20, 40, 40)).ToList();
        Assert.Equal([rect], inside);
    }

    [Fact]
    public void Undo_manager_tracks_state()
    {
        var m = new UndoRedoManager(capacity: 2);
        var value = 0;
        for (var i = 1; i <= 3; i++)
        {
            var v = i;
            var old = value;
            m.Execute(new SetPropertyCommand<int>("set", x => value = x, old, v));
        }
        Assert.Equal(3, value);
        m.Undo();
        m.Undo();
        Assert.False(m.CanUndo); // kapasite 2
        Assert.Equal(1, value);
        m.Redo();
        Assert.Equal(2, value);
    }

    [Fact]
    public void Pages_can_be_added_moved_and_removed()
    {
        var s = NewSession();
        var start = s.Document.Pages.Count;

        var added = s.AddPage(PageType.Schematic, "Kumanda");
        Assert.Same(added, s.ActivePage);
        Assert.Equal(start + 1, s.Document.Pages.Count);

        s.MovePage(added, -1);
        Assert.Equal(1, s.Document.Pages.IndexOf(added));

        Assert.True(s.RemovePage(added));
        Assert.Equal(start, s.Document.Pages.Count);
        Assert.NotSame(added, s.ActivePage);
    }

    [Fact]
    public void Last_page_cannot_be_removed()
    {
        var doc = new ProjectDocument();
        var s = new EditorSession(doc, BuiltInSymbolLibrary.Instance);
        Assert.False(s.RemovePage(s.ActivePage));
        Assert.Single(doc.Pages);
    }

    [Fact]
    public void Document_property_change_is_undoable()
    {
        var s = NewSession();
        s.SetDocumentProperty("Firma", () => s.Document.Project.Company, v => s.Document.Project.Company = v, "ACME");
        Assert.Equal("ACME", s.Document.Project.Company);
        s.Undo();
        Assert.Equal("MYG TECH", s.Document.Project.Company);
    }
}
