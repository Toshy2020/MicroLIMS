using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Workflows;

public interface IPathogenWorkflowEngine : IStatefulWorkflowEngine
{
    // Records one observation (TSB / RVS / XLD_TSI / Simple) and advances
    // the workflow if that observation completes the chain.
    Task<PathogenObservation> RecordObservationAsync(int testOrderId, string stepName, bool growthObserved, int userId);

    // Final call once the chain is complete. Throws if incomplete.
    Task<string> InterpretAsync(int testOrderId);
}

// Universal chain: TSB -> Observation -> Continue -> Detection Media ->
// Growth = Detected / No Growth = Absent.
// Salmonella exception: TSB -> RVS -> XLD+TSI -> Detected/Absent.
// This is the most tightly frozen rule in the spec, so every branch is
// backed by a persisted PathogenObservation - nothing is inferred.
public class PathogenWorkflowEngine : IPathogenWorkflowEngine
{
    private const string Salmonella = "PATHOGEN_SALMONELLA";

    private readonly MicroLimsDbContext _db;

    public PathogenWorkflowEngine(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<PathogenObservation> RecordObservationAsync(int testOrderId, string stepName, bool growthObserved, int userId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);

        var existing = await _db.PathogenObservations
            .Where(o => o.TestOrderId == testOrderId)
            .OrderBy(o => o.StepOrder)
            .ToListAsync();

        ValidateStepOrder(order.TestCode, stepName, existing);

        var observation = new PathogenObservation
        {
            TestOrderId = testOrderId,
            StepName = stepName,
            StepOrder = existing.Count + 1,
            GrowthObserved = growthObserved,
            ObservedByUserId = userId
        };
        _db.PathogenObservations.Add(observation);

        if (order.CurrentStep == WorkflowStep.Waiting)
            await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Running, userId, $"Started with {stepName}");

        await _db.SaveChangesAsync();

        // If this observation completes the chain (or is a definitive
        // "no growth -> stop early" result), mark Ready for review.
        if (IsChainComplete(order.TestCode, existing.Append(observation).ToList()))
        {
            await WorkflowStateMachine.TransitionAsync(_db, order, WorkflowStep.Ready, userId, "Pathogen chain complete");
        }

        return observation;
    }

    public async Task<string> InterpretAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var observations = await _db.PathogenObservations
            .Where(o => o.TestOrderId == testOrderId)
            .OrderBy(o => o.StepOrder)
            .ToListAsync();

        if (!IsChainComplete(order.TestCode, observations))
            throw new InvalidOperationException("Pathogen observation chain is not complete yet.");

        if (order.TestCode.Equals(Salmonella, StringComparison.OrdinalIgnoreCase))
        {
            var tsb = observations.FirstOrDefault(o => o.StepName == "TSB");
            var rvs = observations.FirstOrDefault(o => o.StepName == "RVS");
            var xldTsi = observations.FirstOrDefault(o => o.StepName == "XLD_TSI");

            if (tsb is { GrowthObserved: false }) return "Absent";
            if (rvs is { GrowthObserved: false }) return "Absent";
            return xldTsi is { GrowthObserved: true } ? "Detected" : "Absent";
        }

        // Universal chain: last recorded observation is the Detection
        // Media step; growth there is the definitive call. An earlier
        // "no growth" at TSB also stops the chain early as Absent.
        var last = observations.Last();
        return last.GrowthObserved ? "Detected" : "Absent";
    }

    public async Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = await ValidateAsync(testOrderId);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));

        var next = order.CurrentStep switch
        {
            WorkflowStep.Waiting => WorkflowStep.Running,
            WorkflowStep.Running => WorkflowStep.Ready,
            WorkflowStep.Ready => WorkflowStep.Reviewed,
            WorkflowStep.Reviewed => WorkflowStep.Approved,
            _ => order.CurrentStep
        };

        return await WorkflowStateMachine.TransitionAsync(_db, order, next, performedByUserId, note);
    }

    public async Task<List<string>> ValidateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = new List<string>();

        if (order.CurrentStep == WorkflowStep.Running)
        {
            var observations = await _db.PathogenObservations.Where(o => o.TestOrderId == testOrderId).ToListAsync();
            if (!IsChainComplete(order.TestCode, observations))
                errors.Add("Pathogen observation chain is not yet complete for this test.");
        }

        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }

    private static void ValidateStepOrder(string testCode, string stepName, List<PathogenObservation> existing)
    {
        if (testCode.Equals(Salmonella, StringComparison.OrdinalIgnoreCase))
        {
            var expectedOrder = new[] { "TSB", "RVS", "XLD_TSI" };
            var expectedNext = expectedOrder[existing.Count];
            if (!stepName.Equals(expectedNext, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Salmonella chain violation: expected '{expectedNext}' next, got '{stepName}'.");

            // If a prior step was no-growth, the chain should already be
            // closed - recording a further step is a workflow-order violation.
            if (existing.Any(o => !o.GrowthObserved))
                throw new InvalidOperationException("Cannot record further Salmonella steps after a no-growth result closed the chain.");
        }
        else
        {
            if (existing.Any())
                throw new InvalidOperationException("This pathogen test's observation chain is already complete.");
        }
    }

    private static bool IsChainComplete(string testCode, List<PathogenObservation> observations)
    {
        if (testCode.Equals(Salmonella, StringComparison.OrdinalIgnoreCase))
        {
            if (observations.Any(o => o.StepName == "TSB" && !o.GrowthObserved)) return true;
            if (observations.Any(o => o.StepName == "RVS" && !o.GrowthObserved)) return true;
            return observations.Any(o => o.StepName == "XLD_TSI");
        }

        // Universal chain completes as soon as one observation is recorded
        // (TSB no-growth = Absent immediately, or Detection Media result).
        return observations.Count > 0;
    }
}
