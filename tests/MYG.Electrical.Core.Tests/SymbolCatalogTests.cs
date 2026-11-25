using System.Text;
using System.Text.Json;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Core.Tests;

public class SymbolCatalogTests
{
    private static readonly BuiltInSymbolLibrary Lib = BuiltInSymbolLibrary.Instance;

    [Fact]
    public void Built_in_library_loads_without_warnings_and_is_comprehensive()
    {
        Assert.Empty(Lib.Warnings);
        Assert.True(Lib.All.Count >= 100, $"Sembol sayısı {Lib.All.Count}");
        string[] categories = ["Koruma", "Kontaktör ve Röle", "Motor ve Sürücü", "PLC ve Otomasyon", "Sensör ve Saha",
            "Kumanda ve Sinyal", "Klemens ve Konnektör", "Ölçüm", "Enerji"];
        Assert.All(categories, c => Assert.Contains(Lib.All, s => s.TopCategory == c));
        Assert.All(Lib.All, s => Assert.StartsWith("MYG IEC 60617", s.Library));
    }

    [Theory]
    [InlineData("iec.vfd.3ph")]
    [InlineData("iec.vfd.1ph")]
    [InlineData("iec.poweranalyzer")]
    [InlineData("iec.ct")]
    [InlineData("iec.plc.cpu")]
    [InlineData("iec.plc.di8")]
    [InlineData("iec.plc.do8")]
    [InlineData("iec.hmi")]
    [InlineData("iec.psu.24vdc")]
    [InlineData("iec.relay.phase")]
    [InlineData("iec.pressureswitch.no")]
    [InlineData("iec.door.sensor")]
    [InlineData("iec.mpcb.3p")]
    [InlineData("iec.rcd.4p")]
    [InlineData("iec.contactor.3p.aux")]
    public void Library_covers_reference_project_devices(string id) => Assert.NotNull(Lib.Find(id));

