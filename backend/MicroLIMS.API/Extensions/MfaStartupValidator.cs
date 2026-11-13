using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Security;
using MicroLIMS.Application.Services.Mfa;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.API.Extensions;

/// <summary>
/// Fails startup before the host serves requests when MFA is enabled or active/pending
/// credentials exist without a configured encryption key (<c>Mfa:ActiveKeyId</c>).
/// </summary>
public static class MfaStartupValidator
{
    public static async Task EnsureValidAsync(
        IMicroLimsDbContext db,
        MfaPolicyService policyService,
        ISecretProtector protector,
        CancellationToken ct = default)
    {
        if (protector.IsConfigured)
            return;

        var policy = await policyService.GetPolicyAsync(ct);
        var hasCredentials = await db.UserMfaCredentials
            .AsNoTracking()
            .AnyAsync(c => c.Status == MfaCredentialStatus.Active || c.Status == MfaCredentialStatus.Pending, ct);

        if (policy != MfaPolicy.Off || hasCredentials)
        {
            throw new InvalidOperationException(
                "MFA encryption keys must be configured when MFA is enabled or enrolled credentials exist. " +
                "Set 'Mfa:ActiveKeyId' and 'Mfa:EncryptionKeys' (environment variables 'Mfa__ActiveKeyId' and 'Mfa__EncryptionKeys:<keyId>'), " +
                "or set 'security.mfa.policy' to 'Off' before starting.");
        }
    }
}
