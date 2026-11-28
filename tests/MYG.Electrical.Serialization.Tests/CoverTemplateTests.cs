using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Library;
using MYG.Electrical.Infrastructure.Serialization;

namespace MYG.Electrical.Serialization.Tests;

public sealed class CoverTemplateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "myg-cover-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Old_project_with_automatic_cover_gets_the_standard_template()
    {
        var doc = ProjectDocument.CreateNew("Eski");
        doc.Pages[0].Objects.Clear(); // v8 ve öncesinde kapak boştu, otomatik çiziliyordu
        var json = ProjectSerializer.Serialize(doc).Replace($"\"formatVersion\": {ProjectDocument.CurrentFormatVersion}", "\"formatVersion\": 8");

        var loaded = ProjectSerializer.Deserialize(json);
        var fields = loaded.Pages[0].Objects.OfType<FieldObject>().Select(f => f.Field).ToList();
        Assert.Contains(TitleBlockFields.Company, fields);
        Assert.Contains(TitleBlockFields.Customer, fields);
    }

    [Fact]
    public void Current_format_keeps_an_intentionally_empty_cover()
    {
        var doc = ProjectDocument.CreateNew("Boş kapak");
        doc.Pages[0].Objects.Clear();
        Assert.Empty(ProjectSerializer.Deserialize(ProjectSerializer.Serialize(doc)).Pages[0].Objects);
    }

    [Fact]
    public void Template_file_round_trips_with_fields_texts_and_images()
    {
        var doc = ProjectDocument.CreateNew("A");
        var res = doc.AddResource([9, 8, 7], "logo.png");
        doc.Pages[0].Objects.Add(new ImageObject { X = 1, Y = 2, Width = 3, Height = 4, SourceResourceId = res.Id, RenderResourceId = res.Id });
        var template = CoverTemplate.Capture(doc, doc.Pages[0], "Firma");

        var json = ProjectSerializer.SerializeCoverTemplate(template);
        Assert.Contains($"\"formatVersion\": {ProjectDocument.CurrentFormatVersion}", json);
        var back = ProjectSerializer.DeserializeCoverTemplate(json);
        Assert.Equal(template.Objects.Count, back.Objects.Count);
        Assert.Equal([9, 8, 7], Assert.Single(back.Resources).Data);
        Assert.Contains(back.Objects, o => o is TextObject { Text: "ELEKTRİK PROJESİ", Align: Core.Rendering.TextAlign.Center });
        Assert.Equal(json, ProjectSerializer.SerializeCoverTemplate(back));
    }

    [Fact]
    public void Template_from_newer_version_is_refused()
    {
        var json = ProjectSerializer.SerializeCoverTemplate(CoverTemplate.Standard())
            .Replace($"\"formatVersion\": {ProjectDocument.CurrentFormatVersion}", $"\"formatVersion\": {ProjectDocument.CurrentFormatVersion + 1}");
        Assert.Throws<ProjectFormatException>(() => ProjectSerializer.DeserializeCoverTemplate(json));
    }

    [Fact]
    public void Store_lists_standard_first_and_saves_loads_deletes_user_templates()
    {
        var store = new CoverTemplateStore(_root);
        Assert.Equal([CoverTemplate.StandardName], store.Names());

        var template = CoverTemplate.Standard();
        template.Name = "Data Otomasyon";
        store.Save(template);
        template.Name = "Ağır Sanayi";
        store.Save(template);
        Assert.Equal([CoverTemplate.StandardName, "Ağır Sanayi", "Data Otomasyon"], store.Names());

        var (loaded, error) = store.Load("Data Otomasyon");
        Assert.Null(error);
        Assert.Equal("Data Otomasyon", loaded.Name);
        Assert.Equal(template.Objects.Count, loaded.Objects.Count);

        store.Delete("Data Otomasyon");
        var (fallback, missing) = store.Load("Data Otomasyon");
        Assert.Equal(CoverTemplate.StandardName, fallback.Name);
        Assert.NotNull(missing);
    }

    [Theory]
    [InlineData("")]
    [InlineData("standart")]
    [InlineData("a/b")]
    [InlineData("kapak?")]
    public void Invalid_template_names_are_refused(string name) =>
        Assert.Throws<ArgumentException>(() => CoverTemplateStore.ValidateName(name));

    [Fact]
    public void Broken_template_file_falls_back_to_standard_with_message()
    {
        var store = new CoverTemplateStore(_root);
        Directory.CreateDirectory(store.Folder);
        File.WriteAllText(Path.Combine(store.Folder, "Bozuk" + CoverTemplateStore.Extension), "{ yarım");
        var (t, error) = store.Load("Bozuk");
        Assert.Equal(CoverTemplate.StandardName, t.Name);
        Assert.Contains("okunamadı", error, StringComparison.Ordinal);
    }
}
