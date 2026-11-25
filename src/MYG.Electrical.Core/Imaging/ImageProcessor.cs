namespace MYG.Electrical.Core.Imaging;

/// <summary>Saydamlık ayarlarını orijinal resme uygulayıp PNG üretir. Orijinal veri değişmez.</summary>
public static class ImageProcessor
{
    public static byte[] ApplyTransparency(byte[] source, uint keyColor, double tolerance, IImageCodec codec)
    {
        var decoded = codec.Decode(source);
        var pixels = (byte[])decoded.Bgra.Clone();
        ColorKey.Apply(pixels, keyColor, tolerance);
        return codec.EncodePng(decoded with { Bgra = pixels });
    }
}
