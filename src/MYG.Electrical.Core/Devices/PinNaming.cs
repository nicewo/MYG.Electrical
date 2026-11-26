using System.Globalization;
using System.Text.RegularExpressions;

namespace MYG.Electrical.Core.Devices;

/// <summary>Pin adlarının sıralanma biçimi.</summary>
public enum PinSequence
{
    /// <summary>Addan tahmin: I0.0/Q0.0 → 8 bit, X0.0/Y0.0 → 16 bit (Delta AS), X0 → ondalık.</summary>
    Auto,

    /// <summary>X0, X1, ... X9, X10.</summary>
    Decimal,

    /// <summary>X0 ... X7, X10 (Delta DVP, Mitsubishi).</summary>
    Octal,

    /// <summary>I0.0 ... I0.7, I1.0 (Siemens).</summary>
    Byte8,

    /// <summary>X0.0 ... X0.15, X1.0 (Delta AS serisi).</summary>
    Word16,
}

/// <summary>EPLAN gibi ardışık bağlantı tanımı üretir: X0 → X1, I0.7 → I1.0, X0.15 → X1.0.</summary>
public static partial class PinNaming
{
    [GeneratedRegex(@"^(?<prefix>.*?)(?<major>\d+)(?:(?<sep>[.,])(?<minor>\d+))?(?<suffix>\D*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static PinSequence Resolve(string name, PinSequence sequence)
    {
        if (sequence != PinSequence.Auto)
        {
            return sequence;
        }
        var m = Pattern().Match(name.Trim());
        if (!m.Success)
        {
            return PinSequence.Decimal;
        }
        if (!m.Groups["minor"].Success)
        {
            return PinSequence.Decimal;
        }
        var prefix = m.Groups["prefix"].Value.TrimStart('%').ToUpperInvariant();
        // Siemens (I/Q, Almanca E/A) ve IEC 61131 %IX/%QX bayt.bit: 0–7
        return prefix is "I" or "Q" or "E" or "A" or "IX" or "QX" or "IB" or "QB" ? PinSequence.Byte8 : PinSequence.Word16;
    }

    /// <summary>Sıradaki ad. Sayı içermeyen adlarda (L+, M) aynı ad döner; kullanıcı yeniden yazar.</summary>
    public static string Next(string name, PinSequence sequence = PinSequence.Auto)
    {
        var text = name.Trim();
        var m = Pattern().Match(text);
        if (!m.Success || text.Length == 0)
        {
            return text;
        }
        var seq = Resolve(text, sequence);
        var prefix = m.Groups["prefix"].Value;
        var suffix = m.Groups["suffix"].Value;
        var majorText = m.Groups["major"].Value;

        if (m.Groups["minor"].Success && seq is PinSequence.Byte8 or PinSequence.Word16)
        {
            var minorText = m.Groups["minor"].Value;
            var major = long.Parse(majorText, CultureInfo.InvariantCulture);
            var minor = long.Parse(minorText, CultureInfo.InvariantCulture) + 1;
            var bits = seq == PinSequence.Byte8 ? 8 : 16;
            if (minor >= bits)
            {
                minor = 0;
                major++;
            }
            return $"{prefix}{Pad(major, majorText)}{m.Groups["sep"].Value}{Pad(minor, minorText)}{suffix}";
        }

        if (m.Groups["minor"].Success)
        {
            // Noktalı ad ama ondalık/sekizli istendi: son sayı artar
            var minorText = m.Groups["minor"].Value;
            var minor = Increment(minorText, seq);
            return $"{prefix}{majorText}{m.Groups["sep"].Value}{minor}{suffix}";
        }
        return $"{prefix}{Increment(majorText, seq)}{suffix}";
    }

    /// <summary>Sıradaki n ad (çoklu pin yerleştirme, önizleme).</summary>
    public static IEnumerable<string> Sequence(string first, int count, PinSequence sequence = PinSequence.Auto)
    {
        var current = first;
        for (var i = 0; i < count; i++)
        {
            yield return current;
            current = Next(current, sequence);
        }
    }

    private static string Increment(string digits, PinSequence seq)
    {
        if (seq == PinSequence.Octal)
        {
            var value = Convert.ToInt64(digits.TrimStart('0').Length == 0 ? "0" : digits, 8) + 1;
            return Convert.ToString(value, 8).PadLeft(digits.Length, '0');
        }
        return Pad(long.Parse(digits, CultureInfo.InvariantCulture) + 1, digits);
    }

    /// <summary>Baştaki sıfırlar yalnız asıl adda varsa korunur (DI07 → DI08; X0.15 → X1.0).</summary>
    private static string Pad(long value, string original) =>
        original.Length > 1 && original[0] == '0'
            ? value.ToString(CultureInfo.InvariantCulture).PadLeft(original.Length, '0')
            : value.ToString(CultureInfo.InvariantCulture);
}
