using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.Sso;

public sealed record SsoUnlinkedUserDto(int Id, string Username, string FullName, string Role);

public sealed record SsoAdminOverviewDto(
    string Mode,
    IReadOnlyDictionary<string, object?> Configuration,
    DateTime? LastTestPassedAt,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<SsoUnlinkedUserDto> UnlinkedUsers,
    IReadOnlyList<ApiError> BlockerDetails);

public sealed record SsoTestResultDto(bool Passed, DateTime TestedAt);

public sealed record DirectoryLinkRowRequest(string? Username, string? CompanyUsername);

public enum DirectoryLinkRowStatus
{
    Ok,
    UnknownUser,
    Duplicate,
    AlreadyLinked,
    Invalid
}

public sealed record DirectoryLinkPreviewRowDto(int Row, string Username, string Status, string Message);

public sealed record DirectoryLinkImportResultDto(IReadOnlyList<DirectoryLinkPreviewRowDto> Rows, int? Applied = null);

/// <summary>
/// The company sign-in administration page: status, connection test, and the bulk
/// import that links MicroLIMS users to company usernames.
/// </summary>
public sealed class SsoAdminService
{
    public const int MaxImportRows = 2000;
    private const int MaxReportedReasonLength = 300;
    private const string SecretMask = "[redacted]";

    private readonly IMicroLimsDbContext _db;
    private readonly SsoModeService _ssoMode;
    private readonly SsoOptions _options;
    private readonly IOidcProviderClient _oidc;
    private readonly ILdapDirectory _ldap;
    private readonly UserService _users;
    private readonly TimeProvider _time;

    public SsoAdminService(
        IMicroLimsDbContext db,
        SsoModeService ssoMode,
        SsoOptions options,
        IOidcProviderClient oidc,
        ILdapDirectory ldap,
        UserService users,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _ssoMode = ssoMode;
        _options = options;
        _oidc = oidc;
        _ldap = ldap;
        _users = users;
        _time = timeProvider ?? TimeProvider.System;
    }

    // The mode that blockers, the last passed test and the connection test refer to:
    // the one asked for, else the current mode, else OpenID Connect (the only
    // provider installed first).
    public static SsoMode ResolveTargetMode(SsoMode current, SsoMode? requested) =>
        requested ?? (current == SsoMode.Off ? SsoMode.OpenIdConnect : current);

    public async Task<SsoAdminOverviewDto> GetOverviewAsync(int actingUserId, SsoMode? requestedMode, CancellationToken ct = default)
    {
        var mode = await _ssoMode.GetModeAsync(ct);
        var target = ResolveTargetMode(mode, requestedMode);

        var lastPassed = await _ssoMode.GetLastConnectionTestPassedAtAsync(target, ct);
        var blockers = await _ssoMode.GetSwitchOnBlockersAsync(actingUserId, target, ct);

        var unlinked = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.DirectoryUsername == null && !u.IsEmergencyAccount)
            // Character-code order ("C" collation), so the list reads the same whatever
            // collation the server's database was created with.
            .OrderBy(u => EF.Functions.Collate(u.Username, "C"))
            .Select(u => new SsoUnlinkedUserDto(u.Id, u.Username, u.FullName, u.Role != null ? u.Role.Name : string.Empty))
            .ToListAsync(ct);

