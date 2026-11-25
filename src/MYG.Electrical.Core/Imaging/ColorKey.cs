namespace MYG.Electrical.Core.Imaging;

/// <summary>
/// Renk anahtarlama (saydamlık): seçilen renge yakın pikselleri tamamen saydam yapar.
/// Piksel verisi BGRA32 (B, G, R, A sırasıyla, piksel başına 4 bayt).
/// </summary>
public static class ColorKey
{
    public const double MinTolerance = 0;
    public const double MaxTolerance = 100;
    private const int BytesPerPixel = 4;

    /// <summary>RGB uzayındaki en büyük uzaklık (siyah ↔ beyaz).</summary>
    private static readonly double MaxDistance = Math.Sqrt(3 * 255.0 * 255.0);

    /// <summary>
    /// <paramref name="tolerance"/> 0 → yalnızca birebir aynı renk; 100 → tüm pikseller.
    /// Değer, RGB uzaklığının en büyük uzaklığa oranı olarak yorumlanır.
    /// </summary>
    /// <returns>Saydam yapılan piksel sayısı.</returns>
    public static int Apply(Span<byte> bgra, uint keyColorArgb, double tolerance)
    {
        if (bgra.Length % BytesPerPixel != 0)
        {
            throw new ArgumentException("Piksel verisi BGRA32 olmalı (uzunluk 4'ün katı).", nameof(bgra));
        }
        var t = Math.Clamp(tolerance, MinTolerance, MaxTolerance) / MaxTolerance;
        var limit = t * MaxDistance;
        var limitSquared = limit * limit;

        var kr = (int)((keyColorArgb >> 16) & 0xFF);
        var kg = (int)((keyColorArgb >> 8) & 0xFF);
        var kb = (int)(keyColorArgb & 0xFF);

        var count = 0;
        for (var i = 0; i < bgra.Length; i += BytesPerPixel)
        {
            var db = bgra[i] - kb;
            var dg = bgra[i + 1] - kg;
            var dr = bgra[i + 2] - kr;
            if (db * db + dg * dg + dr * dr <= limitSquared)
            {
                bgra[i] = 0;
                bgra[i + 1] = 0;
                bgra[i + 2] = 0;
                bgra[i + 3] = 0;
                count++;
            }
        }
        return count;
    }

    /// <summary>BGRA32 tampondan (x, y) pikselinin rengini ARGB olarak okur.</summary>
    public static uint PixelAt(ReadOnlySpan<byte> bgra, int width, int x, int y)
    {
        var i = (y * width + x) * BytesPerPixel;
        return ((uint)bgra[i + 3] << 24) | ((uint)bgra[i + 2] << 16) | ((uint)bgra[i + 1] << 8) | bgra[i];
    }
}
