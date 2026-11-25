using System.Globalization;
using MYG.Electrical.Core.Model;
using MYG.Electrical.Core.Pages;

namespace MYG.Electrical.Core.Editing;

/// <summary>IEC 81346 tarzı cihaz tanımı üretir: -K1, -K2, -F1...</summary>
public static class ReferenceGenerator
{
    public const string Separator = "-";

    public static string Next(string prefix, IEnumerable<Page> pages)
    {
        var head = Separator + prefix;
        var max = 0;
        foreach (var d in pages.SelectMany(p => p.Objects).OfType<DeviceObject>())
        {
            if (d.Reference.StartsWith(head, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(d.Reference.AsSpan(head.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                max = Math.Max(max, n);
            }
        }
        return head + (max + 1).ToString(CultureInfo.InvariantCulture);
    }
}
