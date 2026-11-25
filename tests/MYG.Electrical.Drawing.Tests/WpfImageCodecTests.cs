using MYG.Electrical.Core.Imaging;
using MYG.Electrical.Drawing;

namespace MYG.Electrical.Drawing.Tests;

public class WpfImageCodecTests
{
    [Fact]
    public void Transparency_is_applied_and_survives_png_round_trip() => Sta.Run(() =>
    {
        // 2x1: beyaz + kırmızı
        var original = new DecodedImage(2, 1, [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF]);
        var png = WpfImageCodec.Instance.EncodePng(original);

        var processed = ImageProcessor.ApplyTransparency(png, 0xFFFFFFFF, 5, WpfImageCodec.Instance);
        var decoded = WpfImageCodec.Instance.Decode(processed);

        Assert.Equal(2, decoded.Width);
        Assert.Equal(0u, decoded.PixelAt(0, 0) >> 24);          // beyaz saydam oldu
        Assert.Equal(0xFFFF0000u, decoded.PixelAt(1, 0));       // kırmızı aynen kaldı
    });

    [Fact]
    public void Downscale_limits_longest_side() => Sta.Run(() =>
    {
        var big = WpfImageCodec.ToBitmap(new DecodedImage(400, 100, new byte[400 * 100 * 4]));
        var small = WpfImageCodec.Downscale(big, 100);
        Assert.Equal(100, small.Width);
        Assert.Equal(25, small.Height);
    });
}
