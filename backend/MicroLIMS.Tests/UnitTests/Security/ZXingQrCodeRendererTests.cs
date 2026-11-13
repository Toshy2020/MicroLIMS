using System.Xml.Linq;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Infrastructure.Security;
using SkiaSharp;
using Xunit;
using ZXing;
using ZXing.Common;

namespace MicroLIMS.Tests.UnitTests.Security;

public class ZXingQrCodeRendererTests
{
    private const int PixelsPerModule = 6;
    private readonly IQrCodeRenderer _renderer = new ZXingQrCodeRenderer();

    [Theory]
    [InlineData("otpauth://totp/MicroLIMS:alice?secret=JBSWY3DPEHPK3PXP&issuer=MicroLIMS&algorithm=SHA1&digits=6&period=30")]
    [InlineData("otpauth://totp/MicroLIMS:a%40b.example?secret=ORSXG5A%3D&issuer=MicroLIMS&algorithm=SHA1&digits=6&period=60")]
    public void RenderSvg_DecodesBackToContent(string content)
    {
        var svg = _renderer.RenderSvg(content);

        var doc = XDocument.Parse(svg);
        var root = doc.Root!;
        var viewBox = root.Attribute("viewBox")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotNull(viewBox);
        Assert.Equal(4, viewBox!.Length);

        var width = int.Parse(viewBox[2]);
        var height = int.Parse(viewBox[3]);
        Assert.True(width > 20 && height > 20);

        using var bitmap = new SKBitmap(new SKImageInfo(
            width * PixelsPerModule,
            height * PixelsPerModule,
            SKColorType.Rgba8888,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var blackPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
        foreach (var rect in root.Descendants().Where(e => e.Name.LocalName == "rect"))
        {
            var fill = rect.Attribute("fill")?.Value ?? rect.Parent?.Attribute("fill")?.Value;
            if (!string.Equals(fill, "#000000", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fill, "black", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var x = float.Parse(rect.Attribute("x")?.Value ?? "0");
            var y = float.Parse(rect.Attribute("y")?.Value ?? "0");
            var w = float.Parse(rect.Attribute("width")!.Value);
            var h = float.Parse(rect.Attribute("height")!.Value);
            canvas.DrawRect(
                x * PixelsPerModule,
                y * PixelsPerModule,
                w * PixelsPerModule,
                h * PixelsPerModule,
                blackPaint);
        }

        var pixels = bitmap.GetPixelSpan().ToArray();
        var luminance = new RGBLuminanceSource(
            pixels,
            bitmap.Width,
            bitmap.Height,
            RGBLuminanceSource.BitmapFormat.RGBA32);

        var reader = new BarcodeReaderGeneric
        {
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true
            }
        };

        var decoded = reader.Decode(luminance);
        Assert.NotNull(decoded);
        Assert.Equal(BarcodeFormat.QR_CODE, decoded!.BarcodeFormat);
        Assert.Equal(content, decoded.Text);
    }

    [Fact]
    public void RenderSvg_HasTitleAndNoScript()
    {
        var svg = _renderer.RenderSvg("otpauth://totp/MicroLIMS:test?secret=JBSWY3DPEHPK3PXP&issuer=MicroLIMS&algorithm=SHA1&digits=6&period=30");

        var doc = XDocument.Parse(svg);
        var root = doc.Root;
        Assert.NotNull(root);
        Assert.Equal("svg", root!.Name.LocalName);
        Assert.NotNull(root.Attribute("viewBox"));
        Assert.Null(root.Attribute("width"));
        Assert.Null(root.Attribute("height"));

        var title = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "title");
        Assert.NotNull(title);
        Assert.False(string.IsNullOrWhiteSpace(title!.Value));

        Assert.DoesNotContain(root.Descendants(), e => string.Equals(e.Name.LocalName, "script", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
    }
}
