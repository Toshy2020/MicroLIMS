namespace MicroLIMS.Application.Services.Sso;

/// <summary>
/// Configuration bound from the <c>Sso</c> configuration section.
/// </summary>
public sealed class SsoOptions
{
    public const string SectionName = "Sso";

    public OidcOptions Oidc { get; set; } = new();
    public LdapOptions Ldap { get; set; } = new();
}

/// <summary>
/// OpenID Connect provider configuration bound from <c>Sso:Oidc</c>.
/// </summary>
public sealed class OidcOptions
{
    public const string DefaultUsernameClaim = "preferred_username";

    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string UsernameClaim { get; set; } = DefaultUsernameClaim;
    public bool RequireMfaClaim { get; set; }

    // The callback address registered with the provider. Set it when MicroLIMS runs
    // behind a reverse proxy; when blank the address is built from the request.
    public string? RedirectUri { get; set; }
}

/// <summary>
/// Active Directory (LDAP) configuration bound from <c>Sso:Ldap</c>. The connection is always
/// encrypted: LDAPS (default, port 636) or StartTLS. A plain connection is refused.
/// </summary>
public sealed class LdapOptions
{
    public const string DefaultUsernameAttribute = "sAMAccountName";
    public const string DefaultSubjectAttribute = "objectGUID";
    public const int DefaultPort = 636;
    public const int DefaultTimeoutSeconds = 10;

    public string? Host { get; set; }
    public int Port { get; set; } = DefaultPort;

    /// <summary>Upgrade a plain-port connection with StartTLS instead of using LDAPS.</summary>
    public bool UseStartTls { get; set; }

    /// <summary>
    /// Explicit LDAPS switch. Left unset it means LDAPS unless <see cref="UseStartTls"/> is on.
    /// Setting it to false without StartTLS describes a plain connection, which is refused.
    /// </summary>
    public bool? UseLdaps { get; set; }

    public string? BaseDn { get; set; }
    public string? BindDn { get; set; }

    // Secret: never returned, logged, audited or put in a fingerprint.
    public string? BindPassword { get; set; }

    public string UsernameAttribute { get; set; } = DefaultUsernameAttribute;
    public string SubjectAttribute { get; set; } = DefaultSubjectAttribute;
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    public const string PlainConnectionRefused =
        "Sso:Ldap must use LDAPS or StartTLS. A plain (unencrypted) LDAP connection is refused.";

    /// <summary>Problems that make the configuration unusable, each naming the configuration key.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(Host))
            problems.Add("Missing required SSO configuration 'Sso:Ldap:Host'.");
        else if (Host.Contains("://", StringComparison.Ordinal) || Host.Contains('/'))
            problems.Add("'Sso:Ldap:Host' must be a host name or address only, without a scheme or path.");

        if (string.IsNullOrWhiteSpace(BaseDn))
            problems.Add("Missing required SSO configuration 'Sso:Ldap:BaseDn'.");
        if (string.IsNullOrWhiteSpace(BindDn))
            problems.Add("Missing required SSO configuration 'Sso:Ldap:BindDn'.");
        if (string.IsNullOrWhiteSpace(BindPassword))
            problems.Add("Missing required SSO configuration 'Sso:Ldap:BindPassword'.");

        if (Port is < 1 or > 65535)
            problems.Add("'Sso:Ldap:Port' must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(UsernameAttribute))
            problems.Add("Missing required SSO configuration 'Sso:Ldap:UsernameAttribute'.");
        else if (!IsAttributeName(UsernameAttribute))
            problems.Add("'Sso:Ldap:UsernameAttribute' must be a plain LDAP attribute name.");
        if (string.IsNullOrWhiteSpace(SubjectAttribute))
            problems.Add("Missing required SSO configuration 'Sso:Ldap:SubjectAttribute'.");
        else if (!IsAttributeName(SubjectAttribute))
            problems.Add("'Sso:Ldap:SubjectAttribute' must be a plain LDAP attribute name.");
        if (TimeoutSeconds is < 1 or > 120)
            problems.Add("'Sso:Ldap:TimeoutSeconds' must be between 1 and 120.");

        if (UseStartTls && UseLdaps == true)
            problems.Add("Set only one of 'Sso:Ldap:UseStartTls' and 'Sso:Ldap:UseLdaps'.");
        else if (!UseStartTls && UseLdaps == false)
            problems.Add(PlainConnectionRefused);

        return problems;
    }

    // Attribute names go into search filters, so only letters, digits and hyphens are accepted.
    private static bool IsAttributeName(string value)
    {
        var name = value.Trim();
        return name.Length > 0 && char.IsAsciiLetter(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }

    /// <summary>True when the connection starts encrypted (LDAPS); false means StartTLS.</summary>
    public bool EffectiveUseLdaps => !UseStartTls;
}
