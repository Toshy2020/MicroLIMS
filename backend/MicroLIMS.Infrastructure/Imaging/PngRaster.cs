using SkiaSharp;

namespace MicroLIMS.Infrastructure.Imaging;

// Decodes a PNG to 8-bit RGB for embedding in a PDF, compositing any
// transparency over white (the page colour) since PDF image XObjects
// here carry no alpha channel.
public static class PngRaster
{
    public static (int Width, int Height, byte[] Rgb) DecodeToRgb(byte[] png)
    {
        using var decoded = SKBitmap.Decode(png)
            ?? throw new InvalidOperationException("The chart image could not be decoded.");
        using var rgba = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(rgba))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(decoded, 0, 0);
        }

        var px = rgba.GetPixelSpan();
        var rgb = new byte[decoded.Width * decoded.Height * 3];
        for (int src = 0, dst = 0; dst < rgb.Length; src += 4, dst += 3)
        {
            rgb[dst] = px[src];
            rgb[dst + 1] = px[src + 1];
            rgb[dst + 2] = px[src + 2];
        }
        return (decoded.Width, decoded.Height, rgb);
    }
}
