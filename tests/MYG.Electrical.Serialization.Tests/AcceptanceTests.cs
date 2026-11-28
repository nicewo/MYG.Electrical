using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Drc;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Core.Reports;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Infrastructure.Pdf;
using MYG.Electrical.Infrastructure.Reports;
using MYG.Electrical.Infrastructure.Serialization;
using MYG.Electrical.Symbols;
using Serilog;

namespace MYG.Electrical.Serialization.Tests;

/// <summary>
/// Kabul senaryoları (v0.3 cihaz kütüphanesi / potansiyel / DRC / rapor geliştirmesi). Her senaryo örnek makine
/// projesi (<see cref="ReferenceMachine"/>) üzerinde uçtan uca çalışır.
/// </summary>
public class AcceptanceTests
{
    private static readonly BuiltInSymbolLibrary Lib = ReferenceMachine.Lib;

    private static PinRef Pin(DeviceObject d, string id) => new(d.Id, id);

    // 1
    [Fact]
    public void S01_reference_machine_is_built_from_the_device_library_without_errors()
    {
        var m = new ReferenceMachine();
        Assert.Equal(7, m.Doc.Pages.Count);
        Assert.True(m.Doc.Devices.Count >= 30, $"{m.Doc.Devices.Count}");
        var findings = DrcEngine.Run(m.Doc, m.Session.Network, Lib);
        Assert.DoesNotContain(findings, f => f.Severity == DrcSeverity.Error);
        Assert.All(m.Doc.Devices.Where(d => d.TemplateId.Length > 0), d => Assert.StartsWith("MYG Cihaz Kütüphanesi", d.LibraryVersion));
    }

    // 2
    [Fact]
    public void S02_coil_and_contacts_on_different_pages_form_one_device_with_cross_references()
    {
        var m = new ReferenceMachine();
        var parts = CrossReferences.Parts(m.Session.Network, Lib, m.Coil.Reference);
        Assert.Equal(3, parts.Count);                                  // ana kontaklar (s.3), bobin + yardımcı (s.5)
        Assert.Equal([3, 5, 5], parts.Select(p => p.Location.PageNumber).ToArray());
        var mirror = CrossReferences.ContactMirror(m.Session.Network, Lib, m.Coil);
        Assert.Contains(mirror, l => l.StartsWith("Güç 1-2 3-4 5-6 /3.", StringComparison.Ordinal));
        Assert.Contains(mirror, l => l.StartsWith("NO 13-14 /5.", StringComparison.Ordinal));
        Assert.StartsWith("/5.", CrossReferences.MainLocation(m.Session.Network, Lib, m.MainContacts));
    }

    // 3
    [Fact]
    public void S03_contact_capacity_tracks_used_and_free_contacts_and_numbers_new_contacts()
    {
        var m = new ReferenceMachine();
        var record = DeviceRegistry.Find(m.Doc, m.Coil.Reference)!;
        var (slots, unassigned) = ContactUsage.Compute(record, CrossReferences.Parts(m.Session.Network, Lib, m.Coil.Reference));
        Assert.Equal(4, slots.Count(s => s.IsUsed));
        Assert.Equal("NC 21-22", Assert.Single(slots, s => !s.IsUsed).Slot.Display);
        Assert.Empty(unassigned);

        m.Session.ActivePage = m.Control;
        var nc = m.Session.PlaceSymbol(Lib.Find(SymbolIds.NormallyClosed)!, new Point2D(250, 100));
        m.Session.RenameReference(nc, m.Coil.Reference);
        Assert.Equal(("21", "22"), (nc.PinLabel("11"), nc.PinLabel("12")));
        (slots, _) = ContactUsage.Compute(DeviceRegistry.Find(m.Doc, m.Coil.Reference)!, CrossReferences.Parts(m.Session.Network, Lib, m.Coil.Reference));
        Assert.All(slots, s => Assert.True(s.IsUsed));
    }

