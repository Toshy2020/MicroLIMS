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
// CanSelect: the current user may choose this test (otherwise it is shown read-only).
public record RetestEligibleTestDto(string TestCode, string DisplayName, int SectionId, string SectionName, bool CanSelect = false);

public record SelectTestsRequest(IReadOnlyList<string> TestCodes, string Password, string? Comment);

// A periodic retest is received without test orders (ProductWorkflowEngine);
// a Section Head or Supervisor later chooses which of the item's assigned tests
// to run - those that apply to the sample's grade, in the laboratories it was
// received for. The choice is per laboratory: each call covers the still-pending
// laboratories the signer covers, is signed and audited on its own, and the
// sample stays "awaiting" until every laboratory has its tests.
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

    // The tests of the laboratories still pending (empty once every laboratory has its tests,
    // or when the sample never needed a choice). With a user, CanSelect marks the ones they may choose.
    public async Task<IReadOnlyList<RetestEligibleTestDto>> GetEligibleAsync(int sampleId, int? userId = null, CancellationToken ct = default)
    {
        var sample = await _db.Samples.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sampleId, ct)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");
        var eligible = await EligibleAsync(sample, ct);
        if (userId is not int uid || !IsHeadOrSupervisor(await RoleOfAsync(uid, ct)))
            return eligible;
        var scope = await _scope.GetAccessibleSectionIdsAsync(uid, ct);
        return eligible.Select(e => e with { CanSelect = scope is null || scope.Contains(e.SectionId) }).ToList();
    }

    public Task SelectAsync(int sampleId, SelectTestsRequest request, int userId, string? ipAddress, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => SelectCoreAsync(sampleId, request, userId, ipAddress, ct));

    private async Task SelectCoreAsync(int sampleId, SelectTestsRequest request, int userId, string? ipAddress, CancellationToken ct)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == sampleId, ct)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");

        if (!IsHeadOrSupervisor(await RoleOfAsync(userId, ct)))
            throw new UnauthorizedAccessException("Only a Section Head or Supervisor can choose the tests for a periodic retest.");

        const string AlreadySelected = "Tests for this sample have already been selected.";
        if (!sample.AwaitingTestSelection)
            throw new InvalidOperationException(AlreadySelected);
        // A second laboratory may still choose after the first has started testing (InTesting);
        // only a closed sample is refused.
        if (sample.Status is SampleStatus.Voided or SampleStatus.Cancelled or SampleStatus.Rejected or SampleStatus.Approved)
            throw new InvalidOperationException("This sample is closed; tests can no longer be selected.");

        // The laboratories this call covers: still pending, and the signer's own.
        var userScope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        var pending = SampleSectionRollup.PendingSectionIds(sample).ToList();
        var mine = userScope is null ? pending : pending.Where(userScope.Contains).ToList();
        if (mine.Count == 0)
        {
            // A head of a laboratory that already has its tests is told so; anyone else is simply not in scope.
            if (userScope is not null && sample.TestOrders.Any(t => userScope.Contains(t.SectionId)))
                throw new InvalidOperationException(AlreadySelected);
            throw new UnauthorizedAccessException("This sample belongs to a laboratory section you are not assigned to.");
        }

        var codes = (request.TestCodes ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
            throw new InvalidOperationException("Choose at least one test.");

        var eligible = await EligibleAsync(sample, ct);
        var chosen = new List<RetestEligibleTestDto>();
        foreach (var code in codes)
            chosen.Add(eligible.FirstOrDefault(e => e.TestCode == code && mine.Contains(e.SectionId))
                ?? throw new InvalidOperationException($"Test {code} is not available for this sample."));

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

        // Only the laboratories this call covered leave the pending list.
        var remaining = pending.Except(mine).ToList();
        sample.AwaitingTestSelection = remaining.Count > 0;
        sample.PendingTargetSectionIds = remaining.Count > 0 ? SampleSectionRollup.FormatPendingSectionIds(remaining) : null;
        sample.TestSelectionSignature = signature;

        // As when a laboratory is added: an existing preparation is kept, otherwise the tests decide.
        var needs = await PreparationRules.InitialStatusAsync(_db, sample.TestOrders.Select(o => o.SectionId));
        if (!await _db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id))
            sample.PreparationStatus = needs;
        else if (needs == SamplePreparationStatus.NeedsPreparation)
            sample.PreparationStatus = needs;

        var labNames = await _db.DocumentSections.AsNoTracking().Where(s => mine.Contains(s.Id)).Select(s => s.Name).ToListAsync(ct);
        await _audit.RecordUserEventAsync(SelectedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            reason: comment ?? "Tests chosen for periodic retest",
            changes: new[]
            {
                new AuditFieldChange("Laboratory", null, string.Join(", ", labNames.OrderBy(n => n, StringComparer.Ordinal))),
                new AuditFieldChange("Tests", null, string.Join(", ", chosen.Select(c => c.TestCode).OrderBy(c => c, StringComparer.Ordinal)))
            },
            entityId: sample.Id.ToString(), sampleId: sample.Id, cancellationToken: ct);
    }

    private static bool IsHeadOrSupervisor(RoleType? role) => role is RoleType.SectionHead or RoleType.Reviewer;

    private Task<RoleType?> RoleOfAsync(int userId, CancellationToken ct) =>
        _db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => u.Role != null ? (RoleType?)u.Role.Type : null).FirstOrDefaultAsync(ct);

    private async Task<IReadOnlyList<RetestEligibleTestDto>> EligibleAsync(Sample sample, CancellationToken ct)
    {
        var sectionIds = SampleSectionRollup.PendingSectionIds(sample).ToList();
        if (sample.ItemId is not int itemId || sectionIds.Count == 0)
            return Array.Empty<RetestEligibleTestDto>();

        var assigned = await _db.SampleTests.AsNoTracking().Where(t => t.ItemId == itemId).ToListAsync(ct);
        var specs = await _db.Specifications.AsNoTracking().Where(s => s.ItemId == itemId).ToListAsync(ct);
        var notApplicable = await NotApplicableTestService.LoadApprovedAsync(_db, itemId, ct);
        var testSections = await TestSectionLookup.ResolveAsync(_db, assigned.Select(t => t.TestCode), ct);
        var sectionNames = await _db.DocumentSections.AsNoTracking().Where(s => sectionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return assigned
            .Where(t => sectionIds.Contains(testSections[t.TestCode])
                && ProductWorkflowEngine.AppliesToGrade(specs, notApplicable, t.TestCode, sample.Grade))
            .Select(t => new RetestEligibleTestDto(t.TestCode, t.DisplayName, testSections[t.TestCode],
                sectionNames.GetValueOrDefault(testSections[t.TestCode], string.Empty)))
            .OrderBy(e => e.SectionName).ThenBy(e => e.TestCode, StringComparer.Ordinal)
            .ToList();
    }
}
