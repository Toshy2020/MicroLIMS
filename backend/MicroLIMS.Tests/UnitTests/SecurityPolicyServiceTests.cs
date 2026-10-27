using System.Text.Json;
using MicroLIMS.Application.Services;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Snapshot of the read-only Security Policy payload: every field asserted, and
// nothing secret in it. A change to a policy constant must show up here.
public class SecurityPolicyServiceTests
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(30);

    [Fact]
    public void EffectivePolicy_MatchesTheReviewedSnapshot()
    {
        var policy = new SecurityPolicyService(AccessLifetime).GetEffectivePolicy();

        Assert.True(policy.IsReadOnly);

        var actual = policy.Groups.SelectMany(g => g.Settings.Select(s => $"{g.Key}|{g.Title}|{s.Key}|{s.Label}|{s.Value}|{s.Source}")).ToList();

        var expected = new[]
        {
            "password|Password policy|password.minimumLength|Minimum length|8 characters|Code constant: PasswordPolicy",
            "password|Password policy|password.complexity|Required character classes|Uppercase, lowercase, digit and special character|Code constant: PasswordPolicy",
            "password|Password policy|password.history|Password history|Last 5 passwords cannot be reused|Code constant: AuthenticationService",
            "password|Password policy|password.mustChange|Forced password change|Enforced on the server: every request except change-password, own identity and sign-out is refused until the password is changed|Code: MustChangePasswordFilter",
            "lockout|Account lockout|lockout.maxFailedAttempts|Failed logins before lockout|5 attempts|Code constant: AuthenticationService",
            "lockout|Account lockout|lockout.duration|Lockout duration|15 minutes|Code constant: AuthenticationService",
            "tokens|Sessions and tokens|tokens.accessLifetime|Access token lifetime|30 minutes|Configuration: Jwt:AccessTokenMinutes",
            "tokens|Sessions and tokens|tokens.refreshLifetime|Refresh token lifetime|7 days|Code constant: AuthenticationService",
            "tokens|Sessions and tokens|tokens.passwordResetLifetime|Password reset link lifetime|60 minutes|Code constant: AuthenticationService",
            "signature|Electronic signature|signature.maxFailedAttempts|Failed signatures before throttling|5 attempts|Code constant: ElectronicSignatureService",
            "signature|Electronic signature|signature.window|Throttle window|15 minutes|Code constant: ElectronicSignatureService",
            "signature|Electronic signature|signature.locksAccount|Locks the login account|No: further signature attempts are refused, the login is unaffected|Code constant: ElectronicSignatureService"
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EffectivePolicy_ContainsNoSecret()
    {
        const string signingKey = "local-test-only-signing-key-0123456789abcdef0123456789abcdef";
        var json = JsonSerializer.Serialize(new SecurityPolicyService(AccessLifetime).GetEffectivePolicy());

        Assert.DoesNotContain(signingKey, json);
        Assert.DoesNotContain("Jwt:Key", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }
}
