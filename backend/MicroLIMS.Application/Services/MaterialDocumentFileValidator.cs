namespace MicroLIMS.Application.Services;

// Server-side file validation for uploaded material documents.
// Extension, declared MIME type, file signature (magic bytes), and size
// are all checked independently. The frontend may pre-validate for UX,
// but this is the authoritative validation point.
//
// No external malware scanner is integrated. The storage architecture is
// designed to allow future scanner insertion between quarantine and final
// storage without changing the service interface.
public class MaterialDocumentFileValidator
{
    private readonly long _maxFileSizeBytes;

    // Supported formats and their expected magic-byte signatures.
    private static readonly Dictionary<string, (string[] MimeTypes, byte[][] Signatures)> AllowedFormats = new()
    {
        [".pdf"]  = (new[] { "application/pdf" },
                     new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }),                     // %PDF
        [".jpg"]  = (new[] { "image/jpeg" },
                     new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),                            // JPEG SOI
        [".jpeg"] = (new[] { "image/jpeg" },
                     new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
        [".png"]  = (new[] { "image/png" },
                     new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }),  // PNG
        [".webp"] = (new[] { "image/webp" },
                     new byte[][] { Array.Empty<byte>() }),                                  // WEBP verified by RIFF header check below
        [".tiff"] = (new[] { "image/tiff" },
                     new[] {
                         new byte[] { 0x49, 0x49, 0x2A, 0x00 },                            // TIFF little-endian
                         new byte[] { 0x4D, 0x4D, 0x00, 0x2A }                             // TIFF big-endian
                     })
    };

    public MaterialDocumentFileValidator(long maxFileSizeBytes)
    {
        _maxFileSizeBytes = maxFileSizeBytes;
    }

    // Returns null on success, or a safe human-readable error message on failure.
    public string? Validate(string originalFileName, string declaredContentType, long sizeBytes, byte[] firstBytes)
    {
        // 1. File size
        if (sizeBytes > _maxFileSizeBytes)
            return $"File exceeds the maximum allowed size of {_maxFileSizeBytes / 1024 / 1024} MB.";

        if (sizeBytes == 0)
            return "The uploaded file is empty.";

        // 2. Extension
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedFormats.ContainsKey(ext))
            return $"File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF.";

        // 3. Declared MIME type
        var (allowedMimes, signatures) = AllowedFormats[ext];
        var normalizedMime = declaredContentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!allowedMimes.Contains(normalizedMime))
            return $"The declared content type '{normalizedMime}' does not match the file extension '{ext}'.";

        // 4. Magic byte / content signature
        if (firstBytes.Length < 3)
            return "The file content could not be validated — it is too short.";

        // Special case: WEBP uses RIFF container. Bytes 0-3 are "RIFF", bytes 8-11 are "WEBP".
        if (ext == ".webp")
        {
            if (firstBytes.Length < 12 ||
                firstBytes[0] != 0x52 || firstBytes[1] != 0x49 || firstBytes[2] != 0x46 || firstBytes[3] != 0x46 ||
                firstBytes[8] != 0x57 || firstBytes[9] != 0x45 || firstBytes[10] != 0x42 || firstBytes[11] != 0x50)
                return "The file does not appear to be a valid WEBP image.";
            return null; // valid
        }

        var signatureMatch = signatures.Any(sig =>
            sig.Length == 0 || // skip (empty = handled above)
            (firstBytes.Length >= sig.Length && sig.SequenceEqual(firstBytes.Take(sig.Length))));

        if (!signatureMatch)
            return $"The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed.";

        return null; // valid
    }
}
