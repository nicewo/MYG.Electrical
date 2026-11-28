using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Drc;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Network;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;
using MYG.Electrical.Infrastructure.Serialization;
using MYG.Electrical.Symbols;

namespace MYG.Electrical.Serialization.Tests;

/// <summary>
/// "Kamara Fırın Projesi" örneği (EPLAN'dan yeniden çizim). Örnek dosya <see cref="KamaraFirin"/> üreticisiyle
/// oluşturulur; yeniden üretmek için: <c>MYG_WRITE_SAMPLES=1 dotnet test --project tests/MYG.Electrical.Serialization.Tests</c>.
/// </summary>
public class KamaraFirinTests
{
    private static readonly string SamplePath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", KamaraFirin.FileName);

    private static readonly Lazy<KamaraFirin> Built = new(() => new KamaraFirin());

    [Fact]
    public void Embedded_symbols_are_valid()
    {
        foreach (var symbol in KamaraFirinSymbols.All())
        {
            Assert.Null(SymbolLibraryLoader.Validate(symbol));
        }
    }

    [Fact]
    public void Generated_project_matches_original_structure()
    {
        var k = Built.Value;
        Assert.Equal(17, k.Doc.Pages.Count);
        Assert.Equal(
        [
            "Kapak", "Ana Besleme", "Besleme Dağıtım", "24V Besleme, Ethernet Switch", "Pano", "Panel PC", "PLC Besleme",
            "Motor Sürücüler", "Brülör Ateşleme", "Grup 1 Brülör", "Grup 1 Brülör", "Servo Güç", "Servolar",
            "Saha Analog Input", "Saha Digital Input", "Valfler", "Saha Valfler",
        ], k.Doc.Pages.Select(p => p.Name));
        foreach (var reference in new[] { "-Q1", "-D1", "-DK1", "-F9", "-V1", "-ETH", "-PLC1", "-PLC14", "-S1", "-S2", "-K17", "-K21", "-X29" })
        {
            Assert.NotNull(DeviceRegistry.Find(k.Doc, reference));
        }
        // Bütün röle kontakları kapasiteyle uyumlu, kullanılmayan kontak yok
        var findings = Drc(k.Doc, k.Catalog);
        Assert.DoesNotContain(findings, f => f.Code is "DRC-CONT-01" or "DRC-CONT-02" or "DRC-WIRE-01" or "DRC-LINK-01" or "DRC-PIN-02");
    }

    [Fact]
    public void Only_finding_is_the_duplicate_plc_address_of_the_original()
    {
        // Özgün projede I7.3 hem faz sırası rölesine (sayfa 2) hem kapı 2 sensörüne (sayfa 15) verilmiş: DRC yakalar.
        var findings = Drc(Built.Value.Doc, Built.Value.Catalog);
        var error = Assert.Single(findings, f => f.Severity == DrcSeverity.Error);
        Assert.Equal("DRC-TERM-01", error.Code);
        Assert.Contains("I7.3", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(findings, f => f.Severity == DrcSeverity.Warning);
    }

    [Fact]
    public void Page_links_show_their_partner_like_eplan()
    {
        var k = Built.Value;
        var network = ProjectNetwork.Build(k.Doc, k.Catalog);
        string Target(int page, string name, PinDirection side) =>
            network.LinkDisplayTargets(k.Doc.Pages[page - 1].Objects.OfType<PageLinkObject>().Single(l => l.Name == name && l.Direction == side))
                .Single().ToString();

        Assert.Equal("/3.0", Target(2, "L1", PinDirection.Left));
        Assert.Equal("/3.9", Target(10, "L1.9", PinDirection.Right));
        Assert.Equal("/11.0", Target(10, "N", PinDirection.Left));      // aynı adlı N birçok sayfada: akış yönündeki komşu
        Assert.Equal("/3.9", Target(4, "N", PinDirection.Right));
        Assert.Equal("/4.9", Target(14, "DC24.8", PinDirection.Right));
        Assert.Equal("/15.0", Target(14, "DC24.8", PinDirection.Left));
    }

    [Fact]
    public void Bundled_sample_is_current()
    {
        if (Environment.GetEnvironmentVariable("MYG_WRITE_SAMPLES") is { Length: > 0 })
        {
            File.WriteAllText(SamplePath, ProjectSerializer.Serialize(Built.Value.Doc));
        }
        var json = File.ReadAllText(SamplePath);
        Assert.Contains($"\"formatVersion\": {ProjectDocument.CurrentFormatVersion}", json);
        var doc = ProjectSerializer.Deserialize(json);
        Assert.Equal(json, ProjectSerializer.Serialize(doc));

        // Başka bilgisayarda açılış: gömülü semboller boş kataloğa eklenir, DRC üreticiyle aynı sonucu verir
        var catalog = BuiltInSymbolLibrary.CreateCatalog(null);
        Assert.True(ProjectSymbols.Register(doc, catalog) > 30);
        var expected = Drc(Built.Value.Doc, Built.Value.Catalog).Select(f => f.Code + f.Message).Order();
        Assert.Equal(expected, Drc(doc, catalog).Select(f => f.Code + f.Message).Order());
        Assert.Equal(Signature(Built.Value.Doc), Signature(doc));
    }

    private static IReadOnlyList<DrcFinding> Drc(ProjectDocument doc, ISymbolCatalog catalog) =>
        DrcEngine.Run(doc, ProjectNetwork.Build(doc, catalog), catalog);

    /// <summary>Kimliklerden bağımsız içerik özeti: sayfa adı, nesne sayısı, etiketler ve kablo numaraları.</summary>
    private static string Signature(ProjectDocument doc) => string.Join("\n", doc.Pages.Select(p =>
        $"{p.Name}|{p.Objects.Count}|{string.Join(",", p.Objects.OfType<DeviceObject>().Select(d => d.Reference).Order(StringComparer.Ordinal))}" +
        $"|{string.Join(",", p.WireData.Select(w => w.Number).Order(StringComparer.Ordinal))}"));
}
