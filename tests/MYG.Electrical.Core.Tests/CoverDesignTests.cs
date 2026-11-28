using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

/// <summary>Tasarlanabilir kapak: bilgi alanları, şablon, boyutlandırma.</summary>
public class CoverDesignTests
{
    private static EditorSession NewSession(CoverTemplate? cover = null) =>
        new(ProjectDocument.CreateNew("Test", cover), BuiltInSymbolLibrary.Instance);

    [Fact]
    public void New_project_cover_is_made_of_editable_objects_filled_from_project_info()
    {
        var doc = ProjectDocument.CreateNew("Pompa");
        doc.Project.Customer = "ACME";
        var cover = doc.Pages[0];
        var fields = cover.Objects.OfType<FieldObject>().ToList();

        Assert.Contains(fields, f => f.Field == TitleBlockFields.Company);
        Assert.Contains(fields, f => f.Field == TitleBlockFields.Logo);
        var customer = Assert.Single(fields, f => f.Field == TitleBlockFields.Customer);
        Assert.Contains(cover.Objects, o => o is TextObject { Text: "ELEKTRİK PROJESİ" });

        var texts = Render(doc, cover, SceneComposerOptions.Print);
        Assert.Contains("ACME", texts);
        Assert.Contains("Pompa", texts);          // başlık: makine adı
        Assert.Contains(doc.Project.Company, texts);
        Assert.Equal("ACME", customer.RenderedText);
    }

    [Fact]
    public void Hidden_field_is_left_out_of_print_but_shown_faint_in_editor()
    {
        var doc = ProjectDocument.CreateNew("Pompa");
        doc.Project.Customer = "ACME";
        var customer = doc.Pages[0].Objects.OfType<FieldObject>().Single(f => f.Field == TitleBlockFields.Customer);
        customer.Visible = false;

        Assert.DoesNotContain("ACME", Render(doc, doc.Pages[0], SceneComposerOptions.Print));
        Assert.Contains("ACME", Render(doc, doc.Pages[0], SceneComposerOptions.Editor));
    }

    [Fact]
    public void Field_label_and_empty_value_placeholder()
    {
        var doc = ProjectDocument.CreateNew("X");
        var page = doc.Pages[1];
        page.Objects.Add(new FieldObject { Field = TitleBlockFields.ProjectNumber, Label = "Proje No", ShowLabel = true });
        doc.Project.ProjectNumber = "002";
        Assert.Contains("Proje No: 002", Render(doc, page, SceneComposerOptions.Print));

        doc.Project.ProjectNumber = string.Empty;
        Assert.Contains("Proje No: [boş]", Render(doc, page, SceneComposerOptions.Editor)); // editörde boş alan görünür
        Assert.DoesNotContain("Proje No: [boş]", Render(doc, page, SceneComposerOptions.Print));
    }

    [Fact]
    public void Captured_template_starts_new_project_with_its_layout_and_the_new_projects_data()
    {
        var a = ProjectDocument.CreateNew("Eski");
        var cover = a.Pages[0];
        var company = cover.Objects.OfType<FieldObject>().Single(f => f.Field == TitleBlockFields.Company);
        company.X = 50;
        company.Height = 9;
        cover.Objects.RemoveAll(o => o is FieldObject { Field: TitleBlockFields.Revision });
        var template = CoverTemplate.Capture(a, cover, "Firma kapağı");

        var b = ProjectDocument.CreateNew("Yeni", template);
        b.Project.Company = "Data Otomasyon";
        var newCompany = b.Pages[0].Objects.OfType<FieldObject>().Single(f => f.Field == TitleBlockFields.Company);
        Assert.Equal(50, newCompany.X);
        Assert.Equal(9, newCompany.Height);
        Assert.NotEqual(company.Id, newCompany.Id);
        Assert.DoesNotContain(b.Pages[0].Objects, o => o is FieldObject { Field: TitleBlockFields.Revision });
        Assert.Contains("Data Otomasyon", Render(b, b.Pages[0], SceneComposerOptions.Print));
    }

