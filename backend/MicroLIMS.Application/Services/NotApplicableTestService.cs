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
        try
        {
            await _audit.RecordUserEventAsync(
                CreatedActionCode,
                AuditActionCategory.Configuration,
                "NotApplicableTest",
                reason: entity.Justification,
                entityId: entity.Id.ToString());
        }
        catch
        {
            if (!_db.Database.IsRelational())
            {
                _db.NotApplicableTests.Remove(entity);
                await _db.SaveChangesAsync();
            }
            throw;
        }

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
