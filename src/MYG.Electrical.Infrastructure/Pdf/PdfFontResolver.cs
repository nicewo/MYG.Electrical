using PdfSharp.Fonts;

namespace MYG.Electrical.Infrastructure.Pdf;

/// <summary>
/// PDFsharp (Core) için sistem fontlarını bulan çözümleyici. Windows'ta Arial, Linux'ta Liberation Sans / DejaVu Sans kullanılır.
/// Türkçe karakterler (ç, ğ, ı, ö, ş, ü) bu fontlarda mevcuttur.
/// </summary>
public sealed class PdfFontResolver : IFontResolver
{
    public const string FamilyName = "MYG Sans";
    private const string RegularFace = "myg-regular";
    private const string BoldFace = "myg-bold";

    private static readonly string[] RegularCandidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    ];

    private static readonly string[] BoldCandidates =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arialbd.ttf"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeuib.ttf"),
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ];

    private static readonly Lock InstallLock = new();

    /// <summary>Çözümleyiciyi bir kez global olarak kaydeder.</summary>
    public static void EnsureInstalled()
    {
        lock (InstallLock)
        {
            if (GlobalFontSettings.FontResolver is not PdfFontResolver)
            {
                GlobalFontSettings.FontResolver = new PdfFontResolver();
            }
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? BoldFace : RegularFace);

    public byte[]? GetFont(string faceName)
    {
        var candidates = faceName == BoldFace ? BoldCandidates.Concat(RegularCandidates) : RegularCandidates;
        var path = candidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("PDF için uygun bir TrueType font bulunamadı (Arial / Liberation Sans / DejaVu Sans).");
        return File.ReadAllBytes(path);
    }
}
