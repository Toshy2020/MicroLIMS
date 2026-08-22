using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record BackfillResult(int Created, int Updated, int Skipped, List<string> Errors);

// Builds and maintains the flattened ResultRecord read-model the Reports
// module queries, from the three source shapes results are actually
// recorded in (CountTestReading, PathogenObservation, SampleLocation).
// Every Upsert* method is idempotent - re-running it for the same source
// row (same SourceTable/SourceId/Round) updates the existing projection
// row in place rather than duplicating it, thanks to the unique index in
// ResultRecordConfiguration.
public class ResultProjectionService
{
    private readonly MicroLimsDbContext _db;
    private readonly ILogger<ResultProjectionService> _logger;

    public ResultProjectionService(MicroLimsDbContext db, ILogger<ResultProjectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<ResultRecord> GetOrCreateAsync(string sourceTable, int sourceId, int round)
    {
        var existing = await _db.ResultRecords
            .FirstOrDefaultAsync(r => r.SourceTable == sourceTable && r.SourceId == sourceId && r.Round == round);
        if (existing is not null) return existing;

        var created = new ResultRecord { SourceTable = sourceTable, SourceId = sourceId, Round = round };
        _db.ResultRecords.Add(created);
        return created;
    }

    // A pathogen TestOrder has exactly one reportable outcome per round,
    // whichever step ended up concluding it - so its projection row is
    // identified by TestOrder+Round rather than by source row id.
    // Checks the change tracker before the database because a row staged
    // in the same unit of work is not yet visible to a LINQ query, and
    // missing it would add a duplicate.
    private async Task<ResultRecord> GetOrCreatePathogenRecordAsync(int testOrderId, int round)
    {
        var staged = _db.ResultRecords.Local
            .FirstOrDefault(r => r.SourceTable == "WorkflowStepResult" && r.TestOrderId == testOrderId && r.Round == round);
        if (staged is not null) return staged;

        var existing = await _db.ResultRecords
            .FirstOrDefaultAsync(r => r.SourceTable == "WorkflowStepResult" && r.TestOrderId == testOrderId && r.Round == round);
        if (existing is not null) return existing;

        var created = new ResultRecord { SourceTable = "WorkflowStepResult", TestOrderId = testOrderId, Round = round };
        _db.ResultRecords.Add(created);
        return created;
    }

    // A TestOrder's "round" is its 1-based ordinal position among every
    // TestOrder ever created for the same Sample+TestCode, oldest first -
    // round 1 is the original order, round 2 the one SampleApprovalService's
    // RetestRetainedSample decision creates to replace it, etc. Keeping
    // this derived from TestOrder creation order (rather than stored
    // anywhere) means it can never drift from IsSuperseded history.
    private async Task<int> ComputeRoundAsync(int sampleId, string testCode, int testOrderId)
    {
        var orderIds = await _db.TestOrders
            .Where(o => o.SampleId == sampleId && o.TestCode == testCode)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync();
        var index = orderIds.IndexOf(testOrderId);
        return index >= 0 ? index + 1 : 1;
    }

    // ReportedResult is either a plain whole number or "<n" (below the
    // detection limit) - see CountTestReading/SampleLocation's
    // ReportedResult comment. Imputation (substituting DetectionLimit/2
    // for trending) happens at trend-query time, never here.
    private static (bool isBelowDetectionLimit, decimal? detectionLimit) ParseDetectionLimit(string reportedResult)
    {
        if (string.IsNullOrWhiteSpace(reportedResult) || !reportedResult.TrimStart().StartsWith('<'))
            return (false, null);

        var afterLt = reportedResult.TrimStart()[1..].Trim();
        var numberPart = afterLt.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return decimal.TryParse(numberPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var limit)
            ? (true, limit)
            : (true, null);
    }

    // Same Spec -> Action -> Alert precedence used by TestWorkflowEngine.Compare
    // - just mapping the already-decided status string onto ResultLevel.
    private static ResultLevel MapResultLevel(string? status) => status switch
    {
        "OutOfSpecification" => ResultLevel.OutOfSpecification,
        "ActionLimitExceeded" => ResultLevel.ActionLevel,
        "AlertLimitExceeded" => ResultLevel.AlertLevel,
        "WithinLimits" => ResultLevel.WithinLimit,
        _ => ResultLevel.NotApplicable
    };

    // SamplePreparation.Unit is "ml"/"gm"/"bottle"/"cap"/"25cm2" (see its
    // comment) - only the mass ("gm") and volume ("ml") units translate to
    // a CFU/g or CFU/mL result unit. Everything else (bottle/cap/25cm2 - a
    // whole-item or swab-area basis) reports per plate instead. When no
    // SamplePreparation exists at all (shouldn't happen for Product/RM/PM/
    // Water, but guards Count Tests reached some other way), fall back to
    // the category's typical unit.
    private static string DeriveCountUnit(SamplePreparation? preparation, SampleCategory category)
    {
        if (preparation is not null)
        {
            return preparation.Unit.ToLowerInvariant() switch
            {
                "gm" => "CFU/g",
                "ml" => "CFU/mL",
                _ => "CFU/Plate"
            };
        }
        return category == SampleCategory.Water ? "CFU/mL" : "CFU/g";
    }

    public async Task UpsertFromCountTestReadingAsync(int countTestReadingId)
    {
        var reading = await _db.CountTestReadings
            .Include(r => r.TestOrder!).ThenInclude(o => o.Sample!).ThenInclude(s => s.Item)
            .Include(r => r.TestOrder!).ThenInclude(o => o.Sample!).ThenInclude(s => s.WaterSamplingPoint)
            .FirstOrDefaultAsync(r => r.Id == countTestReadingId)
            ?? throw new InvalidOperationException($"CountTestReading {countTestReadingId} not found.");

        var order = reading.TestOrder ?? throw new InvalidOperationException($"CountTestReading {countTestReadingId} has no TestOrder.");
        var sample = order.Sample ?? throw new InvalidOperationException($"TestOrder {order.Id} has no Sample - cannot project CountTestReading {countTestReadingId}.");

        var testDefinition = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == order.TestCode);
        var preparation = await _db.SamplePreparations.FirstOrDefaultAsync(p => p.SampleId == sample.Id);
        var enteredBy = await _db.Users.FirstOrDefaultAsync(u => u.Id == reading.EnteredByUserId);
        var round = await ComputeRoundAsync(sample.Id, order.TestCode, order.Id);

        var (isBelowDetectionLimit, detectionLimit) = ParseDetectionLimit(reading.ReportedResult);

        var record = await GetOrCreateAsync("CountTestReading", reading.Id, round);
        record.SampleId = sample.Id;
        record.TestOrderId = order.Id;
        record.ReferenceNumber = sample.ReferenceNumber;
        record.Category = sample.Category;
        record.SubjectName = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? string.Empty;
        record.SubjectDetail = null;
        record.BatchNumber = sample.BatchNumber;
        record.ControlNumber = sample.ControlNumber;
        record.TestCode = order.TestCode;
        record.TestDisplayName = testDefinition?.DisplayName ?? order.TestCode;
        record.ResultKind = ResultKind.Quantitative;
        record.NumericValue = reading.CalculatedResult;
        record.ReportedValue = reading.ReportedResult;
        record.Unit = DeriveCountUnit(preparation, sample.Category);
        record.IsBelowDetectionLimit = isBelowDetectionLimit;
        record.DetectionLimit = detectionLimit;
        record.AlertLimit = reading.AlertLimit;
        record.ActionLimit = reading.ActionLimit;
        record.SpecLimit = reading.SpecLimit;
        record.ResultLevel = MapResultLevel(reading.Status);
        record.ResultEnteredAt = reading.EnteredAt;
        record.ResultEnteredByUserId = reading.EnteredByUserId;
        record.ResultEnteredByName = enteredBy?.FullName ?? string.Empty;
        record.SampleStatus = sample.Status;
        record.UpdatedAt = DateTime.UtcNow;
    }