    // 4
    [Fact]
    public void S04_potentials_flow_through_protection_and_contacts_and_set_wire_colours()
    {
        var m = new ReferenceMachine();
        var net = m.Session.Network;
        Assert.Equal("L1", net.PotentialOf(Pin(m.Vfd22, "L1"))!.Name);  // MCB (anahtarlı) üzerinden
        Assert.Equal("PE", net.PotentialOf(Pin(m.Vfd22, "PE"))!.Name);
        Assert.Null(net.PotentialOf(Pin(m.Vfd22, "U")));                 // sürücü çıkışı ayrı (iç bağlantı yok)
        Assert.Equal("+24V", net.PotentialOf(Pin(m.Coil, "A1"))!.Name); // sayfa bağlantısı + butonlar üzerinden
        Assert.Equal("0V", net.PotentialOf(Pin(m.Coil, "A2"))!.Name);
        var colours = ColorScheme.Default;
        var a1 = Pin(m.Coil, "A1");
        var wire = net.Wires[m.Control].Single(w => w.From == a1 || w.To == a1);
        Assert.Equal(colours.DcPositive, colours.ForPotential(net.PotentialOf(wire)?.Kind));
        Assert.DoesNotContain(net.PotentialConflicts(), c => c.Direct);
    }

    // 5
    [Fact]
    public void S05_page_links_join_supply_and_control_pages()
    {
        var m = new ReferenceMachine();
        var net = m.Session.Network;
        var supplyLink = m.Supply.Objects.OfType<PageLinkObject>().Single(l => l.Name == "+24V");
        var controlLink = m.Control.Objects.OfType<PageLinkObject>().Single(l => l.Name == "+24V");
        Assert.Equal(net.NetOf(supplyLink.Pin.Ref), net.NetOf(controlLink.Pin.Ref));
        Assert.Equal("/5.1", Assert.Single(net.LinkTargets(supplyLink)).ToString());
        Assert.StartsWith("/4.", Assert.Single(net.LinkTargets(controlLink)).ToString());
    }

    // 6
    [Fact]
    public void S06_wire_numbering_gives_net_numbers_and_potential_names_and_is_undoable()
    {
        var m = new ReferenceMachine();
        var changed = m.Session.NumberWires(renumberAll: true);
        var all = m.Doc.Pages.SelectMany(p => p.WireData).ToList();
        Assert.Equal(m.Session.Network.Wires.Values.Sum(w => w.Count), changed);
        Assert.Contains(all, w => w.Number == "L1");
        Assert.Contains(all, w => w.Number == "+24V");
        Assert.Contains(all, w => w.Number == "1");
        // Aynı nette aynı numara
        var byNet = m.Doc.Pages.SelectMany(p => m.Session.Network.Wires[p].Select(w => (Net: m.Session.Network.NetOf(w.From), Data: p.FindWireData(w.From, w.To)!)));
        Assert.All(byNet.GroupBy(x => x.Net), g => Assert.Single(g.Select(x => x.Data.Number).Distinct()));
        m.Session.Undo();
        Assert.Empty(m.Doc.Pages.SelectMany(p => p.WireData));
    }

    // 7
    [Fact]
    public void S07_duplicate_tag_is_an_error_and_is_highlighted()
    {
        var m = new ReferenceMachine();
        m.Session.ActivePage = m.Control;
        var lamp2 = m.Session.PlaceSymbol(Lib.Find(SymbolIds.Lamp)!, new Point2D(300, 90));
        var lamp1 = m.Control.Objects.OfType<DeviceObject>().First(d => d.SymbolId == SymbolIds.Lamp && d.Id != lamp2.Id);
        m.Session.RenameReference(lamp2, lamp1.Reference);

        var finding = Assert.Single(DrcEngine.Run(m.Doc, m.Session.Network, Lib), f => f.Code == "DRC-TAG-01");
        Assert.Equal(DrcSeverity.Error, finding.Severity);
        Assert.Contains(lamp2.Id, finding.ObjectIds);
        Assert.Contains(lamp1.Id, finding.ObjectIds);
    }

