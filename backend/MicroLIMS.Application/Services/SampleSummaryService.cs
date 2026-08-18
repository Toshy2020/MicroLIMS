using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Word;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Read-heavy mapping for the floating Sample Summary page - everything
// that happened to a Sample, across every TestOrder (including
// superseded retest rounds), in one call. Also the source of the
// exportable PDF/Word version of that same summary.
public class SampleSummaryService
{
    private readonly MicroLimsDbContext _db;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IWordGenerator _wordGenerator;
    private readonly ReviewGateService _reviewGate;

    public SampleSummaryService(MicroLimsDbContext db, IPdfGenerator pdfGenerator, IWordGenerator wordGenerator, ReviewGateService reviewGate)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
        _wordGenerator = wordGenerator;
        _reviewGate = reviewGate;
    }

    public async Task<SampleSummaryDto?> GetSummaryAsync(int sampleId)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId);
        if (sample is null) return null;

        var timeline = await _reviewGate.GetTimelineAsync(ReviewEntityTypes.Sample, sampleId);

        var testOrderIds = sample.TestOrders.Select(t => t.Id).ToList();

        var incubations = await _db.Incubations
            .Where(i => i.TestOrderId != null && testOrderIds.Contains(i.TestOrderId.Value))
            .Include(i => i.Media).ThenInclude(m => m!.Material)
            .Include(i => i.IncubatorEquipment)
            .ToListAsync();
        var results = await _db.Results.Where(r => testOrderIds.Contains(r.TestOrderId)).ToListAsync();
        var countTestReadings = await _db.CountTestReadings.Where(r => testOrderIds.Contains(r.TestOrderId)).ToListAsync();
        var pathogenObservations = await _db.PathogenObservations.Where(p => testOrderIds.Contains(p.TestOrderId)).ToListAsync();
        var workflowHistory = await _db.WorkflowHistories.Where(w => testOrderIds.Contains(w.TestOrderId)).ToListAsync();
        var sampleLocations = await _db.SampleLocations
            .Where(l => testOrderIds.Contains(l.TestOrderId))
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .ToListAsync();
        var testDefinitions = await _db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.MediaType)
            .Where(t => sample.TestOrders.Select(o => o.TestCode).Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        var preparation = await _db.SamplePreparations
            .Include(p => p.Neutralizer)
            .FirstOrDefaultAsync(p => p.SampleId == sampleId);

        var signatures = await _db.ElectronicSignatures
            .Where(s => s.EntityType == "Sample" && s.EntityId == sampleId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new SignatureDto(s.UserFullNameSnapshot, s.UsernameSnapshot, s.RoleSnapshot, s.MeaningOfSignature.ToString(), s.SignedAt, s.Comment))
            .ToListAsync();

        // One batched name lookup for every user referenced anywhere in
        // this summary, instead of a query per row.
        var userIds = new HashSet<int>(results.Select(r => r.EnteredByUserId)
            .Concat(countTestReadings.Select(r => r.EnteredByUserId))
            .Concat(pathogenObservations.Select(p => p.ObservedByUserId))
            .Concat(workflowHistory.Select(w => w.PerformedByUserId))
            .Concat(sampleLocations.Where(l => l.EnteredByUserId is not null).Select(l => l.EnteredByUserId!.Value))
            .Concat(incubations.Where(i => i.StartedByUserId is not null).Select(i => i.StartedByUserId!.Value))
            .Append(sample.ReceivedByUserId));
        if (preparation is not null) userIds.Add(preparation.PreparedByUserId);
        if (sample.ReviewedByUserId is not null) userIds.Add(sample.ReviewedByUserId.Value);
        if (sample.ApprovedByUserId is not null) userIds.Add(sample.ApprovedByUserId.Value);

        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        string NameOf(int userId) => names.TryGetValue(userId, out var n) ? n : "Unknown";

        var sharedTsbInc = incubations.FirstOrDefault(i =>
            !string.IsNullOrEmpty(i.StepName) &&
            (i.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase) ||
             i.StepName.Contains("Enrichment", StringComparison.OrdinalIgnoreCase)));

        int tsbHoursMin = 24;
        foreach (var def in testDefinitions.Values)
        {
            var tsbStep = def.Steps.FirstOrDefault(step =>
                step.StepType == StepType.BrothEnrichment ||
                (!string.IsNullOrEmpty(step.StepName) && step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase)) ||
                (step.MediaType != null && (step.MediaType.Class == MediaClass.GeneralBroth || step.MediaType.Class == MediaClass.SelectiveBroth)));
            if (tsbStep != null && tsbStep.IncubationMinHours > 0)
            {
                tsbHoursMin = tsbStep.IncubationMinHours;
                break;
            }
        }

        bool tsbStarted = sharedTsbInc != null;
        DateTime? tsbStart = sharedTsbInc?.IncubationStartUtc ?? sharedTsbInc?.StartedAt;
        DateTime? tsbMinReadyAt = tsbStart?.AddHours(tsbHoursMin);

        bool tsbIncubating = tsbStarted &&
                             tsbMinReadyAt.HasValue &&
                             DateTime.UtcNow < tsbMinReadyAt.Value &&
                             !sharedTsbInc!.CompletedAt.HasValue;
        bool tsbCompleted = tsbStarted &&
                            (sharedTsbInc!.CompletedAt.HasValue ||
                             (tsbMinReadyAt.HasValue && DateTime.UtcNow >= tsbMinReadyAt.Value));

        var dto = new SampleSummaryDto
        {
            SampleId = sample.Id,
            ReferenceNumber = sample.ReferenceNumber,
            Category = sample.Category.ToString(),
            DisplayName = sample.Item?.Name ?? sample.WaterSamplingPoint?.Code ?? sample.Department?.Name ?? sample.Machine?.Name ?? string.Empty,
            ProductionStage = sample.ProductionStage,
            CauseOfTesting = sample.CauseOfTesting?.Name ?? string.Empty,
            BatchNumber = sample.BatchNumber,
            ControlNumber = sample.ControlNumber,
            Status = sample.Status.ToString(),
            ReceivedByName = NameOf(sample.ReceivedByUserId),
            ReceivedAt = sample.ReceivedAt,
            SampledBy = sample.SampledBy,
            SampleQuantity = sample.SampleQuantity,
            MfgDate = sample.MfgDate,
            ExpDate = sample.ExpDate,
            WaterSamplingPointCode = sample.WaterSamplingPoint?.Code,
            WaterSamplingPointLocation = sample.WaterSamplingPoint?.Location,
            StorageCondition = sample.StorageCondition,
            StorageTimeHours = sample.StorageTimeHours,
            ReviewedByName = sample.ReviewedByUserId is not null ? NameOf(sample.ReviewedByUserId.Value) : null,
            ReviewedAt = sample.ReviewedAt,
            ApprovedByName = sample.ApprovedByUserId is not null ? NameOf(sample.ApprovedByUserId.Value) : null,
            ApprovedAt = sample.ApprovedAt,
            ApprovalDecision = sample.ApprovalDecision?.ToString(),
            Signatures = signatures,
            Preparation = preparation is null ? null : new SamplePreparationSummaryDto
            {
                Amount = preparation.Amount,
                Unit = preparation.Unit,
                Technique = preparation.Technique,
                FiltrationVolume = preparation.FiltrationVolume,
                WashingVolume = preparation.WashingVolume,
                NeutralizerName = preparation.Neutralizer?.Name ?? string.Empty,
                PreparedByName = NameOf(preparation.PreparedByUserId),
                PreparedAt = preparation.PreparedAt
            },
            Timeline = timeline
                .Select(e => new SampleWorkflowEventDto
                {
                    EventType = e.EventType.ToString(),
                    PerformedByName = e.PerformedByNameSnapshot,
                    Timestamp = e.Timestamp,
                    Comment = e.Comment,
                    Decision = e.Decision?.ToString()
                }).ToList(),
            TestOrders = sample.TestOrders.Select(order =>
            {
                TestDefinition? def = null;
                testDefinitions.TryGetValue(order.TestCode, out def);

                bool usesTsb = def?.Steps.Any(step =>
                    step.StepType == StepType.BrothEnrichment ||
                    step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase) ||
                    (step.MediaType != null && (step.MediaType.Class == MediaClass.GeneralBroth || step.MediaType.Class == MediaClass.SelectiveBroth))) ?? false;

                string workflowState;
                string workflowStateDisplay;
                bool isLocked;
                bool isResultAllowed;
                string? lockReason = null;

                if (order.Status == ApprovalStatus.Approved)
                {
                    workflowState = "APPROVED";
                    workflowStateDisplay = "Completed & Approved";
                    isLocked = false;
                    isResultAllowed = false;
                }
                else if (order.CurrentStep == WorkflowStep.Ready)
                {
                    workflowState = "RESULTS_RECORDED";
                    workflowStateDisplay = "Result Recorded — Pending Review";
                    isLocked = false;
                    isResultAllowed = true;
                }
                else if (usesTsb)
                {
                    if (!tsbStarted)
                    {
                        workflowState = "PENDING";
                        workflowStateDisplay = "Pending";
                        isLocked = true;
                        isResultAllowed = false;
                        lockReason = "TSB broth enrichment setup required";
                    }
                    else if (tsbIncubating)
                    {
                        workflowState = "TSB_INCUBATING";
                        workflowStateDisplay = "TSB Incubating";
                        isLocked = true;
                        isResultAllowed = false;
                        lockReason = "Locked until TSB incubation is complete";
                    }
                    else if (tsbCompleted)
                    {
                        workflowState = "READY_FOR_DOWNSTREAM";
                        workflowStateDisplay = "Ready for Downstream Testing";
                        isLocked = false;
                        isResultAllowed = true;
                    }
                    else
                    {
                        workflowState = "PENDING";
                        workflowStateDisplay = "Pending";
                        isLocked = true;
                        isResultAllowed = false;
                    }
                }
                else
                {
                    // Non-TSB count test / independent test
                    var openCountIncubation = incubations.FirstOrDefault(i =>
                        i.TestOrderId == order.Id &&
                        i.CompletedAt == null &&
                        i.IncubationStartUtc.HasValue &&
                        !i.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase) &&
                        !i.StepName.Contains("Enrichment", StringComparison.OrdinalIgnoreCase));

                    if (openCountIncubation != null)
                    {
                        var countStep = def?.Steps.FirstOrDefault(s => s.StepName == openCountIncubation.StepName);
                        var minHours = countStep?.IncubationMinHours > 0 ? countStep.IncubationMinHours : (countStep?.MediaType?.IncubationMinHours ?? 0);
                        var minReadyAt = openCountIncubation.IncubationStartUtc!.Value.AddHours((double)minHours);

                        if (DateTime.UtcNow < minReadyAt)
                        {
                            workflowState = "COUNT_INCUBATING";
                            workflowStateDisplay = "Testing / Incubation In Progress";
                            isLocked = true;
                            isResultAllowed = false;
                            lockReason = $"Count incubation in progress. Available from: {minReadyAt:dd/MM/yyyy HH:mm}";
                        }
                        else
                        {
                            workflowState = "AWAITING_RESULTS";
                            workflowStateDisplay = "Ready — Awaiting Primary Readings";
                            isLocked = false;
                            isResultAllowed = true;
                            lockReason = null;
                        }
                    }
                    else if (order.CurrentStep == WorkflowStep.Incubating)
                    {
                        workflowState = "INCUBATING";
                        workflowStateDisplay = "Testing / Incubation In Progress";
                        isLocked = true;
                        isResultAllowed = false;
                        lockReason = "Incubation in progress";
                    }
                    else if (order.CurrentStep == WorkflowStep.Running)
                    {
                        workflowState = "RUNNING";
                        workflowStateDisplay = "Testing In Progress";
                        isLocked = false;
                        isResultAllowed = true;
                    }
                    else
                    {
                        workflowState = "PENDING";
                        workflowStateDisplay = "Pending";
                        isLocked = false;
                        isResultAllowed = true;
                    }
                }

                string workflowStatus = workflowState switch
                {
                    "TSB_INCUBATING"       => "InProgress",
                    "COUNT_INCUBATING"     => "InProgress",
                    "INCUBATING"           => "InProgress",
                    "RUNNING"              => "InProgress",
                    "READY_FOR_DOWNSTREAM" => "ReadyToRead",
                    "TSB_READY"            => "ReadyToRead",
                    "AWAITING_RESULTS"     => "EnterResult",
                    "RESULTS_RECORDED"     => "PendingReview",
                    "APPROVED"             => "Completed",
                    _                      => "Pending"
                };

                return new TestOrderSummaryDetailDto
                {
                    TestOrderId = order.Id,
                    TestCode = order.TestCode,
                    TestDisplayName = def?.DisplayName ?? order.TestCode,
                    Status = order.Status.ToString(),
                    CurrentStep = order.CurrentStep.ToString(),
                    WorkflowState = workflowState,
                    WorkflowStateDisplay = workflowStateDisplay,
                    WorkflowStatus = workflowStatus,
                    UsesSharedTsb = usesTsb,
                    IsWorkflowLocked = isLocked,
                    IsResultEntryAllowed = isResultAllowed,
                    ResultLockReason = lockReason,
                    IsSuperseded = order.IsSuperseded,
                    Incubations = incubations.Where(i => i.TestOrderId == order.Id)
                        .OrderBy(i => i.StepNumber).ThenBy(i => i.StageNumber)
                        .Select(i =>
                    {
                    var isStage1WithStage2 = i.StageNumber == 1 && incubations.Any(other =>
                        other.TestOrderId == order.Id && (other.ParentIncubationId == i.Id || (other.StepName == i.StepName && other.StageNumber == 2)));
                    var stage2Child = isStage1WithStage2
                        ? incubations.FirstOrDefault(other => other.TestOrderId == order.Id && (other.ParentIncubationId == i.Id || (other.StepName == i.StepName && other.StageNumber == 2)))
                        : null;

                    string? transferredByName = null;
                    DateTime? transferredAt = null;
                    if (isStage1WithStage2)
                    {
                        transferredAt = i.CompletedAt ?? stage2Child?.StartedAt;
                        transferredByName = stage2Child?.StartedByUserId != null ? NameOf(stage2Child.StartedByUserId.Value) : null;
                    }

                    string? completedByName = null;
                    if (i.CompletedAt != null)
                    {
                        if (isStage1WithStage2)
                        {
                            completedByName = stage2Child?.StartedByUserId != null ? NameOf(stage2Child.StartedByUserId.Value) : null;
                        }
                        else
                        {
                            var reading = countTestReadings.Where(r => r.TestOrderId == order.Id && (r.StepName == i.StepName || order.Incubations.Count <= 2))
                                .OrderByDescending(r => r.Id).FirstOrDefault();
                            var loc = sampleLocations.Where(l => l.TestOrderId == order.Id && l.EnteredByUserId != null)
                                .OrderByDescending(l => l.EnteredAt).FirstOrDefault();
                            var obs = pathogenObservations.Where(p => p.TestOrderId == order.Id && p.StepName == i.StepName)
                                .OrderByDescending(p => p.Id).FirstOrDefault();
                            var res = results.Where(r => r.TestOrderId == order.Id).OrderByDescending(r => r.Id).FirstOrDefault();

                            if (reading != null) completedByName = NameOf(reading.EnteredByUserId);
                            else if (loc?.EnteredByUserId != null) completedByName = NameOf(loc.EnteredByUserId.Value);
                            else if (obs != null) completedByName = NameOf(obs.ObservedByUserId);
                            else if (res != null) completedByName = NameOf(res.EnteredByUserId);
                            else
                            {
                                var lastHistory = workflowHistory.Where(w => w.TestOrderId == order.Id).OrderByDescending(w => w.Timestamp).FirstOrDefault();
                                if (lastHistory != null) completedByName = NameOf(lastHistory.PerformedByUserId);
                            }
                        }
                    }

                    return new IncubationDetailDto
                    {
                        StepName = i.StepName,
                        StageNumber = i.StageNumber,
                        MediaLotNumber = i.Media?.LotNumber,
                        MediaMaterialName = i.Media?.Material?.MaterialName,
                        IncubatorName = i.IncubatorEquipment?.Name,
                        Temperature = i.Temperature,
                        Duration = i.Duration,
                        StartedAt = i.StartedAt,
                        ExpectedReadingAt = i.ExpectedReadingAt,
                        CompletedAt = i.CompletedAt,
                        Outcome = i.Outcome,
                        StartedByName = i.StartedByUserId is not null ? NameOf(i.StartedByUserId.Value) : null,
                        TransferredAt = transferredAt,
                        TransferredByName = transferredByName,
                        CompletedByName = completedByName,
                        SameAnalystBothStages = i.StageNumber == 2
                            ? (i.ParentIncubationId != null
                                ? incubations.FirstOrDefault(p => p.Id == i.ParentIncubationId)?.StartedByUserId == i.StartedByUserId
                                : incubations.FirstOrDefault(p => p.TestOrderId == order.Id && p.StepName == i.StepName && p.StageNumber == 1)?.StartedByUserId == i.StartedByUserId)
                            : null
                    };
                }).ToList(),
                Results = results.Where(r => r.TestOrderId == order.Id).Select(r => new ResultDetailDto
                {
                    RawValue = r.RawValue,
                    InterpretedValue = r.InterpretedValue,
                    Type = r.Type.ToString(),
                    EnteredByName = NameOf(r.EnteredByUserId),
                    EnteredAt = r.EnteredAt
                }).ToList(),
                CountTestReadings = countTestReadings.Where(r => r.TestOrderId == order.Id).Select(r => new CountTestReadingDetailDto
                {
                    StepName = r.StepName,
                    PlateReadings = r.PlateReadings,
                    DilutionFactor = r.DilutionFactor,
                    Average = r.Average,
                    CalculatedResult = r.CalculatedResult,
                    ReportedResult = r.ReportedResult,
                    AlertLimit = r.AlertLimit,
                    ActionLimit = r.ActionLimit,
                    SpecLimit = r.SpecLimit,
                    Status = r.Status,
                    EnteredByName = NameOf(r.EnteredByUserId),
                    EnteredAt = r.EnteredAt
                }).ToList(),
                PathogenObservations = pathogenObservations.Where(p => p.TestOrderId == order.Id).Select(p => new PathogenObservationDetailDto
                {
                    StepName = p.StepName,
                    StepOrder = p.StepOrder,
                    Observation = p.Observation.ToString(),
                    ObservedByName = NameOf(p.ObservedByUserId),
                    ObservedAt = p.ObservedAt
                }).ToList(),
                WorkflowHistory = workflowHistory.Where(w => w.TestOrderId == order.Id).OrderBy(w => w.Timestamp).Select(w => new WorkflowHistoryDetailDto
                {
                    FromStep = w.FromStep.ToString(),
                    ToStep = w.ToStep.ToString(),
                    Note = w.Note,
                    PerformedByName = NameOf(w.PerformedByUserId),
                    Timestamp = w.Timestamp
                }).ToList(),
                Locations = sampleLocations.Where(l => l.TestOrderId == order.Id).Select(l => new SampleLocationDetailDto
                {
                    LocationName = l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? string.Empty,
                    GradeClassification = l.RoomTestConfiguration?.Room?.GradeClassification,
                    AlertLimit = l.AlertLimit,
                    ActionLimit = l.ActionLimit,
                    SpecLimit = l.SpecLimit,
                    CFUResult = l.CFUResult,
                    CalculatedResult = l.CalculatedResult,
                    ReportedResult = l.ReportedResult,
                    Status = l.Status,
                    EnteredByName = l.EnteredByUserId is not null ? NameOf(l.EnteredByUserId.Value) : null,
                    EnteredAt = l.EnteredAt
                }).ToList()
                };
            }).ToList()
        };

        return dto;
    }

    // Uses the laid-out renderer (cards, stat boxes, signature blocks) -
    // the same document that gets archived on final decision, so what a
    // user downloads matches the frozen copy exactly.
    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryPdfAsync(int sampleId)
    {
        var summary = await GetSummaryAsync(sampleId);
        if (summary is null) return null;
        var pdf = await _pdfGenerator.GenerateReportAsync(ReportDocumentMapper.ForSample(summary));
        return (FileStemFor(summary), pdf);
    }

    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryWordAsync(int sampleId)
    {
        var summary = await GetSummaryAsync(sampleId);
        if (summary is null) return null;
        var doc = await _wordGenerator.GenerateFromLinesAsync(TitleFor(summary), BuildReportLines(summary));
        return (FileStemFor(summary), doc);
    }

    // Builds the document that gets frozen at final decision. Separate
    // from the download path only so the archive service can hand the
    // same ReportDocument to storage.
    public async Task<Infrastructure.Pdf.ReportDocument?> BuildReportDocumentAsync(int sampleId)
    {
        var summary = await GetSummaryAsync(sampleId);
        return summary is null ? null : ReportDocumentMapper.ForSample(summary);
    }

    private static string TitleFor(SampleSummaryDto s) => $"Sample Summary - {s.ReferenceNumber}";
    private static string FileStemFor(SampleSummaryDto s) => $"SampleSummary_{s.ReferenceNumber}";

    // Flattens every section of the summary into the plain lines
    // SimplePdfWriter/SimpleDocxWriter expect - same shape as the 5
    // sections SampleSummaryDialog.tsx renders, so the export reads as
    // the same document, just on paper/in Word instead of a floating page.
    private static List<string> BuildReportLines(SampleSummaryDto s)
    {
        var lines = new List<string>
        {
            "SAMPLE IDENTITY",
            $"Reference Number: {s.ReferenceNumber}",
            $"Category: {s.Category}",
            $"Item / Point / Room / Machine: {s.DisplayName}",
            $"Production Stage: {s.ProductionStage ?? "-"}",
            $"Batch Number: {s.BatchNumber ?? "-"}",
            $"Control Number: {s.ControlNumber}",
            $"Cause of Testing: {s.CauseOfTesting}",
            $"Received By: {s.ReceivedByName}",
            $"Received At: {s.ReceivedAt:dd-MMM-yyyy HH:mm}",
            $"Sampled By: {s.SampledBy}",
            $"Sample Quantity: {s.SampleQuantity ?? "-"}",
            $"Mfg Date: {(s.MfgDate is null ? "-" : s.MfgDate.Value.ToString("dd-MMM-yyyy"))}",
            $"Exp Date: {(s.ExpDate is null ? "-" : s.ExpDate.Value.ToString("dd-MMM-yyyy"))}",
        };
        if (s.WaterSamplingPointCode is not null)
            lines.Add($"Sampling Point: {s.WaterSamplingPointCode} - {s.WaterSamplingPointLocation}");
        if (s.StorageCondition is not null)
            lines.Add($"Storage Condition: {s.StorageCondition}" + (s.StorageCondition == "Refrigerator" ? $" ({s.StorageTimeHours ?? 0}h)" : ""));
        lines.Add($"Status: {s.Status}");
        lines.Add("");

        if (s.Preparation is not null)
        {
            var p = s.Preparation;
            lines.Add("SAMPLE PREPARATION");
            lines.Add($"Amount: {p.Amount} {p.Unit}");
            lines.Add($"Technique: {p.Technique}");
            if (p.Technique == "Filtration")
            {
                lines.Add($"Filtration Volume: {p.FiltrationVolume}");
                lines.Add($"Washing Volume: {p.WashingVolume}");
            }
            lines.Add($"Neutralizer: {p.NeutralizerName}");
            lines.Add($"Prepared By: {p.PreparedByName}");
            lines.Add($"Prepared At: {p.PreparedAt:dd-MMM-yyyy HH:mm}");
            lines.Add("");
        }

        lines.Add("TEST RESULTS");
        foreach (var order in s.TestOrders)
        {
            lines.Add($"{order.TestCode} - {order.TestDisplayName} [{order.Status}]{(order.IsSuperseded ? " (superseded)" : "")}");

            foreach (var inc in order.Incubations)
            {
                var isStage1Transferred = inc.StageNumber == 1 && (inc.TransferredAt != null || inc.TransferredByName != null || order.Incubations.Any(x => x.StageNumber == 2));
                var isStage2 = inc.StageNumber == 2;
                var stageLabel = isStage1Transferred ? $"STAGE 1 ({inc.StepName})" : isStage2 ? $"STAGE 2 ({inc.StepName})" : $"INCUBATION ({inc.StepName})";

                lines.Add($"  {stageLabel}");
                lines.Add($"    Media Lot: {inc.MediaLotNumber ?? "-"} ({inc.MediaMaterialName ?? "-"})   Incubator: {inc.IncubatorName ?? "-"}");
                lines.Add($"    Temperature: {inc.Temperature ?? "-"}   Duration: {inc.Duration ?? "-"}");
                lines.Add($"    Started At: {FormatDateTime(inc.StartedAt)} (by {inc.StartedByName ?? "-"})");
                if (isStage1Transferred)
                {
                    lines.Add($"    Transferred At: {FormatDateTime(inc.TransferredAt ?? inc.CompletedAt)}   Transferred By: {inc.TransferredByName ?? "-"}");
                }
                else
                {
                    lines.Add($"    Completed At: {FormatDateTime(inc.CompletedAt)}   Completed By: {inc.CompletedByName ?? "-"}");
                }
                if (inc.Outcome is not null) lines.Add($"    Outcome: {inc.Outcome}");
            }

            if (order.Locations.Count > 0)
            {
                lines.Add("  FINAL RESULT (BY LOCATION):");
                foreach (var loc in order.Locations)
                {
                    lines.Add($"    Location: {loc.LocationName}   CFU: {loc.CFUResult?.ToString() ?? "-"}   Reported: {loc.ReportedResult ?? "-"}   Status: {loc.Status ?? "-"}");
                    lines.Add($"    Entered By: {loc.EnteredByName ?? "-"}   Entered At: {FormatDateTime(loc.EnteredAt)}");
                }
            }
            else if (order.CountTestReadings.Count > 0)
            {
                lines.Add("  FINAL RESULT:");
                foreach (var r in order.CountTestReadings)
                {
                    lines.Add($"    Plate Readings: {r.PlateReadings}   Dilution Factor: {r.DilutionFactor}");
                    lines.Add($"    Average: {r.Average}   Calculated: {r.CalculatedResult}   Reported Result: {r.ReportedResult}   Status: {r.Status}");
                    lines.Add($"    Limits (Alert/Action/Spec): {r.AlertLimit ?? "-"} / {r.ActionLimit ?? "-"} / {r.SpecLimit ?? "-"}");
                    lines.Add($"    Entered By: {r.EnteredByName}   Entered At: {FormatDateTime(r.EnteredAt)}");
                }
            }
            else if (order.PathogenObservations.Count > 0)
            {
                lines.Add("  FINAL RESULT:");
                foreach (var p in order.PathogenObservations)
                    lines.Add($"    {p.StepName}: Observation = {p.Observation}   Entered By: {p.ObservedByName}   Entered At: {FormatDateTime(p.ObservedAt)}");
            }
            else if (order.Results.Count > 0)
            {
                lines.Add("  FINAL RESULT:");
                foreach (var r in order.Results)
                    lines.Add($"    Result: {r.InterpretedValue ?? r.RawValue}   Entered By: {r.EnteredByName}   Entered At: {FormatDateTime(r.EnteredAt)}");
            }

            if (order.WorkflowHistory.Count > 0)
            {
                lines.Add("  WORKFLOW HISTORY:");
                foreach (var h in order.WorkflowHistory)
                    lines.Add($"    {h.FromStep} -> {h.ToStep} by {h.PerformedByName} at {h.Timestamp:dd-MMM-yyyy HH:mm}" + (h.Note is null ? "" : $" - {h.Note}"));
            }

            lines.Add("");
        }

        lines.Add("TIMELINE");
        foreach (var e in s.Timeline)
        {
            var decisionSuffix = e.Decision is null ? "" : $" ({e.Decision})";
            var commentSuffix = string.IsNullOrWhiteSpace(e.Comment) ? "" : $" - \"{e.Comment}\"";
            lines.Add($"{e.EventType}{decisionSuffix} - {e.PerformedByName} - {e.Timestamp:dd-MMM-yyyy HH:mm}{commentSuffix}");
        }
        lines.Add("");

        if (s.Signatures.Count > 0)
        {
            lines.Add("SIGNATURES");
            foreach (var sig in s.Signatures)
            {
                var commentSuffix = string.IsNullOrWhiteSpace(sig.Comment) ? "" : $" - \"{sig.Comment}\"";
                lines.Add($"{sig.PrintedName} ({sig.Role}) - {sig.Meaning} - {sig.SignedAt:dd-MMM-yyyy HH:mm}{commentSuffix}");
            }
        }

        return lines;
    }

    private static string FormatDateTime(DateTime? d) => d is null ? "-" : d.Value.ToString("dd-MMM-yyyy HH:mm");
}
