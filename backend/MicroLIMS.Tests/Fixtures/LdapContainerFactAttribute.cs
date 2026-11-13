using MicroLIMS.Application.Services.Sso;
using Xunit;

namespace MicroLIMS.Tests.Fixtures;

// Real-directory tests need a containerised LDAP server (see .github/workflows/ci.yml). Without
// MICROLIMS_TEST_LDAP they report Skipped with this reason - a skipped test is not a passing test.
public static class LdapTestConfiguration
{
    public const string EnvironmentVariable = "MICROLIMS_TEST_LDAP";

    public static string? Raw => Environment.GetEnvironmentVariable(EnvironmentVariable);
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Raw);

    public const string SkipReason =
        "MICROLIMS_TEST_LDAP is not set: no containerised LDAP directory available, so this real-directory test did not run.";

    // Semicolon-separated Key=Value pairs, e.g. "Host=localhost;Port=636;BaseDn=...;BindDn=...;BindPassword=...".
    public static IReadOnlyDictionary<string, string> Parse()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (Raw ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i > 0)
                map[part[..i].Trim()] = part[(i + 1)..];
        }
        return map;
    }

    public static LdapOptions ToOptions(bool startTls = false, int? port = null)
    {
        var c = Parse();
        return new LdapOptions
        {
            Host = c["Host"],
            Port = port ?? int.Parse(c["Port"]),
            UseStartTls = startTls,
            BaseDn = c["BaseDn"],
            BindDn = c["BindDn"],
            BindPassword = c["BindPassword"],
            UsernameAttribute = c.GetValueOrDefault("UsernameAttribute", "uid"),
            SubjectAttribute = c.GetValueOrDefault("SubjectAttribute", "entryUUID"),
            TimeoutSeconds = 10
        };
    }
}

public sealed class LdapContainerFactAttribute : FactAttribute
{
    public LdapContainerFactAttribute()
    {
        if (!LdapTestConfiguration.IsConfigured)
            Skip = LdapTestConfiguration.SkipReason;
    }
}
