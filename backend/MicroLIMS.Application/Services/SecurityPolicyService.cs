using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Validation;

namespace MicroLIMS.Application.Services;

// Reports the effective security values from the constants that enforce them
// (no duplicated numbers), so this page can never disagree with behaviour.
// Read-only: the values are changed in code or configuration, not here.
public class SecurityPolicyService : ISecurityPolicyService
{
    private readonly TimeSpan _accessTokenLifetime;

    public SecurityPolicyService(TimeSpan accessTokenLifetime)
    {
        _accessTokenLifetime = accessTokenLifetime;
    }

    public SecurityPolicyDto GetEffectivePolicy()
    {
        const string passwordPolicySource = "Code constant: PasswordPolicy";
        const string authSource = "Code constant: AuthenticationService";
        const string signatureSource = "Code constant: ElectronicSignatureService";

        var groups = new List<SecurityPolicyGroupDto>
        {
            new("password", "Password policy", new List<SecurityPolicySettingDto>
            {
                new("password.minimumLength", "Minimum length", $"{PasswordPolicy.MinimumLength} characters", passwordPolicySource),
                new("password.complexity", "Required character classes", "Uppercase, lowercase, digit and special character", passwordPolicySource),
                new("password.history", "Password history", $"Last {AuthenticationService.PasswordHistoryLimit} passwords cannot be reused", authSource),
                new("password.mustChange", "Forced password change", "Enforced on the server: every request except change-password, own identity and sign-out is refused until the password is changed", "Code: MustChangePasswordFilter")
            }),
            new("lockout", "Account lockout", new List<SecurityPolicySettingDto>
            {
                new("lockout.maxFailedAttempts", "Failed logins before lockout", $"{AuthenticationService.MaxFailedAttempts} attempts", authSource),
                new("lockout.duration", "Lockout duration", Minutes(AuthenticationService.LockDuration), authSource)
            }),
            new("tokens", "Sessions and tokens", new List<SecurityPolicySettingDto>
            {
                new("tokens.accessLifetime", "Access token lifetime", Minutes(_accessTokenLifetime), "Configuration: Jwt:AccessTokenMinutes"),
                new("tokens.refreshLifetime", "Refresh token lifetime", Days(AuthenticationService.RefreshTokenLifetime), authSource),
                new("tokens.passwordResetLifetime", "Password reset link lifetime", Minutes(AuthenticationService.PasswordResetTokenLifetime), authSource)
            }),
            new("signature", "Electronic signature", new List<SecurityPolicySettingDto>
            {
                new("signature.maxFailedAttempts", "Failed signatures before throttling", $"{ElectronicSignatureService.MaxFailedAttempts} attempts", signatureSource),
                new("signature.window", "Throttle window", Minutes(ElectronicSignatureService.FailedAttemptWindow), signatureSource),
                new("signature.locksAccount", "Locks the login account", "No: further signature attempts are refused, the login is unaffected", signatureSource)
            })
        };

        return new SecurityPolicyDto(true, groups);
    }

    private static string Minutes(TimeSpan span) => $"{(int)span.TotalMinutes} minutes";

    private static string Days(TimeSpan span) => $"{(int)span.TotalDays} days";
}
