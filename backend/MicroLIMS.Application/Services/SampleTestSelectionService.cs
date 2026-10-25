using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Named RetestEligibleTestDto: Services already has an HPLC EligibleTestDto.
public record RetestEligibleTestDto(string TestCode, string DisplayName, int SectionId, string SectionName);

public record SelectTestsRequest(IReadOnlyList<string> TestCodes, string Password, string? Comment);

// A periodic retest is received without test orders (ProductWorkflowEngine);
// a Section Head or Supervisor of one of its laboratories later chooses which
// of the item's assigned tests to run - those that apply to the sample's
// grade, in the laboratories it was received for. The choice is signed.
public class SampleTestSelectionService
{
    public const string SelectedActionCode = "SAMPLE_TESTS_SELECTED";

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatures;
    private readonly IAuditEventService _audit;
    private readonly IUserSectionScopeService _scope;

    public SampleTestSelectionService(IMicroLimsDbContext db, IElectronicSignatureService signatures,
        IAuditEventService audit, IUserSectionScopeService scope)
    {
        _db = db;
        _signatures = signatures;
        _audit = audit;
        _scope = scope;
    }

    // Empty once the tests have been chosen (or when the sample never needed a choice).
    public async Task<IReadOnlyList<RetestEligibleTestDto>> GetEligibleAsync(int sampleId, CancellationToken ct = default)
    {
        var sample = await _db.Samples.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sampleId, ct)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");
        return await EligibleAsync(sample, ct);
    }

    public Task SelectAsync(int sampleId, SelectTestsRequest request, int userId, string? ipAddress, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => SelectCoreAsync(sampleId, request, userId, ipAddress, ct));

    private async Task SelectCoreAsync(int sampleId, SelectTestsRequest request, int userId, string? ipAddress, CancellationToken ct)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == sampleId, ct)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");

        var role = await _db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => u.Role != null ? (RoleType?)u.Role.Type : null).FirstOrDefaultAsync(ct);
        if (role is not (RoleType.SectionHead or RoleType.Reviewer))
            throw new UnauthorizedAccessException("Only a Section Head or Supervisor can choose the tests for a periodic retest.");

        if (!sample.AwaitingTestSelection)
            throw new InvalidOperationException("Tests for this sample have already been selected.");
        if (sample.Status != SampleStatus.Received)
            throw new InvalidOperationException("The tests can no longer be chosen: the sample is closed.");

        var userScope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (userScope is not null && !SampleSectionRollup.PendingSectionIds(sample).Any(userScope.Contains))
            throw new UnauthorizedAccessException("This sample belongs to a laboratory section you are not assigned to.");

        var codes = (request.TestCodes ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
            throw new InvalidOperationException("Choose at least one test.");

        var eligible = await EligibleAsync(sample, ct);
        var chosen = new List<RetestEligibleTestDto>();
        foreach (var code in codes)
        {
            var test = eligible.FirstOrDefault(e => e.TestCode == code)
                ?? throw new InvalidOperationException($"Test {code} is not available for this sample.");
            if (userScope is not null && !userScope.Contains(test.SectionId))
                throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
            chosen.Add(test);
        }

        // Signs first - a failed verification creates nothing (the signature,
        // the orders and the audit event commit in the single save below).
        _db.CurrentUserId = userId;
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var signature = await _signatures.SignAsync(userId, request.Password, SignatureMeaning.TestsSelected,
            ReviewEntityTypes.Sample, sample.Id, comment, ipAddress);

        foreach (var test in chosen)
        {
            sample.TestOrders.Add(new TestOrder
            {
                TestCode = test.TestCode,
                SectionId = test.SectionId,
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Waiting
            });
        }
        sample.AwaitingTestSelection = false;
        sample.PendingTargetSectionIds = null;
        sample.TestSelectionSignature = signature;
        sample.PreparationStatus = await PreparationRules.InitialStatusAsync(_db, sample.TestOrders.Select(o => o.SectionId));

        await _audit.RecordUserEventAsync(SelectedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            reason: comment ?? "Tests chosen for periodic retest",
            changes: new[] { new AuditFieldChange("Tests", null, string.Join(", ", chosen.Select(c => c.TestCode).OrderBy(c => c))) },
            entityId: sample.Id.ToString(), sampleId: sample.Id, cancellationToken: ct);
    }

    private async Task<IReadOnlyList<RetestEligibleTestDto>> EligibleAsync(Sample sample, CancellationToken ct)
    {
        var sectionIds = SampleSectionRollup.PendingSectionIds(sample).ToList();
        if (sample.ItemId is not int itemId || sectionIds.Count == 0)
            return Array.Empty<RetestEligibleTestDto>();

        var assigned = await _db.SampleTests.AsNoTracking().Where(t => t.ItemId == itemId).ToListAsync(ct);
        var specs = await _db.Specifications.AsNoTracking().Where(s => s.ItemId == itemId).ToListAsync(ct);
        var testSections = await TestSectionLookup.ResolveAsync(_db, assigned.Select(t => t.TestCode), ct);
        var sectionNames = await _db.DocumentSections.AsNoTracking().Where(s => sectionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return assigned
            .Where(t => sectionIds.Contains(testSections[t.TestCode])
                && ProductWorkflowEngine.AppliesToGrade(specs, t.TestCode, sample.Grade))
            .Select(t => new RetestEligibleTestDto(t.TestCode, t.DisplayName, testSections[t.TestCode],
                sectionNames.GetValueOrDefault(testSections[t.TestCode], string.Empty)))
            .OrderBy(e => e.SectionName).ThenBy(e => e.TestCode, StringComparer.Ordinal)
            .ToList();
    }
}
