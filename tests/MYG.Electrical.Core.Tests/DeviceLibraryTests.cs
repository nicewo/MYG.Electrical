using System.Text;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class DeviceLibraryTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;
    private static readonly DeviceLibrary Devices = BuiltInDeviceLibrary.Load(Lib);

    [Fact]
    public void Built_in_device_library_loads_and_references_existing_symbols()
    {
        Assert.Empty(Devices.Warnings);
        Assert.True(Devices.All.Count >= 50, $"{Devices.All.Count}");
        Assert.All(Devices.All, t => Assert.NotNull(Lib.Find(t.Symbol)));
        Assert.All(Devices.All.SelectMany(t => t.Parts), p => Assert.NotNull(Lib.Find(p)));
        Assert.Contains("Delta", Devices.Manufacturers);
        string[] required = ["dev.vfd.22kw", "dev.vfd.2.2kw.1ph", "dev.analyzer.modbus", "dev.ct.100", "dev.plc.cpu", "dev.plc.di8",
            "dev.plc.do8", "dev.hmi.delta.7", "dev.psu.24v.10a", "dev.dist.24v", "dev.button.start", "dev.button.stop", "dev.button.estop",
            "dev.contactor.9a.24vdc", "dev.relay.phase", "dev.pressure.switch", "dev.prox.m18.pnp", "dev.door.reed", "dev.terminal.2.5"];
        Assert.All(required, id => Assert.NotNull(Devices.Find(id)));
    }

    [Fact]
    public void Placing_a_device_fills_the_project_record_from_the_template()
    {
        var s = new EditorSession(ProjectDocument.CreateNew(), Lib);
        var t = Devices.Find("dev.contactor.9a.24vdc")!;
        var coil = s.PlaceDevice(t, new Point2D(100, 100))!;

        var rec = Assert.Single(s.Document.Devices);
        Assert.Equal(coil.Reference, rec.Reference);
        Assert.Equal("dev.contactor.9a.24vdc", rec.TemplateId);
        Assert.Equal("24VDC", rec.Properties["Bobin gerilimi"]);
        Assert.Equal(5, rec.Contacts.Count);
        Assert.StartsWith("MYG Cihaz Kütüphanesi", rec.LibraryVersion);
        s.Undo();
        Assert.Empty(s.Document.Devices);
        Assert.Empty(s.ActivePage.Objects);
    }

    [Fact]
    public void Contact_usage_shows_used_and_free_contacts()
    {
        var s = new EditorSession(ProjectDocument.CreateNew(), Lib);
        var coil = s.PlaceDevice(Devices.Find("dev.contactor.9a.24vdc")!, new Point2D(50, 100))!;
        var main = s.PlaceSymbol(Lib.Find(SymbolIds.Contactor)!, new Point2D(150, 100));
        var no = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(250, 100));
        s.RenameReference(main, coil.Reference);
        s.RenameReference(no, coil.Reference);

        var parts = CrossReferences.Parts(s.Network, Lib, coil.Reference);
        var (slots, unassigned) = ContactUsage.Compute(DeviceRegistry.Find(s.Document, coil.Reference)!, parts);

        Assert.Equal(4, slots.Count(x => x.IsUsed));             // 3 güç + 13-14
        Assert.False(slots.Single(x => x.Slot.Kind == ContactKind.Nc).IsUsed); // 21-22 boş
        Assert.Empty(unassigned);
    }

    [Fact]
    public void Extra_contact_beyond_capacity_is_reported()
    {
        var s = new EditorSession(ProjectDocument.CreateNew(), Lib);
        var coil = s.PlaceDevice(Devices.Find("dev.relay.1co.24vdc")!, new Point2D(50, 100))!;
        var a = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(150, 100));
        s.RenameReference(a, coil.Reference); // 13-14: 1CO rölede yok

        var (slots, unassigned) = ContactUsage.Compute(DeviceRegistry.Find(s.Document, coil.Reference)!,
            CrossReferences.Parts(s.Network, Lib, coil.Reference));
        Assert.False(Assert.Single(slots).IsUsed);
        Assert.Single(unassigned);
    }

    [Fact]
    public void Cross_reference_mirror_lists_contacts_with_page_and_column()
    {
        var doc = ProjectDocument.CreateNew();
        doc.Pages.Add(new Page { Name = "Güç" });
        var s = new EditorSession(doc, Lib);
        s.ActivePage = doc.Pages[1];
        var coil = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(300, 100));
        s.ActivePage = doc.Pages[2];
        var main = s.PlaceSymbol(Lib.Find(SymbolIds.Contactor)!, new Point2D(60, 100));
        s.RenameReference(main, coil.Reference);
        s.ActivePage = doc.Pages[1];
        var no = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(120, 100));
        s.RenameReference(no, coil.Reference);

        var mirror = CrossReferences.ContactMirror(s.Network, Lib, coil);
        Assert.Equal(["NO 13-14 /2.2", "Güç 1-2 3-4 5-6 /3.1"], mirror);
        Assert.Equal("/2.7", CrossReferences.MainLocation(s.Network, Lib, main));
    }

    [Fact]
    public void Renumbering_orders_by_page_and_column_and_keeps_parts_together()
    {
        var s = new EditorSession(ProjectDocument.CreateNew(), Lib);
        var k5 = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(300, 100));
        var k6 = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(80, 100));
        var contact = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(40, 200));
        s.RenameReference(contact, k5.Reference); // -K1 (k5) parçası, sayfanın en solunda
        var f = s.PlaceSymbol(Lib.Find(SymbolIds.Fuse)!, new Point2D(200, 100));
        s.RenameReference(f, "-F9");

        var changed = s.RenumberDevices();

        Assert.Equal("-K1", k5.Reference);   // parçası en solda → ilk sıra
        Assert.Equal("-K1", contact.Reference);
        Assert.Equal("-K2", k6.Reference);
        Assert.Equal("-F1", f.Reference);
        Assert.Equal(1, changed);            // yalnızca -F9 → -F1; -K1 ve -K2 zaten sıralı
        Assert.Contains(s.Document.Devices, d => d.Reference == "-F1");
        s.Undo();
        Assert.Equal("-F9", f.Reference);
    }

    [Fact]
    public void Update_device_changes_record_description_and_pin_names_in_one_undo_step()
    {
        var s = new EditorSession(ProjectDocument.CreateNew(), Lib);
        var k = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(100, 100));
        var rec = DeviceRegistry.Find(s.Document, k.Reference)!.Clone();
        rec.Manufacturer = "Schneider";
        rec.Properties["Akım"] = "6A";

        s.UpdateDevice(k, rec, "Pompa çalışıyor", new Dictionary<string, string> { ["13"] = "43", ["14"] = "44" });

        Assert.Equal("Schneider", DeviceRegistry.Find(s.Document, k.Reference)!.Manufacturer);
        Assert.Equal("Pompa çalışıyor", k.Description);
        Assert.Equal("43", k.PinLabel("13"));
        s.Undo();
        Assert.Equal(string.Empty, DeviceRegistry.Find(s.Document, k.Reference)!.Manufacturer);
        Assert.Equal("13", k.PinLabel("13"));
        Assert.Equal(string.Empty, k.Description);
    }

    [Fact]
    public void User_device_file_is_loaded_and_invalid_entries_are_skipped()
    {
        const string json = """
            { "formatVersion": 1, "library": "Benim", "version": "2", "devices": [
              { "id": "u.contactor", "name": "LC1D09BD", "category": "Kontaktör", "manufacturer": "Schneider", "model": "LC1D09BD",
                "symbol": "iec.coil", "contacts": [ { "kind": "no", "pins": ["13","14"] } ], "properties": { "Bobin gerilimi": "24VDC" } },
              { "id": "u.bad", "name": "Yok", "symbol": "iec.yok" },
              { "name": "Kimliksiz", "symbol": "iec.coil" }
            ] }
            """;
        var warnings = new List<string>();
        var list = DeviceLibraryLoader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "u.json", warnings, Lib, userDefined: true);
        var t = Assert.Single(list);
        Assert.Equal(("Schneider", "LC1D09BD", "Benim 2"), (t.Manufacturer, t.Model, t.Library));
        Assert.Equal(ContactKind.No, Assert.Single(t.Contacts).Kind);
        Assert.Equal(2, warnings.Count);
    }
}
