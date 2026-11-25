using MYG.Electrical.Core.Model;

namespace MYG.Electrical.Core.Rendering;

/// <summary>
/// Şema renkleri (ARGB). Varsayılanlar EPLAN benzeri renkli şemadır; kullanıcı ayarlardan değiştirebilir.
/// Renk yalnızca görsel yardımcıdır: potansiyelin adı, türü ve gerilimi ayrıca saklanır ve gösterilir.
/// </summary>
public sealed record ColorScheme
{
    public uint L1 { get; init; } = 0xFFE53935;
    public uint L2 { get; init; } = 0xFF8D4B32;
    public uint L3 { get; init; } = 0xFF242424;
    public uint N { get; init; } = 0xFF1976D2;
    public uint PE { get; init; } = 0xFF159447;
    public uint DcPositive { get; init; } = 0xFFE53935;
    public uint DcNegative { get; init; } = 0xFF1565C0;
    public uint Signal { get; init; } = 0xFF00A6A6;
    public uint Communication { get; init; } = 0xFF8755C5;

    /// <summary>Potansiyeli belirsiz kablo.</summary>
    public uint Wire { get; init; } = DrawingStyles.WireColor;

    public uint Symbol { get; init; } = 0xFF174EA6;
    public uint Frame { get; init; } = 0xFF315B9B;
    public uint Text { get; init; } = 0xFF252B33;
    public uint Selection { get; init; } = 0xFFFF9800;
    public uint Error { get; init; } = 0xFFD32F2F;

    public static ColorScheme Default { get; } = new();

    /// <summary>Siyah-beyaz baskı için.</summary>
    public static ColorScheme Monochrome { get; } = new()
    {
        L1 = DrawingStyles.Black, L2 = DrawingStyles.Black, L3 = DrawingStyles.Black, N = DrawingStyles.Black,
        PE = DrawingStyles.Black, DcPositive = DrawingStyles.Black, DcNegative = DrawingStyles.Black,
        Signal = DrawingStyles.Black, Communication = DrawingStyles.Black, Wire = DrawingStyles.Black,
        Symbol = DrawingStyles.Black, Frame = DrawingStyles.Black, Text = DrawingStyles.Black,
    };

    public uint ForPotential(PotentialKind? kind) => kind switch
    {
        PotentialKind.L1 => L1,
        PotentialKind.L2 => L2,
        PotentialKind.L3 => L3,
        PotentialKind.N => N,
        PotentialKind.PE => PE,
        PotentialKind.DcPositive => DcPositive,
        PotentialKind.DcNegative => DcNegative,
        PotentialKind.Signal => Signal,
        PotentialKind.Communication => Communication,
        _ => Wire,
    };

    /// <summary>"#RRGGBB" / "#AARRGGBB" çözümü (ayar dosyası için).</summary>
    public static bool TryParseHex(string? text, out uint color)
    {
        color = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var s = text.Trim().TrimStart('#');
        if (s.Length is not (6 or 8) || !uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            return false;
        }
        color = s.Length == 6 ? 0xFF000000 | v : v;
        return true;
    }

    public static string ToHex(uint color) => $"#{color & 0xFFFFFF:X6}";
}

/// <summary>Potansiyel ve sayfa bağlantısı işaretlerinin ölçüleri (mm).</summary>
public static class ConnectorGeometry
{
    public const double NameHeight = 3.0;
    public const double PotentialHeight = 4.0;
    public const double PotentialBarHalf = 2.5;
    public const double LinkArrowLength = 4.0;
    public const double LinkArrowHalf = 2.0;
    public const double CrossReferenceHeight = 2.5;
    public const double WireNumberHeight = 2.2;
}
