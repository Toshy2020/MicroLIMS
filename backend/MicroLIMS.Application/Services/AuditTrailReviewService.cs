using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

// Periodic audit trail review (EU GMP Annex 11 s.9, 21 CFR 11.10(e)).
// Scope and CutoffAuditLogId are fixed when the review starts so entries
// written afterwards never silently enter the review. Once signed, the review
// is immutable (enforced here and by PostgreSQL triggers).
public class AuditTrailReviewService : IAuditTrailReviewService
{
    private const int DefaultPageSize = 25;
    private const int MaxTitleLength = 200;
    private const int MaxEntityNameLength = 150;
    private const int MaxRecordIdLength = 100;
    private const int MaxTextLength = 2000;

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatureService;
    private readonly AuditSearchService _auditSearch;
    private readonly PermissionService _permissionService;
    private readonly TimeProvider _time;

    public AuditTrailReviewService(
        IMicroLimsDbContext db,
        IElectronicSignatureService signatureService,
        AuditSearchService auditSearch,
        PermissionService permissionService,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _signatureService = signatureService;
        _auditSearch = auditSearch;
        _permissionService = permissionService;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuditTrailReviewDto> StartAsync(
        StartAuditTrailReviewRequest request,
        int actingUserId,
        CancellationToken ct = default)
    {
        await RequireAuditReviewPermissionAsync(actingUserId, ct);

        var title = RequireBoundedText(request.Title, "Title", MaxTitleLength);
        var now = _time.GetUtcNow().UtcDateTime;
        var periodFrom = NormalizeUtc(request.PeriodFromUtc);
        var periodTo = NormalizeUtc(request.PeriodToUtc);

        if (periodFrom >= periodTo)
            throw new BusinessRuleException(
                MessageCodes.AuditReviewPeriodStartNotBeforeEnd,
                "Review period start must be earlier than period end.");
        if (periodTo > now)
            throw new BusinessRuleException(
                MessageCodes.AuditReviewPeriodEndInFuture,
                "Review period end cannot be in the future.");

        if (!Enum.IsDefined(request.ScopeKind))
            throw new BusinessRuleException(
                MessageCodes.AuditReviewInvalidScopeKind,
                "Invalid audit trail review scope kind.");

        var (scopeEntityName, scopeRecordId) = ValidateScope(request.ScopeKind, request.EntityName, request.RecordId);

        var cutoff = await _db.AuditLogs
            .AsNoTracking()
            .Select(a => (int?)a.Id)
            .MaxAsync(ct) ?? 0;

        var entryCount = await BuildScopeQuery(periodFrom, periodTo, request.ScopeKind, scopeEntityName, scopeRecordId)
            .Where(a => a.Id <= cutoff)
            .CountAsync(ct);

        var review = new AuditTrailReview
        {
            Title = title,
            PeriodFromUtc = periodFrom,
            PeriodToUtc = periodTo,
            ScopeKind = request.ScopeKind,
            ScopeEntityName = scopeEntityName,
            ScopeRecordId = scopeRecordId,
            CutoffAuditLogId = cutoff,
            EntryCount = entryCount,
            StartedByUserId = actingUserId,
            StartedAtUtc = now,
            Status = AuditTrailReviewStatus.InProgress
        };

        _db.CurrentUserId = actingUserId;
        _db.AuditTrailReviews.Add(review);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(review.Id, ct);
    }

    public async Task<PagedResult<AuditLogDto>> GetEntriesAsync(
        int reviewId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var review = await _db.AuditTrailReviews
            .AsNoTracking()
            .Where(r => r.Id == reviewId)
            .Select(r => new
            {
                r.PeriodFromUtc,
                r.PeriodToUtc,
                r.ScopeKind,
                r.ScopeEntityName,
                r.ScopeRecordId,
                r.CutoffAuditLogId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                MessageCodes.AuditReviewNotFound,
                $"Audit trail review {reviewId} not found.",
                new Dictionary<string, string> { ["reviewId"] = reviewId.ToString(CultureInfo.InvariantCulture) });

        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize, DefaultPageSize);

        var baseQuery = BuildScopeQuery(
                review.PeriodFromUtc,
                review.PeriodToUtc,
                review.ScopeKind,
                review.ScopeEntityName,
                review.ScopeRecordId)
            .Where(a => a.Id <= review.CutoffAuditLogId);

        var totalCount = await baseQuery.CountAsync(ct);
        var rows = await baseQuery
            .OrderByDescending(a => a.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        var items = await _auditSearch.ToDtosAsync(rows, ct);
        return new PagedResult<AuditLogDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AuditTrailReviewFindingDto> AddFindingAsync(
        int reviewId,
        int auditLogId,
        string note,
        int actingUserId,
        CancellationToken ct = default)
    {
        await RequireAuditReviewPermissionAsync(actingUserId, ct);

        var trimmedNote = RequireBoundedText(note, "Finding note", MaxTextLength);

        var review = await _db.AuditTrailReviews
            .AsNoTracking()
            .Where(r => r.Id == reviewId)
            .Select(r => new
            {
                r.Id,
                r.Status,
                r.PeriodFromUtc,
                r.PeriodToUtc,
                r.ScopeKind,
                r.ScopeEntityName,
                r.ScopeRecordId,
                r.CutoffAuditLogId
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                MessageCodes.AuditReviewNotFound,
                $"Audit trail review {reviewId} not found.",
                new Dictionary<string, string> { ["reviewId"] = reviewId.ToString(CultureInfo.InvariantCulture) });

        if (review.Status != AuditTrailReviewStatus.InProgress)
            throw new BusinessRuleException(
                MessageCodes.AuditReviewSignedCannotAddFindings,
                "Findings cannot be added to a signed audit trail review.");

        var inScope = await BuildScopeQuery(
                review.PeriodFromUtc,
                review.PeriodToUtc,
                review.ScopeKind,
                review.ScopeEntityName,
                review.ScopeRecordId)
            .Where(a => a.Id == auditLogId && a.Id <= review.CutoffAuditLogId)
            .AnyAsync(ct);

        if (!inScope)
            throw new BusinessRuleException(
                MessageCodes.AuditReviewEntryOutsideScope,
                $"Audit log entry {auditLogId} is outside the fixed scope of audit trail review {reviewId}.",
                new Dictionary<string, string>
                {
                    ["auditLogId"] = auditLogId.ToString(CultureInfo.InvariantCulture),
                    ["reviewId"] = reviewId.ToString(CultureInfo.InvariantCulture)
                });

        var duplicate = await _db.AuditTrailReviewFindings
            .AsNoTracking()
            .AnyAsync(f => f.ReviewId == reviewId && f.AuditLogId == auditLogId, ct);

        if (duplicate)
            throw new BusinessRuleException(
                MessageCodes.AuditReviewDuplicateFinding,
                $"A finding has already been recorded for audit log entry {auditLogId} in this review.",
                new Dictionary<string, string> { ["auditLogId"] = auditLogId.ToString(CultureInfo.InvariantCulture) });

        var now = _time.GetUtcNow().UtcDateTime;
        var finding = new AuditTrailReviewFinding
        {
            ReviewId = reviewId,
            AuditLogId = auditLogId,
            Note = trimmedNote,
            Status = AuditFindingStatus.Open,
            RecordedByUserId = actingUserId,
            RecordedAtUtc = now
        };

        _db.CurrentUserId = actingUserId;
        _db.AuditTrailReviewFindings.Add(finding);
        await _db.SaveChangesAsync(ct);

        var names = await LoadUserNamesAsync(new[] { actingUserId }, ct);
        return new AuditTrailReviewFindingDto(
            finding.Id,
            finding.ReviewId,
            finding.AuditLogId,
            finding.Note,
            finding.Status,
            finding.ClosedComment,
            finding.ClosedByUserId,
            null,
            finding.ClosedAtUtc,
            finding.RecordedByUserId,
            names.GetValueOrDefault(finding.RecordedByUserId),
            finding.RecordedAtUtc);
    }

    public Task<AuditTrailReviewDto> SignAsync(
        int reviewId,
        string password,
        string conclusion,
        uint expectedVersion,
        int actingUserId,
        string? ipAddress,
        CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => SignCoreAsync(reviewId, password, conclusion, expectedVersion, actingUserId, ipAddress, ct));

    private async Task<AuditTrailReviewDto> SignCoreAsync(
        int reviewId,
        string password,
        string conclusion,
        uint expectedVersion,
        int actingUserId,
        string? ipAddress,
        CancellationToken ct)
    {
        await RequireAuditReviewPermissionAsync(actingUserId, ct);

        var review = await _db.AuditTrailReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException(
                MessageCodes.AuditReviewNotFound,
                $"Audit trail review {reviewId} not found.",
                new Dictionary<string, string> { ["reviewId"] = reviewId.ToString(CultureInfo.InvariantCulture) });

        if (review.StartedByUserId != actingUserId)
            throw new UnauthorizedAccessException("Only the user who started the audit trail review may sign it.");

        _db.ExpectedVersion = expectedVersion;
        RecordVersion.EnsureCurrent(_db, review);

        if (review.Status != AuditTrailReviewStatus.InProgress)
            throw new BusinessRuleException(
                MessageCodes.AuditReviewAlreadySigned,
                "This audit trail review is already signed.");

        var trimmedConclusion = RequireBoundedText(conclusion, "Conclusion", MaxTextLength);

        var signature = await _signatureService.SignAsync(
            actingUserId,
            password ?? string.Empty,
            SignatureMeaning.AuditTrailReviewed,
            "AuditTrailReview",
            review.Id,
            trimmedConclusion,
            ipAddress);

        var findingCount = await _db.AuditTrailReviewFindings
            .AsNoTracking()
            .CountAsync(f => f.ReviewId == review.Id, ct);

        var now = _time.GetUtcNow().UtcDateTime;
        review.Status = AuditTrailReviewStatus.Signed;
        review.Outcome = findingCount > 0
            ? AuditTrailReviewOutcome.FindingsRecorded
            : AuditTrailReviewOutcome.NoFindings;
        review.Conclusion = trimmedConclusion;
        review.SignedAtUtc = now;
        review.ElectronicSignature = signature;

        _db.CurrentUserId = actingUserId;
        await _db.SaveChangesAsync(ct);

        return await GetAsync(review.Id, ct);
    }

    public async Task<AuditTrailReviewFindingDto> CloseFindingAsync(
        int findingId,
        string comment,
        int actingUserId,
        CancellationToken ct = default)
    {
        await RequireAuditReviewPermissionAsync(actingUserId, ct);

        var trimmedComment = RequireBoundedText(comment, "Closing comment", MaxTextLength);

        var finding = await _db.AuditTrailReviewFindings
            .FirstOrDefaultAsync(f => f.Id == findingId, ct)
            ?? throw new NotFoundException(
                MessageCodes.AuditReviewFindingNotFound,
                $"Audit trail review finding {findingId} not found.",
                new Dictionary<string, string> { ["findingId"] = findingId.ToString(CultureInfo.InvariantCulture) });

        if (finding.Status != AuditFindingStatus.Open)
            throw new BusinessRuleException(
                MessageCodes.AuditReviewFindingAlreadyClosed,
                "This finding is already closed.");

        var now = _time.GetUtcNow().UtcDateTime;
        _db.CurrentUserId = actingUserId;
        finding.Status = AuditFindingStatus.Closed;
        finding.ClosedComment = trimmedComment;
        finding.ClosedByUserId = actingUserId;
        finding.ClosedAtUtc = now;

        await _db.SaveChangesAsync(ct);

        var names = await LoadUserNamesAsync(new[] { finding.RecordedByUserId, actingUserId }, ct);
        return new AuditTrailReviewFindingDto(
            finding.Id,
            finding.ReviewId,
            finding.AuditLogId,
            finding.Note,
            finding.Status,
            finding.ClosedComment,
            finding.ClosedByUserId,
            names.GetValueOrDefault(actingUserId),
            finding.ClosedAtUtc,
            finding.RecordedByUserId,
            names.GetValueOrDefault(finding.RecordedByUserId),
            finding.RecordedAtUtc);
    }

    public async Task<IReadOnlyList<AuditTrailReviewListItemDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize, DefaultPageSize);

        var rows = await _db.AuditTrailReviews
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.PeriodFromUtc,
                r.PeriodToUtc,
                r.ScopeKind,
                r.ScopeEntityName,
                r.ScopeRecordId,
                r.CutoffAuditLogId,
                r.EntryCount,
                FindingCount = r.Findings.Count(),
                OpenFindingCount = r.Findings.Count(f => f.Status == AuditFindingStatus.Open),
                r.StartedByUserId,
                r.StartedAtUtc,
                r.Status,
                r.Outcome,
                r.SignedAtUtc,
                SignerFullName = r.ElectronicSignature != null ? r.ElectronicSignature.UserFullNameSnapshot : null,
                SignatureMeaning = r.ElectronicSignature != null ? (SignatureMeaning?)r.ElectronicSignature.MeaningOfSignature : null,
                r.Version
            })
            .ToListAsync(ct);

        var starterNames = await LoadUserNamesAsync(rows.Select(r => r.StartedByUserId), ct);

        var items = new List<AuditTrailReviewListItemDto>(rows.Count);
        foreach (var r in rows)
        {
            var newerEntriesCount = await BuildScopeQuery(
                    r.PeriodFromUtc,
                    r.PeriodToUtc,
                    r.ScopeKind,
                    r.ScopeEntityName,
                    r.ScopeRecordId)
                .Where(a => a.Id > r.CutoffAuditLogId)
                .CountAsync(ct);

            items.Add(new AuditTrailReviewListItemDto(
                r.Id,
                r.Title,
                r.PeriodFromUtc,
                r.PeriodToUtc,
                r.ScopeKind,
                r.ScopeEntityName,
                r.ScopeRecordId,
                r.CutoffAuditLogId,
                r.EntryCount,
                newerEntriesCount,
                r.FindingCount,
                r.OpenFindingCount,
                r.StartedByUserId,
                starterNames.GetValueOrDefault(r.StartedByUserId),
                r.StartedAtUtc,
                r.Status,
                r.Outcome,
                r.SignedAtUtc,
                r.SignerFullName,
                r.SignatureMeaning is { } m ? ReportVocabulary.SignatureMeaning(m.ToString()) : null,
                r.Version));
        }

        return items;
    }

