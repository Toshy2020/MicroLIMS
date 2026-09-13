namespace MicroLIMS.Infrastructure.Storage;

// Turns whatever a record persisted into the provider-neutral key its file
// lives under. Rows written before keys were made relative hold the
// base-path-combined location - "storage/documents/2/2_controlledpdf.pdf",
// or "storage\documents/2/..." when written on Windows - so the base path
// is stripped when present. Keys that are already relative pass through.
public static class StorageKey
{
    public static string Normalize(string storedValue, string? legacyBasePath)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
            throw new ArgumentException("A storage key is required.", nameof(storedValue));

        var key = ToForwardSlashes(storedValue);

        if (!string.IsNullOrWhiteSpace(legacyBasePath))
        {
            var prefix = ToForwardSlashes(legacyBasePath).TrimEnd('/') + "/";
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                key = key[prefix.Length..];
        }

        key = key.TrimStart('/');

        // Keys are generated server-side, but a ".." segment would let a
        // local implementation read outside its base directory.
        if (key.Length == 0 || key.Split('/').Any(segment => segment == ".."))
            throw new ArgumentException($"'{storedValue}' is not a valid storage key.", nameof(storedValue));

        return key;
    }

    private static string ToForwardSlashes(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        return normalized;
    }
}