    // 8
    [Fact]
    public void S08_short_circuit_between_potentials_is_detected()
    {
        var m = new ReferenceMachine();
        m.Session.ActivePage = m.Control;
        // +24V → klemens → 0V: aynı iletkende iki potansiyel = kesin kısa devre
        m.Session.AddObject(new PotentialObject { X = 300, Y = 30, Name = "+24V", PotentialKind = PotentialKind.DcPositive });
        m.Session.PlaceSymbol(Lib.Find(SymbolIds.Terminal)!, new Point2D(300, 50));
        m.Session.AddObject(new PotentialObject { X = 300, Y = 70, Rotation = 180, Name = "0V", PotentialKind = PotentialKind.DcNegative });
        // L1 → NO kontak → N: kontak kapanınca kısa devre (olası)
        m.Session.AddObject(new PotentialObject { X = 350, Y = 30, Name = "L1", PotentialKind = PotentialKind.L1 });
        m.Session.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(350, 50));
        m.Session.AddObject(new PotentialObject { X = 350, Y = 80, Rotation = 180, Name = "N", PotentialKind = PotentialKind.N });

        var findings = DrcEngine.Run(m.Doc, m.Session.Network, Lib);
        var direct = Assert.Single(findings, f => f.Code == "DRC-POT-01");
        Assert.Equal(DrcSeverity.Error, direct.Severity);
        Assert.Contains("+24V", direct.Message, StringComparison.Ordinal);
        var possible = Assert.Single(findings, f => f.Code == "DRC-POT-02");
        Assert.Equal(DrcSeverity.Warning, possible.Severity);
        Assert.Contains("L1", possible.Message, StringComparison.Ordinal);
    }

    // 9
    [Fact]
    public void S09_unconnected_pins_and_dangling_junctions_are_reported()
    {
        var m = new ReferenceMachine();
        m.Session.ActivePage = m.Control;
        m.Session.PlaceSymbol(Lib.Find(SymbolIds.Relay)!, new Point2D(330, 200));
        m.Session.PlaceJunction(JunctionShape.Tee, new Point2D(380, 40));
        var findings = DrcEngine.Run(m.Doc, m.Session.Network, Lib);
        Assert.Contains(findings, f => f.Code == "DRC-PIN-01" && f.Message.Contains("A1", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Code == "DRC-WIRE-01" && f.Severity == DrcSeverity.Error);
        Assert.False(string.IsNullOrWhiteSpace(DrcEngine.Disclaimer));
        Assert.Contains("sertifika", DrcEngine.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    // 10
    [Fact]
    public void S10_reports_cover_devices_bom_terminals_cables_and_plc_io()
    {
        var m = new ReferenceMachine();
        m.Session.NumberWires(renumberAll: true);
        var net = m.Session.Network;
        var devices = ProjectReports.DeviceList(m.Doc, net, Lib);
        var bom = ProjectReports.BillOfMaterials(m.Doc);
        var terminals = ProjectReports.TerminalPlan(m.Doc, net, Lib);
        var cables = ProjectReports.CableList(m.Doc, net);
        var io = ProjectReports.PlcIoList(m.Doc, net, Lib);

        Assert.Contains(devices.Rows, r => r[0] == m.Vfd22.Reference && r[7].Contains("22kW", StringComparison.Ordinal));
        Assert.Contains(bom.Rows, r => r[2].Contains("Akım trafosu 100/5", StringComparison.Ordinal) && r[1] == "3");
        Assert.Equal(3, terminals.Rows.Count);
        Assert.Contains(terminals.Rows, r => r[2].Contains("-T", StringComparison.Ordinal)); // CT P2 → klemens
        Assert.Equal(net.Wires.Values.Sum(w => w.Count), cables.Rows.Count);
        Assert.All(cables.Rows, r => Assert.False(string.IsNullOrEmpty(r[0])));
        Assert.Contains(io.Rows, r => r[1] == "DI0" && r[4].EndsWith(":14", StringComparison.Ordinal)); // kapı sensörü 14
        // Tek kanal giriş: sayfa bağlantısı üzerinden başka sayfadaki faz rölesi kontağına bağlı
        Assert.Contains(io.Rows, r => r[1] == "I1.0" && r[3] == "Faz sırası doğru" && r[4] == $"{m.PhaseRelay.Reference}:14");
        Assert.Contains(io.Rows, r => r[1] == "DO0" && r[4].StartsWith("-K", StringComparison.Ordinal));
    }

    // 11
    [Fact]
    public void S11_reports_are_written_as_xlsx_with_text_cells_and_as_csv()
    {
        var m = new ReferenceMachine();
        var tables = Enum.GetValues<ReportKind>().Select(k => ProjectReports.Create(k, m.Doc, m.Session.Network, Lib)).ToList();
        var ms = new MemoryStream();
        XlsxReportWriter.Write(tables, ms);
        ms.Position = 0;
        using (var doc = SpreadsheetDocument.Open(ms, false))
        {
            var wb = doc.WorkbookPart!;
            var sheets = wb.Workbook!.Sheets!.Elements<Sheet>().Select(x => x.Name!.Value).ToList();
            Assert.Equal(["Cihaz listesi", "Malzeme listesi", "Klemens planı", "Kablo listesi", "PLC G-Ç listesi"], sheets);
            var first = wb.WorksheetParts.First().Worksheet!.Descendants<Cell>().First();
            Assert.Equal(CellValues.InlineString, first.DataType!.Value);
        }

        var csv = new MemoryStream();
        CsvReportWriter.Write(tables[0], csv);
        var text = Encoding.UTF8.GetString(csv.ToArray());
        Assert.StartsWith("﻿Etiket;Açıklama", text);
        Assert.Equal("\"a;b\"", CsvReportWriter.Escape("a;b"));
        Assert.Equal("'=1+1", CsvReportWriter.Escape("=1+1"));
    }

    // 12
    [Fact]
    public void S12_full_project_round_trips_losslessly()
    {
        var m = new ReferenceMachine();
        m.Session.NumberWires(renumberAll: true);
        var json = ProjectSerializer.Serialize(m.Doc);
        var back = ProjectSerializer.Deserialize(json);
        Assert.Equal(json, ProjectSerializer.Serialize(back));
        var backNet = ProjectNetwork.Build(back, Lib);
        Assert.Equal(m.Session.Network.Wires.Values.Sum(w => w.Count), backNet.Wires.Values.Sum(w => w.Count));
        Assert.Equal(DrcEngine.Run(m.Doc, m.Session.Network, Lib).Select(f => f.Code + f.Message),
                     DrcEngine.Run(back, backNet, Lib).Select(f => f.Code + f.Message));
    }

    // 13
    [Fact]
    public void S13_old_v3_sample_opens_with_device_records_and_same_wiring()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "Demo.myge");
        var json = File.ReadAllText(path);
        Assert.Contains("\"formatVersion\": 3", json);
        var doc = ProjectSerializer.Deserialize(json);
        var tags = doc.Pages.SelectMany(p => p.Objects.OfType<DeviceObject>()).Select(d => d.Reference).Where(r => r.Length > 0).Distinct().Count();
        Assert.Equal(tags, doc.Devices.Count);
        var engine = new AutoWireEngine();
        var before = ProjectSerializer.Deserialize(json).Pages.Sum(p => engine.Compute(p.Objects).Count);
        Assert.Equal(before, ProjectNetwork.Build(doc, Lib).Wires.Values.Sum(w => w.Count));
    }

    // 14
    [Fact]
    public void S14_user_symbols_and_devices_need_no_code_change()
    {
        var dir = Directory.CreateTempSubdirectory("myg-user-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Symbols"));
            Directory.CreateDirectory(Path.Combine(dir, "Devices"));
            File.WriteAllText(Path.Combine(dir, "Symbols", "valf.json"), """
                { "formatVersion": 1, "library": "Atölye", "version": "1", "symbols": [
                  { "id": "user.valve.52", "name": "5/2 valf", "category": "Pnömatik", "referencePrefix": "Y",
                    "primitives": [ { "kind": "rect", "x": -5, "y": -3, "width": 10, "height": 6 } ],
                    "pins": [ { "id": "1", "x": 0, "y": -10, "direction": "up", "function": "coil" },
                              { "id": "2", "x": 0, "y": 10, "direction": "down", "function": "coil" } ] } ] }
                """);
            File.WriteAllText(Path.Combine(dir, "Devices", "festo.json"), """
                { "formatVersion": 1, "library": "Atölye", "version": "1", "devices": [
                  { "id": "user.festo.valve", "name": "Valf 5/2 24 VDC", "category": "Pnömatik", "manufacturer": "Festo",
                    "symbol": "user.valve.52", "properties": { "Gerilim": "24VDC" } } ] }
                """);
            var catalog = BuiltInSymbolLibrary.CreateCatalog(Path.Combine(dir, "Symbols"));
            var devices = BuiltInDeviceLibrary.Create(catalog, Path.Combine(dir, "Devices"));
            Assert.Empty(catalog.Warnings);
            Assert.Empty(devices.Warnings);
            var s = new Core.Editing.EditorSession(ProjectDocument.CreateNew(), catalog);
            var v = s.PlaceDevice(devices.Find("user.festo.valve")!, new Point2D(100, 100))!;
            Assert.Equal("-Y1", v.Reference);
            Assert.Equal("Festo", DeviceRegistry.Find(s.Document, "-Y1")!.Manufacturer);
            Assert.Contains("Festo", devices.Manufacturers);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // 15
    [Fact]
    public void S15_pdf_export_in_colour_and_monochrome_and_renumbering_keep_project_consistent()
    {
        var m = new ReferenceMachine();
        var exporter = new PdfExporter(Lib, new LoggerConfiguration().CreateLogger());
        var colour = new MemoryStream();
        var mono = new MemoryStream();
        Assert.Empty(exporter.Export(m.Doc, colour, new PdfExportOptions { Colors = ColorScheme.Default }));
        Assert.Empty(exporter.Export(m.Doc, mono, new PdfExportOptions { Colors = ColorScheme.Monochrome }));
        Assert.True(colour.Length > 10_000);

        Assert.True(ColorScheme.TryParseHex("#159447", out var pe) && pe == ColorScheme.Default.PE);
        Assert.True(ColorSchemeProperties.TrySet(ColorScheme.Default, "PE", 0xFF00FF00, out var changed) && changed.PE == 0xFF00FF00);

        var before = CrossReferences.Parts(m.Session.Network, Lib, m.Coil.Reference).Count;
        m.Session.RenumberDevices();
        var tags = m.Doc.Pages.SelectMany(p => p.Objects.OfType<DeviceObject>())
            .Where(d => Lib.Find(d.SymbolId)?.Role is SymbolRole.Main or SymbolRole.Standalone && d.Reference.Length > 0)
            .Select(d => d.Reference).ToList();
        Assert.Equal(tags.Count, tags.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(before, CrossReferences.Parts(m.Session.Network, Lib, m.Coil.Reference).Count);
        Assert.DoesNotContain(DrcEngine.Run(m.Doc, m.Session.Network, Lib), f => f.Severity == DrcSeverity.Error);
    }

    [Fact]
    public void Bundled_sample_project_is_current_and_has_no_drc_errors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "PompaIstasyonu.myge");
        if (Environment.GetEnvironmentVariable("MYG_WRITE_SAMPLES") is { Length: > 0 })
        {
            // Format yükseltmesinden sonra örneği güncel sürüme çevir (açıp kaydetmekle aynı)
            File.WriteAllText(path, ProjectSerializer.Serialize(ProjectSerializer.Deserialize(File.ReadAllText(path))));
        }
        var json = File.ReadAllText(path);
        Assert.Contains($"\"formatVersion\": {ProjectDocument.CurrentFormatVersion}", json);
        var doc = ProjectSerializer.Deserialize(json);
        var findings = DrcEngine.Run(doc, ProjectNetwork.Build(doc, Lib), Lib);
        Assert.DoesNotContain(findings, f => f.Severity == DrcSeverity.Error);
        Assert.Equal(json, ProjectSerializer.Serialize(doc));
    }
}
