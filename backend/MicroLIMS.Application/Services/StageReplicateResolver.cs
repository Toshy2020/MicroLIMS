using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Distinguishes "the sample's stage role could not be determined at all"
// from "the stage role is known but this test has no replicate counts
// configured for it" - callers (the future Standard-Comparison Assay
// entry workflow) need to tell these apart to give the right blocking
// message, per the Stage model's gate rule.
public enum StageReplicateResolutionStatus
{
    Resolved,
    SampleStageNotReconciled,
    NotConfiguredForStage
}

public record StageReplicateResolution(
    StageReplicateResolutionStatus Status,
    ProductionStageRole? StageRole,
    int? StandardReplicates,
    int? SampleReplicates,
    string? Message)
{
    public bool IsConfigured => Status == StageReplicateResolutionStatus.Resolved;
}

// Read-only resolver: given a TestOrder, works out the sample's resolved
// ProductionStageRole (via Sample.ProductionStageId) and looks up the
// TestDefinitionStageReplicate row configured for that (TestDefinition,
// Role) pair. No workflow enforcement here - this slice only exposes the
// lookup; the Standard-Comparison Assay entry workflow (a later slice)
// is what actually blocks entry on a "not configured" outcome.
public static class StageReplicateResolver
{
    public static async Task<StageReplicateResolution> ResolveForTestOrderAsync(
        MicroLimsDbContext db, int testOrderId, CancellationToken cancellationToken = default)
    {
        var order = await db.TestOrders
            .Where(o => o.Id == testOrderId)
            .Select(o => new { o.TestCode, o.SampleId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        var testDefinitionId = await db.TestDefinitions
            .Where(t => t.Code == order.TestCode)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (testDefinitionId is null)
            throw new InvalidOperationException($"Test code '{order.TestCode}' is not in the Test Master.");

        return await ResolveAsync(db, order.SampleId, testDefinitionId.Value, cancellationToken);
    }

    public static async Task<StageReplicateResolution> ResolveAsync(
        MicroLimsDbContext db, int sampleId, int testDefinitionId, CancellationToken cancellationToken = default)
    {
        var productionStageId = await db.Samples
            .Where(s => s.Id == sampleId)
            .Select(s => s.ProductionStageId)
            .FirstOrDefaultAsync(cancellationToken);

        if (productionStageId is null)
        {
            return new StageReplicateResolution(
                StageReplicateResolutionStatus.SampleStageNotReconciled,
                null, null, null,
                "Sample has no reconciled production stage - it cannot be resolved to a stage role. Contact a Section Head to set its stage.");
        }

        var stageRole = await db.ProductionStages
            .Where(p => p.Id == productionStageId.Value)
            .Select(p => (ProductionStageRole?)p.Role)
            .FirstOrDefaultAsync(cancellationToken);

        if (stageRole is null)
        {
            return new StageReplicateResolution(
                StageReplicateResolutionStatus.SampleStageNotReconciled,
                null, null, null,
                "Sample has no reconciled production stage - it cannot be resolved to a stage role. Contact a Section Head to set its stage.");
        }

        var replicate = await db.TestDefinitionStageReplicates
            .Where(r => r.TestDefinitionId == testDefinitionId && r.Role == stageRole.Value)
            .Select(r => new { r.StandardReplicates, r.SampleReplicates })
            .FirstOrDefaultAsync(cancellationToken);

        if (replicate is null)
        {
            return new StageReplicateResolution(
                StageReplicateResolutionStatus.NotConfiguredForStage,
                stageRole, null, null,
                $"No replicate configuration is set for stage role {stageRole} on this test - configure it in Test Master.");
        }

        return new StageReplicateResolution(
            StageReplicateResolutionStatus.Resolved,
            stageRole, replicate.StandardReplicates, replicate.SampleReplicates, null);
    }
}