    /// <summary>Mevcut projelerde kayıtlı olan 14 sembolün geometrisi ve pinleri değişmemeli.</summary>
    [Fact]
    public void Legacy_symbol_geometry_is_unchanged()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-symbols.json");
        using var stream = File.OpenRead(path);
        var warnings = new List<string>();
        var legacy = SymbolLibraryLoader.Read(stream, "legacy", warnings);
        Assert.Empty(warnings);
        Assert.Equal(14, legacy.Count);
        foreach (var old in legacy)
        {
            var now = Lib.Find(old.Id);
            Assert.NotNull(now);
            Assert.Equal(old.Primitives.Count, now.Primitives.Count);
            for (var i = 0; i < old.Primitives.Count; i++)
            {
                Assert.Equal(Normalize(old.Primitives[i]), Normalize(now.Primitives[i]));
            }
            Assert.Equal(
                old.ConnectionPoints.Select(p => (p.Id, p.X, p.Y, p.Direction)),
                now.ConnectionPoints.Select(p => (p.Id, p.X, p.Y, p.Direction)));
        }
    }

    private static string Normalize(SymbolPrimitive p)
    {
        // Kayan nokta gösterimindeki farkları yok say (JSON yazımı 1.7556 / 1.75562... gibi).
        var json = JsonSerializer.Serialize(p, SymbolLibraryLoader.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return string.Join(";", doc.RootElement.EnumerateObject().Select(e =>
            e.Value.ValueKind == JsonValueKind.Number ? $"{e.Name}={Math.Round(e.Value.GetDouble(), 6)}" : $"{e.Name}={e.Value}"));
    }

    [Fact]
    public void Internal_connections_and_roles_are_consistent()
    {
        foreach (var s in Lib.All)
        {
            var pins = s.ConnectionPoints.Select(p => p.Id).ToHashSet();
            Assert.All(s.InternalConnections, c => Assert.True(pins.Contains(c.A) && pins.Contains(c.B), s.Id));
            if (s.Role == SymbolRole.Graphic)
            {
                Assert.Equal(string.Empty, s.ReferencePrefix);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(s.ReferencePrefix), s.Id);
            }
        }
        Assert.Equal(SymbolRole.Main, Lib.Find(SymbolIds.Coil)!.Role);
        Assert.Equal(SymbolRole.Contact, Lib.Find(SymbolIds.Contactor)!.Role);
        Assert.Equal(SymbolRole.Contact, Lib.Find(SymbolIds.NormallyOpen)!.Role);
        Assert.Equal(PinFunction.Coil, Lib.Find(SymbolIds.Coil)!.ConnectionPoints[0].Function);
        Assert.Equal(PinFunction.ProtectiveEarth, Lib.Find("iec.motor.3ph.pe")!.ConnectionPoints.Single(p => p.Id == "PE").Function);
    }

    [Fact]
    public void Invalid_symbols_are_skipped_with_warning_and_rest_load()
    {
        const string json = """
            {
              "formatVersion": 1, "library": "Test", "version": "0.1",
              "symbols": [
                { "id": "u.ok", "name": "İyi", "category": "Kullanıcı/Deneme", "referencePrefix": "U",
                  "primitives": [ { "kind": "polygon", "points": [0,0, 5,0, 5,5] } ],
                  "pins": [ { "id": "1", "x": 0, "y": -5, "direction": "up", "function": "input" } ] },
                { "id": "u.dup", "name": "Çift pin", "pins": [ { "id": "1", "direction": "up" }, { "id": "1", "direction": "down" } ] },
                { "id": "u.nopins", "name": "Pinsiz" },
                { "name": "Kimliksiz", "pins": [ { "id": "1" } ] },
                { "id": "u.badconn", "name": "Bağlantı", "pins": [ { "id": "1" } ], "internalConnections": [ { "a": "1", "b": "9" } ] },
                { "id": "u.badkind", "name": "Bilinmeyen şekil", "primitives": [ { "kind": "spline" } ], "pins": [ { "id": "1" } ] }
              ]
            }
            """;
        var warnings = new List<string>();
        var result = SymbolLibraryLoader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), "test.json", warnings, userDefined: true);
        var ok = Assert.Single(result);
        Assert.Equal("u.ok", ok.Id);
        Assert.True(ok.IsUserDefined);
        Assert.Equal("Test 0.1", ok.Library);
        Assert.Equal(PinFunction.Input, ok.ConnectionPoints[0].Function);
        Assert.IsType<PolygonPrimitive>(ok.Primitives[0]);
        Assert.Equal(5, warnings.Count);
    }

    [Fact]
    public void Corrupt_file_gives_warning_not_exception()
    {
        var warnings = new List<string>();
        var result = SymbolLibraryLoader.Read(new MemoryStream("{ bozuk"u8.ToArray()), "bad.json", warnings);
        Assert.Empty(result);
        Assert.Single(warnings);
    }

    [Fact]
    public void User_directory_adds_symbols_but_cannot_override_built_in_ids()
    {
        var dir = Directory.CreateTempSubdirectory("myg-symbols-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "benim.json"), $$"""
                { "formatVersion": 1, "library": "Benim", "symbols": [
                  { "id": "user.valve", "name": "Özel valf", "category": "Kullanıcı", "pins": [ { "id": "1", "x": 0, "y": -10, "direction": "up" } ] },
                  { "id": "{{SymbolIds.Coil}}", "name": "Sahte bobin", "pins": [ { "id": "X", "direction": "up" } ] }
                ] }
                """);
            var catalog = BuiltInSymbolLibrary.CreateCatalog(dir);
            Assert.NotNull(catalog.Find("user.valve"));
            Assert.Equal("Bobin", catalog.Find(SymbolIds.Coil)!.Name);
            Assert.Equal(Lib.All.Count + 1, catalog.All.Count);
            Assert.Contains(catalog.Warnings, w => w.Contains(SymbolIds.Coil, StringComparison.Ordinal));
            Assert.Contains("Kullanıcı", catalog.Categories);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_user_directory_is_fine()
    {
        var catalog = BuiltInSymbolLibrary.CreateCatalog(Path.Combine(Path.GetTempPath(), "yok-" + Guid.NewGuid()));
        Assert.Equal(Lib.All.Count, catalog.All.Count);
        Assert.Empty(catalog.Warnings);
    }

    [Fact]
    public void Symbol_definition_roundtrips_through_json()
    {
        foreach (var s in Lib.All)
        {
            var json = JsonSerializer.Serialize(s, SymbolLibraryLoader.JsonOptions);
            var back = JsonSerializer.Deserialize<SymbolDefinition>(json, SymbolLibraryLoader.JsonOptions)!;
            Assert.Equal(s.Primitives, back.Primitives);
            Assert.Equal(s.InternalConnections, back.InternalConnections);
            Assert.Equal(s.ConnectionPoints.Select(p => (p.Id, p.X, p.Y, p.Direction, p.Function, p.Description)),
                back.ConnectionPoints.Select(p => (p.Id, p.X, p.Y, p.Direction, p.Function, p.Description)));
        }
    }
}
