using System.Text;

namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// RFC 4515 escaping for values placed inside an LDAP search filter.
/// </summary>
public static class LdapFilter
{
    /// <summary>
    /// Escapes <c>\</c>, <c>*</c>, <c>(</c>, <c>)</c>, NUL and every non-ASCII byte (as UTF-8) so
    /// the value can only ever be compared, never interpreted as filter syntax.
    /// </summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length + 8);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if (b is (byte)'\\' or (byte)'*' or (byte)'(' or (byte)')' or 0 || b > 0x7F)
                sb.Append('\\').Append(b.ToString("x2"));
            else
                sb.Append((char)b);
        }

        return sb.ToString();
    }

    /// <summary>Builds <c>(attribute=value)</c> with the value escaped. The attribute name is validated by <see cref="LdapOptions"/>.</summary>
    public static string Equality(string attribute, string? value) => $"({attribute}={Escape(value)})";

    /// <summary>Escapes raw bytes (e.g. a binary <c>objectGUID</c>) for use as a filter value.</summary>
    public static string EscapeBytes(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 3);
        foreach (var b in bytes)
            sb.Append('\\').Append(b.ToString("x2"));
        return sb.ToString();
    }
}
