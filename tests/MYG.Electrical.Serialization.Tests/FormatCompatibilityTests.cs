using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Serialization;

namespace MYG.Electrical.Serialization.Tests;

/// <summary>
/// Format sürümü uyumluluğu. Sürüm numaraları bilerek sabit (literal) yazılmıştır: testler
/// <see cref="ProjectDocument.CurrentFormatVersion"/> sabitine bağlı olsaydı, sabitin yanlış kalması
/// (37afe6e'de olduğu gibi) yakalanamazdı.
/// </summary>
public class FormatCompatibilityTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ProjectDocument.FileExtension));

    [Fact]
    public void Current_format_version_is_6() => Assert.Equal(6, ProjectDocument.CurrentFormatVersion);

    [Fact]
    public void Project_with_image_is_written_as_version_6()
    {
        var doc = ProjectDocument.CreateNew();
        var res = doc.AddResource(TestPng.Create(2, 2, (_, _) => 0xFF000000), "a.png");
        doc.Pages[1].Objects.Add(new ImageObject { Width = 10, Height = 10, SourceResourceId = res.Id, RenderResourceId = res.Id });

        var json = ProjectSerializer.Serialize(doc);

        Assert.Contains("\"formatVersion\": 6", json);
        Assert.Contains("\"kind\": \"image\"", json);
        Assert.Contains("\"resources\"", json);
    }

    [Fact]
    public void Version1_project_opens_with_four_way_junction_and_same_wiring()
    {
        var doc = ProjectSerializer.Deserialize(Fixture("v1-four-way-junction"));

        var j = Assert.Single(doc.Pages[0].Objects.OfType<JunctionObject>());
        Assert.Equal(JunctionShape.Cross, j.Shape);
        Assert.Equal(2, new AutoWireEngine().Compute(doc.Pages[0].Objects).Count); // -K1 → ┼ → -K2
        Assert.Equal(6, doc.FormatVersion);
    }

    [Fact]
    public void Version2_project_keeps_junction_shape_and_rotation()
    {
        var doc = ProjectSerializer.Deserialize(Fixture("v2-angle-junction"));

        var j = Assert.Single(doc.Pages[0].Objects.OfType<JunctionObject>());
        Assert.Equal(JunctionShape.Angle, j.Shape);
        Assert.Equal(90, j.Rotation);
        Assert.Single(new AutoWireEngine().Compute(doc.Pages[0].Objects));
    }

    [Fact]
    public void Version2_file_with_image_from_buggy_build_still_opens()
    {
        var doc = ProjectSerializer.Deserialize(Fixture("v2-image-mislabeled"));

        var img = Assert.Single(doc.Pages[0].Objects.OfType<ImageObject>());
        var res = doc.FindResource(img.RenderResourceId);
        Assert.NotNull(res);
        Assert.Equal("\u0089PNG", System.Text.Encoding.Latin1.GetString(res.Data, 0, 4));
        Assert.True(img.TransparencyEnabled);
    }

    [Theory]
    [InlineData("v1-four-way-junction")]
    [InlineData("v2-angle-junction")]
    [InlineData("v2-image-mislabeled")]
    [InlineData("v3-image")]
    public void Old_files_are_upgraded_to_version_6_without_losing_objects(string fixture)
    {
        var original = ProjectSerializer.Deserialize(Fixture(fixture));

        var saved = ProjectSerializer.Serialize(original);
        var reopened = ProjectSerializer.Deserialize(saved);

        Assert.Contains("\"formatVersion\": 6", saved);
        Assert.Equal(original.Pages.SelectMany(p => p.Objects).Select(o => (o.Id, o.GetType())),
                     reopened.Pages.SelectMany(p => p.Objects).Select(o => (o.Id, o.GetType())));
        Assert.Equal(original.Resources.Select(r => r.Id), reopened.Resources.Select(r => r.Id));
        // Kaydet → aç → kaydet aynı dosyayı üretmeli (kayıpsız ve kararlı)
        Assert.Equal(saved, ProjectSerializer.Serialize(reopened));
    }

    [Fact]
    public void Version3_devices_get_project_device_records()
    {
        var doc = ProjectSerializer.Deserialize(Fixture("v1-four-way-junction"));

        var placed = doc.Pages.SelectMany(p => p.Objects).OfType<DeviceObject>()
            .Select(d => d.Reference).Where(r => r.Length > 0).Distinct().Order().ToList();
        Assert.NotEmpty(placed);
        Assert.Equal(placed, doc.Devices.Select(d => d.Reference).Order().ToList());
        Assert.Equal(MYG.Electrical.Core.Pages.ProjectSettings.DefaultReferenceFormat, doc.Settings.ReferenceFormat);
        Assert.All(doc.Pages, p => Assert.Empty(p.WireData));
    }

    [Fact]
    public void Newer_format_is_refused_instead_of_being_partially_loaded()
    {
        var json = Fixture("v3-image").Replace("\"formatVersion\": 3", "\"formatVersion\": 7");
        var ex = Assert.Throws<ProjectFormatException>(() => ProjectSerializer.Deserialize(json));
        Assert.Contains("daha yeni", ex.Message);
    }

    [Theory]
    [InlineData("\"tolerance\": 12", "\"tolerance\": 12, \"blendMode\": \"multiply\"")]   // nesnede yeni alan
    [InlineData("\"company\": \"MYG TECH\"", "\"company\": \"MYG TECH\", \"website\": \"x\"")] // projede yeni alan
    public void Unknown_fields_are_refused_to_prevent_silent_data_loss(string find, string replace)
    {
        var json = Fixture("v3-image").Replace(find, replace);
        Assert.NotEqual(Fixture("v3-image"), json);

        var ex = Assert.Throws<ProjectFormatException>(() => ProjectSerializer.Deserialize(json));
        Assert.Contains("okunamadı", ex.Message);
    }
}
