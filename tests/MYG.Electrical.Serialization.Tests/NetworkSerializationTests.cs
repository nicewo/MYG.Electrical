using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Pdf;
using MYG.Electrical.Infrastructure.Serialization;
using MYG.Electrical.Symbols;
using Serilog;

namespace MYG.Electrical.Serialization.Tests;

public class NetworkSerializationTests
{
    private static ProjectDocument Sample()
    {
        var doc = ProjectDocument.CreateNew();
        var page = doc.Pages[1];
        var pot = new PotentialObject { X = 100, Y = 50, Name = "+24V", PotentialKind = PotentialKind.DcPositive, Voltage = "24VDC", Description = "Kumanda" };
        var fuse = BuiltInSymbolLibrary.Instance.Find(SymbolIds.Fuse)!.CreateDevice(100, 70, "-F1");
        fuse.Description = "Kumanda sigortası";
        fuse.PinDesignations["1"] = "1.1";
        var link = new PageLinkObject { X = 100, Y = 100, Rotation = 90, Name = "24V-A", Description = "Sayfa 3'e" };
        page.Objects.AddRange([pot, fuse, link]);
        page.WireData.Add(new WireData { A = pot.Pin.Ref, B = new PinRef(fuse.Id, "1"), Number = "+24V", Color = "RD", CrossSection = "1 mm²", NumberLocked = true });
        doc.Devices.Add(new DeviceRecord { Reference = "-F1", Manufacturer = "ABB", Model = "S201-C2", Properties = { ["Akım"] = "2A" } });
        doc.Settings.ReferenceFormat = "={page}-{prefix}{n}";
        return doc;
    }

    [Fact]
    public void Potentials_page_links_wire_data_and_devices_roundtrip()
    {
        var doc = Sample();
        var json = ProjectSerializer.Serialize(doc);
        Assert.Contains("\"kind\": \"potential\"", json);
        Assert.Contains("\"kind\": \"pageLink\"", json);
        Assert.Contains("\"potentialKind\": \"dcPositive\"", json);

        var back = ProjectSerializer.Deserialize(json);
        var pot = Assert.Single(back.Pages[1].Objects.OfType<PotentialObject>());
        Assert.Equal(("+24V", PotentialKind.DcPositive, "24VDC", "Kumanda"), (pot.Name, pot.PotentialKind, pot.Voltage, pot.Description));
        var link = Assert.Single(back.Pages[1].Objects.OfType<PageLinkObject>());
        Assert.Equal((90.0, "24V-A"), (link.Rotation, link.Name));
        var wd = Assert.Single(back.Pages[1].WireData);
        Assert.Equal(("+24V", "RD", "1 mm²", true), (wd.Number, wd.Color, wd.CrossSection, wd.NumberLocked));
        Assert.Equal(pot.Id, wd.A.OwnerId);
        var fuse = back.Pages[1].Objects.OfType<DeviceObject>().Single();
        Assert.Equal("1.1", fuse.PinLabel("1"));
        Assert.Equal("Kumanda sigortası", fuse.Description);
        var rec = Assert.Single(back.Devices);
        Assert.Equal(("ABB", "S201-C2", "2A"), (rec.Manufacturer, rec.Model, rec.Properties["Akım"]));
        Assert.Equal("={page}-{prefix}{n}", back.Settings.ReferenceFormat);
        Assert.Equal(json, ProjectSerializer.Serialize(back));
    }

    [Fact]
    public void Pdf_export_with_potentials_and_links_works()
    {
        var warnings = new PdfExporter(BuiltInSymbolLibrary.Instance, new LoggerConfiguration().CreateLogger())
            .Export(Sample(), new MemoryStream());
        Assert.Empty(warnings);
    }
}
