using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Serialization;
using MYG.Electrical.Symbols;
using Serilog;

namespace MYG.Electrical.Serialization.Tests;

public class ProjectSerializerTests
{
    internal static ProjectDocument SampleProject()
    {
        var doc = ProjectDocument.CreateNew("Machine01");
        doc.Project.Customer = "Müşteri A.Ş.";
        doc.Project.ProjectNumber = "P-2026-001";
        var page = doc.Pages[1];
        var lib = BuiltInSymbolLibrary.Instance;
        var k1 = lib.Find(SymbolIds.Contactor)!.CreateDevice(100, 60, "-K1");
        k1.Rotation = 90;
        page.Objects.Add(k1);
        page.Objects.Add(lib.Find(SymbolIds.Motor)!.CreateDevice(100, 150, "-M1"));
        page.Objects.Add(new JunctionObject { X = 50, Y = 40, Visible = false, Shape = JunctionShape.Angle, Rotation = 270 });
        page.Objects.Add(new LineObject { X1 = 1, Y1 = 2, X2 = 3, Y2 = 4, Thickness = 0.5 });
        page.Objects.Add(new RectangleObject { X = 10, Y = 10, Width = 30, Height = 20 });
        page.Objects.Add(new TextObject { X = 5, Y = 5, Text = "Güç Devresi ğüşıöç", Height = 5 });
        page.Objects.Add(new SymbolObject { SymbolId = SymbolIds.Lamp, X = 200, Y = 100, Scale = 1.5 });
        page.TitleBlock[TitleBlockFields.PageTitle] = "Ana Güç";
        return doc;
    }

    [Fact]
    public void Round_trip_preserves_all_object_types_and_values()
    {
        var original = SampleProject();

        var json = ProjectSerializer.Serialize(original);
        var loaded = ProjectSerializer.Deserialize(json);

        Assert.Equal(original.Pages.Count, loaded.Pages.Count);
        Assert.Equal("Müşteri A.Ş.", loaded.Project.Customer);
        var src = original.Pages[1].Objects;
        var dst = loaded.Pages[1].Objects;
        Assert.Equal(src.Count, dst.Count);
        for (var i = 0; i < src.Count; i++)
        {
            Assert.Equal(src[i].GetType(), dst[i].GetType());
            Assert.Equal(src[i].Id, dst[i].Id);
        }

        var k1 = Assert.IsType<DeviceObject>(dst[0]);
        Assert.Equal("-K1", k1.Reference);
        Assert.Equal(90, k1.Rotation);
        Assert.Equal(6, k1.ConnectionPoints.Count);
        Assert.Equal(PinDirection.Up, k1.ConnectionPoints[0].Direction);
        var junction = Assert.IsType<JunctionObject>(dst[2]);
        Assert.False(junction.Visible);
        Assert.Equal(JunctionShape.Angle, junction.Shape);
        Assert.Equal(270, junction.Rotation);
        Assert.Equal("Güç Devresi ğüşıöç", Assert.IsType<TextObject>(dst[5]).Text);
        Assert.Equal(1.5, Assert.IsType<SymbolObject>(dst[6]).Scale);
        Assert.Equal("Ana Güç", loaded.Pages[1].TitleBlock[TitleBlockFields.PageTitle]);
    }

    [Fact]
    public void Json_contains_required_fields_and_no_wire_objects()
    {
        var json = ProjectSerializer.Serialize(SampleProject());

        Assert.Contains($"\"formatVersion\": {ProjectDocument.CurrentFormatVersion}", json);
        Assert.Contains("\"shape\": \"angle\"", json);
        Assert.Contains("\"kind\": \"device\"", json);
        Assert.Contains("\"type\": \"cover\"", json);
        Assert.DoesNotContain("wire", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"anchor\"", json); // hesaplanan özellikler saklanmaz
    }

    [Fact]
    public void Specification_example_is_readable()
    {
        const string json = """
        {
          "formatVersion": 1,
          "project": { "name": "Machine01", "company": "MYG TECH", "revision": "A" },
          "pages": [
            { "id": "page-001", "name": "Kapak", "type": "Cover", "width": 420, "height": 297, "objects": [] },
            { "id": "page-002", "name": "Guc Devresi", "type": "Schematic", "width": 420, "height": 297, "objects": [] }
          ]
        }
        """;

        var doc = ProjectSerializer.Deserialize(json);

        Assert.Equal("Machine01", doc.Project.Name);
        Assert.Equal(PageType.Cover, doc.Pages[0].Type);
        Assert.Equal("page-002", doc.Pages[1].Id);
        Assert.Equal(420, doc.Pages[1].Width);
    }

