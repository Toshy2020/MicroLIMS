namespace MicroLIMS.API.Extensions;

// The validated JWT settings, resolved once at startup. Both the
// signing side (JwtTokenService, registered in
// ServiceCollectionExtensions) and the validation side (AddJwtBearer in
// Program.cs) read this single instance. They previously read the
// configuration independently and only the validation side carried a
// fallback, so a missing key silently signed with one value and
// validated with another.
public sealed record JwtSettings(string Key, string Issuer, string Audience);

public static class JwtConfiguration
{
    // Never a usable key. It exists only so that a deployment still
    // carrying the old tracked placeholder fails closed instead of
    // starting with a signing key that is public in this repository's
    // Git history. Tolerated in Development so an existing untracked
    // appsettings.Development.json does not break a developer mid-task.
    public const string RejectedDevelopmentKey =
        "DEV_ONLY_INSECURE_SECRET_KEY_CHANGE_IN_PRODUCTION_MIN_32_CHARS";

    // HMAC-SHA256 signs with a 256-bit key. A shorter value is rejected
    // by SymmetricSecurityKey at first use, which would surface as a
    // failed login rather than a failed startup - check it here instead.
    public const int MinimumKeyLength = 32;

    private const string DefaultIssuer = "MicroLIMS";
    private const string DefaultAudience = "MicroLIMS.Client";

    // Throws InvalidOperationException - and therefore aborts startup -
    // rather than returning any fallback. No message includes the
    // configured value.
    public static JwtSettings Resolve(IConfiguration config, IHostEnvironment environment)
    {
        // Jwt__Key is the environment-variable spelling documented in
        // DEPLOYMENT.md. The environment-variable provider already maps
        // it onto Jwt:Key; reading it explicitly also honours a literal
        // "Jwt__Key" entry, which is how the Render deployment is set up.
        var key = config["Jwt:Key"] ?? config["Jwt__Key"];

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "JWT signing key is not configured. Set 'Jwt:Key' (environment variable 'Jwt__Key') to a " +
                $"randomly generated secret of at least {MinimumKeyLength} characters. For local development use " +
                "'dotnet user-secrets set \"Jwt:Key\" \"<secret>\"' or an untracked appsettings.Development.json. " +
                "The application will not start without it.");

        if (!environment.IsDevelopment() && string.Equals(key, RejectedDevelopmentKey, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The configured 'Jwt:Key' is the known development placeholder, which is public in this " +
                $"repository's history and cannot be used in the '{environment.EnvironmentName}' environment. " +
                "Generate a new random secret. The application will not start with this value.");

        if (key.Length < MinimumKeyLength)
            throw new InvalidOperationException(
                $"The configured 'Jwt:Key' is too short - HMAC-SHA256 requires at least {MinimumKeyLength} " +
                "characters. The application will not start with this value.");

        return new JwtSettings(
            key,
            config["Jwt:Issuer"] ?? DefaultIssuer,
            config["Jwt:Audience"] ?? DefaultAudience);
    }
}
