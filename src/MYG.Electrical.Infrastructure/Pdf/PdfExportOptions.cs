using MYG.Electrical.Core.Rendering;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Infrastructure.Pdf;

public sealed class PdfExportOptions
{
    /// <summary>Dışa aktarılacak sayfa indeksleri (0 tabanlı). Null ise tüm sayfalar.</summary>
    public IReadOnlyList<int>? PageIndices { get; init; }

    /// <summary>
    /// Çıktı kağıt formatı. Null ise her sayfa kendi boyutunda (1:1) basılır;
    /// verilirse çizim bu formata orantılı ölçeklenir (ör. A3 şema → A4 PDF). Yönelim sayfadan alınır.
    /// </summary>
    public PageFormat? TargetFormat { get; init; }

    /// <summary>Renk şeması (kullanıcı ayarı). Siyah-beyaz baskı için <see cref="ColorScheme.Monochrome"/>.</summary>
    public ColorScheme Colors { get; init; } = ColorScheme.Default;
}
