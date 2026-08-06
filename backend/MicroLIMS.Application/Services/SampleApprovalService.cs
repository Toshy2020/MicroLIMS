using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Sample-level approval, replacing the per-TestOrder ApprovalService for
// the main approval flow: Section Head decides once for the whole
// Sample. Only 4 of ApprovalDecision's 6 values are reachable here -
// Investigation/OOSInvestigation stay TestOrder-level-only (ApprovalService).
public class SampleApprovalService
{
    private readonly MicroLimsDbContext _db;
    private readonly ReviewGateService _reviewGate;
    private readonly SampleSummaryService _summary;
    private readonly RecordArchiveService _archive;
    private readonly ResultProjectionService _resultProjection;

    public SampleApprovalService(MicroLimsDbContext db, ReviewGateService reviewGate,
        SampleSummaryService summary, RecordArchiveService archive, ResultProjectionService resultProjection)
    {
        _db = db;
        _reviewGate = reviewGate;
        _summary = summary;
        _archive = archive;
        _resultProjection = resultProjection;
    }

    public async Task DecideAsync(int sampleId, int sectionHeadUserId, string password, ApprovalDecision decision, string? comment, string? ipAddress)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (sample.Status != SampleStatus.UnderApproval)
            throw new InvalidOperationException("Sample must be under approval before a decision can be made.");

        if (sectionHeadUserId == sample.ReviewedByUserId)
            throw new InvalidOperationException("You cannot approve a sample you reviewed.");

        var currentOrders = sample.TestOrders.Where(t => !t.IsSuperseded).ToList();
        foreach (var order in currentOrders)
        {
            if (order.AssignedAnalystId == sectionHeadUserId)
                throw new InvalidOperationException("You cannot approve a sample you tested.");

            var enteredResult = await _db.Results.AnyAsync(r => r.TestOrderId == order.Id && r.EnteredByUserId == sectionHeadUserId)
                || await _db.CountTestReadings.AnyAsync(r => r.TestOrderId == order.Id && r.EnteredByUserId == sectionHeadUserId)
                || await _db.PathogenObservations.AnyAsync(p => p.TestOrderId == order.Id && p.ObservedByUserId == sectionHeadUserId);
            if (enteredResult)
                throw new InvalidOperationException("You cannot approve a sample you tested.");
        }

        var meaning = decision switch
        {
            ApprovalDecision.Approve => SignatureMeaning.Approved,
            ApprovalDecision.Reject => SignatureMeaning.Rejected,
            ApprovalDecision.NewSampleRequest => SignatureMeaning.Rejected,
            ApprovalDecision.RetestRetainedSample => SignatureMeaning.RetestRequested,
            _ => throw new InvalidOperationException($"'{decision}' is not a valid sample-level decision.")
        };

        // Signs first - if password verification fails, nothing below is
        // written (the signature, the event, and the state change below
        // commit together in the single SaveChangesAsync at the end).
        var signature = await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Sample, sampleId, sectionHeadUserId, password,
            meaning, ReviewWorkflowEventType.ApprovalDecisionMade, comment, ipAddress, decision);

        switch (decision)
        {
            case ApprovalDecision.Approve:
                sample.Status = SampleStatus.Approved;
                sample.ApprovedByUserId = sectionHeadUserId;
                sample.ApprovedAt = DateTime.UtcNow;
                sample.ApprovalDecision = ApprovalDecision.Approve;
                foreach (var order in currentOrders) order.Status = ApprovalStatus.Approved;
                break;

            case ApprovalDecision.Reject:
                sample.Status = SampleStatus.Rejected;
                sample.ApprovalDecision = ApprovalDecision.Reject;
                foreach (var order in currentOrders) order.Status = ApprovalStatus.Rejected;
                break;

            case ApprovalDecision.NewSampleRequest:
                sample.Status = SampleStatus.Rejected;
                sample.ApprovalDecision = ApprovalDecision.NewSampleRequest;
                foreach (var order in currentOrders) order.Status = ApprovalStatus.Rejected;
                break;

            case ApprovalDecision.RetestRetainedSample:
                sample.Status = SampleStatus.InTesting;
                sample.ApprovalDecision = ApprovalDecision.RetestRetainedSample;
                foreach (var order in currentOrders)
                {
                    order.IsSuperseded = true;
                    _db.WorkflowHistories.Add(new WorkflowHistory
                    {
                        TestOrderId = order.Id,
                        FromStep = order.CurrentStep,
                        ToStep = order.CurrentStep,
                        Note = $"Retest ordered by {signature.UserFullNameSnapshot}",
                        PerformedByUserId = sectionHeadUserId
                    });

                    _db.TestOrders.Add(new TestOrder
                    {
                        SampleId = sampleId,
                        TestCode = order.TestCode,
                        Status = ApprovalStatus.Pending,
                        CurrentStep = WorkflowStep.Waiting,
                        AssignedAnalystId = order.AssignedAnalystId,
                        RoomId = order.RoomId,
                        IsSuperseded = false
                    });
                }
                // AutoSubmitForReviewIfReadyAsync fires naturally once the
                // fresh TestOrders' results complete a second round.
                break;
        }

        await _db.SaveChangesAsync();

        // Approval happens after every result is already projected, so the
        // ApprovedBy/ApprovedAt/SampleStatus fields on this Sample's
        // ResultRecord rows are only ever filled in on this second pass.
        if (decision is ApprovalDecision.Approve or ApprovalDecision.Reject)
            await _resultProjection.RefreshApprovalFieldsAsync(sampleId);

        // Freeze an immutable PDF of the record as decided. Only for
        // decisions that close the sample - a retest sends it back into
        // testing, so there is no final version to archive yet.
        if (decision != ApprovalDecision.RetestRetainedSample)
        {
            var document = await _summary.BuildReportDocumentAsync(sampleId);
            if (document is not null)
                await _archive.ArchiveAsync(ReviewEntityTypes.Sample, sampleId, document, $"Sample {decision}", sectionHeadUserId);
        }
    }
}
