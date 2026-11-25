using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class DeviceRecordTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;

    private static EditorSession NewSession() => new(ProjectDocument.CreateNew("Test"), Lib);

    [Fact]
    public void Placing_a_symbol_creates_device_record_with_symbol_data_and_undo_removes_it()
    {
        var s = NewSession();
        var coil = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(100, 100));

        var record = Assert.Single(s.Document.Devices);
        Assert.Equal("-K1", coil.Reference);
        Assert.Equal("-K1", record.Reference);
        Assert.Equal("Kontaktör ve Röle/Bobinler", record.Category);
        Assert.Equal("24VDC", record.Properties["Bobin gerilimi"]);
        Assert.StartsWith("MYG IEC 60617", record.LibraryVersion);

        s.Undo();
        Assert.Empty(s.Document.Devices);
        s.Redo();
        Assert.Single(s.Document.Devices);
    }

    [Fact]
    public void Contacts_with_same_reference_share_one_record()
    {
        var s = NewSession();
        var coil = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(100, 100));
        var contact = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(200, 100));
        Assert.Equal("-K2", contact.Reference);

        s.RenameReference(contact, coil.Reference);

        Assert.Equal("-K1", contact.Reference);
        Assert.Equal(2, DeviceRegistry.Placements(s.Document, "-K1").Count());
        // -K2 kaydı yerinde kalır (cihaz verisi kaybolmasın); yerleşimsiz kayıt DRC'de bildirilir.
        Assert.Contains(s.Document.Devices, d => d.Reference == "-K1");
    }

    [Fact]
    public void Renaming_sole_placement_renames_its_record_and_undo_restores()
    {
        var s = NewSession();
        var m = s.PlaceSymbol(Lib.Find(SymbolIds.Motor)!, new Point2D(100, 100));
        DeviceRegistry.Find(s.Document, m.Reference)!.Manufacturer = "Gamak";

        s.RenameReference(m, "-M10");

        var rec = Assert.Single(s.Document.Devices);
        Assert.Equal("-M10", rec.Reference);
        Assert.Equal("Gamak", rec.Manufacturer);

        s.Undo();
        Assert.Equal("-M1", m.Reference);
        Assert.Equal("-M1", Assert.Single(s.Document.Devices).Reference);
    }

    [Fact]
    public void Renaming_shared_placement_to_new_tag_copies_record()
    {
        var s = NewSession();
        var coil = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(100, 100));
        var contact = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(200, 100));
        s.RenameReference(contact, coil.Reference);

        s.RenameReference(contact, "-K5");

        Assert.NotNull(DeviceRegistry.Find(s.Document, "-K1"));
        Assert.NotNull(DeviceRegistry.Find(s.Document, "-K5"));
    }

    [Fact]
    public void Graphic_symbols_get_no_reference_and_no_record()
    {
        var s = NewSession();
        var earth = s.PlaceSymbol(Lib.Find("iec.earth")!, new Point2D(100, 100));
        Assert.Equal(string.Empty, earth.Reference);
        Assert.Empty(s.Document.Devices);
    }

    [Theory]
    [InlineData("-{prefix}{n}", "-K3")]
    [InlineData("={page}-{prefix}{n}", "=2-K1")]
    [InlineData("-{prefix}0{n}", "-K01")] // farklı biçimdeki -K1, -K2 bu sırayı etkilemez
    public void Reference_format_template_is_respected(string format, string expected)
    {
        var doc = ProjectDocument.CreateNew();
        doc.Settings.ReferenceFormat = format;
        doc.Devices.Add(new DeviceRecord { Reference = "-K2" });
        doc.Devices.Add(new DeviceRecord { Reference = "-K1" });
        Assert.Equal(expected, ReferenceGenerator.Next("K", doc, pageNumber: 2));
    }

    [Fact]
    public void Deleted_numbers_are_not_reused_while_record_exists()
    {
        var s = NewSession();
        var k1 = s.PlaceSymbol(Lib.Find(SymbolIds.Relay)!, new Point2D(100, 100));
        s.Select(k1);
        s.DeleteSelection();
        var next = s.PlaceSymbol(Lib.Find(SymbolIds.Relay)!, new Point2D(100, 100));
        Assert.Equal("-K2", next.Reference);
    }

    [Fact]
    public void Paste_creates_new_records_in_same_undo_step()
    {
        var s = NewSession();
        var f = s.PlaceSymbol(Lib.Find(SymbolIds.Fuse)!, new Point2D(100, 100));
        s.Select(f);
        s.CopySelection();
        s.Paste();

        Assert.Equal(["-F1", "-F2"], s.Document.Devices.Select(d => d.Reference).ToArray());
        s.Undo();
        Assert.Equal(["-F1"], s.Document.Devices.Select(d => d.Reference).ToArray());
    }

    [Fact]
    public void Ensure_records_creates_missing_records_once()
    {
        var doc = ProjectDocument.CreateNew();
        doc.Pages[1].Objects.Add(Lib.Find(SymbolIds.Coil)!.CreateDevice(10, 10, "-K1"));
        doc.Pages[1].Objects.Add(Lib.Find(SymbolIds.NormallyOpen)!.CreateDevice(50, 10, "-K1"));
        doc.Pages[1].Objects.Add(Lib.Find(SymbolIds.Fuse)!.CreateDevice(90, 10, "-F1"));

        Assert.Equal(2, DeviceRegistry.EnsureRecords(doc, Lib));
        Assert.Equal(0, DeviceRegistry.EnsureRecords(doc, Lib));
        Assert.Equal("Koruma/Sigortalar", DeviceRegistry.Find(doc, "-f1")!.Category);
    }

    [Fact]
    public void Pin_designation_overrides_label_but_not_identity()
    {
        var d = Lib.Find(SymbolIds.NormallyOpen)!.CreateDevice(0, 0, "-K1");
        d.PinDesignations["13"] = "23";
        Assert.Equal("23", d.PinLabel("13"));
        Assert.Equal("14", d.PinLabel("14"));
        var copy = (DeviceObject)d.CloneWithNewId();
        copy.PinDesignations["13"] = "33";
        Assert.Equal("23", d.PinLabel("13"));
    }
}