        return new SsoAdminOverviewDto(
            mode.ToString(), BuildConfiguration(), lastPassed,
            blockers.Select(blocker => blocker.Message).ToList(), unlinked, blockers);
    }

    // Explicit allow-list of values safe to show. Never add a secret (client
    // secret, bind password): only whether one is configured.
    private IReadOnlyDictionary<string, object?> BuildConfiguration()
    {
        var oidc = _options.Oidc;
        var ldap = _options.Ldap;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["oidc.authority"] = Blank(oidc.Authority),
            ["oidc.clientId"] = Blank(oidc.ClientId),
            ["oidc.usernameClaim"] = string.IsNullOrWhiteSpace(oidc.UsernameClaim) ? OidcOptions.DefaultUsernameClaim : oidc.UsernameClaim.Trim(),
            ["oidc.requireMfaClaim"] = oidc.RequireMfaClaim,
            ["oidc.redirectUri"] = Blank(oidc.RedirectUri),
            ["oidc.clientSecretConfigured"] = !string.IsNullOrWhiteSpace(oidc.ClientSecret),
            ["ldap.host"] = Blank(ldap.Host),
            ["ldap.port"] = ldap.Port,
            ["ldap.transport"] = ldap.EffectiveUseLdaps ? "LDAPS" : "StartTLS",
            ["ldap.baseDn"] = Blank(ldap.BaseDn),
            ["ldap.bindDn"] = Blank(ldap.BindDn),
            ["ldap.usernameAttribute"] = string.IsNullOrWhiteSpace(ldap.UsernameAttribute) ? LdapOptions.DefaultUsernameAttribute : ldap.UsernameAttribute.Trim(),
            ["ldap.subjectAttribute"] = string.IsNullOrWhiteSpace(ldap.SubjectAttribute) ? LdapOptions.DefaultSubjectAttribute : ldap.SubjectAttribute.Trim(),
            ["ldap.timeoutSeconds"] = ldap.TimeoutSeconds,
            ["ldap.serviceAccountPasswordConfigured"] = !string.IsNullOrWhiteSpace(ldap.BindPassword)
        };

        static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public async Task<SsoTestResultDto> TestConnectionAsync(int actingUserId, SsoMode? requestedMode, CancellationToken ct = default)
    {
        var target = ResolveTargetMode(await _ssoMode.GetModeAsync(ct), requestedMode);
        if (target == SsoMode.Off)
            throw new BusinessRuleException(
                MessageCodes.AuthSsoChooseProviderToTest,
                "Choose OpenID Connect or Active Directory to test a connection.");

        try
        {
            if (target == SsoMode.ActiveDirectory)
                await _ldap.TestAsync(ct);
            else
                await _oidc.TestAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var reason = Scrub(ex.Message);
            // Saved on its own (no transaction is open here), so the failure
            // record stays even though the request ends in an error.
            await _ssoMode.RecordConnectionTestFailedAsync(target, $"{ex.GetType().Name}: {reason}", actingUserId, ct);
            var (code, message, parameters) = AdminReadableFailure(ex, reason, target);
            throw new BusinessRuleException(code, message, parameters);
        }

        await _ssoMode.RecordConnectionTestPassedAsync(target, actingUserId, ct);
        var testedAt = await _ssoMode.GetLastConnectionTestPassedAtAsync(target, ct) ?? _time.GetUtcNow().UtcDateTime;
        return new SsoTestResultDto(true, testedAt);
    }

    private static (string Code, string Message, Dictionary<string, string>? Params) AdminReadableFailure(
        Exception ex, string reason, SsoMode target) => ex switch
    {
        DirectoryUnavailableException => (
            MessageCodes.AuthSsoTestFailedDirectoryUnavailable,
            $"The connection test failed: the company directory could not be used ({reason})",
            new Dictionary<string, string> { ["reason"] = reason }),
        InvalidOperationException when target == SsoMode.ActiveDirectory => (
            MessageCodes.AuthSsoTestFailedReason,
            $"The connection test failed: {reason}",
            new Dictionary<string, string> { ["reason"] = reason }),
        HttpRequestException => (
            MessageCodes.AuthSsoTestFailedIdpUnreachable,
            $"The connection test failed: the identity provider could not be reached ({reason})",
            new Dictionary<string, string> { ["reason"] = reason }),
        TaskCanceledException => (
            MessageCodes.AuthSsoTestFailedIdpTimeout,
            "The connection test failed: the identity provider did not answer in time",
            null),
        OidcValidationException => (
            MessageCodes.AuthSsoTestFailedReason,
            $"The connection test failed: {reason}",
            new Dictionary<string, string> { ["reason"] = reason }),
        InvalidOperationException => (
            MessageCodes.AuthSsoTestFailedReason,
            $"The connection test failed: {reason}",
            new Dictionary<string, string> { ["reason"] = reason }),
        _ => (
            MessageCodes.AuthSsoTestFailedConfigUnreadable,
            $"The connection test failed: the identity provider's configuration could not be read ({reason})",
            new Dictionary<string, string> { ["reason"] = reason }),
    };

    // A provider or directory message must never carry the client secret or the
    // service account password back to the page or into the security event.
    private string Scrub(string? message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "no detail was given" : message.Trim();
        foreach (var secret in new[] { _options.Oidc.ClientSecret, _options.Ldap.BindPassword })
        {
            if (!string.IsNullOrEmpty(secret))
                text = text.Replace(secret, SecretMask, StringComparison.Ordinal);
        }
        return text.Length > MaxReportedReasonLength ? text[..MaxReportedReasonLength] + "..." : text;
    }

    public async Task<DirectoryLinkImportResultDto> PreviewLinksAsync(IReadOnlyList<DirectoryLinkRowRequest>? rows, CancellationToken ct = default)
    {
        var evaluated = await EvaluateAsync(rows, ct);
        return new DirectoryLinkImportResultDto(evaluated.Select(e => e.Dto).ToList());
    }

    // Applies only the rows that pass, all in one transaction: if any write
    // fails nothing is linked. The sign-in method is not changed.
    public Task<DirectoryLinkImportResultDto> ImportLinksAsync(
        IReadOnlyList<DirectoryLinkRowRequest>? rows, string? reason, int actingUserId, CancellationToken ct = default)
    {
        var trimmedReason = AdminActionReason.Require(reason, "import company username links");
        EnsureRowCount(rows);

        return UnitOfWork.RunAsync(_db, async () =>
        {
            var evaluated = await EvaluateAsync(rows, ct);
            var okRows = evaluated.Where(e => e.Status == DirectoryLinkRowStatus.Ok).ToList();

            var ids = okRows.Select(e => e.UserId!.Value).ToList();
            var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
            foreach (var row in okRows)
            {
                var user = users[row.UserId!.Value];
                _users.ApplySsoLinkChange(user, user.SignInMethod, row.CompanyUsername, trimmedReason, actingUserId);
            }

            await _db.SaveChangesAsync(ct);
            return new DirectoryLinkImportResultDto(evaluated.Select(e => e.Dto).ToList(), okRows.Count);
        });
    }

    private static void EnsureRowCount(IReadOnlyList<DirectoryLinkRowRequest>? rows)
    {
        if (rows is null || rows.Count == 0)
            throw new BusinessRuleException(MessageCodes.AuthSsoImportFileEmpty, "The file has no rows.");
        if (rows.Count > MaxImportRows)
            throw new BusinessRuleException(
                MessageCodes.AuthSsoImportTooManyRows,
                $"The file has {rows.Count} rows. At most {MaxImportRows} rows can be imported at once.",
                new Dictionary<string, string>
                {
                    ["maxRows"] = MaxImportRows.ToString(CultureInfo.InvariantCulture),
                    ["rowCount"] = rows.Count.ToString(CultureInfo.InvariantCulture),
                });
    }

    private sealed record EvaluatedRow(DirectoryLinkPreviewRowDto Dto, DirectoryLinkRowStatus Status, int? UserId, string? CompanyUsername);

    private async Task<List<EvaluatedRow>> EvaluateAsync(IReadOnlyList<DirectoryLinkRowRequest>? rows, CancellationToken ct)
    {
        EnsureRowCount(rows);

        var parsed = rows!.Select((r, i) =>
        {
            var username = r?.Username?.Trim() ?? string.Empty;
            var valid = DirectoryUsernameRules.TryNormalize(r?.CompanyUsername, out var company, out var error);
            return new { Row = i + 1, Username = username, Company = valid ? company : null, CompanyError = error };
        }).ToList();

        var companyCounts = parsed.Where(p => p.Company is not null)
            .GroupBy(p => p.Company!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var usernameCounts = parsed.Where(p => p.Username.Length > 0)
            .GroupBy(p => p.Username, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var usernames = usernameCounts.Keys.ToList();
        var targets = await _db.Users.AsNoTracking()
            .Where(u => usernames.Contains(u.Username))
            .Select(u => new { u.Id, u.Username, u.IsActive, u.IsEmergencyAccount, u.DirectoryUsername })
            .ToListAsync(ct);
        var byUsername = targets.ToDictionary(t => t.Username, StringComparer.Ordinal);

        var companies = companyCounts.Keys.ToList();
        var holders = await _db.Users.AsNoTracking()
            .Where(u => u.DirectoryUsername != null && companies.Contains(u.DirectoryUsername))
            .Select(u => new { u.Id, u.Username, DirectoryUsername = u.DirectoryUsername! })
            .ToListAsync(ct);
        var holderByCompany = holders.ToDictionary(h => h.DirectoryUsername, StringComparer.Ordinal);

        var result = new List<EvaluatedRow>(parsed.Count);
        foreach (var p in parsed)
        {
            EvaluatedRow Row(DirectoryLinkRowStatus status, string message, int? userId = null) =>
                new(new DirectoryLinkPreviewRowDto(p.Row, p.Username, status.ToString(), message), status, userId, p.Company);

            if (p.Username.Length == 0)
            {
                result.Add(Row(DirectoryLinkRowStatus.Invalid, "The MicroLIMS username is blank."));
                continue;
            }
            if (p.Company is null)
            {
                result.Add(Row(DirectoryLinkRowStatus.Invalid, p.CompanyError));
                continue;
            }
            if (companyCounts[p.Company] > 1)
            {
                result.Add(Row(DirectoryLinkRowStatus.Duplicate, $"The company username '{p.Company}' appears more than once in the file."));
                continue;
            }
            if (usernameCounts[p.Username] > 1)
            {
                result.Add(Row(DirectoryLinkRowStatus.Duplicate, $"The MicroLIMS username '{p.Username}' appears more than once in the file."));
                continue;
            }
            if (!byUsername.TryGetValue(p.Username, out var target))
            {
                result.Add(Row(DirectoryLinkRowStatus.UnknownUser, $"No MicroLIMS user is named '{p.Username}'."));
                continue;
            }
            if (target.IsEmergencyAccount)
            {
                result.Add(Row(DirectoryLinkRowStatus.Invalid, "This is an emergency administrator account. Emergency accounts keep laboratory sign-in and are never linked."));
                continue;
            }
            if (!target.IsActive)
            {
                result.Add(Row(DirectoryLinkRowStatus.Invalid, "This account is disabled. Enable it before linking it."));
                continue;
            }
            if (target.DirectoryUsername is not null)
            {
                result.Add(Row(DirectoryLinkRowStatus.AlreadyLinked, $"This user is already linked to the company username '{target.DirectoryUsername}'."));
                continue;
            }
            if (holderByCompany.TryGetValue(p.Company, out var holder) && holder.Id != target.Id)
            {
                result.Add(Row(DirectoryLinkRowStatus.AlreadyLinked, $"The company username '{p.Company}' is already linked to the MicroLIMS user '{holder.Username}'."));
                continue;
            }

            result.Add(Row(DirectoryLinkRowStatus.Ok, $"Will be linked to the company username '{p.Company}'.", target.Id));
        }

        return result;
    }
}
