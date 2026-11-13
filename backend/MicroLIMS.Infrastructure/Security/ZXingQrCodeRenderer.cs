using System.Globalization;
using System.Text;
using MicroLIMS.Application.Abstractions.Security;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace MicroLIMS.Infrastructure.Security;

/// <summary>
/// Renders QR codes as self-contained black-on-white SVGs using ZXing.Net (error correction M).
/// </summary>
public sealed class ZXingQrCodeRenderer : IQrCodeRenderer
{
    private const int QuietZoneModules = 4;

    public string RenderSvg(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("QR code content cannot be null, empty, or whitespace.", nameof(content));

        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M,
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
            [EncodeHintType.MARGIN] = QuietZoneModules
        };

        var writer = new QRCodeWriter();
        BitMatrix matrix = writer.encode(content, BarcodeFormat.QR_CODE, 0, 0, hints);

        var width = matrix.Width;
        var height = matrix.Height;

        var sb = new StringBuilder(capacity: width * height / 2);
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" role=\"img\" aria-label=\"Authenticator QR code\">");
        sb.Append("<title>Authenticator QR code</title>");
        sb.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"0\" y=\"0\" width=\"{width}\" height=\"{height}\" fill=\"#ffffff\"/>");
        sb.Append("<g fill=\"#000000\">");

        for (var y = 0; y < height; y++)
        {
            var x = 0;
            while (x < width)
            {
                if (!matrix[x, y])
                {
                    x++;
                    continue;
                }

                var runStart = x;
                while (x < width && matrix[x, y])
                    x++;

                var runWidth = x - runStart;
                sb.Append(CultureInfo.InvariantCulture,
                    $"<rect x=\"{runStart}\" y=\"{y}\" width=\"{runWidth}\" height=\"1\"/>");
            }
        }

        sb.Append("</g></svg>");
        return sb.ToString();
    }
}
