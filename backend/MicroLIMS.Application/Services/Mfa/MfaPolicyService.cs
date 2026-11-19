using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Mfa;

public enum MfaPolicy { Off, Optional, RequiredForRoles, RequiredForAll }

public enum MfaLoginRequirement { None, Verify, EnrolWithinGrace, EnrolNow }

public enum MfaUserState { NotRequired, Enrolled, DueByDate, Overdue, TokenPendingConfirmation }

public sealed record MfaRequirement(MfaLoginRequirement Kind, DateTime? EnrolmentDueAtUtc);

/// <summary>
/// Pre-loaded MFA policy settings so list queries can evaluate every user without re-querying settings.
/// </summary>
public sealed record MfaPolicySnapshot(MfaPolicy Policy, string RequiredRolesRaw, int GraceDays)
{
    public bool IsRequiredForRole(string roleType) =>
        MfaPolicyService.IsRequiredForRole(Policy, RequiredRolesRaw, roleType);

    public MfaUserState ComputeUserState(
        string roleType,
        bool hasActiveCredential,
        bool hasPendingHardwareToken,
        DateTime? mfaRequiredSinceUtc,
        DateTime nowUtc)
    {
        if (hasActiveCredential)
            return MfaUserState.Enrolled;

        if (hasPendingHardwareToken)
            return MfaUserState.TokenPendingConfirmation;

        if (!IsRequiredForRole(roleType))
            return MfaUserState.NotRequired;

        return MfaPolicyService.IsWithinGrace(GraceDays, mfaRequiredSinceUtc, nowUtc, out _)
            ? MfaUserState.DueByDate
            : MfaUserState.Overdue;
    }
}

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

    public async Task<MfaPolicySnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(ct);
        var requiredRolesRaw = policy == MfaPolicy.RequiredForRoles
            ? await ReadStringAsync(SystemSettingKeys.MfaRequiredRoles, ct)
            : string.Empty;
        var graceDays = await ReadIntAsync(SystemSettingKeys.MfaEnrolmentGraceDays, ct);
        return new MfaPolicySnapshot(policy, requiredRolesRaw, graceDays);
    }

    public async Task<MfaUserState> GetUserStateAsync(
        string roleType,
        bool hasActiveCredential,
        bool hasPendingHardwareToken,
        DateTime? mfaRequiredSinceUtc,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        if (hasActiveCredential)
            return MfaUserState.Enrolled;

        if (hasPendingHardwareToken)
            return MfaUserState.TokenPendingConfirmation;

        var policy = await GetPolicyAsync(ct);
        if (!await IsRequiredForRoleAsync(policy, roleType, ct))
            return MfaUserState.NotRequired;

        var graceDays = await ReadIntAsync(SystemSettingKeys.MfaEnrolmentGraceDays, ct);
        return IsWithinGrace(graceDays, mfaRequiredSinceUtc, nowUtc, out _)
            ? MfaUserState.DueByDate
            : MfaUserState.Overdue;
    }

    /// <summary>
    /// Resolves <see cref="MfaUserState"/> for a batch of users using one settings read
    /// and one <c>AsNoTracking</c> credential query.
    /// </summary>
    public async Task<Dictionary<int, MfaUserState>> GetUserStatesAsync(
        IMicroLimsDbContext db,
        IEnumerable<(int UserId, string RoleType, DateTime? MfaRequiredSinceUtc)> users,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var userList = users as IReadOnlyCollection<(int UserId, string RoleType, DateTime? MfaRequiredSinceUtc)> ?? users.ToList();
        var states = new Dictionary<int, MfaUserState>(userList.Count);
        if (userList.Count == 0)
            return states;

        var snapshot = await GetSnapshotAsync(ct);
        var userIds = userList.Select(u => u.UserId).Distinct().ToList();
        var credentials = await db.UserMfaCredentials
            .AsNoTracking()
            .Where(c => userIds.Contains(c.UserId)
                        && (c.Status == MfaCredentialStatus.Active
                            || (c.Status == MfaCredentialStatus.Pending && c.Type == MfaCredentialType.HardwareToken)))
            .Select(c => new { c.UserId, c.Status })
            .ToListAsync(ct);

        var activeIds = credentials.Where(c => c.Status == MfaCredentialStatus.Active).Select(c => c.UserId).ToHashSet();
        var pendingHardwareTokenIds = credentials.Where(c => c.Status == MfaCredentialStatus.Pending).Select(c => c.UserId).ToHashSet();

        foreach (var u in userList)
        {
            states[u.UserId] = snapshot.ComputeUserState(
                u.RoleType,
                activeIds.Contains(u.UserId),
                pendingHardwareTokenIds.Contains(u.UserId),
                u.MfaRequiredSinceUtc,
                nowUtc);
        }

        return states;
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

        var graceDays = await ReadIntAsync(SystemSettingKeys.MfaEnrolmentGraceDays, ct);
        return IsWithinGrace(graceDays, mfaRequiredSinceUtc, nowUtc, out var dueAtUtc)
            ? new MfaRequirement(MfaLoginRequirement.EnrolWithinGrace, dueAtUtc)
            : new MfaRequirement(MfaLoginRequirement.EnrolNow, null);
    }

    public Task<int> GetTrustedDeviceHoursAsync(CancellationToken ct = default) =>
        ReadIntAsync(SystemSettingKeys.MfaTrustedDeviceHours, ct);

    public Task<int> GetEnrolmentGraceDaysAsync(CancellationToken ct = default) =>
        ReadIntAsync(SystemSettingKeys.MfaEnrolmentGraceDays, ct);

    public static bool IsRequiredPolicy(MfaPolicy policy) =>
        policy is MfaPolicy.RequiredForRoles or MfaPolicy.RequiredForAll;

    public static bool IsRequiredForRole(MfaPolicy policy, string rawRoles, string roleType)
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

    internal static bool IsWithinGrace(int graceDays, DateTime? mfaRequiredSinceUtc, DateTime nowUtc, out DateTime? dueAtUtc)
    {
        if (graceDays <= 0)
        {
            dueAtUtc = null;
            return false;
        }

        var sinceUtc = mfaRequiredSinceUtc ?? nowUtc;
        var due = sinceUtc.AddDays(graceDays);
        if (nowUtc >= due)
        {
            dueAtUtc = null;
            return false;
        }

        dueAtUtc = due;
        return true;
    }

    private async Task<bool> IsRequiredForRoleAsync(MfaPolicy policy, string roleType, CancellationToken ct)
    {
        if (policy != MfaPolicy.RequiredForRoles)
            return IsRequiredForRole(policy, string.Empty, roleType);

        var rawRoles = await ReadStringAsync(SystemSettingKeys.MfaRequiredRoles, ct);
        return IsRequiredForRole(policy, rawRoles, roleType);
    }

    private Task<int> ReadIntAsync(string key, CancellationToken ct) =>
        _settings is null
            ? Task.FromResult(int.Parse(SystemSettingDefinitions.Get(key).Default, System.Globalization.CultureInfo.InvariantCulture))
            : _settings.GetIntOrDefaultAsync(key, ct);

    private Task<string> ReadStringAsync(string key, CancellationToken ct) =>
        _settings is null
            ? Task.FromResult(SystemSettingDefinitions.Get(key).Default)
            : _settings.GetStringAsync(key, ct);

    public static MfaPolicy ParsePolicy(string? raw)
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
