using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Builds the GET /api/test-workflow/{id}/current-step response: the engine's
// current step plus the sample context, incubation lock, previous steps,
// shared TSB summary and pending return that the step dialogs render. It
// reuses the order, template and step facts the engine already loaded rather
// than querying those rows again.
public class CurrentStepViewService
{
    private readonly MicroLimsDbContext _db;
    private readonly ITestWorkflowEngine _engine;

    public CurrentStepViewService(MicroLimsDbContext db, ITestWorkflowEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<object> GetAsync(int testOrderId)
    {
        var details = await _engine.GetCurrentStepDetailsAsync(testOrderId);
        var current = details.Result;
        var order = details.Order;
        var facts = details.Facts;

        // Sample's "name" has no single field - it is whichever of
        // Item/WaterSamplingPoint/Department/Machine is set for this
        // sample's category, same precedence SampleSummaryService uses
        // for its own DisplayName.
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .FirstOrDefaultAsync(s => s.Id == order.SampleId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        // Media lot, its material and the incubator for every incubation on
        // this order in one query. EF attaches them to the tracked
        // incubations the engine loaded, which the sections below read.
        await _db.Incubations
            .Where(i => i.TestOrderId == testOrderId)
            .Include(i => i.Media).ThenInclude(m => m!.Material)
            .Include(i => i.IncubatorEquipment)
            .LoadAsync();

        string? configuredUnit = null;
        decimal? configuredDilutionFactor = null;
        if (sample.ItemId is not null)
        {
            var spec = await SpecificationLookup.PrimaryAsync(_db, sample.ItemId.Value, order.TestCode);
            configuredUnit = spec?.Unit;
            configuredDilutionFactor = spec?.DilutionFactor;
        }
        else if (sample.WaterSamplingPointId is not null)
        {
            var config = await _db.SamplingConfigurations.FirstOrDefaultAsync(c => c.TestCode == order.TestCode && c.WaterSamplingPointId == sample.WaterSamplingPointId);
            configuredUnit = config?.Unit;
        }

        var cfuUnit = !string.IsNullOrWhiteSpace(configuredUnit)
            ? (configuredUnit.StartsWith("CFU/", StringComparison.OrdinalIgnoreCase) ? configuredUnit : $"CFU/{configuredUnit}")
            : TestWorkflowEngine.GetCfuUnit(sample.Category, null);

        var sampleContext = new Dictionary<string, object?>
        {
            ["sampleName"] = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? sample.Department?.Name ?? sample.Machine?.Name ?? string.Empty,
            ["batchNumber"] = sample.BatchNumber,
            ["controlNumber"] = sample.ControlNumber,
            ["reason"] = sample.CauseOfTesting?.Name,
            ["systemReferenceNumber"] = sample.ReferenceNumber,
            ["sampleType"] = sample.Category.ToString(),
            ["cfuUnit"] = cfuUnit,
            // Null for direct-count sample types (Water/EM/AfterCleaning -
            // always forced to 1 server-side, see TestWorkflowEngine.
            // RecordCountTestAsync) and for pathogen/observation tests,
            // which have no DF. Null for an Item-based count test means
            // "not configured yet" - the frontend blocks result entry in
            // that case rather than falling back to free-entry.
            ["configuredDilutionFactor"] = configuredDilutionFactor
        };

        // Stage (Sample.ProductionStage) is a Finished-Product-only concept
        // - ProductWorkflowEngine.ReceiveAsync only ever populates it for
        // that category, leaving it null for Raw Material, Packaging
        // Material, Water, EM and After Cleaning. The key is left out
        // entirely for every other category, never sent as null.
        if (sample.Category == SampleCategory.FinishedProduct)
            sampleContext["stage"] = sample.ProductionStage;

        var openIncubation = current.OpenIncubation;
        DateTime? minReadyAtUtc = null;
        long remainingMinSeconds = 0;
        // Stage 1 is timed only by the medium this test actually used (see
        // IncubationWindowResolver); stage 2 keeps its own transfer settings.
        IncubationWindow? openWindow = null;
        var windowNotConfigured = false;
        if (openIncubation != null)
        {
            var startUtc = openIncubation.IncubationStartUtc ?? openIncubation.StartedAt;
            int minHours = 0;
            if (openIncubation.StageNumber == 2)
            {
                var stage2 = current.Step?.IncubationStages?.FirstOrDefault(s => s.StageNumber == 2);
                minHours = stage2?.IncubationMinHours ?? current.Step?.IncubationMinHours ?? 0;
            }
            else
            {
                // The incubations' lots and materials were loaded above, so
                // the window resolves from them without another query.
                openWindow = current.Step is null ? null : IncubationWindowResolver.ForIncubation(current.Step, openIncubation);
                windowNotConfigured = openWindow is null;
                minHours = openWindow?.MinHours ?? 0;
            }

            if (minHours > 0)
            {
                minReadyAtUtc = startUtc.AddHours(minHours);
                remainingMinSeconds = Math.Max(0, (long)Math.Ceiling((minReadyAtUtc.Value - DateTime.UtcNow).TotalSeconds));
            }
            else if (!windowNotConfigured && openIncubation.IncubationEndUtc.HasValue)
            {
                minReadyAtUtc = openIncubation.IncubationEndUtc.Value;
                remainingMinSeconds = Math.Max(0, (long)Math.Ceiling((openIncubation.IncubationEndUtc.Value - DateTime.UtcNow).TotalSeconds));
            }
        }

        var isMinLockActive = openIncubation != null && minReadyAtUtc.HasValue && DateTime.UtcNow < minReadyAtUtc.Value && !openIncubation.MinimumDurationOverriddenByUserId.HasValue;
        var incubationLock = openIncubation?.IncubationEndUtc is null ? null : new
        {
            isLocked = windowNotConfigured || isMinLockActive || !openIncubation.IsIncubationComplete,
            windowNotConfigured,
            incubationEndUtc = openIncubation.IncubationEndUtc,
            remainingSeconds = Math.Max(0, (long)Math.Ceiling((openIncubation.IncubationEndUtc.Value - DateTime.UtcNow).TotalSeconds)),
            minReadyAt = minReadyAtUtc,
            remainingMinimumSeconds = remainingMinSeconds,
            stageNumber = openIncubation.StageNumber,
            minimumDurationOverridden = openIncubation.MinimumDurationOverriddenByUserId.HasValue,
            minimumDurationOverriddenAt = openIncubation.MinimumDurationOverriddenAt
        };

        // Grouped rather than keyed straight off the rows: a pre-existing
        // race could leave two rows for the same (TestOrderId, StepName),
        // and a straight dictionary threw ArgumentException on the duplicate
        // key - taking the whole step down for a test order over a flag
        // lookup. The unique index added in AddWorkflowStepResultUniqueStepName
        // stops new duplicates; this keeps the read working for any that predate it.
        var wsrDict = facts.StepResults
            .GroupBy(r => r.StepName)
            .ToDictionary(g => g.Key, g => g.Any(r => r.IsSharedSessionStep == true));

        var previousSteps = facts.Incubations
            .OrderBy(i => i.StepNumber).ThenBy(i => i.Id)
            .Select(i => new
            {
                stepNumber = i.StepNumber,
                stepName = i.StepName,
                stageNumber = i.StageNumber,
                status = i.CompletedAt == null ? "Incubating" : "Complete",
                mediaName = i.Media?.LotNumber,
                lotNumber = i.Media?.LotNumber,
                incubatorName = i.IncubatorEquipment?.Code,
                incubatorSetTemp = i.IncubatorEquipment?.SetPointTemperature,
                incubationStartUtc = i.IncubationStartUtc,
                incubationEndUtc = i.IncubationEndUtc,
                observation = i.Outcome
            })
            .ToList();

        var enrichedPreviousSteps = previousSteps.Select(p => new
        {
            p.stepNumber,
            p.stepName,
            p.stageNumber,
            p.status,
            p.mediaName,
            p.lotNumber,
            p.incubatorName,
            p.incubatorSetTemp,
            p.incubationStartUtc,
            p.incubationEndUtc,
            p.observation,
            isSharedSessionStep = wsrDict.TryGetValue(p.stepName, out var isShared) && isShared == true
        }).ToList();

        // Check if there is a shared TSB on this sample
        object? sharedTsbSummary = null;
        var sharedTsbResult = facts.StepResults
            .Where(r => (r.StepName == "Broth Enrichment" || r.StepName.Contains("TSB")) && r.IsSharedSessionStep == true)
            .OrderByDescending(r => r.Id)
            .FirstOrDefault();

        // A shared TSB result can point at the incubation of a sibling order
        // in the same pathogen session, which this order's facts don't hold.
        var tsbIncubation = sharedTsbResult is null
            ? null
            : facts.Incubations.FirstOrDefault(i => i.Id == sharedTsbResult.IncubationId)
              ?? await _db.Incubations
                  .Include(i => i.Media).ThenInclude(m => m!.Material)
                  .Include(i => i.IncubatorEquipment)
                  .FirstOrDefaultAsync(i => i.Id == sharedTsbResult.IncubationId);

        if (sharedTsbResult != null && tsbIncubation != null)
        {
            var inc = tsbIncubation;
            var tsbStepName = sharedTsbResult.StepName;
            var startedUser = inc.StartedByUserId.HasValue
                ? await _db.Users.Where(u => u.Id == inc.StartedByUserId.Value).Select(u => u.Username).FirstOrDefaultAsync()
                : null;

            var tsbStep = await _db.TestWorkflowSteps
                .Include(s => s.StepMedia).ThenInclude(m => m.Material)
                .Include(s => s.StepMedia).ThenInclude(m => m.IncubationCondition)
                .Where(s => s.TestDefinition != null && s.TestDefinition.Code == order.TestCode && (s.StepName == tsbStepName || s.StepType == StepType.BrothEnrichment || s.StepName.Contains("TSB")))
                .OrderBy(s => s.StepName == tsbStepName ? 0 : 1)
                .FirstOrDefaultAsync();
            // This test's own TSB medium decides its window - never a default.
            var tsbWindow = tsbStep is null ? null : IncubationWindowResolver.ForIncubation(tsbStep, inc);

            var tsbMinReady = tsbWindow is null ? null : inc.IncubationStartUtc?.AddHours(tsbWindow.MinHours);
            var tsbRemMinSec = tsbMinReady.HasValue ? Math.Max(0, (long)Math.Ceiling((tsbMinReady.Value - DateTime.UtcNow).TotalSeconds)) : 0;
            var tsbOverridden = inc.MinimumDurationOverriddenByUserId.HasValue;

            sharedTsbSummary = new
            {
                mediaLotNumber = inc.Media?.LotNumber,
                incubatorCode = inc.IncubatorEquipment?.Code,
                incubationStartUtc = inc.IncubationStartUtc,
                incubationEndUtc = inc.IncubationEndUtc,
                minReadyAt = tsbMinReady,
                remainingMinimumSeconds = tsbRemMinSec,
                minimumDurationOverridden = tsbOverridden,
                isLocked = tsbWindow is null || (tsbMinReady.HasValue && DateTime.UtcNow < tsbMinReady.Value && !tsbOverridden) || !inc.IsIncubationComplete,
                windowNotConfigured = tsbWindow is null,
                startedByUserName = startedUser ?? "Analyst",
                isCompleted = inc.CompletedAt.HasValue
            };
        }

        // Shown window: the medium this test is incubating on, else the step's
        // configured media (their overall range when more than one is
        // permitted) - never the step-level fields.
        var shownWindow = openWindow ?? IncubationWindowResolver.ForStep(current.Step?.StepMedia);
        var tempMin = shownWindow?.TempMin ?? 0;
        var tempMax = shownWindow?.TempMax ?? 0;
        var incMin = shownWindow?.MinHours ?? 0;
        var incMax = shownWindow?.MaxHours ?? 0;

        var returnInfo = await TestReturnHelper.GetPendingReturnAsync(_db, testOrderId, hasActiveReadings: facts.ActiveCountReadings.Count > 0);

        return new
        {
            testOrderId,
            step = current.Step is null ? null : new
            {
                current.Step.Id, current.Step.StepOrder, current.Step.StepName,
                stepType = current.Step.StepType.ToString(),
                current.Step.TargetOrganismId,
                phenotypicTestType = current.Step.PhenotypicTestType?.ToString(),
                phenotypicTestTypes = current.Step.PhenotypicTests.OrderBy(t => t.DisplayOrder).Select(t => t.PhenotypicTestType.ToString()),
                incubationMinHours = incMin,
                incubationMaxHours = incMax,
                temperatureMin = tempMin,
                temperatureMax = tempMax,
                current.Step.IsFinalStep,
                current.Step.RequiresIncubationTransfer,
                // The operative incubation window/temperature for this
                // medium, per the Media Configuration Migration's Test
                // Master reversal - not the step-level fields above, which
                // are no longer read at execution time.
                stepMedia = current.Step.StepMedia.OrderBy(m => m.DisplayOrder).Select(m => new
                {
                    m.Id,
                    m.MaterialId, materialName = m.Material!.MaterialName, m.TempMin, m.TempMax,
                    m.IncubationMinHours, m.IncubationMaxHours, m.IsRequired
                }),
                incubationStages = current.Step.IncubationStages.OrderBy(x => x.StageNumber).Select(x => new
                {
                    x.StageNumber,
                    x.TempMin,
                    x.TempMax,
                    x.IncubationMinHours,
                    x.IncubationMaxHours
                })
            },
            stepNumber = current.Step?.StepOrder,
            totalSteps = current.TotalSteps,
            testName = order.TestCode,
            workflowType = current.WorkflowType.ToString(),
            sampleContext,
            incubationLock,
            returnInfo = returnInfo is null ? null : new
            {
                reason = returnInfo.Reason,
                returnedAt = returnInfo.ReturnedAt
            },
            previousSteps = enrichedPreviousSteps,
            sharedTsbSummary,
            allStepsComplete = current.AllStepsComplete,
            finalResult = current.FinalResult,
            allSteps = current.AllSteps.Select(s => new { s.StepOrder, s.StepName }),
            completedSteps = current.CompletedSteps.Select(s => new
            {
                s.StepOrder, s.StepName, stepType = s.StepType.ToString(), s.IsFinalStep,
                s.Outcome, s.ObservedAt, s.ReportedResult, s.CalculatedResult, s.Status
            })
        };
    }
}
