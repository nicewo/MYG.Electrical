using MYG.Electrical.Core.AutoWire;
using MYG.Electrical.Core.Editing;
using MYG.Electrical.Core.Geometry;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Core.Symbols;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Serilog;

namespace MYG.Electrical.Infrastructure.Pdf;

/// <summary>
/// Projeyi vektörel PDF'e aktarır. Ekran görüntüsü kullanılmaz; aynı <see cref="SceneComposer"/>
/// kompozisyonu PDF hedefine ayrı bir renderer (<see cref="PdfSceneSink"/>) ile çizilir.
/// </summary>
public sealed class PdfExporter(ISymbolCatalog catalog, ILogger logger)
{
    private readonly AutoWireEngine _wireEngine = new();

    /// <returns>Kullanıcıya gösterilecek uyarılar (ör. sayfa dışına taşan nesneler).</returns>
    public IReadOnlyList<string> Export(ProjectDocument document, string path, PdfExportOptions? options = null)
    {
        using var stream = File.Create(path);
        var warnings = Export(document, stream, options);
        logger.Information("PDF oluşturuldu: {Path} ({Warnings} uyarı)", path, warnings.Count);
        return warnings;
    }

    /// <returns>Kullanıcıya gösterilecek uyarılar (ör. sayfa dışına taşan nesneler).</returns>
    public IReadOnlyList<string> Export(ProjectDocument document, Stream output, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new PdfExportOptions();
        PdfFontResolver.EnsureInstalled();

        var indices = options.PageIndices ?? Enumerable.Range(0, document.Pages.Count).ToList();
        if (indices.Count == 0)
        {
            throw new InvalidOperationException("Dışa aktarılacak sayfa yok.");
        }
        if (indices.Any(i => i < 0 || i >= document.Pages.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Geçersiz sayfa numarası.");
        }
        var warnings = new List<string>();

        var composer = new SceneComposer(catalog, document.FindResource);
        using var pdf = new PdfDocument();
        pdf.Info.Title = document.Project.Name;
        pdf.Info.Author = document.Project.PreparedBy;
        pdf.Info.Subject = document.Project.MachineName;
        pdf.Info.Creator = "MYG Electrical";

        foreach (var index in indices)
        {
            var page = document.Pages[index];
            var outside = page.Objects.Count(o => !page.Bounds.Contains(ObjectBounds.Get(o, catalog)));
            if (outside > 0)
            {
                warnings.Add($"{index + 1}. sayfa '{page.Name}': {outside} nesne sayfa sınırının dışına taşıyor, PDF'te kesilecek.");
            }
            var (paperW, paperH) = options.TargetFormat is { } fmt
                ? PageSizes.Get(fmt, page.Orientation)
                : (page.Width, page.Height);
            var scale = Math.Min(paperW / page.Width, paperH / page.Height);

            var pdfPage = pdf.AddPage();
            pdfPage.Width = XUnit.FromMillimeter(paperW);
            pdfPage.Height = XUnit.FromMillimeter(paperH);

            using var gfx = XGraphics.FromPdfPage(pdfPage);
            gfx.ScaleTransform(GeometryConstants.PointPerMm);
            gfx.TranslateTransform((paperW - page.Width * scale) / 2, (paperH - page.Height * scale) / 2);
            gfx.ScaleTransform(scale);

            var sink = new PdfSceneSink(gfx, logger);
            PageFrameComposer.Compose(document.Project, page, index + 1, document.Pages.Count, sink);
            SceneComposer.ComposeWires(_wireEngine.Compute(page.Objects), sink);
            composer.ComposeObjects(page.Objects, sink, SceneComposerOptions.Print);
        }

        pdf.Save(output);
        return warnings;
    }
}
