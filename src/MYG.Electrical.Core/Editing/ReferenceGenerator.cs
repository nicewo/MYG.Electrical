using System.Globalization;
using System.Text.RegularExpressions;
using MYG.Electrical.Core.Devices;
using MYG.Electrical.Core.Pages;
using MYG.Electrical.Core.Symbols;

namespace MYG.Electrical.Core.Editing;

/// <summary>
/// IEC 81346 tarzı cihaz etiketi üretir (-K1, -K2, -F1...). Biçim proje ayarındaki şablondan gelir
/// (<see cref="ProjectSettings.ReferenceFormat"/>): {prefix} sınıf harfi, {n} sıra, {page} sayfa numarası.
/// Yeni numara, aynı biçimdeki en büyük numaranın bir fazlasıdır (silinen numaralar yeniden kullanılmaz).
/// </summary>
public static class ReferenceGenerator
{
    public const string Separator = "-";

    public static string Next(string prefix, ProjectDocument doc, int pageNumber = 1)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }
        var format = string.IsNullOrWhiteSpace(doc.Settings?.ReferenceFormat) ? ProjectSettings.DefaultReferenceFormat : doc.Settings.ReferenceFormat;
        if (!format.Contains("{n}", StringComparison.Ordinal))
        {
            format += "{n}";
        }
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        var pattern = "^" + Regex.Escape(format)
            .Replace(Regex.Escape("{prefix}"), Regex.Escape(prefix), StringComparison.Ordinal)
            .Replace(Regex.Escape("{page}"), Regex.Escape(page), StringComparison.Ordinal)
            .Replace(Regex.Escape("{n}"), "(?<n>[0-9]+)", StringComparison.Ordinal) + "$";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        var max = 0;
        foreach (var reference in DeviceRegistry.UsedReferences(doc))
        {
            var m = regex.Match(reference);
            if (m.Success && int.TryParse(m.Groups["n"].ValueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                max = Math.Max(max, n);
            }
        }
        return format.Replace("{prefix}", prefix, StringComparison.Ordinal)
            .Replace("{page}", page, StringComparison.Ordinal)
            .Replace("{n}", (max + 1).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>Sembol için yeni etiket; etiket almayan (grafik) sembollerde boş.</summary>
    public static string Next(SymbolDefinition symbol, ProjectDocument doc, int pageNumber = 1) =>
        symbol.Role == SymbolRole.Graphic ? string.Empty : Next(symbol.ReferencePrefix, doc, pageNumber);
}
