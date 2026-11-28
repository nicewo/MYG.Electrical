using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Drc;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class DeviceBuilderTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;

    /// <summary>EPLAN gibi: kesikli kutu, üstte 4 giriş (X0.0..X0.3) ve L+/M, altta 2 çıkış (Y0.0, Y0.1), etiket metni.</summary>
    private static List<DrawingObject> DrawPlc(double x0 = 100, double y0 = 100)
    {
        var objects = new List<DrawingObject>
        {
            new RectangleObject { X = x0, Y = y0, Width = 80, Height = 30, Dashed = true },
            new TextObject { X = x0 + 3, Y = y0 + 15, Text = "AS228T", Height = 3 },
            new DevicePinObject { X = x0 + 5, Y = y0 - 5, Name = "L+", Function = PinFunction.SupplyPositive },
            new DevicePinObject { X = x0 + 15, Y = y0 - 5, Name = "M", Function = PinFunction.SupplyNegative },
        };
        var name = "X0.0";
        for (var i = 0; i < 4; i++)
        {
            objects.Add(new DevicePinObject { X = x0 + 35 + i * 10, Y = y0 - 5, Name = name, Function = PinFunction.Input });
            name = PinNaming.Next(name);
        }
        objects.Add(new DevicePinObject { X = x0 + 35, Y = y0 + 35, Rotation = 180, Name = "Y0.0", Function = PinFunction.Output });
        objects.Add(new DevicePinObject { X = x0 + 45, Y = y0 + 35, Rotation = 180, Name = "Y0.1", Function = PinFunction.Output });
        return objects;
    }

    private static DeviceBuildOptions Options(string name = "Delta AS228T", SymbolRole role = SymbolRole.Standalone) =>
        new() { Name = name, Manufacturer = "Delta", Model = "AS228T-A", Category = "PLC ve Otomasyon/Delta", Role = role };

    [Fact]
    public void Drawing_becomes_symbol_with_pins_on_grid_and_device_template()
    {
        var result = DeviceBuilder.Build(DrawPlc(), Options(), _ => false);

        var sym = result.Symbol;
        Assert.Equal("user.delta-as228t", sym.Id);
        Assert.Equal("userdev.delta-as228t", result.Template.Id);
        Assert.Equal(new Point2D(100, 95), result.Origin);               // seçimin sol üstü, grid'e oturmuş
        Assert.Equal(["L+", "M", "X0.0", "X0.1", "X0.2", "X0.3", "Y0.0", "Y0.1"], sym.ConnectionPoints.Select(p => p.Id));
        Assert.All(sym.ConnectionPoints, p => Assert.True(p.X % 5 == 0 && p.Y % 5 == 0, p.Id));
        Assert.Equal(PinDirection.Down, sym.ConnectionPoints.Single(p => p.Id == "Y0.0").Direction);
        Assert.Equal(PinFunction.Input, sym.ConnectionPoints.Single(p => p.Id == "X0.2").Function);
        Assert.Contains(sym.Primitives, p => p is RectPrimitive { Dashed: true, Width: 80 });
        Assert.Contains(sym.Primitives, p => p is TextPrimitive { Text: "AS228T", LeftAligned: true });
        Assert.Equal(8, sym.Primitives.Count(p => p is LinePrimitive));   // her pinin uç çizgisi
        Assert.Equal("A", sym.ReferencePrefix);
        Assert.True(sym.IsUserDefined);
        Assert.Equal(("Delta", "AS228T-A", "PLC ve Otomasyon/Delta"), (result.Template.Manufacturer, result.Template.Model, result.Template.Category));
        Assert.Null(SymbolLibraryLoader.Validate(sym));
    }

    [Fact]
    public void Missing_or_duplicate_pin_names_are_rejected()
    {
        Assert.Throws<DeviceBuildException>(() => DeviceBuilder.Build([new RectangleObject { Width = 10, Height = 10 }], Options(), _ => false));
        var dup = DrawPlc();
        dup.Add(new DevicePinObject { X = 300, Y = 95, Name = "x0.0", Function = PinFunction.Input });
        var ex = Assert.Throws<DeviceBuildException>(() => DeviceBuilder.Build(dup, Options(), _ => false));
        Assert.Contains("X0.0", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<DeviceBuildException>(() => DeviceBuilder.Build(DrawPlc(), Options(name: " "), _ => false));
    }

    [Fact]
    public void Ids_are_made_unique()
    {
        var taken = new HashSet<string> { "user.delta-as228t", "userdev.delta-as228t" };
        var r = DeviceBuilder.Build(DrawPlc(), Options(), taken.Contains);
        Assert.Equal("user.delta-as228t-2", r.Symbol.Id);
        Assert.Equal("user.delta-as228t-2", r.Template.Symbol);
        Assert.Equal("cihaz-ozel-g-c", DeviceBuilder.Slug("Cihaz Özel G/Ç"));
    }

    [Fact]
    public void Replacing_the_drawing_keeps_wires_and_is_one_undo_step()
    {
        var catalog = new SymbolCatalog(Lib.All);
        var s = new EditorSession(ProjectDocument.CreateNew(), catalog);
        var drawing = DrawPlc();
        foreach (var o in drawing)
        {
            s.AddObject(o, select: false);
        }
        // X0.0 girişine kapı sensörü (üstten)
        var sensor = s.PlaceSymbol(Lib.Find(SymbolIds.NormallyOpen)!, new Point2D(135, 70)); // 14 (135,80) ↓ → X0.0 (135,95) ↑
        Assert.Contains(s.Wires, w => w.Involves(sensor.Id));
        var before = s.ActivePage.Objects.Count;

        var result = DeviceBuilder.Build(drawing, Options(), id => catalog.Find(id) is not null);
        catalog.AddOrReplace(result.Symbol);
        var plc = s.ReplaceWithDevice(drawing, result.Template, result.Origin)!;

        Assert.Equal("-A1", plc.Reference);
        Assert.Equal(2, s.ActivePage.Objects.Count);                              // sensör + PLC
        var wire = Assert.Single(s.Wires);
        Assert.Equal(new PinRef(plc.Id, "X0.0"), wire.To == new PinRef(sensor.Id, "14") ? wire.From : wire.To);
        Assert.Equal("Delta", DeviceRegistry.Find(s.Document, "-A1")!.Manufacturer);

        s.Undo();
        Assert.Equal(before, s.ActivePage.Objects.Count);
        Assert.DoesNotContain(s.Document.Devices, d => d.Reference == "-A1");
    }

    [Fact]
    public void Multi_page_plc_parts_show_each_others_page_and_column()
    {
        var catalog = new SymbolCatalog(Lib.All);
        var inputs = DeviceBuilder.Build(DrawPlc(), Options("AS228T Girişler", SymbolRole.Main), id => catalog.Find(id) is not null);
        catalog.AddOrReplace(inputs.Symbol);
        var outputs = DeviceBuilder.Build(DrawPlc(), Options("AS228T Çıkışlar", SymbolRole.Contact), id => catalog.Find(id) is not null);
        catalog.AddOrReplace(outputs.Symbol);

        var doc = ProjectDocument.CreateNew();
        doc.Pages.Add(new Page { Name = "PLC çıkışları" });
        var s = new EditorSession(doc, catalog);
        s.ActivePage = doc.Pages[1];
        var main = s.PlaceDevice(inputs.Template, new Point2D(100, 100))!;
        s.ActivePage = doc.Pages[2];
        var part = s.PlaceDevice(outputs.Template, new Point2D(250, 100))!;
        s.RenameReference(part, main.Reference);

        Assert.Equal(["AS228T Çıkışlar /3.5"], CrossReferences.ContactMirror(s.Network, catalog, main));
        Assert.Equal("/2.2", CrossReferences.MainLocation(s.Network, catalog, part));
        var findings = DrcEngine.Run(doc, s.Network, catalog);
        Assert.DoesNotContain(findings, f => f.Code is "DRC-TAG-01" or "DRC-TERM-01" && f.Message.Contains("-A1", StringComparison.Ordinal)
            && f.Severity == DrcSeverity.Error && !f.Message.Contains("L+", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_embeds_user_symbols_and_registers_them_on_another_machine()
    {
        var catalog = new SymbolCatalog(Lib.All);
        var r = DeviceBuilder.Build(DrawPlc(), Options(), _ => false);
        catalog.AddOrReplace(r.Symbol);
        var s = new EditorSession(ProjectDocument.CreateNew(), catalog);
        s.PlaceDevice(r.Template, new Point2D(100, 100));

        ProjectSymbols.Embed(s.Document, catalog);
        var embedded = Assert.Single(s.Document.Symbols);
        Assert.Equal(r.Symbol.Id, embedded.Id);

        var otherPc = new SymbolCatalog(Lib.All);
        Assert.Null(otherPc.Find(r.Symbol.Id));
        Assert.Equal(1, ProjectSymbols.Register(s.Document, otherPc));
        Assert.Equal(8, otherPc.Find(r.Symbol.Id)!.ConnectionPoints.Count);
        Assert.False(otherPc.AddOrReplace(new SymbolDefinition { Id = SymbolIds.Coil, Name = "sahte" })); // yerleşik ezilmez
    }
}