    [Theory]
    [InlineData("""{ "project": {}, "pages": [] }""", "formatVersion")]
    [InlineData("""{ "formatVersion": 99, "pages": [] }""", "daha yeni")]
    [InlineData("""{ "formatVersion": 0, "pages": [] }""", "Geçersiz")]
    [InlineData("bu json değil", "JSON")]
    [InlineData("""{ "formatVersion": 1, "pages": [ { "objects": [ { "kind": "bilinmeyen" } ] } ] }""", "okunamadı")]
    public void Invalid_files_raise_readable_errors(string json, string expectedMessagePart)
    {
        var ex = Assert.Throws<ProjectFormatException>(() => ProjectSerializer.Deserialize(json));
        Assert.Contains(expectedMessagePart, ex.Message);
    }

    [Fact]
    public void Wires_are_regenerated_after_load()
    {
        var doc = ProjectDocument.CreateNew();
        var coil = BuiltInSymbolLibrary.Instance.Find(SymbolIds.Coil)!;
        doc.Pages[1].Objects.Add(coil.CreateDevice(50, 50, "-K1"));
        doc.Pages[1].Objects.Add(coil.CreateDevice(50, 100, "-K2"));

        var loaded = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(doc));
        var wires = new Core.AutoWire.AutoWireEngine().Compute(loaded.Pages[1].Objects);

        var w = Assert.Single(wires);
        Assert.Equal(new Point2D(50, 60), w.Start);
    }

    [Fact]
    public async Task File_service_saves_and_opens_project()
    {
        var dir = Path.Combine(Path.GetTempPath(), "myg-tests-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "Machine01" + ProjectDocument.FileExtension);
        var service = new ProjectFileService(new LoggerConfiguration().CreateLogger());
        try
        {
            await service.SaveAsync(SampleProject(), path, TestContext.Current.CancellationToken);
            await service.SaveAsync(SampleProject(), path, TestContext.Current.CancellationToken); // üzerine yazma
            var loaded = await service.OpenAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(7, loaded.Pages[1].Objects.Count);
            Assert.Single(Directory.GetFiles(dir)); // geçici dosya kalmamalı
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Opening_missing_file_raises_readable_error()
    {
        var service = new ProjectFileService(new LoggerConfiguration().CreateLogger());
        await Assert.ThrowsAsync<ProjectFormatException>(() => service.OpenAsync("/yok/dosya.myge", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Version1_junctions_are_migrated_as_four_way_cross()
    {
        const string json = """
        {
          "formatVersion": 1,
          "project": { "name": "Eski" },
          "pages": [ { "id": "p1", "name": "S", "type": "Schematic", "width": 420, "height": 297,
            "objects": [ { "kind": "junction", "id": "8d0b2c1e-6f5a-4b7e-9a51-2f0c3d4e5f60", "x": 10, "y": 20, "size": 1.2, "visible": true } ] } ]
        }
        """;

        var doc = ProjectSerializer.Deserialize(json);

        var j = Assert.IsType<JunctionObject>(Assert.Single(doc.Pages[0].Objects));
        Assert.Equal(JunctionShape.Cross, j.Shape);
        Assert.Equal(4, j.ActiveDirections.Count);
        Assert.Equal(ProjectDocument.CurrentFormatVersion, doc.FormatVersion);
    }

    [Fact]
    public void Images_are_embedded_and_unused_resources_are_not_written()
    {
        var doc = ProjectDocument.CreateNew();
        var png = TestPng.Create(4, 4, (x, y) => 0xFF00FF00);
        var src = doc.AddResource(png, "logo.png");
        doc.AddResource([7, 7, 7], "silinmis.png");
        doc.Pages[1].Objects.Add(new ImageObject
        {
            X = 10, Y = 20, Width = 40, Height = 40, SourceResourceId = src.Id, RenderResourceId = src.Id,
            TransparencyEnabled = true, TransparentColor = 0xFFFFFFFF, Tolerance = 35,
        });

        var json = ProjectSerializer.Serialize(doc);
        var loaded = ProjectSerializer.Deserialize(json);

        var res = Assert.Single(loaded.Resources);
        Assert.Equal(png, res.Data);
        Assert.Equal("logo.png", res.FileName);
        var img = Assert.IsType<ImageObject>(Assert.Single(loaded.Pages[1].Objects));
        Assert.True(img.TransparencyEnabled);
        Assert.Equal(35, img.Tolerance);
        Assert.Equal(0xFFFFFFFFu, img.TransparentColor);
        Assert.Contains("\"kind\": \"image\"", json);
        Assert.DoesNotContain("\"settings\"", json);
        Assert.Equal(2, doc.Resources.Count); // kayıt bellekteki listeyi değiştirmez
    }
}