    // Projects only the FINAL workflow step's outcome for a pathogen
    // TestOrder - intermediate stages (enrichment/selective/confirmatory
    // setup) are not independently reportable results, just chain
    // plumbing. Sourced from WorkflowStepResult rather than
    // PathogenObservation because a pathogen chain can now conclude on
    // any of several steps (a non-conforming/no-growth selective-plating
    // call ends the chain early; a confirmed detection ends it on
    // Confirmatory Plating or Biochemical Test) - the most recently
    // submitted WorkflowStepResult for this TestOrder IS whichever step
    // actually concluded it, not necessarily the template's IsFinalStep row.
    public async Task UpsertFromPathogenResultAsync(int testOrderId)
    {
        var order = await _db.TestOrders
            .Include(o => o.Sample!).ThenInclude(s => s.Item)
            .Include(o => o.Sample!).ThenInclude(s => s.WaterSamplingPoint)
            .FirstOrDefaultAsync(o => o.Id == testOrderId)
            ?? throw new InvalidOperationException($"TestOrder {testOrderId} not found.");
        var sample = order.Sample ?? throw new InvalidOperationException($"TestOrder {testOrderId} has no Sample.");

        var testDefinition = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == order.TestCode)
            ?? throw new InvalidOperationException($"Test code \"{order.TestCode}\" has no workflow template configured.");

