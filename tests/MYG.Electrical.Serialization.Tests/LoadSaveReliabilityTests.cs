using System.Diagnostics;
using System.Text.Json.Serialization;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Pdf;
using MYG.Electrical.Infrastructure.Serialization;
using MYG.Electrical.Symbols;
using Serilog;

namespace MYG.Electrical.Serialization.Tests;

public class LoadSaveReliabilityTests
{
    private static readonly ILogger NoLog = new LoggerConfiguration().CreateLogger();

    /// <summary>Her nesne türünden, varsayılan olmayan değerlerle bir örnek.</summary>
    private static readonly Dictionary<string, Func<DrawingObject>> Samples = new()
    {
        ["line"] = () => new LineObject { X1 = 1.5, Y1 = 2, X2 = 30, Y2 = 2, Thickness = 0.7 },
        ["rectangle"] = () => new RectangleObject { X = 3, Y = 4, Width = 50, Height = 20, Thickness = 0.5, Dashed = true },
        ["text"] = () => new TextObject { X = 7, Y = 8, Text = "Çağrı ğüşıöç", Height = 5, Rotation = 90 },
        ["symbol"] = () => new SymbolObject { SymbolId = SymbolIds.Lamp, X = 9, Y = 10, Rotation = 180, Scale = 1.5 },
        ["device"] = () => BuiltInSymbolLibrary.Instance.Find(SymbolIds.Contactor)!.CreateDevice(100, 60, "-K7"),
        ["junction"] = () => new JunctionObject { X = 11, Y = 12, Shape = JunctionShape.Tee, Rotation = 270, Size = 2, Visible = false },
        ["image"] = () => new ImageObject
        {
            X = 13, Y = 14, Width = 40, Height = 20, SourceResourceId = "res-a", RenderResourceId = "res-b",
            TransparencyEnabled = true, TransparentColor = 0xFF102030, Tolerance = 42,
        },
        ["potential"] = () => new PotentialObject
        {
            X = 15, Y = 16, Rotation = 270, Name = "L2", PotentialKind = PotentialKind.L2, Voltage = "400VAC", Description = "Şebeke",
        },
        ["pageLink"] = () => new PageLinkObject { X = 17, Y = 18, Rotation = 180, Name = "+24V/3", Description = "Kumanda" },
        ["devicePin"] = () => new DevicePinObject { X = 20, Y = 25, Rotation = 180, Name = "Y0.3", Function = PinFunction.Output, Description = "Pompa" },
    };

    [Fact]
    public void Every_registered_object_type_has_a_round_trip_sample()
    {
        // Yeni bir nesne türü eklenip burada örneği yazılmazsa bu test kırılır.
        Assert.Equal(ProjectSerializer.KnownObjectKinds.OrderBy(k => k), Samples.Keys.OrderBy(k => k));
    }

    [Fact]
    public void Every_object_type_round_trips_all_public_settable_properties()
    {
        var doc = ProjectDocument.CreateNew();
        foreach (var make in Samples.Values)
        {
            doc.Pages[1].Objects.Add(make());
        }

        var loaded = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(doc));

