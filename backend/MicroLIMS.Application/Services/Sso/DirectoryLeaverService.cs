using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Abstractions.Email;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Sso;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Sso;

public sealed record LeaverRunResult(int Checked, int Deactivated, bool DirectoryUnavailable);

/// <summary>
/// Active Directory mode only: asks the company directory about every active, linked directory user and
/// closes the accounts it reports disabled or no longer present. The run is two phases so a failed lookup
/// can never leave a half-applied result: first every batch is looked up (read-only), and only when all
/// lookups have answered are the changes written, in one transaction. If the directory cannot answer at
/// any point nothing is changed. A user who is enabled again in the directory is never reactivated here
/// (re-activation is an administrator decision). Administrators are emailed after the commit.
/// </summary>
public sealed class DirectoryLeaverService
{
    public const string ProcessName = "DirectoryLeaverWorker";
    public const int BatchSize = 100;

    private readonly IMicroLimsDbContext _db;
    private readonly ILdapDirectory _directory;
    private readonly SsoModeService _ssoMode;
    private readonly AuthenticationService _auth;
    private readonly ISecurityAuditService _securityAudit;
    private readonly IEmailSender _emailSender;
    private readonly TimeProvider _time;
    private readonly ILogger<DirectoryLeaverService> _logger;

    public DirectoryLeaverService(
        IMicroLimsDbContext db,
        ILdapDirectory directory,
        SsoModeService ssoMode,
        AuthenticationService auth,
        ISecurityAuditService securityAudit,
        IEmailSender emailSender,
        TimeProvider? timeProvider = null,
        ILogger<DirectoryLeaverService>? logger = null)
    {
        _db = db;
        _directory = directory;
        _ssoMode = ssoMode;
        _auth = auth;
        _securityAudit = securityAudit;
        _emailSender = emailSender;
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<DirectoryLeaverService>.Instance;
    }

    private sealed record Leaver(int UserId, string SubjectId, bool Missing);

    public async Task<LeaverRunResult> RunAsync(CancellationToken ct = default)
    {
        if (await _ssoMode.GetModeAsync(ct) != SsoMode.ActiveDirectory)
            return new LeaverRunResult(0, 0, false);

        // Phase 1: look everything up. Nothing is written, so a failure anywhere leaves nothing behind.
        var leavers = new List<Leaver>();
        var checkedCount = 0;
        var lastId = 0;
        try
        {
            while (true)
            {
                var batch = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Id > lastId
                        && u.IsActive
                        && u.SignInMethod == SignInMethod.Directory
                        && u.DirectorySubjectId != null)
                    .OrderBy(u => u.Id)
                    .Select(u => new { u.Id, SubjectId = u.DirectorySubjectId! })
                    .Take(BatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                    break;

                lastId = batch[^1].Id;
                checkedCount += batch.Count;

                var answers = await _directory.FindManyBySubjectAsync(
                    batch.Select(b => b.SubjectId).Distinct(StringComparer.Ordinal).ToList(), ct);

                foreach (var user in batch)
                {
                    // An id the directory could not have issued is left out of the answer: not a leaver.
                    if (!answers.TryGetValue(user.SubjectId, out var entry))
                        continue;
                    if (entry is null || entry.Disabled)
                        leavers.Add(new Leaver(user.Id, user.SubjectId, Missing: entry is null));
                }
            }
        }
        catch (DirectoryUnavailableException ex)
        {
            _logger.LogWarning(ex, "[{Worker}] Company directory unavailable; no account was changed.", ProcessName);
            return new LeaverRunResult(checkedCount, 0, true);
        }

        if (leavers.Count == 0)
            return new LeaverRunResult(checkedCount, 0, false);

        // Phase 2: every lookup answered; write all changes in one transaction.
        var deactivatedNames = new List<string>();
        var previousProcess = _db.CurrentSystemProcessName;
        var previousReason = _db.CurrentReason;
        var previousUser = _db.CurrentUserId;
        _db.CurrentUserId = null;
        _db.CurrentSystemProcessName = ProcessName;
        _db.CurrentReason = "Account disabled or removed in the company directory";
        try
        {
            await UnitOfWork.RunAsync(_db, async () =>
            {
                deactivatedNames.Clear();
                var nowUtc = _time.GetUtcNow().UtcDateTime;
                var ids = leavers.Select(l => l.UserId).ToList();
                var users = await _db.Users
                    .Where(u => ids.Contains(u.Id) && u.IsActive)
                    .OrderBy(u => u.Id)
                    .ToListAsync(ct);

                foreach (var user in users)
                {
                    var leaver = leavers.First(l => l.UserId == user.Id);
                    // The user may have been relinked or changed since the lookup: only the exact account that was asked about.
                    if (!string.Equals(user.DirectorySubjectId, leaver.SubjectId, StringComparison.Ordinal)
                        || user.SignInMethod != SignInMethod.Directory)
                        continue;

                    await DirectoryAccountActions.DeactivateAsync(
                        user, nowUtc, _auth, _securityAudit,
                        leaver.Missing
                            ? "Account no longer exists in the company directory."
                            : "Account disabled in the company directory.");
                    deactivatedNames.Add(user.Username);
                }

                await _db.SaveChangesAsync(ct);
            });
        }
        finally
        {
            _db.CurrentSystemProcessName = previousProcess;
            _db.CurrentReason = previousReason;
            _db.CurrentUserId = previousUser;
        }

        // Only after the commit, and never able to undo it.
        if (deactivatedNames.Count > 0)
            await NotifyAdministratorsAsync(deactivatedNames, ct);

        return new LeaverRunResult(checkedCount, deactivatedNames.Count, false);
    }

    private async Task NotifyAdministratorsAsync(IReadOnlyList<string> usernames, CancellationToken ct)
    {
        List<string> emails;
        try
        {
            emails = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive
                    && u.Role != null
                    && u.Role.Type == RoleType.SystemAdministrator
                    && u.Email != null
                    && u.Email != "")
                .Select(u => u.Email!)
                .Distinct()
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[{Worker}] Could not read administrator recipients; no notification sent.", ProcessName);
            return;
        }

        var subject = "MicroLIMS: Accounts deactivated by the company directory";
        var body = "The company directory reports these MicroLIMS accounts as disabled or removed. "
                   + "They were deactivated and their sessions ended:\n\n"
                   + string.Join("\n", usernames.Select(n => $"- {n}"));
        foreach (var email in emails)
        {
            try
            {
                await _emailSender.SendAsync(email, subject, body);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[{Worker}] Failed to send leaver notification to {Recipient}.", ProcessName, email);
            }
        }
    }
}
