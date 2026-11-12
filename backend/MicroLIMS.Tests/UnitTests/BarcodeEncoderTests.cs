using MicroLIMS.Application.Interfaces;
using MicroLIMS.Infrastructure.Labels;
using SkiaSharp;
using Xunit;
using ZXing;
using ZXing.Common;

namespace MicroLIMS.Tests.UnitTests;

public class BarcodeEncoderTests
{
    private const int PixelsPerModule = 4;
    private const int WhiteMarginModules = 10;

    private readonly IBarcodeEncoder _encoder = new ZXingBarcodeEncoder();

    public static TheoryData<string> LongDataMatrixPayloads => new()
    {
        "CRY:" + string.Concat(Enumerable.Range(0, 250).Select(i => (char)('A' + (i % 26))))
    };

    [Theory]
    [InlineData("S:FP1026001")]
    [InlineData("EQ:INC-F-ML-F-01-003")]
    public void Code128_round_trips(string payload)
    {
        var modules = _encoder.EncodeCode128(payload);

        using var bitmap = PaintCode128(modules);
        var result = DecodeBitmap(bitmap, BarcodeFormat.CODE_128);

        Assert.NotNull(result);
        Assert.Equal(BarcodeFormat.CODE_128, result.BarcodeFormat);
        Assert.Equal(payload, result.Text);
    }

    [Theory]
    [InlineData("MED:TSA/03/26")]
    [InlineData("SP:MP-VIT-C 05/11/2026")]
    [InlineData("MAT:STK-2026-00123")]
    [MemberData(nameof(LongDataMatrixPayloads))]
    public void DataMatrix_round_trips(string payload)
    {
        var modules = _encoder.EncodeDataMatrix(payload);

        using var bitmap = PaintDataMatrix(modules);
        var result = DecodeBitmap(bitmap, BarcodeFormat.DATA_MATRIX);

        Assert.NotNull(result);
        Assert.Equal(BarcodeFormat.DATA_MATRIX, result.BarcodeFormat);
        Assert.Equal(payload, result.Text);
    }

    [Theory]
    [InlineData("CRY:1")]
    [InlineData("MED:TSA/03/26")]
    [InlineData("MAT:STK-2026-00123")]
    public void DataMatrix_is_square(string payload)
    {
        var modules = _encoder.EncodeDataMatrix(payload);

        Assert.Equal(modules.GetLength(0), modules.GetLength(1));
        Assert.True(modules.GetLength(0) >= 10);
    }

    [Fact]
    public void Code128_has_no_quiet_zone()
    {
        var modules = _encoder.EncodeCode128("S:FP1026001");

        Assert.NotEmpty(modules);
        Assert.True(modules[0]);
        Assert.True(modules[^1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_content_throws_ArgumentException(string? content)
    {
        Assert.Throws<ArgumentException>(() => _encoder.EncodeCode128(content!));
        Assert.Throws<ArgumentException>(() => _encoder.EncodeDataMatrix(content!));
    }

    private static SKBitmap PaintCode128(bool[] modules)
    {
        const int barHeightModules = 15;
        var width = (modules.Length + 2 * WhiteMarginModules) * PixelsPerModule;
        var height = (barHeightModules + 2 * WhiteMarginModules) * PixelsPerModule;

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var blackPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
        var barTop = WhiteMarginModules * PixelsPerModule;
        var barHeight = barHeightModules * PixelsPerModule;

        for (var i = 0; i < modules.Length; i++)
        {
            if (!modules[i]) continue;
            var x = (WhiteMarginModules + i) * PixelsPerModule;
            canvas.DrawRect(x, barTop, PixelsPerModule, barHeight, blackPaint);
        }

        return bitmap;
    }

    private static SKBitmap PaintDataMatrix(bool[,] modules)
    {
        var rows = modules.GetLength(0);
        var cols = modules.GetLength(1);
        var width = (cols + 2 * WhiteMarginModules) * PixelsPerModule;
        var height = (rows + 2 * WhiteMarginModules) * PixelsPerModule;

        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var blackPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                if (!modules[r, c]) continue;
                var x = (WhiteMarginModules + c) * PixelsPerModule;
                var y = (WhiteMarginModules + r) * PixelsPerModule;
                canvas.DrawRect(x, y, PixelsPerModule, PixelsPerModule, blackPaint);
            }
        }

        return bitmap;
    }

    private static Result? DecodeBitmap(SKBitmap bitmap, BarcodeFormat expectedFormat)
    {
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
                PossibleFormats = [expectedFormat],
                TryHarder = true
            }
        };

        return reader.Decode(luminance);
    }
}