    [Fact]
    public void Template_carries_its_images()
    {
        var a = ProjectDocument.CreateNew("A");
        var res = a.AddResource([1, 2, 3, 4], "logo.png");
        a.Pages[0].Objects.Add(new ImageObject { X = 30, Y = 30, Width = 40, Height = 20, SourceResourceId = res.Id, RenderResourceId = res.Id });
        var template = CoverTemplate.Capture(a, a.Pages[0], "Logolu");
        Assert.Single(template.Resources);

        var b = ProjectDocument.CreateNew("B", template);
        Assert.NotNull(b.FindResource(res.Id));
    }

    [Fact]
    public void Applying_a_template_replaces_cover_and_is_one_undo_step()
    {
        var s = NewSession();
        var cover = s.Document.Pages[0];
        var before = cover.Objects.Select(o => o.Id).ToList();
        var template = new CoverTemplate { Name = "Sade", Objects = [new FieldObject { X = 100, Y = 100, Field = TitleBlockFields.Title, Height = 15 }] };

        s.ApplyCoverTemplate(template, cover);
        Assert.Single(cover.Objects);
        Assert.Same(cover, s.ActivePage);

        s.Undo();
        Assert.Equal(before, cover.Objects.Select(o => o.Id).ToList());
        s.Redo();
        Assert.Single(cover.Objects);
    }

    [Fact]
    public void Resizing_text_scales_height_and_keeps_top_left_corner()
    {
        var s = NewSession();
        var field = new FieldObject { X = 100, Y = 50, Field = TitleBlockFields.Company, Height = 4 };
        s.AddObject(field);
        var bounds = ObjectBounds.Get(field, s.Catalog);
        var before = ObjectResize.Capture(field);

        s.PreviewResize(field, before, bounds, new Point2D(bounds.Right + 50, bounds.Top + bounds.Height * 2));
        Assert.Equal(8, field.Height, 6);
        Assert.Equal(bounds.Top, field.Y - field.Height / 2, 6);
        s.CommitResize(field, before);

        s.Undo();
        Assert.Equal(4, field.Height);
        s.Redo();
        Assert.Equal(8, field.Height, 6);
    }

    [Fact]
    public void Resizing_image_keeps_aspect_ratio()
    {
        var image = new ImageObject { X = 10, Y = 10, Width = 40, Height = 20 };
        ObjectResize.Apply(image, ObjectResize.Capture(image), image.Bounds, new Point2D(90, 25));
        Assert.Equal(80, image.Width, 6);
        Assert.Equal(40, image.Height, 6);
        Assert.False(ObjectResize.CanResize(new LineObject()));
    }

    /// <summary>Sayfanın çıktı metinleri.</summary>
    private static List<string> Render(ProjectDocument doc, Page page, SceneComposerOptions options)
    {
        var sink = new TextSink();
        new SceneComposer(BuiltInSymbolLibrary.Instance, doc.FindResource) { Document = doc }.ComposeObjects(page.Objects, sink, options);
        return sink.Texts;
    }

    private sealed class TextSink : ISceneSink
    {
        public List<string> Texts { get; } = [];
        public void DrawText(string text, Point2D position, double height, uint color, TextAlign align = TextAlign.Left, bool bold = false) => Texts.Add(text);
        public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) { }
        public void DrawRectangle(Rect2D rect, StrokeStyle? stroke, uint? fill = null) { }
        public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle? stroke, uint? fill = null) { }
        public void DrawArc(Point2D center, double radius, double startAngle, double sweepAngle, StrokeStyle stroke) { }
        public void DrawPolygon(IReadOnlyList<Point2D> points, bool closed, StrokeStyle? stroke, uint? fill = null) { }
        public void DrawImage(string path, Rect2D rect) { }
        public bool DrawImageData(string id, byte[] data, Rect2D rect) => true;
    }
}
