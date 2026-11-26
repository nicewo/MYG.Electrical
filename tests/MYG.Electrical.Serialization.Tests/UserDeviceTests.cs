using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Infrastructure.Library;
using MYG.Electrical.Infrastructure.Serialization;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Serialization.Tests;

public class UserDeviceTests
{
    private static DeviceBuildResult BuildPlc(string name = "Delta AS228T")
    {
        var objects = new List<DrawingObject> { new RectangleObject { X = 100, Y = 100, Width = 60, Height = 20, Dashed = true } };
        var n = "X0.0";
        for (var i = 0; i < 4; i++)
        {
            objects.Add(new DevicePinObject { X = 105 + i * 10, Y = 95, Name = n, Function = PinFunction.Input });
            n = PinNaming.Next(n);
        }
        return DeviceBuilder.Build(objects, new DeviceBuildOptions { Name = name, Manufacturer = "Delta" }, _ => false);
    }

    [Fact]
    public void User_library_files_are_written_and_reloaded_into_the_left_list()
    {
        var dir = Directory.CreateTempSubdirectory("myg-userlib-").FullName;
        try
        {
            var store = new UserLibraryStore(dir);
            var r = BuildPlc();
            store.Save(r.Symbol, r.Template);
            store.Save(r.Symbol, r.Template);                    // aynı kimlik: çoğalmaz, yerine yazılır
            var r2 = BuildPlc("Delta AS332T");
            store.Save(r2.Symbol, r2.Template);

            var catalog = BuiltInSymbolLibrary.CreateCatalog(Path.Combine(dir, "Symbols"));
            var devices = BuiltInDeviceLibrary.Create(catalog, Path.Combine(dir, "Devices"));
            Assert.Empty(catalog.Warnings);
            Assert.Empty(devices.Warnings);
            var sym = catalog.Find("user.delta-as228t")!;
            Assert.Equal(["X0.0", "X0.1", "X0.2", "X0.3"], sym.ConnectionPoints.Select(p => p.Id));
            Assert.Contains(sym.Primitives, p => p is RectPrimitive { Dashed: true });
            var t = devices.Find("userdev.delta-as228t")!;
            Assert.Equal(("Delta", "PLC ve Otomasyon/Kullanıcı"), (t.Manufacturer, t.Category));
            Assert.NotNull(devices.Find("userdev.delta-as332t"));
            Assert.Equal(2, devices.All.Count(d => d.IsUserDefined));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Project_with_user_device_opens_on_a_machine_without_the_library()
    {
        var r = BuildPlc();
        var catalog = BuiltInSymbolLibrary.CreateCatalog(null);
        catalog.AddOrReplace(r.Symbol);
        var s = new Core.Editing.EditorSession(ProjectDocument.CreateNew(), catalog);
        s.PlaceDevice(r.Template, new Point2D(100, 100));
        s.AddObject(new DevicePinObject { X = 50, Y = 50, Name = "I0.0", Function = PinFunction.Input }); // tasarım pini de kaydedilir
        ProjectSymbols.Embed(s.Document, catalog);

        var json = ProjectSerializer.Serialize(s.Document);
        Assert.Contains("\"symbols\": [", json);
        Assert.Contains("\"kind\": \"devicePin\"", json);
        var back = ProjectSerializer.Deserialize(json);
        Assert.Equal(json, ProjectSerializer.Serialize(back));

        var otherPc = BuiltInSymbolLibrary.CreateCatalog(null);
        Assert.Equal(1, ProjectSymbols.Register(back, otherPc));
        var net = Core.Network.ProjectNetwork.Build(back, otherPc);
        var findings = Core.Drc.DrcEngine.Run(back, net, otherPc);
        Assert.DoesNotContain(findings, f => f.Code == "DRC-SYM-01");
        Assert.Equal(4, back.Pages[1].Objects.OfType<DeviceObject>().Single().ConnectionPoints.Count);
    }
}
