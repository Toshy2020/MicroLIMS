namespace MicroLIMS.Application.Services;

// What a discussion post may carry. Posting is open to every signed-in
// user, and attachments used to be unlimited in number, size (up to the
// server's 30 MB request default, each buffered in memory) and type, with
// the client's own Content-Type stored and served back on download.
//
// Same approach as MaterialDocumentFileValidator: an extension allowlist,
// the file's leading bytes checked against the format where it has a
// signature, and the served content type derived from the extension rather
// than taken from the client.
public static class DiscussionAttachmentPolicy
{
    public const int MaxFiles = 5;
    public const long MaxFileBytes = 10 * 1024 * 1024;

    // Transport ceiling for the whole multipart request: every file at its
    // limit plus room for the text fields.
    public const long MaxRequestBytes = MaxFiles * MaxFileBytes + 1024 * 1024;

    private static readonly byte[] ZipSignature = { 0x50, 0x4B, 0x03, 0x04 }; // docx/xlsx/pptx are ZIP containers
    private static readonly byte[] OleSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // legacy doc/xls

    private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }),
            [".png"] = ("image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }),
            [".jpg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
            [".jpeg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
            [".gif"] = ("image/gif", new[] { "GIF87a"u8.ToArray(), "GIF89a"u8.ToArray() }),
            [".doc"] = ("application/msword", new[] { OleSignature }),
            [".xls"] = ("application/vnd.ms-excel", new[] { OleSignature }),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", new[] { ZipSignature }),
            [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new[] { ZipSignature }),
            [".pptx"] = ("application/vnd.openxmlformats-officedocument.presentationml.presentation", new[] { ZipSignature }),
            // Plain text has no signature; it is served as text/plain with
            // Content-Disposition: attachment, so it is never rendered.
            [".txt"] = ("text/plain", Array.Empty<byte[]>()),
            [".csv"] = ("text/csv", Array.Empty<byte[]>())
        };

    public static string AllowedExtensionsDisplay =>
        string.Join(", ", Allowed.Keys.Select(k => k.TrimStart('.').ToUpperInvariant()));

    // Null when the whole set is acceptable; otherwise the first reason it
    // is not, phrased for the person posting.
    public static string? Validate(IReadOnlyCollection<(string FileName, byte[] Data)> files)
    {
        if (files.Count > MaxFiles)
            return $"A post can carry at most {MaxFiles} attachments; {files.Count} were attached.";

        foreach (var (fileName, data) in files)
        {
            var name = Path.GetFileName(fileName);
            var extension = Path.GetExtension(name);

            if (data.Length == 0)
                return $"'{name}' is empty.";
            if (data.Length > MaxFileBytes)
                return $"'{name}' is larger than the {MaxFileBytes / 1024 / 1024} MB limit for attachments.";
            if (string.IsNullOrEmpty(extension) || !Allowed.TryGetValue(extension, out var format))
                return $"'{name}' is not an allowed attachment type. Allowed types: {AllowedExtensionsDisplay}.";
            if (format.Signatures.Length > 0
                && !format.Signatures.Any(sig => data.Length >= sig.Length && data.AsSpan(0, sig.Length).SequenceEqual(sig)))
                return $"'{name}' does not look like a valid {extension.TrimStart('.').ToUpperInvariant()} file. It may be corrupted or misnamed.";
        }

        return null;
    }

    // The content type the attachment is stored and served with - from the
    // (already validated) extension, never from the client's declaration.
    public static string ContentTypeFor(string fileName) =>
        Allowed.TryGetValue(Path.GetExtension(fileName), out var format) ? format.ContentType : "application/octet-stream";
}
