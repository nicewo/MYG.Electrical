using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MYG.Electrical.Serialization.Tests;

/// <summary>Testler için bağımlılıksız küçük PNG (RGBA8) üretici.</summary>
internal static class TestPng
{
    public static byte[] Create(int width, int height, Func<int, int, uint> argbAt)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filtre: yok
            for (var x = 0; x < width; x++)
            {
                var c = argbAt(x, y);
                raw.WriteByte((byte)(c >> 16));
                raw.WriteByte((byte)(c >> 8));
                raw.WriteByte((byte)c);
                raw.WriteByte((byte)(c >> 24));
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;  // bit derinliği
        ihdr[9] = 6;  // RGBA
        Chunk(png, "IHDR", ihdr);

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(z);
        }
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var c = Crc32([.. typeBytes, .. data]);
        BinaryPrimitives.WriteUInt32BigEndian(len, c);
        s.Write(len);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return ~crc;
    }
}