    public async Task<AuditTrailReviewDto> GetAsync(int id, CancellationToken ct = default)
    {
        var review = await _db.AuditTrailReviews
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.PeriodFromUtc,
                r.PeriodToUtc,
                r.ScopeKind,
                r.ScopeEntityName,
                r.ScopeRecordId,
                r.CutoffAuditLogId,
                r.EntryCount,
                r.StartedByUserId,
                r.StartedAtUtc,
                r.Status,
                r.Outcome,
                r.Conclusion,
                r.SignedAtUtc,
                r.ElectronicSignatureId,
                SignerFullName = r.ElectronicSignature != null ? r.ElectronicSignature.UserFullNameSnapshot : null,
                SignerUsername = r.ElectronicSignature != null ? r.ElectronicSignature.UsernameSnapshot : null,
                SignatureMeaning = r.ElectronicSignature != null ? (SignatureMeaning?)r.ElectronicSignature.MeaningOfSignature : null,
                r.Version,
                Findings = r.Findings
                    .OrderBy(f => f.Id)
                    .Select(f => new
                    {
                        f.Id,
                        f.ReviewId,
                        f.AuditLogId,
                        f.Note,
                        f.Status,
                        f.ClosedComment,
                        f.ClosedByUserId,
                        f.ClosedAtUtc,
                        f.RecordedByUserId,
                        f.RecordedAtUtc
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                MessageCodes.AuditReviewNotFound,
                $"Audit trail review {id} not found.",
                new Dictionary<string, string> { ["reviewId"] = id.ToString(CultureInfo.InvariantCulture) });

        var userIds = new List<int> { review.StartedByUserId };
        foreach (var f in review.Findings)
        {
            userIds.Add(f.RecordedByUserId);
            if (f.ClosedByUserId.HasValue)
                userIds.Add(f.ClosedByUserId.Value);
        }

        var userNames = await LoadUserNamesAsync(userIds, ct);

        var newerEntriesCount = await BuildScopeQuery(
                review.PeriodFromUtc,
                review.PeriodToUtc,
                review.ScopeKind,
                review.ScopeEntityName,
                review.ScopeRecordId)
            .Where(a => a.Id > review.CutoffAuditLogId)
            .CountAsync(ct);

        var findings = review.Findings
            .Select(f => new AuditTrailReviewFindingDto(
                f.Id,
                f.ReviewId,
                f.AuditLogId,
                f.Note,
                f.Status,
                f.ClosedComment,
                f.ClosedByUserId,
                f.ClosedByUserId is { } cid ? userNames.GetValueOrDefault(cid) : null,
                f.ClosedAtUtc,
                f.RecordedByUserId,
                userNames.GetValueOrDefault(f.RecordedByUserId),
                f.RecordedAtUtc))
            .ToList();

        return new AuditTrailReviewDto(
            review.Id,
            review.Title,
            review.PeriodFromUtc,
            review.PeriodToUtc,
            review.ScopeKind,
            review.ScopeEntityName,
            review.ScopeRecordId,
            review.CutoffAuditLogId,
            review.EntryCount,
            newerEntriesCount,
            review.StartedByUserId,
            userNames.GetValueOrDefault(review.StartedByUserId),
            review.StartedAtUtc,
            review.Status,
            review.Outcome,
            review.Conclusion,
            review.SignedAtUtc,
            review.ElectronicSignatureId,
            review.SignerFullName,
            review.SignerUsername,
            review.SignatureMeaning is { } m ? ReportVocabulary.SignatureMeaning(m.ToString()) : null,
            review.Version,
            findings);
    }

    private async Task RequireAuditReviewPermissionAsync(int actingUserId, CancellationToken ct)
    {
        if (!await _permissionService.UserHasPermissionAsync(actingUserId, PermissionConstants.AuditReview, ct))
            throw new UnauthorizedAccessException("You do not hold the Audit.Review permission required to perform audit trail reviews.");
    }

    private IQueryable<AuditLog> BuildScopeQuery(
        DateTime periodFromUtc,
        DateTime periodToUtc,
        AuditReviewScopeKind scopeKind,
        string? scopeEntityName,
        string? scopeRecordId)
    {
        var request = new AuditSearchRequest(
            FromDate: periodFromUtc,
            ToDate: periodToUtc,
            BatchNumber: null,
            ControlNumber: null,
            MediaLotNumber: null,
            SampleReferenceNumber: null,
            ReferenceStrainCode: null,
            CryovialCode: null,
            SampleId: null,
            TestOrderId: null,
            UserId: null,
            EntityName: scopeKind is AuditReviewScopeKind.EntityName or AuditReviewScopeKind.Record ? scopeEntityName : null,
            Action: null,
            EntityId: scopeKind == AuditReviewScopeKind.Record ? scopeRecordId : null);

        return _auditSearch.ApplyFilters(request);
    }

    private async Task<Dictionary<int, string>> LoadUserNamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
            return new Dictionary<int, string>();

        return await _db.Users
            .AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string RequireBoundedText(string? value, string fieldName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            switch (fieldName)
            {
                case "Title":
                    throw new BusinessRuleException(MessageCodes.AuditReviewTitleRequired, "Title is required.");
                case "Finding note":
                    throw new BusinessRuleException(MessageCodes.AuditReviewFindingNoteRequired, "Finding note is required.");
                case "Conclusion":
                    throw new BusinessRuleException(MessageCodes.AuditReviewConclusionRequired, "Conclusion is required.");
                case "Closing comment":
                    throw new BusinessRuleException(MessageCodes.AuditReviewClosingCommentRequired, "Closing comment is required.");
                case "Scope entity name":
                    throw new BusinessRuleException(MessageCodes.AuditReviewScopeEntityNameRequired, "Scope entity name is required.");
                default:
                    throw new BusinessRuleException(MessageCodes.AuditReviewScopeRecordIdRequired, "Scope record ID is required.");
            }
        }
        if (trimmed.Length > maxLength)
        {
            var max = maxLength.ToString(CultureInfo.InvariantCulture);
            switch (fieldName)
            {
                case "Title":
                    throw new BusinessRuleException(
                        MessageCodes.AuditReviewTitleTooLong,
                        $"Title must be {maxLength} characters or fewer.",
                        new Dictionary<string, string> { ["max"] = max });
                case "Finding note":
                    throw new BusinessRuleException(
                        MessageCodes.AuditReviewFindingNoteTooLong,
                        $"Finding note must be {maxLength} characters or fewer.",
                        new Dictionary<string, string> { ["max"] = max });
                case "Conclusion":
                    throw new BusinessRuleException(
                        MessageCodes.AuditReviewConclusionTooLong,
                        $"Conclusion must be {maxLength} characters or fewer.",
                        new Dictionary<string, string> { ["max"] = max });
                case "Closing comment":
                    throw new BusinessRuleException(
                        MessageCodes.AuditReviewClosingCommentTooLong,
                        $"Closing comment must be {maxLength} characters or fewer.",
                        new Dictionary<string, string> { ["max"] = max });
                case "Scope entity name":
                    throw new BusinessRuleException(
                        MessageCodes.AuditReviewScopeEntityNameTooLong,
                        $"Scope entity name must be {maxLength} characters or fewer.",
                        new Dictionary<string, string> { ["max"] = max });
                default:
                    throw new BusinessRuleException(
                        MessageCodes.AuditReviewScopeRecordIdTooLong,
                        $"Scope record ID must be {maxLength} characters or fewer.",
                        new Dictionary<string, string> { ["max"] = max });
            }
        }
        return trimmed;
    }

    private static (string? EntityName, string? RecordId) ValidateScope(
        AuditReviewScopeKind scopeKind,
        string? rawEntityName,
        string? rawRecordId)
    {
        return scopeKind switch
        {
            AuditReviewScopeKind.All => (null, null),
            AuditReviewScopeKind.EntityName => (
                RequireBoundedText(rawEntityName, "Scope entity name", MaxEntityNameLength),
                null),
            AuditReviewScopeKind.Record => (
                RequireBoundedText(rawEntityName, "Scope entity name", MaxEntityNameLength),
                RequireBoundedText(rawRecordId, "Scope record ID", MaxRecordIdLength)),
            _ => throw new BusinessRuleException(
                MessageCodes.AuditReviewInvalidScopeKind,
                "Invalid audit trail review scope kind.")
        };
    }
}
