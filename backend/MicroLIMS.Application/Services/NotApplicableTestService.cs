using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public class NotApplicableTestService
{
    public const string CreatedActionCode = "NotApplicableTest.Created";
    public const string HeadApprovedActionCode = "NotApplicableTest.HeadApproved";
    public const string FinalApprovedActionCode = "NotApplicableTest.FinalApproved";
    public const string WithdrawnActionCode = "NotApplicableTest.Withdrawn";

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatures;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public NotApplicableTestService(
        IMicroLimsDbContext db,
        IElectronicSignatureService signatures,
        IAuditEventService audit,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _signatures = signatures;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    public static async Task<IReadOnlyCollection<NotApplicableTest>> LoadApprovedAsync(
        IMicroLimsDbContext db,
        int itemId,
        CancellationToken ct = default) =>
        await db.NotApplicableTests
            .AsNoTracking()
            .Where(n => n.ItemId == itemId && n.Status == NotApplicableStatus.Approved)
            .ToListAsync(ct);

    public Task<NotApplicableTestDto> CreateAsync(int userId, CreateNotApplicableTestRequest r) =>
        UnitOfWork.RunAsync(_db, () => CreateCoreAsync(userId, r));

    private async Task<NotApplicableTestDto> CreateCoreAsync(int userId, CreateNotApplicableTestRequest r)
    {
        var sampleTest = await _db.SampleTests
            .FirstOrDefaultAsync(st => st.ItemId == r.ItemId && st.TestCode == r.TestCode);
        if (sampleTest is null)
            throw new InvalidOperationException($"Test '{r.TestCode}' is not assigned to item {r.ItemId}.");

        var normalizedGrade = SpecificationService.NormalizeGrade(r.Grade);
        if (normalizedGrade != null)
        {
            var itemGrades = await _db.Specifications.AsNoTracking()
                .Where(s => s.ItemId == r.ItemId && s.Grade != null)
                .Select(s => s.Grade!)
                .Distinct()
                .ToListAsync();

            var matchingGrade = itemGrades.FirstOrDefault(g => string.Equals(g, normalizedGrade, StringComparison.OrdinalIgnoreCase));
            if (matchingGrade is null)
                throw new InvalidOperationException($"Grade '{normalizedGrade}' is not one of the item's specification grades.");
            normalizedGrade = matchingGrade;
        }

        await EnsureNotMoistureSupplierAsync(r.ItemId, r.TestCode, normalizedGrade);

        var exists = await _db.NotApplicableTests.AnyAsync(x =>
            x.ItemId == r.ItemId &&
            x.TestCode == r.TestCode &&
            x.Grade == normalizedGrade &&
            x.Status != NotApplicableStatus.Withdrawn);
        if (exists)
            throw new InvalidOperationException(
                $"A non-withdrawn not-applicable record already exists for item {r.ItemId}, test '{r.TestCode}'{(normalizedGrade != null ? $" and grade '{normalizedGrade}'" : "")}.");

        if (string.IsNullOrWhiteSpace(r.Justification))
            throw new InvalidOperationException("Justification is required.");
        if (string.IsNullOrWhiteSpace(r.Criticality))
            throw new InvalidOperationException("Criticality is required.");
        if (string.IsNullOrWhiteSpace(r.ImmediateAction))
            throw new InvalidOperationException("Immediate action is required.");
        if (string.IsNullOrWhiteSpace(r.LongTermAction))
            throw new InvalidOperationException("Long-term action is required.");

        var entity = new NotApplicableTest
        {
            ItemId = r.ItemId,
            Grade = normalizedGrade,
            TestCode = r.TestCode,
            Justification = r.Justification.Trim(),
            Criticality = r.Criticality.Trim(),
            ImmediateAction = r.ImmediateAction.Trim(),
            LongTermAction = r.LongTermAction.Trim(),
            Reference = string.IsNullOrWhiteSpace(r.Reference) ? null : r.Reference.Trim(),
            Status = NotApplicableStatus.Draft,
            CreatedByUserId = userId,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        _db.NotApplicableTests.Add(entity);
        await _db.SaveChangesAsync();

        _db.CurrentUserId = userId;
        await _audit.RecordUserEventAsync(
            CreatedActionCode,
            AuditActionCategory.Configuration,
            "NotApplicableTest",
            reason: entity.Justification,
            entityId: entity.Id.ToString());

        return MapToDto(entity, sampleTest.DisplayName);
    }

    public async Task<NotApplicableTestDto> HeadApproveAsync(
        int userId,
        int id,
        string password,
        string? comment,
        string? ipAddress = null)
    {
        var entity = await _db.NotApplicableTests
            .Include(x => x.HeadSignature)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException($"Not-applicable test record {id} not found.");

        if (entity.Status != NotApplicableStatus.Draft)
            throw new InvalidOperationException("Only draft records can be approved by the Section Head.");

        var signature = await _signatures.SignAsync(
            userId,
            password,
            SignatureMeaning.NotApplicableHeadApproved,
            "NotApplicableTest",
            id,
            comment,
            ipAddress);

        entity.Status = NotApplicableStatus.HeadApproved;
        entity.HeadSignature = signature;

        _db.CurrentUserId = userId;
        await _audit.RecordUserEventAsync(
            HeadApprovedActionCode,
            AuditActionCategory.Configuration,
            "NotApplicableTest",
            reason: comment,
            entityId: id.ToString());

        var displayName = await ResolveDisplayNameAsync(entity.ItemId, entity.TestCode);
        return MapToDto(entity, displayName);
    }

    public async Task<NotApplicableTestDto> FinalApproveAsync(
        int userId,
        int id,
        string password,
        string? comment,
        string? ipAddress = null)
    {
        var entity = await _db.NotApplicableTests
            .Include(x => x.HeadSignature)
            .Include(x => x.FinalSignature)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException($"Not-applicable test record {id} not found.");

        if (entity.Status != NotApplicableStatus.HeadApproved)
            throw new InvalidOperationException("Only a Section Head approved record can be given final approval.");

        var headSignerUserId = entity.HeadSignature?.UserId
            ?? (entity.HeadSignatureId.HasValue
                ? await _db.ElectronicSignatures.Where(s => s.Id == entity.HeadSignatureId.Value).Select(s => s.UserId).FirstOrDefaultAsync()
                : (int?)null);

        if (headSignerUserId is null or 0)
            throw new InvalidOperationException("The Section Head approval could not be verified.");

        if (headSignerUserId == userId)
            throw new InvalidOperationException("The QC Manager approval must come from a different person than the Section Head approval.");

        await EnsureNotMoistureSupplierAsync(entity.ItemId, entity.TestCode, entity.Grade);

        var signature = await _signatures.SignAsync(
            userId,
            password,
            SignatureMeaning.NotApplicableFinalApproved,
            "NotApplicableTest",
            id,
            comment,
            ipAddress);

        entity.Status = NotApplicableStatus.Approved;
        entity.FinalSignature = signature;

        _db.CurrentUserId = userId;
        await _audit.RecordUserEventAsync(
            FinalApprovedActionCode,
            AuditActionCategory.Configuration,
            "NotApplicableTest",
            reason: comment,
            entityId: id.ToString());

        var displayName = await ResolveDisplayNameAsync(entity.ItemId, entity.TestCode);
        return MapToDto(entity, displayName);
    }

    public async Task<NotApplicableTestDto> WithdrawAsync(
        int userId,
        int id,
        string password,
        string reason,
        string? ipAddress = null)
    {
        var entity = await _db.NotApplicableTests
            .Include(x => x.HeadSignature)
            .Include(x => x.FinalSignature)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException($"Not-applicable test record {id} not found.");

        if (entity.Status == NotApplicableStatus.Withdrawn)
            throw new InvalidOperationException("The record is already withdrawn.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Reason is required.");

        var signature = await _signatures.SignAsync(
            userId,
            password,
            SignatureMeaning.NotApplicableWithdrawn,
            "NotApplicableTest",
            id,
            reason.Trim(),
            ipAddress);

        entity.Status = NotApplicableStatus.Withdrawn;

        _db.CurrentUserId = userId;
        await _audit.RecordUserEventAsync(
            WithdrawnActionCode,
            AuditActionCategory.Configuration,
            "NotApplicableTest",
            reason: reason.Trim(),
            entityId: id.ToString());

        var displayName = await ResolveDisplayNameAsync(entity.ItemId, entity.TestCode);
        return MapToDto(entity, displayName);
    }

    public async Task<List<NotApplicableTestDto>> ListForItemAsync(int itemId)
    {
        var records = await _db.NotApplicableTests
            .AsNoTracking()
            .Include(x => x.HeadSignature)
            .Include(x => x.FinalSignature)
            .Where(x => x.ItemId == itemId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        var assignedNames = await _db.SampleTests
            .Where(st => st.ItemId == itemId)
            .ToDictionaryAsync(st => st.TestCode, st => st.DisplayName);

        var defNames = await _db.TestDefinitions
            .ToDictionaryAsync(td => td.Code, td => td.DisplayName);

        return records.Select(r =>
        {
            var name = assignedNames.GetValueOrDefault(r.TestCode)
                ?? defNames.GetValueOrDefault(r.TestCode)
                ?? r.TestCode;
            return MapToDto(r, name);
        }).ToList();
    }

    // A loss-on-drying or water test feeds the dried or anhydrous basis assays of the item; declaring it not
    // applicable would leave those assays with no moisture to correct by. The checks below guard that from
    // every side: record create/approve, spec save, and a moisture-role change.
    private static ResultBasis? BasisFedBy(MoistureRole role) => role switch
    {
        MoistureRole.LossOnDrying => ResultBasis.PercentDriedBasis,
        MoistureRole.Water => ResultBasis.PercentAnhydrousBasis,
        _ => null
    };

    private static string DeclaredMessage(string testName, string itemName) =>
        $"{testName} is declared not applicable for {itemName}; it cannot supply moisture for a dried/anhydrous assay. Withdraw the not-applicable record first.";

    // True when a spec of the item (grade null on either side = all grades) reports on the basis this role feeds.
    private static async Task<bool> SpecNeedsRoleAsync(IMicroLimsDbContext db, int itemId, MoistureRole role, string? grade)
    {
        var basis = BasisFedBy(role);
        return basis != null && await db.Specifications.AsNoTracking().AnyAsync(s =>
            s.ItemId == itemId && s.ResultBasis == basis
            && (grade == null || s.Grade == null || s.Grade.ToLower() == grade.ToLower()));
    }

    private async Task EnsureNotMoistureSupplierAsync(int itemId, string testCode, string? grade)
    {
        var role = await _db.TestDefinitions.Where(td => td.Code == testCode).Select(td => td.MoistureRole).FirstOrDefaultAsync();
        if (await SpecNeedsRoleAsync(_db, itemId, role, grade))
            throw new InvalidOperationException(
                $"{await ResolveDisplayNameAsync(itemId, testCode)} supplies the moisture for a dried/anhydrous assay of this item; it cannot be declared not applicable.");
    }

    // Spec save: refuse a dried/anhydrous basis while a live record makes the matching moisture test not applicable.
    public static async Task EnsureSpecBasisHasMoistureAsync(IMicroLimsDbContext db, int itemId, ResultBasis? basis, string? grade, CancellationToken ct = default)
    {
        var role = basis switch
        {
            ResultBasis.PercentDriedBasis => MoistureRole.LossOnDrying,
            ResultBasis.PercentAnhydrousBasis => MoistureRole.Water,
            _ => MoistureRole.None
        };
        if (role == MoistureRole.None) return;

        var testName = await (from n in db.NotApplicableTests.AsNoTracking()
                              join td in db.TestDefinitions.AsNoTracking() on n.TestCode equals td.Code
                              where n.ItemId == itemId && n.Status != NotApplicableStatus.Withdrawn && td.MoistureRole == role
                                    && (grade == null || n.Grade == null || n.Grade.ToLower() == grade.ToLower())
                              select td.DisplayName).FirstOrDefaultAsync(ct);
        if (testName != null)
            throw new InvalidOperationException(DeclaredMessage(testName, await ItemNameAsync(db, itemId, ct)));
    }

    // Moisture-role change: refuse giving a test a role while a live record on it leaves a dried/anhydrous spec without it.
    public static async Task EnsureRoleChangeKeepsMoistureAsync(IMicroLimsDbContext db, string testCode, string testName, MoistureRole newRole, CancellationToken ct = default)
    {
        if (newRole == MoistureRole.None) return;
        var records = await db.NotApplicableTests.AsNoTracking()
            .Where(n => n.TestCode == testCode && n.Status != NotApplicableStatus.Withdrawn)
            .Select(n => new { n.ItemId, n.Grade }).ToListAsync(ct);
        foreach (var r in records)
            if (await SpecNeedsRoleAsync(db, r.ItemId, newRole, r.Grade))
                throw new InvalidOperationException(DeclaredMessage(testName, await ItemNameAsync(db, r.ItemId, ct)));
    }

    private static async Task<string> ItemNameAsync(IMicroLimsDbContext db, int itemId, CancellationToken ct) =>
        await db.Items.AsNoTracking().Where(i => i.Id == itemId).Select(i => i.Name).FirstOrDefaultAsync(ct) ?? $"item {itemId}";

    private async Task<string> ResolveDisplayNameAsync(int itemId, string testCode) =>
        await _db.SampleTests
            .Where(st => st.ItemId == itemId && st.TestCode == testCode)
            .Select(st => st.DisplayName)
            .FirstOrDefaultAsync()
        ?? await _db.TestDefinitions
            .Where(td => td.Code == testCode)
            .Select(td => td.DisplayName)
            .FirstOrDefaultAsync()
        ?? testCode;

    private static NotApplicableTestDto MapToDto(NotApplicableTest na, string testDisplayName) =>
        new(
            Id: na.Id,
            ItemId: na.ItemId,
            Grade: na.Grade,
            TestCode: na.TestCode,
            Justification: na.Justification,
            Criticality: na.Criticality,
            ImmediateAction: na.ImmediateAction,
            LongTermAction: na.LongTermAction,
            Reference: na.Reference,
            Status: na.Status,
            HeadSignatureId: na.HeadSignatureId,
            FinalSignatureId: na.FinalSignatureId,
            CreatedByUserId: na.CreatedByUserId,
            CreatedAt: na.CreatedAt,
            TestDisplayName: testDisplayName,
            HeadSignedBy: na.HeadSignature?.UserFullNameSnapshot,
            HeadSignedAt: na.HeadSignature?.SignedAt,
            FinalSignedBy: na.FinalSignature?.UserFullNameSnapshot,
            FinalSignedAt: na.FinalSignature?.SignedAt);
}
