namespace MYG.Electrical.Core.Imaging;

/// <summary>Çözülmüş resim: BGRA32 piksel verisi.</summary>
public sealed record DecodedImage(int Width, int Height, byte[] Bgra)
{
    public uint PixelAt(int x, int y) => ColorKey.PixelAt(Bgra, Width, x, y);
}

/// <summary>Resim çözme/kodlama. Platforma özgüdür (WPF uygulaması Drawing katmanında sağlar).</summary>
public interface IImageCodec
{
    DecodedImage Decode(byte[] fileData);
    byte[] EncodePng(DecodedImage image);
}
