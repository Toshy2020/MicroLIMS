using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// The release gate on a received stock lot (Master Testing Materials, phase 2).
// A lot in a category with "approval before use" is held (PendingReview) until a
// second person signs for it here; MaterialService.ConsumeAsync and the lot
// pickers refuse a held or rejected lot. Single step, like media release, with
// the same segregation of duties: whoever received the lot cannot release it.
public class MaterialReleaseService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly SegregationOfDutiesGuard _segregationOfDuties;
    private readonly ReviewGateService _reviewGate;
    private readonly TimeProvider _time;

    public MaterialReleaseService(IMicroLimsDbContext db, IUserSectionScopeService scope,
        SegregationOfDutiesGuard segregationOfDuties, ReviewGateService reviewGate, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _scope = scope;
        _segregationOfDuties = segregationOfDuties;
        _reviewGate = reviewGate;
    }

    public Task DecideAsync(int materialId, int userId, string password, bool approved, string? comment, string? ipAddress) =>
        UnitOfWork.RunAsync(_db, () => DecideCoreAsync(materialId, userId, password, approved, comment, ipAddress));

    private async Task DecideCoreAsync(int materialId, int userId, string password, bool approved, string? comment, string? ipAddress)
    {
        await _scope.EnsureMaterialAccessAsync(userId, materialId);

        var material = await _db.Materials.FirstOrDefaultAsync(m => m.Id == materialId)
            ?? throw new NotFoundException($"Material {materialId} not found.");
        RecordVersion.EnsureCurrent(_db, material);

        if (material.ApprovalStatus is null)
            throw new InvalidOperationException($"Lot {material.BatchNumber} does not need a release.");
        if (material.ApprovalStatus != ApprovalGateStatus.PendingReview)
            throw new InvalidOperationException($"Lot {material.BatchNumber} has already been decided ({material.ApprovalStatus}).");
        if (!approved && string.IsNullOrWhiteSpace(comment))
            throw new InvalidOperationException("A comment is required when rejecting a lot.");

        if (await _segregationOfDuties.DidUserReceiveMaterialAsync(materialId, userId))
            throw new InvalidOperationException("You cannot release a lot you received.");

        // Signs first - a wrong password throws and nothing below is written (the
        // signature, the event and the state change commit in one SaveChangesAsync).
        await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Material, materialId, userId, password,
            approved ? SignatureMeaning.Approved : SignatureMeaning.Rejected,
            ReviewWorkflowEventType.ApprovalDecisionMade, comment?.Trim(), ipAddress,
            approved ? ApprovalDecision.Approve : ApprovalDecision.Reject);

        material.ApprovalStatus = approved ? ApprovalGateStatus.Approved : ApprovalGateStatus.Rejected;
        material.ApprovedByUserId = userId;
        material.ApprovedAt = _time.GetUtcNow().UtcDateTime;
        material.LastModifiedByUserId = userId;
        material.LastModifiedAt = material.ApprovedAt.Value;

        await _db.SaveChangesAsync();
    }
}
