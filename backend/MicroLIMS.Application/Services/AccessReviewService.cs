using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Periodic access review (EU GMP Annex 11 s.12, 21 CFR 11.10(d)(g)).
//
// Immutability: the report is generated once into AccessReview.SnapshotJson with
// its SHA-256. This service never edits a snapshot or a signed review, and a
// database trigger enforces the same (only Draft -> Signed, no DELETE/TRUNCATE).
// Findings can be recorded and edited only while the review is Draft.
//
// Signing rule: the signer needs Access.Review (endpoint policy) and may not sign
// a review about their own access - a finding about their own account, or the
// report flagging their own account (dormant, never signed in, SoD conflict).
// The generator may sign: the review attests the report, and the reviewer reads
// it in full before signing with their own password.
public class AccessReviewService : IAccessReviewService
{
    public const int ReportVersion = 1;
    public const int StatusHistoryMonths = 12;
    internal const string EntityName = "AccessReview";

    public static readonly IReadOnlyList<string> FindingKinds = new[]
    {
        "SodConflict", "Dormant", "NeverLoggedIn", "RoleReview", "PermissionReview", "Other"
    };

    private static readonly string[] StatusActions = { "USER_ENABLED", "USER_DISABLED", "USER_ROLE_CHANGED" };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatureService;
    private readonly ISystemSettingsReader? _settings;
    private readonly TimeProvider _time;

    public AccessReviewService(
        IMicroLimsDbContext db,
        IElectronicSignatureService signatureService,
        ISystemSettingsReader? settings = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _signatureService = signatureService;
        _settings = settings;
        _time = timeProvider ?? TimeProvider.System;
    }

    // ---- generate -----------------------------------------------------------

    public Task<AccessReviewDetailDto> GenerateAsync(string? periodLabel, string? reason, int actingUserId) =>
        UnitOfWork.RunAsync(_db, () => GenerateCoreAsync(periodLabel, reason, actingUserId));

    private async Task<AccessReviewDetailDto> GenerateCoreAsync(string? rawLabel, string? rawReason, int actingUserId)
    {
        var reason = AdminActionReason.Require(rawReason, "generate an access review");
        var now = _time.GetUtcNow().UtcDateTime;
        var label = rawLabel?.Trim();
        if (string.IsNullOrEmpty(label)) label = $"Access review {now:yyyy-MM-dd}";
        if (label.Length > 100) throw new InvalidOperationException("The period label must be 100 characters or fewer.");

        var dormantDays = await _settings.GetIntOrDefaultAsync(SystemSettingKeys.AccessReviewDormantDays);
        var snapshot = await BuildSnapshotAsync(label, dormantDays, now);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);

        _db.CurrentUserId = actingUserId;
        _db.CurrentReason = reason;
        var review = new AccessReview
        {
            GeneratedAt = now,
            GeneratedByUserId = actingUserId,
            PeriodLabel = label,
            SnapshotJson = json,
            SnapshotSha256 = Sha256(json),
            Status = AccessReviewStatus.Draft
        };
        _db.AccessReviews.Add(review);
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(Audit(review.Id, "ACCESS_REVIEW_GENERATED", "AccessReviewGenerated", AuditActionCategory.Security,
            null,
            JsonSerializer.Serialize(new { review.Id, review.PeriodLabel, review.SnapshotSha256, dormantDays, snapshot.Summary, Reason = reason }),
            reason, actingUserId, now));
        await _db.SaveChangesAsync();

