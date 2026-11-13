using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;

namespace MicroLIMS.Application.Services.Mfa;

public enum MfaPolicy { Off, Optional, RequiredForRoles, RequiredForAll }

public enum MfaLoginRequirement { None, Verify, EnrolWithinGrace, EnrolNow }

public sealed record MfaRequirement(MfaLoginRequirement Kind, DateTime? EnrolmentDueAtUtc);

/// <summary>
/// Evaluates the configured multi-factor authentication policy, required roles,
/// enrolment grace window, and trusted-device lifetime from system settings.
/// </summary>
public sealed class MfaPolicyService
{
    private readonly ISystemSettingsReader? _settings;

    public MfaPolicyService(ISystemSettingsReader? settings = null)
    {
        _settings = settings;
    }

    public async Task<MfaPolicy> GetPolicyAsync(CancellationToken ct = default)
    {
        var raw = await ReadStringAsync(SystemSettingKeys.MfaPolicy, ct);
        return ParsePolicy(raw);
    }

    public async Task<bool> IsRequiredForRoleAsync(string roleType, CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(ct);
        return await IsRequiredForRoleAsync(policy, roleType, ct);
    }

    public async Task<MfaRequirement> EvaluateAsync(
        string roleType,
        bool hasActiveCredential,
        DateTime? mfaRequiredSinceUtc,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(ct);
        if (policy == MfaPolicy.Off)
            return new MfaRequirement(MfaLoginRequirement.None, null);

        // An enrolled user always verifies unless MFA is switched Off globally.
        if (hasActiveCredential)
            return new MfaRequirement(MfaLoginRequirement.Verify, null);

        if (!await IsRequiredForRoleAsync(policy, roleType, ct))
            return new MfaRequirement(MfaLoginRequirement.None, null);

        var graceDays = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.MfaEnrolmentGraceDays, ct);
        if (graceDays <= 0)
            return new MfaRequirement(MfaLoginRequirement.EnrolNow, null);

        var sinceUtc = mfaRequiredSinceUtc ?? nowUtc;
        var dueAtUtc = sinceUtc.AddDays(graceDays);
        return nowUtc >= dueAtUtc
            ? new MfaRequirement(MfaLoginRequirement.EnrolNow, null)
            : new MfaRequirement(MfaLoginRequirement.EnrolWithinGrace, dueAtUtc);
    }

    public Task<int> GetTrustedDeviceHoursAsync(CancellationToken ct = default) =>
        _settings.GetIntOrDefaultAsync(SystemSettingKeys.MfaTrustedDeviceHours, ct);

    private async Task<bool> IsRequiredForRoleAsync(MfaPolicy policy, string roleType, CancellationToken ct)
    {
        switch (policy)
        {
            case MfaPolicy.Off:
            case MfaPolicy.Optional:
                return false;
            case MfaPolicy.RequiredForAll:
                return true;
            case MfaPolicy.RequiredForRoles:
                if (string.IsNullOrWhiteSpace(roleType))
                    return false;

                var candidate = roleType.Trim();
                var rawRoles = await ReadStringAsync(SystemSettingKeys.MfaRequiredRoles, ct);
                foreach (var configuredRole in rawRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (string.Equals(configuredRole, candidate, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            default:
                return true;
        }
    }

    private Task<string> ReadStringAsync(string key, CancellationToken ct) =>
        _settings is null
            ? Task.FromResult(SystemSettingDefinitions.Get(key).Default)
            : _settings.GetStringAsync(key, ct);

    private static MfaPolicy ParsePolicy(string? raw)
    {
        var trimmed = raw?.Trim();
        if (string.Equals(trimmed, nameof(MfaPolicy.Off), StringComparison.OrdinalIgnoreCase))
            return MfaPolicy.Off;
        if (string.Equals(trimmed, nameof(MfaPolicy.Optional), StringComparison.OrdinalIgnoreCase))
            return MfaPolicy.Optional;
        if (string.Equals(trimmed, nameof(MfaPolicy.RequiredForRoles), StringComparison.OrdinalIgnoreCase))
            return MfaPolicy.RequiredForRoles;
        // Unknown or malformed values fail closed.
        return MfaPolicy.RequiredForAll;
    }
}
