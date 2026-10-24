using System.Buffers.Binary;

namespace MicroLIMS.Application.Helpers;

// Reads a PNG's pixel size from its IHDR chunk without decoding it, and
// rejects anything that is not a PNG - enough to validate an uploaded
// chart image and to lay it out at the right aspect ratio.
public static class PngHeader
{
    private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static bool TryReadSize(byte[] png, out int width, out int height)
    {
        width = height = 0;
        if (png.Length < 24 || !png.AsSpan(0, 8).SequenceEqual(Signature)) return false;
        if (!png.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return false;
        width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        return width > 0 && height > 0;
    }
}