        return await GetAsync(review.Id);
    }

    // The report: one entry per account, active or not.
    internal async Task<AccessReviewSnapshot> BuildSnapshotAsync(string label, int dormantDays, DateTime now)
    {
        var users = await _db.Users.AsNoTracking().Include(u => u.Role).OrderBy(u => u.Username).ToListAsync();

        var grants = await _db.RolePermissions.AsNoTracking()
            .Select(rp => new { rp.RoleId, rp.Permission!.Code })
            .ToListAsync();
        var codesByRole = grants.GroupBy(g => g.RoleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList());

        var memberships = await _db.UserOrgMemberships.AsNoTracking()
            .Select(m => new
            {
                m.UserId, m.DepartmentId, DepartmentName = m.Department.Name,
                m.SectionId, SectionName = m.Section != null ? m.Section.Name : null, m.PhyschemArea
            })
            .ToListAsync();
        var membershipsByUser = memberships.GroupBy(m => m.UserId).ToDictionary(g => g.Key,
            g => g.OrderBy(m => m.DepartmentName, StringComparer.Ordinal).ThenBy(m => m.SectionName, StringComparer.Ordinal)
                  .Select(m => new AccessReviewMembership(m.DepartmentId, m.DepartmentName, m.SectionId, m.SectionName, m.PhyschemArea?.ToString()))
                  .ToList());

        var logins = await _db.LoginHistories.AsNoTracking()
            .Where(l => l.Success && l.UserId != null)
            .GroupBy(l => l.UserId!.Value)
            .Select(g => new { UserId = g.Key, Last = g.Max(l => l.Timestamp) })
            .ToListAsync();
        var lastLogin = logins.ToDictionary(l => l.UserId, l => l.Last);

        var since = now.AddMonths(-StatusHistoryMonths);
        var auditRows = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(User) && StatusActions.Contains(a.Action) && a.Timestamp >= since)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
            .Select(a => new { a.EntityId, a.Action, a.Timestamp, a.UserId, a.Reason, a.NewValue })
            .ToListAsync();
        var historyByUser = auditRows
            .Select(a => new { Ok = int.TryParse(a.EntityId, out var id), Id = id, Row = a })
            .Where(x => x.Ok)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.Select(x =>
                new AccessReviewStatusChange(x.Row.Timestamp, x.Row.Action, x.Row.UserId, x.Row.Reason, x.Row.NewValue)).ToList());

        var entries = new List<AccessReviewUserEntry>();
        foreach (var u in users)
        {
            var codes = codesByRole.GetValueOrDefault(u.RoleId) ?? new List<string>();
            var permissions = codes.Select(c => new AccessReviewPermission(c, PermissionConstants.Hidden.Contains(c))).ToList();
            var conflicts = SegregationOfDutiesRules.FindConflicts(codes).Select(SegregationOfDutiesRules.Describe).ToList();

            DateTime? last = lastLogin.TryGetValue(u.Id, out var l) ? l : null;
            var neverLoggedIn = last is null;
            var reference = last ?? u.CreatedAt;
            var days = (int)Math.Floor((now - reference).TotalDays);
            var dormant = u.IsActive && days > dormantDays;

            entries.Add(new AccessReviewUserEntry(
                u.Id, u.Username, u.FullName, u.IsActive, u.DeactivatedAt,
                u.RoleId, u.Role?.Name ?? string.Empty, u.Role?.Type.ToString() ?? string.Empty,
                permissions,
                membershipsByUser.GetValueOrDefault(u.Id) ?? new List<AccessReviewMembership>(),
                last, neverLoggedIn, dormant, days < 0 ? 0 : days,
                historyByUser.GetValueOrDefault(u.Id) ?? new List<AccessReviewStatusChange>(),
                conflicts));
        }

        var summary = new AccessReviewSummary(
            entries.Count, entries.Count(e => e.IsActive), entries.Count(e => !e.IsActive),
            entries.Count(e => e.Dormant), entries.Count(e => e.NeverLoggedIn), entries.Count(e => e.SodConflicts.Count > 0));
        return new AccessReviewSnapshot(ReportVersion, now, label, dormantDays, StatusHistoryMonths, summary, entries);
    }

    // ---- read ---------------------------------------------------------------

    public async Task<IReadOnlyList<AccessReviewSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var reviews = await _db.AccessReviews.AsNoTracking()
            .OrderByDescending(r => r.GeneratedAt).ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
        var counts = await _db.AccessReviewFindings.AsNoTracking()
            .GroupBy(f => f.ReviewId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var names = await NamesAsync(reviews.SelectMany(r => new[] { (int?)r.GeneratedByUserId, r.ReviewerUserId }), cancellationToken);

        return reviews.Select(r => new AccessReviewSummaryDto(
            r.Id, r.GeneratedAt, r.GeneratedByUserId, names.GetValueOrDefault(r.GeneratedByUserId),
            r.PeriodLabel, r.Status.ToString(), r.ReviewerUserId,
            r.ReviewerUserId is { } rid ? names.GetValueOrDefault(rid) : null,
            r.SignedAt, counts.GetValueOrDefault(r.Id), Deserialize(r.SnapshotJson).Summary)).ToList();
    }

    public async Task<AccessReviewDetailDto> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var review = await _db.AccessReviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Access review {id} not found.");
        var findings = await _db.AccessReviewFindings.AsNoTracking()
            .Where(f => f.ReviewId == id).OrderBy(f => f.Id).ToListAsync(cancellationToken);
        var snapshot = Deserialize(review.SnapshotJson);
        var names = await NamesAsync(new[] { (int?)review.GeneratedByUserId, review.ReviewerUserId }, cancellationToken);
        var usernames = snapshot.Users.ToDictionary(u => u.UserId, u => u.Username);

        return new AccessReviewDetailDto(
            review.Id, review.GeneratedAt, review.GeneratedByUserId, names.GetValueOrDefault(review.GeneratedByUserId),
            review.PeriodLabel, review.Status.ToString(), review.ReviewerUserId,
            review.ReviewerUserId is { } rid ? names.GetValueOrDefault(rid) : null,
            review.SignedAt, review.ElectronicSignatureId, review.Conclusion,
            review.SnapshotSha256, string.Equals(Sha256(review.SnapshotJson), review.SnapshotSha256, StringComparison.Ordinal),
            snapshot, findings.Select(f => ToDto(f, usernames)).ToList());
    }

    // ---- findings -----------------------------------------------------------

    public Task<AccessReviewFindingDto> AddFindingAsync(int reviewId, SaveAccessReviewFindingRequest request, int actingUserId) =>
        UnitOfWork.RunAsync(_db, () => SaveFindingAsync(reviewId, null, request, actingUserId));

    public Task<AccessReviewFindingDto> UpdateFindingAsync(int reviewId, int findingId, SaveAccessReviewFindingRequest request, int actingUserId) =>
        UnitOfWork.RunAsync(_db, () => SaveFindingAsync(reviewId, findingId, request, actingUserId));

    private async Task<AccessReviewFindingDto> SaveFindingAsync(int reviewId, int? findingId, SaveAccessReviewFindingRequest request, int actingUserId)
    {
        var review = await _db.AccessReviews.FirstOrDefaultAsync(r => r.Id == reviewId)
            ?? throw new NotFoundException($"Access review {reviewId} not found.");
        EnsureDraft(review);

        var snapshot = Deserialize(review.SnapshotJson);
        var subject = snapshot.Users.FirstOrDefault(u => u.UserId == request.UserId)
            ?? throw new InvalidOperationException("The account is not part of this review's report.");

        var kind = request.Kind?.Trim();
        if (string.IsNullOrEmpty(kind) || !FindingKinds.Contains(kind, StringComparer.Ordinal))
            throw new InvalidOperationException($"The finding kind must be one of: {string.Join(", ", FindingKinds)}.");
        var detail = request.Detail?.Trim();
        if (string.IsNullOrEmpty(detail)) throw new InvalidOperationException("A finding needs a detail.");
        if (detail.Length > 1000) throw new InvalidOperationException("The finding detail must be 1000 characters or fewer.");
        if (!Enum.TryParse<AccessReviewFindingAction>(request.Action, ignoreCase: true, out var action)
            || !Enum.IsDefined(action))
            throw new InvalidOperationException("The finding action must be NoChange or ChangeRequested.");
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 1000 }) throw new InvalidOperationException("The finding note must be 1000 characters or fewer.");
        if (action == AccessReviewFindingAction.ChangeRequested && note is null)
            throw new InvalidOperationException("A note describing the requested change is required.");

        var now = _time.GetUtcNow().UtcDateTime;
        AccessReviewFinding finding;
        string? previous = null;
        if (findingId is null)
        {
            finding = new AccessReviewFinding { ReviewId = reviewId, RecordedByUserId = actingUserId, RecordedAt = now };
            _db.AccessReviewFindings.Add(finding);
        }
        else
        {
            finding = await _db.AccessReviewFindings.FirstOrDefaultAsync(f => f.Id == findingId && f.ReviewId == reviewId)
                ?? throw new NotFoundException($"Finding {findingId} not found.");
            if (finding.UserId != request.UserId)
                throw new InvalidOperationException("A finding cannot be moved to another account; record a new finding.");
            previous = JsonSerializer.Serialize(new { finding.Kind, finding.Detail, Action = finding.Action.ToString(), finding.Note });
            finding.RecordedByUserId = actingUserId;
            finding.RecordedAt = now;
        }
        finding.UserId = request.UserId;
        finding.Kind = kind;
        finding.Detail = detail;
        finding.Action = action;
        finding.Note = note;

        _db.CurrentUserId = actingUserId;
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(Audit(reviewId, findingId is null ? "ACCESS_REVIEW_FINDING_ADDED" : "ACCESS_REVIEW_FINDING_UPDATED",
            findingId is null ? "AccessReviewFindingAdded" : "AccessReviewFindingUpdated", AuditActionCategory.Security,
            previous,
            JsonSerializer.Serialize(new { FindingId = finding.Id, finding.UserId, subject.Username, finding.Kind, finding.Detail, Action = action.ToString(), finding.Note }),
            null, actingUserId, now));
        await _db.SaveChangesAsync();

        return ToDto(finding, snapshot.Users.ToDictionary(u => u.UserId, u => u.Username));
    }

    // ---- sign ---------------------------------------------------------------

    public Task<AccessReviewDetailDto> SignAsync(int reviewId, string? password, string? conclusion, int actingUserId, string? ipAddress) =>
        UnitOfWork.RunAsync(_db, () => SignCoreAsync(reviewId, password, conclusion, actingUserId, ipAddress));

    private async Task<AccessReviewDetailDto> SignCoreAsync(int reviewId, string? password, string? rawConclusion, int actingUserId, string? ipAddress)
    {
        var review = await _db.AccessReviews.FirstOrDefaultAsync(r => r.Id == reviewId)
            ?? throw new NotFoundException($"Access review {reviewId} not found.");
        EnsureDraft(review);

        var conclusion = rawConclusion?.Trim();
        if (string.IsNullOrEmpty(conclusion)) throw new InvalidOperationException("A conclusion is required to sign the review.");
        if (conclusion.Length > 2000) throw new InvalidOperationException("The conclusion must be 2000 characters or fewer.");

        if (!string.Equals(Sha256(review.SnapshotJson), review.SnapshotSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("The stored report no longer matches its checksum; the review cannot be signed.");

        // A review about the signer's own access cannot be signed by the signer.
        var snapshot = Deserialize(review.SnapshotJson);
        var ownFinding = await _db.AccessReviewFindings.AnyAsync(f => f.ReviewId == reviewId && f.UserId == actingUserId);
        var ownEntry = snapshot.Users.FirstOrDefault(u => u.UserId == actingUserId);
        var ownFlagged = ownEntry is not null && (ownEntry.Dormant || ownEntry.NeverLoggedIn || ownEntry.SodConflicts.Count > 0);
        if (ownFinding || ownFlagged)
            throw new InvalidOperationException(
                "You cannot sign a review that concerns your own access (a finding about your account, or your account is flagged in the report). Another reviewer must sign it.");

        var now = _time.GetUtcNow().UtcDateTime;
        _db.CurrentUserId = actingUserId;

        // Signs before anything changes: a wrong password throws, and the
        // refused attempt is recorded by the signature service.
        var signature = await _signatureService.SignAsync(actingUserId, password ?? string.Empty, SignatureMeaning.AccessReviewSigned,
            EntityName, review.Id, $"{review.PeriodLabel}; report sha256 {review.SnapshotSha256}", ipAddress);

        review.Status = AccessReviewStatus.Signed;
        review.ReviewerUserId = actingUserId;
        review.SignedAt = now;
        review.ElectronicSignature = signature;
        review.Conclusion = conclusion;
        await _db.SaveChangesAsync();

        var findingCount = await _db.AccessReviewFindings.CountAsync(f => f.ReviewId == reviewId);
        _db.AuditLogs.Add(Audit(review.Id, "ACCESS_REVIEW_SIGNED", "AccessReviewSigned", AuditActionCategory.Approval,
            JsonSerializer.Serialize(new { Status = nameof(AccessReviewStatus.Draft) }),
            JsonSerializer.Serialize(new { Status = nameof(AccessReviewStatus.Signed), review.SnapshotSha256, SignatureId = signature.Id, FindingCount = findingCount, Conclusion = conclusion }),
            null, actingUserId, now));
        await _db.SaveChangesAsync();

        return await GetAsync(review.Id);
    }

    // ---- export -------------------------------------------------------------

    public async Task<AccessReviewExportResult> ExportCsvAsync(int reviewId, int actingUserId)
    {
        var detail = await GetAsync(reviewId);
        var findingsByUser = detail.Findings.GroupBy(f => f.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var sb = new StringBuilder();
        string[] header =
        {
            "ReviewId", "PeriodLabel", "ReviewStatus", "SnapshotSha256", "GeneratedAt",
            "Username", "FullName", "Active", "DeactivatedAt", "Role", "RoleType",
            "Permissions", "SectionMemberships", "LastSuccessfulLogin", "NeverLoggedIn", "Dormant", "DaysSinceLastActivity",
            "SodConflicts", "StatusHistory", "Findings"
        };
        sb.Append(string.Join(",", header.Select(Csv))).Append("\r\n");

        foreach (var u in detail.Snapshot.Users)
        {
            var userFindings = findingsByUser.GetValueOrDefault(u.UserId) ?? new List<AccessReviewFindingDto>();
            var fields = new[]
            {
                detail.Id.ToString(), detail.PeriodLabel, detail.Status, detail.SnapshotSha256, Iso(detail.GeneratedAt),
                u.Username, u.FullName, u.IsActive ? "Active" : "Inactive", Iso(u.DeactivatedAt), u.RoleName, u.RoleType,
                string.Join("; ", u.Permissions.Select(p => p.Hidden ? $"{p.Code} (hidden)" : p.Code)),
                string.Join("; ", u.Sections.Select(s => s.SectionName is null ? $"{s.DepartmentName} (whole department)" : $"{s.DepartmentName} / {s.SectionName}")),
                Iso(u.LastSuccessfulLogin), u.NeverLoggedIn ? "Yes" : "No", u.Dormant ? "Yes" : "No", u.DaysSinceLastActivity?.ToString() ?? "",
                string.Join("; ", u.SodConflicts),
                string.Join("; ", u.StatusHistory.Select(h => $"{Iso(h.Timestamp)} {h.Action}")),
                string.Join(" | ", userFindings.Select(f => $"{f.Kind}: {f.Detail} [{f.Action}]{(f.Note is null ? "" : " " + f.Note)}"))
            };
            sb.Append(string.Join(",", fields.Select(Csv))).Append("\r\n");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        _db.CurrentUserId = actingUserId;
        _db.AuditLogs.Add(Audit(reviewId, "ACCESS_REVIEW_EXPORTED", "AccessReviewExported", AuditActionCategory.ImportExport,
            null,
            JsonSerializer.Serialize(new { Format = "csv", detail.Status, detail.SnapshotSha256, Rows = detail.Snapshot.Users.Count }),
            null, actingUserId, now));
        await _db.SaveChangesAsync();

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new AccessReviewExportResult($"access-review-{detail.Id}-{detail.GeneratedAt:yyyyMMdd}.csv", "text/csv; charset=utf-8", bytes);
    }

    // ---- helpers ------------------------------------------------------------

    private static void EnsureDraft(AccessReview review)
    {
        if (review.Status != AccessReviewStatus.Draft)
            throw new InvalidOperationException("This access review is signed and can no longer be changed.");
    }

    private static AuditLog Audit(int reviewId, string action, string actionCode, AuditActionCategory category,
        string? previous, string? next, string? reason, int userId, DateTime now) => new()
    {
        EntityName = EntityName,
        EntityId = reviewId.ToString(),
        Action = action,
        ActionCode = actionCode,
        ActionCategory = category,
        PreviousValue = previous,
        NewValue = next,
        Reason = reason,
        UserId = userId,
        Timestamp = now
    };

    private static AccessReviewFindingDto ToDto(AccessReviewFinding f, IReadOnlyDictionary<int, string> usernames) =>
        new(f.Id, f.ReviewId, f.UserId, usernames.GetValueOrDefault(f.UserId), f.Kind, f.Detail,
            f.Action.ToString(), f.Note, f.RecordedByUserId, f.RecordedAt);

    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static AccessReviewSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<AccessReviewSnapshot>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored access review report could not be read.");

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string Iso(DateTime? value) => value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") ?? "";

    // Quoted always; a cell that a spreadsheet could read as a formula is neutralised.
    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
