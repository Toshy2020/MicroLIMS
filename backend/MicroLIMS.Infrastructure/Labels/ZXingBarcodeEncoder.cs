using MicroLIMS.Application.Interfaces;
using ZXing;
using ZXing.Common;
using ZXing.Datamatrix;
using ZXing.Datamatrix.Encoder;
using ZXing.OneD;

namespace MicroLIMS.Infrastructure.Labels;

public sealed class ZXingBarcodeEncoder : IBarcodeEncoder
{
    public bool[] EncodeCode128(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Barcode content cannot be null, empty, or whitespace.", nameof(content));
        }

        var writer = new Code128Writer();
        var options = new Code128EncodingOptions
        {
            Width = 1,
            Height = 1,
            Margin = 0
        };

        var matrix = writer.encode(content, BarcodeFormat.CODE_128, 1, 1, options.Hints);
        return ExtractSingleRowWithoutQuietZone(matrix);
    }

    public bool[,] EncodeDataMatrix(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Barcode content cannot be null, empty, or whitespace.", nameof(content));
        }

        var writer = new DataMatrixWriter();
        var options = new DatamatrixEncodingOptions
        {
            Width = 1,
            Height = 1,
            Margin = 0,
            SymbolShape = SymbolShapeHint.FORCE_SQUARE
        };

        var matrix = writer.encode(content, BarcodeFormat.DATA_MATRIX, 1, 1, options.Hints);
        return ExtractMatrixWithoutQuietZone(matrix);
    }

    private static bool[] ExtractSingleRowWithoutQuietZone(BitMatrix matrix)
    {
        var left = 0;
        while (left < matrix.Width && !matrix[left, 0])
        {
            left++;
        }

        var right = matrix.Width - 1;
        while (right >= left && !matrix[right, 0])
        {
            right--;
        }

        if (left > right)
        {
            throw new InvalidOperationException("Encoded Code 128 matrix contains no dark modules.");
        }

        var modules = new bool[right - left + 1];
        for (var i = 0; i < modules.Length; i++)
        {
            modules[i] = matrix[left + i, 0];
        }

        return modules;
    }

    private static bool[,] ExtractMatrixWithoutQuietZone(BitMatrix matrix)
    {
        var minX = matrix.Width;
        var minY = matrix.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < matrix.Height; y++)
        {
            for (var x = 0; x < matrix.Width; x++)
            {
                if (!matrix[x, y])
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            throw new InvalidOperationException("Encoded Data Matrix contains no dark modules.");
        }

        var rows = maxY - minY + 1;
        var cols = maxX - minX + 1;
        var modules = new bool[rows, cols];

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                modules[r, c] = matrix[minX + c, minY + r];
            }
        }

        return modules;
    }
}
