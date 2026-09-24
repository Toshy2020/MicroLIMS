using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Adds a second laboratory's tests to a sample already received - same
// sample, same reference - so one combined CoA still covers the batch.
public class AddLaboratoryService
{
    private readonly MicroLimsDbContext _db;
    private readonly ReviewGateService _reviewGate;

    public AddLaboratoryService(MicroLimsDbContext db, ReviewGateService reviewGate)
    {
        _db = db;
        _reviewGate = reviewGate;
    }

    public async Task AddAsync(int sampleId, int sectionId, int userId, string password, string reason, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to add a laboratory.");

        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (SampleSectionRollup.Overall(sample) is OverallSampleStatus.Rejected or OverallSampleStatus.Voided or OverallSampleStatus.Cancelled)
            throw new InvalidOperationException("A laboratory cannot be added to a rejected, voided or cancelled sample.");
        if (sample.TestOrders.Any(t => t.SectionId == sectionId))
            throw new InvalidOperationException("This laboratory is already testing the sample.");
        if (sample.ItemId is not int itemId)
            throw new InvalidOperationException("A laboratory can be added only to a product, raw material or packaging material sample.");

        var item = await _db.Items.Include(i => i.AssignedTests).FirstAsync(i => i.Id == itemId);
        var sections = await TestSectionLookup.ResolveAsync(_db, item.AssignedTests.Select(t => t.TestCode));
        var tests = item.AssignedTests.Where(t => sections[t.TestCode] == sectionId).ToList();
        if (tests.Count == 0)
        {
            var name = await _db.DocumentSections.Where(s => s.Id == sectionId).Select(s => s.Name).FirstOrDefaultAsync() ?? $"section {sectionId}";
            throw new InvalidOperationException($"Item '{item.Name}' has no tests for {name}.");
        }

        SampleSectionRollup.FreezeOpenSections(sample);
        await _reviewGate.SignAndLogAsync(ReviewEntityTypes.Sample, sampleId, userId, password,
            SignatureMeaning.LaboratoryAdded, ReviewWorkflowEventType.LaboratoryAdded, reason.Trim(), ipAddress, null, sectionId);

        foreach (var test in tests)
            sample.TestOrders.Add(new TestOrder { TestCode = test.TestCode, SectionId = sectionId, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        // New work re-opens a sample whose other labs had already finished.
        SampleSectionRollup.GetOrAdd(sample, sectionId).Status = SectionSignoffStatus.InTesting;
        SampleSectionRollup.Apply(sample);
        if (sample.Status == SampleStatus.InTesting && sample.ApprovalDecision == ApprovalDecision.Approve)
        {
            sample.ApprovalDecision = null;
            sample.ApprovedAt = null;
            sample.ApprovedByUserId = null;
        }

        // Microbiology tests need the sample prepared; a sample made Ready by
        // physicochemical-only testing goes back to needing preparation.
        var needs = await PreparationRules.InitialStatusAsync(_db, new[] { sectionId });
        if (needs == SamplePreparationStatus.NeedsPreparation && !await _db.SamplePreparations.AnyAsync(p => p.SampleId == sampleId))
            sample.PreparationStatus = SamplePreparationStatus.NeedsPreparation;

        await _db.SaveChangesAsync();
    }
}
