using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Sso;

public enum SsoMode
{
    Off,
    OpenIdConnect,
    ActiveDirectory
}

/// <summary>
/// Resolves the configured SSO mode, checks switch-on prerequisites, records connection-test
/// results, and decides whether a user is allowed to sign in locally.
/// </summary>
public sealed class SsoModeService
{
    public const string BlockerConnectionTestRequired = "Run a successful connection test first.";
    public const string BlockerEmergencyAdminRequired = "At least one emergency administrator account with a second factor is required.";
    public const string BlockerActingAdminNotLinked = "Link your own account to a company username first.";

    private const int ConnectionTestLookbackLimit = 50;
    private const int MaxTechnicalReasonLength = 1000;

    private readonly IMicroLimsDbContext _db;
    private readonly ISystemSettingsReader? _settings;
    private readonly ISecurityAuditService _securityAudit;
    private readonly ISsoConfigurationFingerprint _fingerprint;
    private readonly ILogger<SsoModeService> _logger;

    public SsoModeService(
        IMicroLimsDbContext db,
        ISystemSettingsReader? settings,
        ISecurityAuditService securityAudit,
        ISsoConfigurationFingerprint? fingerprint = null,
        ILogger<SsoModeService>? logger = null)
    {
        _db = db;
        _settings = settings;
        _securityAudit = securityAudit;
        _fingerprint = fingerprint ?? new DefaultSsoConfigurationFingerprint();
        _logger = logger ?? NullLogger<SsoModeService>.Instance;
    }

    public async Task<SsoMode> GetModeAsync(CancellationToken ct = default)
    {
        var raw = await ReadSettingSafeAsync(ct);
        if (TryParseMode(raw, out var mode))
            return mode;

        _logger.LogWarning("Unknown configured SSO mode '{ConfiguredSsoMode}'. Treating SSO mode as Off.", raw);
        return SsoMode.Off;
    }

    public async Task<bool> IsLocalLoginAllowedAsync(User user, CancellationToken ct = default)
    {
        var mode = await GetModeAsync(ct);
        return IsLocalLoginAllowed(mode, user);
    }

    public bool IsLocalLoginAllowed(SsoMode mode, User user) =>
        mode == SsoMode.Off || user.IsEmergencyAccount;

    public async Task<IReadOnlyList<ApiError>> GetSwitchOnBlockersAsync(
        int actingUserId,
        SsoMode target,
        CancellationToken ct = default)
    {
        if (target == SsoMode.Off)
            return Array.Empty<ApiError>();

        var blockers = new List<ApiError>(3);

        if (await GetLastConnectionTestPassedAtAsync(target, ct) is null)
            blockers.Add(new ApiError(MessageCodes.SettingsSsoConnectionTestRequired, null, BlockerConnectionTestRequired));

        var hasEmergencyAdmin = await _db.Users
            .AsNoTracking()
            .AnyAsync(
                u => u.IsActive
                     && u.IsEmergencyAccount
                     && u.SignInMethod == SignInMethod.Local
                     && u.Role != null
                     && u.Role.Type == RoleType.SystemAdministrator
                     && _db.UserMfaCredentials.Any(c => c.UserId == u.Id && c.Status == MfaCredentialStatus.Active),
                ct);
        if (!hasEmergencyAdmin)
            blockers.Add(new ApiError(MessageCodes.SettingsSsoEmergencyAdminRequired, null, BlockerEmergencyAdminRequired));

        var actingUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == actingUserId, ct);
        if (actingUser is null || string.IsNullOrWhiteSpace(actingUser.DirectoryUsername))
            blockers.Add(new ApiError(MessageCodes.SettingsSsoActingAdminNotLinked, null, BlockerActingAdminNotLinked));

