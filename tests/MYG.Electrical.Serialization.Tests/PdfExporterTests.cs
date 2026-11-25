using System.Text;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Infrastructure.Pdf;
using MYG.Electrical.Symbols;
using PdfSharp.Pdf.IO;
using Serilog;

namespace MYG.Electrical.Serialization.Tests;

public class PdfExporterTests
{
    private static readonly PdfExporter Exporter = new(BuiltInSymbolLibrary.Instance, new LoggerConfiguration().CreateLogger());
    private const double MmTolerance = 0.5;

    private static PdfSharp.Pdf.PdfDocument ExportAndRead(ProjectDocument doc, PdfExportOptions? options = null)
    {
        using var ms = new MemoryStream();
        Exporter.Export(doc, ms, options);
        var bytes = ms.ToArray();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        return PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
    }

    [Fact]
    public void Exports_all_pages_with_real_size()
    {
        var doc = ProjectSerializerTests.SampleProject();
        doc.Pages.Add(Page.Create("A4 dikey", PageType.Schematic, PageFormat.A4, PageOrientation.Portrait));

        using var pdf = ExportAndRead(doc);

        Assert.Equal(3, pdf.PageCount);
        Assert.Equal(420, pdf.Pages[0].Width.Millimeter, MmTolerance);
        Assert.Equal(297, pdf.Pages[0].Height.Millimeter, MmTolerance);
        Assert.Equal(210, pdf.Pages[2].Width.Millimeter, MmTolerance);
        Assert.Equal(297, pdf.Pages[2].Height.Millimeter, MmTolerance);
    }

    [Fact]
    public void Exports_single_page_scaled_to_target_format()
    {
        var doc = ProjectSerializerTests.SampleProject();

        using var pdf = ExportAndRead(doc, new PdfExportOptions { PageIndices = [1], TargetFormat = PageFormat.A4 });

        Assert.Equal(1, pdf.PageCount);
        Assert.Equal(297, pdf.Pages[0].Width.Millimeter, MmTolerance);
        Assert.Equal(210, pdf.Pages[0].Height.Millimeter, MmTolerance);
    }

    [Fact]
    public void Exports_every_page_size()
    {
        var doc = new ProjectDocument();
        foreach (var f in Enum.GetValues<PageFormat>())
        {
            foreach (var o in Enum.GetValues<PageOrientation>())
            {
                doc.Pages.Add(Page.Create($"{f} {o}", PageType.Schematic, f, o));
            }
        }

        using var pdf = ExportAndRead(doc);

        Assert.Equal(8, pdf.PageCount);
    }

    [Fact]
    public void Empty_selection_is_rejected()
    {
        var doc = ProjectDocument.CreateNew();
        Assert.Throws<InvalidOperationException>(() => Exporter.Export(doc, new MemoryStream(), new PdfExportOptions { PageIndices = [] }));
    }
}