        var finalResult = await _db.WorkflowStepResults
            .Where(r => r.TestOrderId == testOrderId)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"TestOrder {testOrderId} has no workflow step result recorded yet.");

        // SkippedBiochemical/BiochemicalResultText are the only fields a
        // concluding WorkflowStepResult can carry that represent a
        // positive detection call (see AnalystDecision.SubmitAsDetected
        // and the biochemical submission path) - a step that ended the
        // chain WITHOUT either (e.g. a non-conforming/no-growth selective-
        // plating result) is a negative outcome.
        // A reviewer send-back (RequiresBiochemical) puts the order back
        // into testing: the detection call it carried is no longer a
        // reportable outcome, and reporting it as "Detected" would state
        // a result the reviewer explicitly refused to accept.
        var reportedValue = finalResult.RequiresBiochemical
            ? "Pending Confirmation"
            : finalResult.SkippedBiochemical || finalResult.BiochemicalResultText is not null
                ? "Detected"
                : "Not Detected";
        var enteredByUserId = finalResult.SubmittedByUserId;
        var enteredAt = finalResult.SubmittedAtUtc;

        var enteredBy = await _db.Users.FirstOrDefaultAsync(u => u.Id == enteredByUserId);
        var round = await ComputeRoundAsync(sample.Id, order.TestCode, order.Id);

        // Keyed on TestOrder+Round, NOT on the concluding
        // WorkflowStepResult's id: which step concludes a pathogen chain
        // can change (a send-back and a later biochemical submission both
        // move the concluding row), and keying on SourceId would leave
        // the superseded row standing as a second, contradictory
        // ResultRecord for the same test order. SourceId still records
        // which row the current projection came from.
        var record = await GetOrCreatePathogenRecordAsync(order.Id, round);
        record.SourceId = finalResult.Id;
        record.SampleId = sample.Id;
        record.TestOrderId = order.Id;
        record.ReferenceNumber = sample.ReferenceNumber;
        record.Category = sample.Category;
        record.SubjectName = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? string.Empty;
        record.SubjectDetail = null;
        record.BatchNumber = sample.BatchNumber;
        record.ControlNumber = sample.ControlNumber;
        record.TestCode = order.TestCode;
        record.TestDisplayName = testDefinition.DisplayName;
        record.ResultKind = ResultKind.Qualitative;
        record.NumericValue = null;
        record.ReportedValue = reportedValue;
        record.Unit = null;
        record.IsBelowDetectionLimit = false;
        record.DetectionLimit = null;
        record.AlertLimit = null;
        record.ActionLimit = null;
        record.SpecLimit = null;
        record.ResultLevel = ResultLevel.NotApplicable;
        record.ResultEnteredAt = enteredAt;
        record.ResultEnteredByUserId = enteredByUserId;
        record.ResultEnteredByName = enteredBy?.FullName ?? string.Empty;
        record.SampleStatus = sample.Status;
        record.UpdatedAt = DateTime.UtcNow;
    }

    // EM/After Cleaning batch results - one ResultRecord per location, the
    // point of the whole batch model (each room/part is separately
    // reportable). Handles both shapes SampleLocation can carry: a CFU
    // count (RecordBatchResultsAsync) or a Detected/Absent call
    // (RecordBatchPathogenResultsAsync) - distinguished by whether
    // CalculatedResult was ever set.
    public async Task UpsertFromSampleLocationAsync(int sampleLocationId)
    {
        var location = await _db.SampleLocations
            .Include(l => l.Sample!)
            .Include(l => l.TestOrder!)
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .Include(l => l.WaterSamplingPoint)
            .FirstOrDefaultAsync(l => l.Id == sampleLocationId)
            ?? throw new InvalidOperationException($"SampleLocation {sampleLocationId} not found.");

        if (location.Status is null)
            throw new InvalidOperationException($"SampleLocation {sampleLocationId} has no result recorded yet.");

        var sample = location.Sample ?? throw new InvalidOperationException($"SampleLocation {sampleLocationId} has no Sample.");
        var order = location.TestOrder ?? throw new InvalidOperationException($"SampleLocation {sampleLocationId} has no TestOrder.");

        var testDefinition = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Code == order.TestCode);
        var enteredBy = location.EnteredByUserId is int enteredByUserId
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == enteredByUserId)
            : null;
        var round = await ComputeRoundAsync(sample.Id, order.TestCode, order.Id);

        var record = await GetOrCreateAsync("SampleLocation", location.Id, round);
        record.SampleId = sample.Id;
        record.TestOrderId = order.Id;
        record.ReferenceNumber = sample.ReferenceNumber;
        record.Category = sample.Category;
        record.SubjectName = location.RoomTestConfiguration?.Room?.Name ?? location.MachinePartConfiguration?.MachinePart?.Name ?? location.WaterSamplingPoint?.Code ?? string.Empty;
        record.SubjectDetail = location.RoomTestConfiguration?.Room?.GradeClassification;
        record.BatchNumber = sample.BatchNumber;
        record.ControlNumber = sample.ControlNumber;
        record.TestCode = order.TestCode;
        record.TestDisplayName = testDefinition?.DisplayName ?? order.TestCode;

        if (location.CalculatedResult.HasValue)
        {
            var (isBelowDetectionLimit, detectionLimit) = ParseDetectionLimit(location.ReportedResult ?? string.Empty);
            record.ResultKind = ResultKind.Quantitative;
            record.NumericValue = location.CalculatedResult;
            record.ReportedValue = location.ReportedResult ?? string.Empty;
            record.Unit = location.Unit; // set by TestWorkflowEngine.DeriveBatchLocationUnit at result-entry time
            record.IsBelowDetectionLimit = isBelowDetectionLimit;
            record.DetectionLimit = detectionLimit;
            record.AlertLimit = location.AlertLimit;
            record.ActionLimit = location.ActionLimit;
            record.SpecLimit = location.SpecLimit;
            record.ResultLevel = MapResultLevel(location.Status);
        }
        else
        {
            // Pathogen batch result (Detected/Absent) - no numeric limits.
            record.ResultKind = ResultKind.Qualitative;
            record.NumericValue = null;
            record.ReportedValue = location.ReportedResult ?? location.Status ?? string.Empty;
            record.Unit = null;
            record.IsBelowDetectionLimit = false;
            record.DetectionLimit = null;
            record.AlertLimit = null;
            record.ActionLimit = null;
            record.SpecLimit = null;
            record.ResultLevel = ResultLevel.NotApplicable;
        }

        record.ResultEnteredAt = location.EnteredAt ?? DateTime.UtcNow;
        record.ResultEnteredByUserId = location.EnteredByUserId ?? 0;
        record.ResultEnteredByName = enteredBy?.FullName ?? string.Empty;
        record.SampleStatus = sample.Status;
        record.UpdatedAt = DateTime.UtcNow;
    }

    // Called after a Sample-level approval decision - approval always
    // happens after every TestOrder's results are already projected, so
    // ApprovedBy/ApprovedAt/SampleStatus are filled in on a second pass
    // across every projection row belonging to the Sample.
    public async Task RefreshApprovalFieldsAsync(int sampleId)
    {
        var sample = await _db.Samples.FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        var approvedBy = sample.ApprovedByUserId is int approvedByUserId
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == approvedByUserId)
            : null;

        var records = await _db.ResultRecords.Where(r => r.SampleId == sampleId).ToListAsync();
        foreach (var record in records)
        {
            record.ApprovedByUserId = sample.ApprovedByUserId;
            record.ApprovedByName = approvedBy?.FullName;
            record.ApprovedAt = sample.ApprovedAt;
            record.SampleStatus = sample.Status;
            record.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // One-time (but safely repeatable) sweep projecting every existing
    // source row - for standing up the projection against data that
    // predates this feature. Each source row is saved individually so one
    // bad row can't roll back the whole run.
    public async Task<BackfillResult> BackfillAsync()
    {
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();

        var countTestReadingIds = await _db.CountTestReadings.Select(r => r.Id).ToListAsync();
        _logger.LogInformation("ResultRecord backfill: projecting {Count} CountTestReading rows.", countTestReadingIds.Count);
        foreach (var id in countTestReadingIds)
        {
            var existedBefore = await _db.ResultRecords.AnyAsync(r => r.SourceTable == "CountTestReading" && r.SourceId == id);
            try
            {
                await UpsertFromCountTestReadingAsync(id);
                await _db.SaveChangesAsync();
                if (existedBefore) updated++; else created++;
            }
            catch (InvalidOperationException ex)
            {
                skipped++;
                errors.Add($"CountTestReading {id}: {ex.Message}");
            }
        }

        // Only TestOrders that have reached at least one WorkflowStepResult
        // are reportable - an in-progress or never-started pathogen chain
        // has nothing to project yet.
        var pathogenOrderIds = await _db.TestOrders
            .Where(o => _db.WorkflowStepResults.Any(r => r.TestOrderId == o.Id))
            .Select(o => o.Id)
            .Distinct()
            .ToListAsync();
        _logger.LogInformation("ResultRecord backfill: projecting {Count} pathogen TestOrder rows.", pathogenOrderIds.Count);
        foreach (var id in pathogenOrderIds)
        {
            var existedBefore = await _db.ResultRecords.AnyAsync(r => r.SourceTable == "WorkflowStepResult" && r.TestOrderId == id);
            try
            {
                await UpsertFromPathogenResultAsync(id);
                await _db.SaveChangesAsync();
                if (existedBefore) updated++; else created++;
            }
            catch (InvalidOperationException ex)
            {
                skipped++;
                errors.Add($"TestOrder {id} (pathogen): {ex.Message}");
            }
        }

        var sampleLocationIds = await _db.SampleLocations.Where(l => l.Status != null).Select(l => l.Id).ToListAsync();
        _logger.LogInformation("ResultRecord backfill: projecting {Count} SampleLocation rows.", sampleLocationIds.Count);
        foreach (var id in sampleLocationIds)
        {
            var existedBefore = await _db.ResultRecords.AnyAsync(r => r.SourceTable == "SampleLocation" && r.SourceId == id);
            try
            {
                await UpsertFromSampleLocationAsync(id);
                await _db.SaveChangesAsync();
                if (existedBefore) updated++; else created++;
            }
            catch (InvalidOperationException ex)
            {
                skipped++;
                errors.Add($"SampleLocation {id}: {ex.Message}");
            }
        }

        // Samples that were already Approved/Rejected before this feature
        // existed never went through SampleApprovalService.DecideAsync's
        // RefreshApprovalFieldsAsync call - without this, their freshly
        // backfilled rows above would carry the right SampleStatus but
        // null ApprovedBy/ApprovedAt.
        var decidedSampleIds = await _db.Samples
            .Where(s => s.Status == SampleStatus.Approved || s.Status == SampleStatus.Rejected)
            .Select(s => s.Id)
            .ToListAsync();
        _logger.LogInformation("ResultRecord backfill: refreshing approval fields for {Count} already-decided samples.", decidedSampleIds.Count);
        foreach (var sampleId in decidedSampleIds)
            await RefreshApprovalFieldsAsync(sampleId);

        _logger.LogInformation("ResultRecord backfill complete: {Created} created, {Updated} updated, {Skipped} skipped.", created, updated, skipped);
        return new BackfillResult(created, updated, skipped, errors);
    }
}