        return blockers;
    }

    public Task RecordConnectionTestPassedAsync(SsoMode mode, CancellationToken ct = default) =>
        RecordConnectionTestPassedAsync(mode, actingUserId: null, ct);

    public async Task RecordConnectionTestPassedAsync(
        SsoMode mode,
        int? actingUserId,
        CancellationToken ct = default)
    {
        if (mode == SsoMode.Off)
            throw new BusinessRuleException(
                MessageCodes.AuthSsoConnectionTestModeRequired,
                "A connection test applies only to OpenIdConnect or ActiveDirectory.");

        var metadata = SerializeMetadata(mode, _fingerprint.GetFingerprint(mode));
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.SsoConnectionTestPassed,
            SecurityEventOutcome.Success,
            ActorUserId: actingUserId ?? _db.CurrentUserId,
            Metadata: metadata));

        await _db.SaveChangesAsync(ct);
    }

    public Task RecordConnectionTestFailedAsync(SsoMode mode, string technicalReason, CancellationToken ct = default) =>
        RecordConnectionTestFailedAsync(mode, technicalReason, actingUserId: null, ct);

    public async Task RecordConnectionTestFailedAsync(
        SsoMode mode,
        string technicalReason,
        int? actingUserId,
        CancellationToken ct = default)
    {
        if (mode == SsoMode.Off)
            throw new BusinessRuleException(
                MessageCodes.AuthSsoConnectionTestModeRequired,
                "A connection test applies only to OpenIdConnect or ActiveDirectory.");

        var truncatedReason = string.IsNullOrWhiteSpace(technicalReason)
            ? null
            : technicalReason.Trim();
        if (truncatedReason is { Length: > MaxTechnicalReasonLength })
            truncatedReason = truncatedReason[..MaxTechnicalReasonLength];

        var metadata = SerializeMetadata(mode, _fingerprint.GetFingerprint(mode));
        _securityAudit.Record(new SecurityEventRequest(
            SecurityEventCodes.SsoConnectionTestFailed,
            SecurityEventOutcome.Failure,
            ActorUserId: actingUserId ?? _db.CurrentUserId,
            Reason: truncatedReason,
            Metadata: metadata));

        await _db.SaveChangesAsync(ct);
    }

    public async Task<DateTime?> GetLastConnectionTestPassedAtAsync(SsoMode mode, CancellationToken ct = default)
    {
        if (mode == SsoMode.Off)
            return null;

        var expectedMode = mode.ToString();
        var expectedFingerprint = _fingerprint.GetFingerprint(mode);

        var recent = await _db.SecurityAuditEvents
            .AsNoTracking()
            .Where(e => e.EventCode == SecurityEventCodes.SsoConnectionTestPassed
                     || e.EventCode == SecurityEventCodes.SsoConnectionTestFailed)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Take(ConnectionTestLookbackLimit)
            .Select(e => new { e.EventCode, e.Outcome, e.OccurredAtUtc, e.Metadata })
            .ToListAsync(ct);

        foreach (var entry in recent)
        {
            if (!TryParseMetadata(entry.Metadata, out var parsed)
                || !string.Equals(parsed.Mode, expectedMode, StringComparison.Ordinal)
                || !string.Equals(parsed.ConfigFingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                continue;
            }

            return entry.EventCode == SecurityEventCodes.SsoConnectionTestPassed
                && entry.Outcome == SecurityEventOutcome.Success
                ? entry.OccurredAtUtc
                : null;
        }

        return null;
    }

    public static bool TryParseMode(string? raw, out SsoMode mode)
    {
        var trimmed = raw?.Trim();
        if (string.Equals(trimmed, nameof(SsoMode.Off), StringComparison.OrdinalIgnoreCase))
        {
            mode = SsoMode.Off;
            return true;
        }
        if (string.Equals(trimmed, nameof(SsoMode.OpenIdConnect), StringComparison.OrdinalIgnoreCase))
        {
            mode = SsoMode.OpenIdConnect;
            return true;
        }
        if (string.Equals(trimmed, nameof(SsoMode.ActiveDirectory), StringComparison.OrdinalIgnoreCase))
        {
            mode = SsoMode.ActiveDirectory;
            return true;
        }

        mode = SsoMode.Off;
        return false;
    }

    private async Task<string> ReadSettingSafeAsync(CancellationToken ct)
    {
        if (_settings is null)
            return SystemSettingDefinitions.Get(SystemSettingKeys.SsoMode).Default;

        try
        {
            return await _settings.GetStringAsync(SystemSettingKeys.SsoMode, ct);
        }
        catch (KeyNotFoundException)
        {
            // Unit/integration test stubs that only configure MFA/lockout settings should fall back to Off.
            return SystemSettingDefinitions.Get(SystemSettingKeys.SsoMode).Default;
        }
    }

    private static string SerializeMetadata(SsoMode mode, string configFingerprint) =>
        JsonSerializer.Serialize(new ConnectionTestMetadata(mode.ToString(), configFingerprint));

    private static bool TryParseMetadata(string? json, out ConnectionTestMetadata parsed)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            parsed = default;
            return false;
        }

        try
        {
            parsed = JsonSerializer.Deserialize<ConnectionTestMetadata>(json);
            return !string.IsNullOrWhiteSpace(parsed.Mode);
        }
        catch (JsonException)
        {
            parsed = default;
            return false;
        }
    }

    private readonly record struct ConnectionTestMetadata(
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("configFingerprint")] string ConfigFingerprint);
}
