using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class NetworkTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;

    /// <summary>
    /// +24V → sigorta → NO kontak → bobin → 0V (x = 100 hattında, dikey).
    /// </summary>
    private static (ProjectDocument Doc, Page Page, PotentialObject Plus, PotentialObject Zero, DeviceObject Fuse, DeviceObject Contact, DeviceObject Coil) ControlCircuit()
    {
        var doc = ProjectDocument.CreateNew();
        var page = doc.Pages[1];
        var plus = new PotentialObject { X = 100, Y = 50, Name = "+24V", PotentialKind = PotentialKind.DcPositive, Voltage = "24VDC" };
        var fuse = Lib.Find(SymbolIds.Fuse)!.CreateDevice(100, 70, "-F1");
        var contact = Lib.Find(SymbolIds.NormallyOpen)!.CreateDevice(100, 100, "-K1");
        var coil = Lib.Find(SymbolIds.Coil)!.CreateDevice(100, 130, "-K2");
        var zero = new PotentialObject { X = 100, Y = 160, Rotation = 180, Name = "0V", PotentialKind = PotentialKind.DcNegative };
        page.Objects.AddRange([plus, fuse, contact, coil, zero]);
        return (doc, page, plus, zero, fuse, contact, coil);
    }

    [Fact]
    public void Potential_flows_through_permanent_and_switched_connections_but_not_through_loads()
    {
        var c = ControlCircuit();
        var net = ProjectNetwork.Build(c.Doc, Lib);

        Assert.Equal(4, net.Wires[c.Page].Count);
        Assert.Equal("+24V", net.PotentialOf(new PinRef(c.Fuse.Id, "2"))!.Name);       // sigorta: kalıcı
        Assert.Equal("+24V", net.PotentialOf(new PinRef(c.Coil.Id, "A1"))!.Name);      // kontak: anahtarlı
        Assert.Equal(PotentialKind.DcNegative, net.PotentialOf(new PinRef(c.Coil.Id, "A2"))!.Kind); // bobin geçirmez
        Assert.Equal(net.NetOf(new PinRef(c.Fuse.Id, "1")), net.NetOf(new PinRef(c.Contact.Id, "13")));
        Assert.NotEqual(net.NetOf(new PinRef(c.Contact.Id, "13")), net.NetOf(new PinRef(c.Contact.Id, "14")));
        Assert.Empty(net.PotentialConflicts());
    }

    [Fact]
    public void Wire_colour_follows_potential_kind()
    {
        var c = ControlCircuit();
        var net = ProjectNetwork.Build(c.Doc, Lib);
        var colors = ColorScheme.Default;
        var wireToCoil = net.Wires[c.Page].Single(w => w.Involves(c.Coil.Id) && w.Involves(c.Contact.Id));
        Assert.Equal(colors.DcPositive, colors.ForPotential(net.PotentialOf(wireToCoil)?.Kind));
        var wireToZero = net.Wires[c.Page].Single(w => w.Involves(c.Zero.Id));
        Assert.Equal(colors.DcNegative, colors.ForPotential(net.PotentialOf(wireToZero)?.Kind));
    }

    [Fact]
    public void Two_potentials_on_one_net_is_a_direct_conflict()
    {
        var c = ControlCircuit();
        c.Page.Objects.Remove(c.Coil); // +24V ... kontak 14 → doğrudan 0V
        c.Zero.Y = 140;
        c.Zero.Rotation = 180;
        c.Page.Objects.Remove(c.Contact);
        var net = ProjectNetwork.Build(c.Doc, Lib);

        var conflict = Assert.Single(net.PotentialConflicts());
        Assert.True(conflict.Direct);
    }

    [Fact]
    public void Potentials_meeting_through_a_contact_is_a_possible_conflict()
    {
        var c = ControlCircuit();
        c.Page.Objects.Remove(c.Coil);
        c.Zero.Y = 120; // kontak 14 (110) ile yüz yüze
        var net = ProjectNetwork.Build(c.Doc, Lib);

        var conflict = Assert.Single(net.PotentialConflicts());
        Assert.False(conflict.Direct);
    }

    [Fact]
    public void Page_links_with_same_name_join_nets_across_pages_and_report_location()
    {
        var doc = ProjectDocument.CreateNew();
        var p1 = doc.Pages[1];
        var p2 = new Page { Name = "Kumanda" };
        doc.Pages.Add(p2);
        var l1 = new PotentialObject { X = 50, Y = 50, Rotation = 270, Name = "L1", PotentialKind = PotentialKind.L1 }; // kablo sağa
        var linkOut = new PageLinkObject { X = 300, Y = 50, Name = "L1-A" };                                          // kablo soldan
        p1.Objects.AddRange([l1, linkOut]);
        var linkIn = new PageLinkObject { X = 60, Y = 80, Rotation = 180, Name = "l1-a" };                              // kablo sağa
        var lamp = Lib.Find(SymbolIds.Lamp)!.CreateDevice(100, 80, "-P1");
        lamp.Rotation = 90; // X1 solda
        p2.Objects.AddRange([linkIn, lamp]);

        var net = ProjectNetwork.Build(doc, Lib);

        Assert.Single(net.Wires[p1]);
        var lampPin = lamp.GetGlobalPins().Single(p => p.Direction == PinDirection.Left);
        Assert.Equal("L1", net.PotentialOf(lampPin.Ref)!.Name);
        Assert.Equal(net.NetOf(l1.Pin.Ref), net.NetOf(lampPin.Ref));
        var target = Assert.Single(net.LinkTargets(linkOut));
        Assert.Equal(3, target.PageNumber);
        Assert.Equal("/3.1", target.ToString());
    }

    [Fact]
    public void Wire_numbering_gives_one_number_per_net_and_potential_names()
    {
        var c = ControlCircuit();
        var s = new EditorSession(c.Doc, Lib);
        s.ActivePage = c.Page;

        var changed = s.NumberWires(renumberAll: true);

        Assert.Equal(4, changed);
        string Number(Guid a, Guid b) => c.Page.WireData.Single(w => w.A.OwnerId == a && w.B.OwnerId == b || w.A.OwnerId == b && w.B.OwnerId == a).Number;
        Assert.Equal("+24V", Number(c.Plus.Id, c.Fuse.Id));
        Assert.Equal("+24V", Number(c.Fuse.Id, c.Contact.Id));
        Assert.Equal("1", Number(c.Contact.Id, c.Coil.Id));
        Assert.Equal("0V", Number(c.Coil.Id, c.Zero.Id));

        s.Undo();
        Assert.Empty(c.Page.WireData);
    }

    [Fact]
    public void Locked_numbers_are_kept_and_numbering_only_missing_continues_counter()
    {
        var c = ControlCircuit();
        c.Page.Objects.Remove(c.Plus);
        c.Page.Objects.Remove(c.Zero);
        var s = new EditorSession(c.Doc, Lib);
        s.ActivePage = c.Page;
        var wire = s.Wires.Single(w => w.Involves(c.Contact.Id) && w.Involves(c.Coil.Id));
        s.SetWireData(wire, new WireData { Number = "K-7", NumberLocked = true, CrossSection = "0,75 mm²" });

        s.NumberWires(renumberAll: false);

        var data = c.Page.FindWireData(wire.From, wire.To)!;
        Assert.Equal("K-7", data.Number);
        Assert.Equal("0,75 mm²", data.CrossSection);
        var other = c.Page.WireData.Single(w => !w.NumberLocked);
        Assert.Equal("1", other.Number);
    }

    [Fact]
    public void Wire_data_survives_moving_both_ends_together()
    {
        var c = ControlCircuit();
        var s = new EditorSession(c.Doc, Lib);
        s.ActivePage = c.Page;
        var wire = s.Wires.Single(w => w.Involves(c.Fuse.Id) && w.Involves(c.Contact.Id));
        s.SetWireData(wire, new WireData { Number = "5", Color = "BU" });

        s.SelectMany([c.Fuse, c.Contact, c.Coil, c.Plus, c.Zero]);
        s.MoveSelection(50, 0);

        var moved = s.Wires.Single(w => w.Involves(c.Fuse.Id) && w.Involves(c.Contact.Id));
        Assert.Equal("BU", c.Page.FindWireData(moved.From, moved.To)!.Color);
    }

    [Fact]
    public void Second_contact_of_same_device_gets_next_ordinal()
    {
        var s = new EditorSession(ProjectDocument.CreateNew(), Lib);
        var coil = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(50, 100));
        var no1 = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(100, 100));
        var no2 = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(150, 100));
        var nc = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyClosed)!, new Point2D(200, 100));

        s.RenameReference(no1, coil.Reference);
        s.RenameReference(no2, coil.Reference);
        s.RenameReference(nc, coil.Reference);

        Assert.Equal(("13", "14"), (no1.PinLabel("13"), no1.PinLabel("14")));
        Assert.Equal(("23", "24"), (no2.PinLabel("13"), no2.PinLabel("14")));
        Assert.Equal(("31", "32"), (nc.PinLabel("11"), nc.PinLabel("12")));

        s.Undo();
        Assert.Equal("11", nc.PinLabel("11"));
    }

    [Fact]
    public void Power_contacts_are_not_renumbered()
    {
        var s = new EditorSession(ProjectDocument.CreateNew(), Lib);
        var coil = s.PlaceSymbol(Lib.Find(SymbolIds.Coil)!, new Point2D(50, 100));
        var main = s.PlaceSymbol(Lib.Find(SymbolIds.Contactor)!, new Point2D(150, 100));
        s.RenameReference(main, coil.Reference);
        Assert.Empty(main.PinDesignations);
    }

    [Fact]
    public void Analysis_of_large_project_is_fast()
    {
        var doc = ProjectDocument.CreateNew();
        for (var p = 0; p < 40; p++)
        {
            var page = new Page { Name = $"S{p}" };
            for (var i = 0; i < 30; i++)
            {
                page.Objects.Add(Lib.Find(SymbolIds.Contactor)!.CreateDevice(40 + i * 10, 60, $"-K{p}_{i}"));
                page.Objects.Add(Lib.Find(SymbolIds.CircuitBreaker)!.CreateDevice(40 + i * 10, 100, $"-F{p}_{i}"));
            }
            doc.Pages.Add(page);
        }
        var engine = new AutoWireEngine();
        var wires = doc.Pages.ToDictionary(p => p, p => engine.Compute(p.Objects));

        var best = long.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ProjectNetwork.Build(doc, Lib, wires);
            best = Math.Min(best, sw.ElapsedMilliseconds);
        }
        Assert.True(best < 150, $"{best} ms");
    }
}
