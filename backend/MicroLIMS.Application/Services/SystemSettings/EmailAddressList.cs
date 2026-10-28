using System.Net.Mail;

namespace MicroLIMS.Application.Services.SystemSettings;

// A list of email addresses held in one setting value: comma-separated, in
// first-seen order, lower-cased and de-duplicated. Entered text may separate
// addresses with commas, semicolons or white space.
public static class EmailAddressList
{
    public const int DefaultMaxAddresses = 20;
    private const int MaxAddressLength = 254;

    public static IReadOnlyList<string> Parse(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();

    public static bool IsValidAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length > MaxAddressLength) return false;
        if (address.Any(char.IsWhiteSpace) || address.Contains('<') || address.Contains('>')) return false;
        if (!MailAddress.TryCreate(address, out var parsed)) return false;
        // MailAddress accepts "user@localhost" and display-name forms; require a plain user@domain.tld.
        return string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase)
               && parsed.Host.Contains('.');
    }

    // Returns the canonical text to store; throws InvalidOperationException (answered as 400).
    public static string Normalize(string key, string? raw, int maxAddresses)
    {
        var addresses = Parse(raw);
        if (addresses.Count > maxAddresses)
            throw new InvalidOperationException($"'{key}' accepts at most {maxAddresses} addresses.");
        var invalid = addresses.Where(a => !IsValidAddress(a)).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException($"'{key}' contains an invalid email address: {string.Join(", ", invalid.Take(3))}.");
        return string.Join(",", addresses);
    }
}