        var before = doc.Pages[1].Objects;
        var after = loaded.Pages[1].Objects;
        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].GetType(), after[i].GetType());
            foreach (var prop in before[i].GetType().GetProperties()
                         .Where(p => p.CanWrite && p.GetSetMethod() is not null && p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length == 0))
            {
                var expected = prop.GetValue(before[i]);
                var actual = prop.GetValue(after[i]);
                if (expected is System.Collections.IEnumerable list && expected is not string)
                {
                    Assert.Equal(list.Cast<object>().Count(), ((System.Collections.IEnumerable)actual!).Cast<object>().Count());
                }
                else
                {
                    Assert.True(Equals(expected, actual), $"{before[i].GetType().Name}.{prop.Name}: {expected} ≠ {actual}");
                }
            }
        }
    }

    [Fact]
    public void Unknown_object_kind_reports_page_and_position()
    {
        var json = """
        { "formatVersion": 3, "project": {}, "pages": [ { "id": "p1", "name": "Kumanda", "objects": [
          { "kind": "line", "id": "11111111-1111-1111-1111-111111111111" },
          { "kind": "plcCard", "id": "22222222-2222-2222-2222-222222222222" } ] } ] }
        """;

        var ex = Assert.Throws<ProjectFormatException>(() => ProjectSerializer.Deserialize(json));

        Assert.Contains("Kumanda", ex.Message);
        Assert.Contains("2. nesne", ex.Message);
        Assert.Contains("plcCard", ex.Message);
    }

    [Fact]
    public void Object_without_kind_is_reported()
    {
        var json = """{ "formatVersion": 3, "pages": [ { "name": "S", "objects": [ { "x": 1 } ] } ] }""";
        var ex = Assert.Throws<ProjectFormatException>(() => ProjectSerializer.Deserialize(json));
        Assert.Contains("(tür yok)", ex.Message);
    }

    [Fact]
    public void Corrupt_image_data_opens_with_warning_and_placeholder()
    {
        var json = """
        { "formatVersion": 3, "project": { "name": "P" },
          "pages": [ { "id": "p1", "name": "S", "width": 420, "height": 297, "objects": [
            { "kind": "image", "id": "11111111-1111-1111-1111-111111111111", "x": 10, "y": 10, "width": 20, "height": 20,
              "sourceResourceId": "res-x", "renderResourceId": "res-x" } ] } ],
          "resources": [ { "id": "res-x", "fileName": "logo.png", "data": "bu-base64-degil!!" } ] }
        """;

        var result = ProjectSerializer.DeserializeWithReport(json);

        Assert.Contains(result.Warnings, w => w.Contains("logo.png") && w.Contains("bozuk"));
        Assert.Empty(result.Document.Resources[0].Data);
        // PDF yer tutucu ile hatasız üretilir
        new PdfExporter(BuiltInSymbolLibrary.Instance, NoLog).Export(result.Document, new MemoryStream());
    }

    [Fact]
    public void Undecodable_image_bytes_draw_placeholder_in_pdf()
    {
        var doc = ProjectDocument.CreateNew();
        var res = doc.AddResource([1, 2, 3, 4, 5], "bozuk.png");
        doc.Pages[1].Objects.Add(new ImageObject { X = 10, Y = 10, Width = 20, Height = 20, SourceResourceId = res.Id, RenderResourceId = res.Id });

        new PdfExporter(BuiltInSymbolLibrary.Instance, NoLog).Export(doc, new MemoryStream());
    }

    [Fact]
    public void Missing_and_broken_data_is_repaired_with_warnings()
    {
        var json = """
        { "formatVersion": 3, "project": null,
          "pages": [
            { "id": "dup", "name": "A", "format": "a4", "orientation": "portrait", "width": 0, "height": -5, "objects": [
              { "kind": "line", "id": "11111111-1111-1111-1111-111111111111" },
              { "kind": "line", "id": "11111111-1111-1111-1111-111111111111" },
              null,
              { "kind": "image", "id": "33333333-3333-3333-3333-333333333333", "renderResourceId": "res-yok" } ] },
            { "id": "dup", "name": "B", "objects": null } ] }
        """;

        var result = ProjectSerializer.DeserializeWithReport(json);
        var doc = result.Document;

        Assert.NotNull(doc.Project);
        Assert.Equal(210, doc.Pages[0].Width);
        Assert.Equal(297, doc.Pages[0].Height);
        Assert.Equal(3, doc.Pages[0].Objects.Count);
        Assert.Equal(3, doc.Pages[0].Objects.Select(o => o.Id).Distinct().Count());
        Assert.NotEqual(doc.Pages[0].Id, doc.Pages[1].Id);
        Assert.NotNull(doc.Pages[1].Objects);
        Assert.True(result.Warnings.Count >= 5, string.Join(" | ", result.Warnings));
        Assert.Contains(result.Warnings, w => w.Contains("resmin verisi projede yok"));
    }

    [Fact]
    public void Project_without_pages_gets_one_page()
    {
        var result = ProjectSerializer.DeserializeWithReport("""{ "formatVersion": 3 }""");
        Assert.Single(result.Document.Pages);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Clean_project_has_no_warnings()
    {
        var json = ProjectSerializer.Serialize(ProjectSerializerTests.SampleProject());
        Assert.Empty(ProjectSerializer.DeserializeWithReport(json).Warnings);
    }

    [Fact]
    public void Failed_save_leaves_existing_file_untouched()
    {
        var dir = Directory.CreateTempSubdirectory("myg-save-");
        var path = Path.Combine(dir.FullName, "p.myge");
        var service = new ProjectFileService(NoLog);
        try
        {
            service.Save(ProjectDocument.CreateNew("Sağlam"), path);
            var good = File.ReadAllText(path);

            var broken = ProjectDocument.CreateNew("Bozuk");
            broken.Pages[1].Objects.Add(new TextObject { X = double.NaN }); // JSON'a yazılamaz
            Assert.ThrowsAny<Exception>(() => service.Save(broken, path));

            Assert.Equal(good, File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(dir.FullName)); // geçici dosya kalmadı
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Second_save_keeps_previous_version_as_backup()
    {
        var dir = Directory.CreateTempSubdirectory("myg-bak-");
        var path = Path.Combine(dir.FullName, "p.myge");
        var service = new ProjectFileService(NoLog);
        try
        {
            service.Save(ProjectDocument.CreateNew("Birinci"), path);
            service.Save(ProjectDocument.CreateNew("İkinci"), path);

            Assert.Contains("İkinci", File.ReadAllText(path));
            Assert.Contains("Birinci", File.ReadAllText(path + ProjectFileService.BackupExtension));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Large_image_project_saves_and_loads_quickly()
    {
        var doc = ProjectDocument.CreateNew();
        var data = new byte[8 * 1024 * 1024];
        new Random(42).NextBytes(data);
        var res = doc.AddResource(data, "buyuk.jpg");
        for (var i = 0; i < 20; i++) // aynı resim 20 kez: tek kopya saklanmalı
        {
            doc.Pages[1].Objects.Add(new ImageObject { X = i, Width = 10, Height = 10, SourceResourceId = res.Id, RenderResourceId = res.Id });
        }

        var sw = Stopwatch.StartNew();
        var json = ProjectSerializer.Serialize(doc);
        var loaded = ProjectSerializer.Deserialize(json);
        sw.Stop();

        Assert.Single(loaded.Resources);
        Assert.Equal(data, loaded.Resources[0].Data);
        Assert.InRange(json.Length, data.Length * 4 / 3, data.Length * 4 / 3 + 200_000); // yalnızca bir base64 kopya
        Assert.True(sw.ElapsedMilliseconds < 3000, $"{sw.ElapsedMilliseconds} ms");
    }
}
