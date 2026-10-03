using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Sign-off flow (spec 2026-10-03): review, return, reject, approve.
// Every signing method validates state first (a refused action leaves no
// signature), signs, then mutates.
public partial class WorkingStandardService
{
    private const string EntityType = "WorkingStandardQualification";

    public async Task<WorkingStandardQualificationDto> ReviewAsync(int id, WorkingStandardSignRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, userId, ct);
        RequireStatus(q, WorkingStandardQualificationStatus.Assayed, "Only an assayed qualification can be reviewed.");
        if (!q.Passed) throw new InvalidOperationException("A failed qualification can only be rejected.");
        if (userId == q.PreparedByUserId)
            throw new InvalidOperationException("The analyst who submitted the qualification cannot review it.");

        var sig = await _signatures.SignAsync(userId, r.Password, SignatureMeaning.Reviewed, EntityType, q.Id, r.Comment, ip);
        q.Status = WorkingStandardQualificationStatus.Reviewed;
        q.ReviewedByUserId = userId;
        q.ReviewedAt = _clock.UtcNow.UtcDateTime;
        q.ReviewedSignatureId = sig.Id;
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, userId, ct);
    }

    // No signature: the reason and the audit trail record the return.
    public async Task<WorkingStandardQualificationDto> ReturnAsync(int id, WorkingStandardReturnRequest r, int userId, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, userId, ct);
        RequireStatus(q, WorkingStandardQualificationStatus.Assayed, "Only an assayed qualification can be returned.");
        var reason = RequireReason(r.Reason);

        q.Status = WorkingStandardQualificationStatus.Draft;
        q.ReturnReason = reason;
        q.MeanAssayPercent = null;
        q.RsdPercent = null;
        q.PotencyPercent = null;
        q.ReplicateAssaysJson = null;
        q.FailureReasons = null;
        q.Passed = false;
        q.PreparedByUserId = null;
        q.PreparedAt = null;
        q.PreparedSignatureId = null;
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, userId, ct);
    }

    public async Task<WorkingStandardQualificationDto> RejectAtReviewAsync(int id, WorkingStandardReasonRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, userId, ct);
        RequireStatus(q, WorkingStandardQualificationStatus.Assayed, "Only an assayed qualification can be rejected at review.");
        return await RejectAsync(q, r, userId, ip, ct);
    }

    public async Task<WorkingStandardQualificationDto> RejectAtApprovalAsync(int id, WorkingStandardReasonRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, userId, ct);
        RequireStatus(q, WorkingStandardQualificationStatus.Reviewed, "Only a reviewed qualification can be rejected at approval.");
        if (userId == q.PreparedByUserId)
            throw new InvalidOperationException("The analyst who submitted the qualification cannot reject it at approval.");
        return await RejectAsync(q, r, userId, ip, ct);
    }

    public async Task<WorkingStandardQualificationDto> ApproveAsync(int id, WorkingStandardSignRequest r, int userId, string? ip, CancellationToken ct = default)
    {
        var q = await LoadAsync(id, userId, ct);
        RequireStatus(q, WorkingStandardQualificationStatus.Reviewed, "Only a reviewed qualification can be approved.");
        if (!q.Passed) throw new InvalidOperationException("A failed qualification can only be rejected.");
        if (userId == q.PreparedByUserId)
            throw new InvalidOperationException("The analyst who submitted the qualification cannot approve it.");
        if (userId == q.ReviewedByUserId)
            throw new InvalidOperationException("The reviewer cannot also approve the qualification.");

        var sig = await _signatures.SignAsync(userId, r.Password, SignatureMeaning.Approved, EntityType, q.Id, r.Comment, ip);

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var expiry = _clock.LabToday.AddMonths(WorkingStandardRules.ValidityMonths).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (q.Kind == WorkingStandardQualificationKind.Initial)
        {
            var code = await SolutionPreparationCode.NextAsync(
                _db.Materials.Where(m => m.MaterialType == MaterialType.WorkingStandard && m.Code != null).Select(m => m.Code!),
                "WS-", _clock.ToLabLocal(nowUtc), ct);
            q.WorkingStandardMaterial = new Material
            {
                SectionId = q.SectionId, MaterialType = MaterialType.WorkingStandard,
                MaterialMasterEntryId = q.MaterialMasterEntryId, MaterialName = q.MaterialMasterEntry!.Name,
                ManufacturerName = "In-house", BatchNumber = q.SourceBatchNumber,
                ReceivingDate = nowUtc, ExpiryDate = expiry, Code = code, Location = q.Location!,
                QuantityReceived = q.QuantityGrams!.Value, QuantityRemaining = q.QuantityGrams.Value, Unit = MaterialUnit.Gram,
                Purity = q.PotencyPercent, MoisturePercent = q.MoisturePercent,
                CreatedByUserId = userId, CreatedAt = nowUtc, LastModifiedByUserId = userId, LastModifiedAt = nowUtc,
            };
            _db.Materials.Add(q.WorkingStandardMaterial);
        }
        else
        {
            var lot = q.WorkingStandardMaterial!;
            lot.Purity = q.PotencyPercent;
            lot.MoisturePercent = q.MoisturePercent;
            lot.ExpiryDate = expiry;
            lot.LastModifiedByUserId = userId;
            lot.LastModifiedAt = nowUtc;
        }

        q.Status = WorkingStandardQualificationStatus.Approved;
        q.ApprovedByUserId = userId;
        q.ApprovedAt = nowUtc;
        q.ApprovedSignatureId = sig.Id;
        _db.CurrentUserId = userId;
        if (!await _db.TrySaveChangesAsync(UniqueIndexNames.WorkingStandardCode))
            throw new InvalidOperationException("Another approval took that working standard code at the same moment - sign again.");
        return await GetAsync(id, userId, ct);
    }

    private async Task<WorkingStandardQualificationDto> RejectAsync(WorkingStandardQualification q, WorkingStandardReasonRequest r, int userId, string? ip, CancellationToken ct)
    {
        var reason = RequireReason(r.Reason);
        var sig = await _signatures.SignAsync(userId, r.Password, SignatureMeaning.Rejected, EntityType, q.Id, reason, ip);
        q.Status = WorkingStandardQualificationStatus.Rejected;
        q.RejectedByUserId = userId;
        q.RejectedAt = _clock.UtcNow.UtcDateTime;
        q.RejectedSignatureId = sig.Id;
        q.RejectReason = reason;
        _db.CurrentUserId = userId;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(q.Id, userId, ct);
    }

    private static void RequireStatus(WorkingStandardQualification q, WorkingStandardQualificationStatus expected, string message)
    {
        if (q.Status != expected) throw new InvalidOperationException(message);
    }

    private static string RequireReason(string? reason)
    {
        var t = reason?.Trim();
        if (string.IsNullOrEmpty(t) || t.Length > 500) throw new InvalidOperationException("Enter a reason.");
        return t;
    }
}
