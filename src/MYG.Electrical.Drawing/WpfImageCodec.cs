using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MYG.Electrical.Core.Imaging;

namespace MYG.Electrical.Drawing;

/// <summary>WPF görüntüleme API'si ile PNG/JPG/BMP/GIF/TIFF çözme ve PNG kodlama.</summary>
public sealed class WpfImageCodec : IImageCodec
{
    private const int BytesPerPixel = 4;
    private const double Dpi = 96;

    public static WpfImageCodec Instance { get; } = new();

    public DecodedImage Decode(byte[] fileData) => FromBitmap(LoadBitmap(fileData));

    public byte[] EncodePng(DecodedImage image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(ToBitmap(image)));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Dosya verisinden donmuş (thread-safe) bitmap.</summary>
    public static BitmapSource LoadBitmap(byte[] fileData)
    {
        using var ms = new MemoryStream(fileData, writable: false);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public static DecodedImage FromBitmap(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = bgra.PixelWidth * BytesPerPixel;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        return new DecodedImage(bgra.PixelWidth, bgra.PixelHeight, pixels);
    }

    public static BitmapSource ToBitmap(DecodedImage image)
    {
        var bmp = BitmapSource.Create(image.Width, image.Height, Dpi, Dpi, PixelFormats.Bgra32, null,
            image.Bgra, image.Width * BytesPerPixel);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Önizleme için en uzun kenarı <paramref name="maxSize"/> pikseli geçmeyecek şekilde küçültür.</summary>
    public static DecodedImage Downscale(BitmapSource source, int maxSize)
    {
        var scale = Math.Min(1.0, (double)maxSize / Math.Max(source.PixelWidth, source.PixelHeight));
        if (scale >= 1.0)
        {
            return FromBitmap(source);
        }
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return FromBitmap(scaled);
    }
}
