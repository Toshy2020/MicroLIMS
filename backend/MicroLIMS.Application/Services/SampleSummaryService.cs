using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.Abstractions.Word;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

// Read-heavy mapping for the floating Sample Summary page - everything
// that happened to a Sample, across every TestOrder (including
// superseded retest rounds), in one call. Also the source of the
// exportable PDF/Word version of that same summary.
public class SampleSummaryService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly IWordGenerator _wordGenerator;
    private readonly ReviewGateService _reviewGate;
    private readonly TimeProvider _time;

    public SampleSummaryService(IMicroLimsDbContext db, IPdfGenerator pdfGenerator, IWordGenerator wordGenerator, ReviewGateService reviewGate, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _pdfGenerator = pdfGenerator;
        _wordGenerator = wordGenerator;
        _reviewGate = reviewGate;
    }

    // A retest can be partial - only the TestCode(s) actually selected for
    // retest go superseded on this sample, every other TestCode is left
    // live right here (SampleApprovalService.DecideAsync's retest branches
    // only touch `selectedOrders`). So pull-through has to work per
    // TestCode, never as an all-or-nothing decision for the whole sample:
    // otherwise a sample with e.g. three passing live tests and one
    // superseded (failing) one would show only the three passers, silently
    // dropping the very test that triggered the OOS investigation from its
    // own Summary/Report/COA. For each of this sample's own superseded
    // TestCodes, this pulls in the TestOrder(s) of whichever descendant(s)
    // actually carried that retest to its conclusion - recursing through
    // NewSampleRequest's two siblings and multi-level escalation the same
    // way SampleApprovalService.PropagateOosOutcomeAsync does.
    private async Task<(List<TestOrder> Orders, Dictionary<int, string> SourceReferenceByOrderId)> ResolveEffectiveTestOrdersAsync(Sample sample, int depth = 0)
    {
        var orders = new List<TestOrder>(sample.TestOrders);
        var sourceRef = new Dictionary<int, string>();

        var supersededTestCodes = sample.TestOrders.Where(o => o.IsSuperseded).Select(o => o.TestCode).ToHashSet();
        if (supersededTestCodes.Count == 0 || sample.OosGroupCode is null || depth > 10)
            return (orders, sourceRef);

        var children = await _db.Samples.Include(c => c.TestOrders)
            .Where(c => c.OriginSampleId == sample.Id)
            .ToListAsync();

        foreach (var child in children)
        {
            var (childOrders, childSourceRef) = await ResolveEffectiveTestOrdersAsync(child, depth + 1);
            foreach (var order in childOrders)
            {
                // Only pull in TestCodes this sample actually superseded -
                // a child only ever carries the tests selected for its
                // retest, so this is mostly a defensive filter.
                if (!supersededTestCodes.Contains(order.TestCode)) continue;
                orders.Add(order);
                sourceRef[order.Id] = childSourceRef.TryGetValue(order.Id, out var deeper) ? deeper : child.ReferenceNumber;
            }
        }

        return (orders, sourceRef);
    }

    // sectionIds: the viewer's laboratory sections (null = unrestricted). Only
    // those sections' tests are shown; every section is still listed in
    // Sections with its review/approval state.
    // The Certificate of Analysis page's read model. Once every lab is final
    // (the sample is Approved or Rejected - the rule behind
    // CombinedCoaAvailable), a member of any of its labs may view and print
    // the combined certificate, other labs' results included (user decision,
    // lab separation stage 4). Before that it is as lab-scoped as the summary.
    public async Task<SampleSummaryDto?> GetCertificateSummaryAsync(int sampleId, IReadOnlyCollection<int>? sectionIds)
    {
        var status = await _db.Samples.Where(s => s.Id == sampleId).Select(s => (SampleStatus?)s.Status).FirstOrDefaultAsync();
        if (status is null) return null;
        var final = status is SampleStatus.Approved or SampleStatus.Rejected;
        return await GetSummaryAsync(sampleId, final ? null : sectionIds);
    }

    public async Task<SampleSummaryDto?> GetSummaryAsync(int sampleId, IReadOnlyCollection<int>? sectionIds = null)
    {
        var sample = await _db.Samples
            .Include(s => s.SectionSignoffs)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId);
        if (sample is null) return null;

        var timeline = await _reviewGate.GetTimelineAsync(ReviewEntityTypes.Sample, sampleId);

        var (effectiveTestOrders, sourceRefByOrderId) = await ResolveEffectiveTestOrdersAsync(sample);
        if (sectionIds is not null)
            effectiveTestOrders = effectiveTestOrders.Where(t => sectionIds.Contains(t.SectionId)).ToList();
        var sectionRows = await _db.DocumentSections.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => new SampleSectionRow(s.Code, s.Name));

        var testOrderIds = effectiveTestOrders.Select(t => t.Id).ToList();

        var incubations = await _db.Incubations
            .Where(i => i.TestOrderId != null && testOrderIds.Contains(i.TestOrderId.Value))
            .Include(i => i.Media).ThenInclude(m => m!.Material)
            .Include(i => i.IncubatorEquipment)
            .ToListAsync();
        var results = await _db.Results.Where(r => testOrderIds.Contains(r.TestOrderId)).ToListAsync();
        var countTestReadings = await _db.CountTestReadings.Where(r => testOrderIds.Contains(r.TestOrderId)).ToListAsync();
        var activeAnalyses = await _db.TestAnalyses.AsNoTracking()
            .Where(e => testOrderIds.Contains(e.TestOrderId) && e.IsActive)
            .Include(e => e.Equipment)
            .Select(e => new ActiveAnalysisRow
            {
                Id = e.Id,
                TestOrderId = e.TestOrderId,
                AnalysisType = e.AnalysisType,
                EquipmentId = e.EquipmentId,
                EquipmentCode = e.Equipment != null ? e.Equipment.Code : null,
                EquipmentName = e.Equipment != null ? e.Equipment.Name : null,
                AnalysedAt = e.AnalysedAt,
                UnitAmount = e.UnitAmount,
                ConditionsJson = e.ConditionsJson,
                ValidityRecordType = e.ValidityRecordType,
                ValidityRecordId = e.ValidityRecordId,
                EnteredByUserId = e.EnteredByUserId,
                EnteredAt = e.EnteredAt,
                Comment = e.Comment,
                Results = e.ParameterResults.Where(r => r.IsActive).Select(r => new ActiveParameterResultRow
                {
                    Id = r.Id,
                    SpecificationId = r.SpecificationId,
                    ParameterName = r.ParameterName,
                    ReportedValue = r.ReportedValue,
                    ReportedDisplay = r.ReportedDisplay,
                    Unit = r.Unit,
                    SpecLimit = r.SpecLimit,
                    ResultBasis = r.ResultBasis,
                    ComparisonStatus = r.ComparisonStatus,
                    OverRange = r.OverRange,
                    BelowLoq = r.BelowLoq,
                    CalculationJson = r.CalculationJson,
                    StageReached = r.StageReached,
                    Readings = r.Readings.OrderBy(rd => rd.Index).Select(rd => new ResultReadingDetailDto
                    {
                        Id = rd.Id,
                        Kind = rd.Kind,
                        Index = rd.Index,
                        Stage = rd.Stage,
                        TimePointMinutes = rd.TimePointMinutes,
                        Value1 = rd.Value1,
                        Value2 = rd.Value2,
                        Value3 = rd.Value3,
                        Text = rd.Text,
                        ComputedValue = rd.ComputedValue,
                        Passed = rd.Passed
                    }).ToList()
                }).ToList()
            })
            .ToListAsync();
        var pathogenObservations = await _db.PathogenObservations.Where(p => testOrderIds.Contains(p.TestOrderId)).ToListAsync();
        var biochemicalResults = await _db.WorkflowStepResults
            .Where(r => testOrderIds.Contains(r.TestOrderId) && r.BiochemicalResultText != null)
            .ToListAsync();
        var workflowHistory = await _db.WorkflowHistories.Where(w => testOrderIds.Contains(w.TestOrderId)).ToListAsync();
        var sampleLocations = await _db.SampleLocations
            .Where(l => testOrderIds.Contains(l.TestOrderId))
            .Include(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .Include(l => l.WaterSamplingPoint)
            .Include(l => l.SamplingConfiguration!).ThenInclude(c => c.WaterSamplingPoint)
            .ToListAsync();
        var locationPathogenObservations = await _db.LocationPathogenObservations
            .Where(o => testOrderIds.Contains(o.TestOrderId))
            .Include(o => o.SampleLocation!).ThenInclude(l => l.RoomTestConfiguration!).ThenInclude(c => c.Room)
            .Include(o => o.SampleLocation!).ThenInclude(l => l.MachinePartConfiguration!).ThenInclude(c => c.MachinePart)
            .Include(o => o.SampleLocation!).ThenInclude(l => l.WaterSamplingPoint)
            .Include(o => o.SampleLocation!).ThenInclude(l => l.SamplingConfiguration!).ThenInclude(c => c.WaterSamplingPoint)
            .Include(o => o.ConfirmatoryPlateObservations)
            .OrderBy(o => o.ObservedAt)
            .ToListAsync();
        var testDefinitions = await _db.TestDefinitions
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.Material)
            .Include(t => t.Steps)
                .ThenInclude(s => s.StepMedia)
                    .ThenInclude(m => m.IncubationCondition)
            .Include(t => t.Steps)
                .ThenInclude(s => s.IncubationStages)
            .Where(t => effectiveTestOrders.Select(o => o.TestCode).Contains(t.Code))
            .ToDictionaryAsync(t => t.Code);

        var mediaLookup = incubations
            .Where(i => i.MediaId.HasValue && i.Media != null)
            .GroupBy(i => i.MediaId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (MaterialId: g.First().Media!.MaterialId, MediaProductId: g.First().Media!.Material?.MediaProductId));

        var preparation = await _db.SamplePreparations
            .FirstOrDefaultAsync(p => p.SampleId == sampleId);

        // Specification text per (Item, TestCode) - already resolved onto
        // CountTestReadingDetailDto.SpecLimit for quantitative tests at
        // result-entry time, but qualitative (pathogen) test orders have no
        // equivalent anywhere, so this is looked up generically here for
        // every test order regardless of type. Empty/no match is expected
        // for a test code nobody has configured a Specification for yet
        // (e.g. every pathogen test, until Test Master's Items page is used
        // to add one) - not an error, renders as "-" wherever shown.
        // A test can carry several specification parameters (e.g. Impurity
        // A / B / Total); they are listed in display order. Only the rows
        // for the sample's production stage are shown (SpecificationLookup).
        var sampleStageRole = await SpecificationLookup.SampleStageRoleAsync(_db, sample.Id);
        var specificationsByTestCode = sample.ItemId is null
            ? new Dictionary<string, List<Specification>>()
            : (await _db.Specifications
                .Include(sp => sp.Stages)
                .Where(sp => sp.ItemId == sample.ItemId.Value)
                .OrderBy(sp => sp.DisplayOrder).ThenBy(sp => sp.Id)
                .ToListAsync())
                .GroupBy(sp => sp.TestCode)
                .ToDictionary(g => g.Key, g => SpecificationLookup.SelectForStage(g, sampleStageRole));

        var signatures = await _db.ElectronicSignatures
            .Where(s => s.EntityType == "Sample" && s.EntityId == sampleId)
            .OrderBy(s => s.SignedAt)
            .Select(s => new SignatureDto(s.UserFullNameSnapshot, s.UsernameSnapshot, s.RoleSnapshot, s.MeaningOfSignature.ToString(), s.SignedAt, s.Comment))
            .ToListAsync();

        var data = new SampleSummaryData
        {
            Sample = sample,
            EffectiveTestOrders = effectiveTestOrders,
            SourceRefByOrderId = sourceRefByOrderId,
            SectionRows = sectionRows,
            Incubations = incubations,
            Results = results,
            CountTestReadings = countTestReadings,
            ActiveAnalyses = activeAnalyses,
            PathogenObservations = pathogenObservations,
            BiochemicalResults = biochemicalResults,
            WorkflowHistory = workflowHistory,
            SampleLocations = sampleLocations,
            LocationPathogenObservations = locationPathogenObservations,
            TestDefinitions = testDefinitions,
            MediaLookup = mediaLookup,
            SpecificationsByTestCode = specificationsByTestCode,
            Now = _time.GetUtcNow().UtcDateTime
        };

        // One batched name lookup for every user referenced anywhere in
        // this summary, instead of a query per row.
        var userIds = SampleSummaryUserIds.Collect(data, preparation);

        var names = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        string NameOf(int userId) => names.TryGetValue(userId, out var n) ? n : "Unknown";

        var assignedOrder = effectiveTestOrders.FirstOrDefault(o => o.AssignedAnalystId.HasValue);
        int? assignedAnalystId = assignedOrder?.AssignedAnalystId;
        string? assignedAnalystName = assignedAnalystId.HasValue ? NameOf(assignedAnalystId.Value) : null;

        string summaryDisplayName = sample.Category switch
        {
            SampleCategory.AfterCleaning => sample.Machine?.Name ?? string.Empty,
            SampleCategory.Water => sample.WaterSamplingPoint?.Code ?? string.Empty,
            SampleCategory.EnvironmentalMonitoring => sample.Department?.Name ?? string.Empty,
            _ => sample.Item?.Name ?? string.Empty
        };

        var dto = new SampleSummaryDto
        {
            SampleId = sample.Id,
            ReferenceNumber = sample.ReferenceNumber,
            Category = sample.Category.ToString(),
            DisplayName = summaryDisplayName,
            ProductionStage = sample.ProductionStage,
            CauseOfTesting = sample.CauseOfTesting?.Name ?? string.Empty,
            BatchNumber = sample.BatchNumber,
            ControlNumber = sample.ControlNumber,
            Status = sample.Status.ToString(),
            AssignedAnalystId = assignedAnalystId,
            AssignedAnalystName = assignedAnalystName,
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
            CertificateRemarks = sample.CertificateRemarks,
            PreviousProductName = sample.Category == SampleCategory.AfterCleaning ? sample.PreviousProductName : null,
            PreviousProductBatchNumber = sample.Category == SampleCategory.AfterCleaning ? (sample.PreviousProductBatchNumber ?? sample.BatchNumber) : null,
            Signatures = signatures,
            Preparation = preparation is null ? null : new SamplePreparationSummaryDto
            {
                Amount = preparation.Amount,
                Technique = preparation.Technique,
                FiltrationVolume = preparation.FiltrationVolume,
                WashingVolume = preparation.WashingVolume,
                Diluent = preparation.Diluent,
                Neutralizer = preparation.Neutralizer,
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
            TestOrders = effectiveTestOrders.Select(order => SampleSummaryTestOrderBuilder.Build(order, data, NameOf)).ToList()
        };

        dto.Sections = SampleSummarySectionsBuilder.Build(sample, sectionIds, sectionRows, NameOf);
        dto.AllSectionsVisible = dto.Sections.All(x => x.CanView);
        dto.OverallStatus = SampleSectionRollup.Overall(sample).ToString();
        dto.CombinedCoaAvailable = sample.Status is SampleStatus.Approved or SampleStatus.Rejected;
        dto.Certificate = CertificateOfAnalysisBuilder.Build(dto);

        return dto;
    }

    // Uses the laid-out renderer (cards, stat boxes, signature blocks) -
    // the same document that gets archived on final decision, so what a
    // user downloads matches the frozen copy exactly.
    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryPdfAsync(int sampleId, IReadOnlyCollection<int>? sectionIds = null)
    {
        var summary = await GetSummaryAsync(sampleId, sectionIds);
        if (summary is null) return null;
        var pdf = await _pdfGenerator.GenerateReportAsync(ReportDocumentMapper.ForSample(summary));
        return (FileStemFor(summary), pdf);
    }

    public async Task<(string fileNameStem, byte[] bytes)?> GenerateSummaryWordAsync(int sampleId, IReadOnlyCollection<int>? sectionIds = null)
    {
        var summary = await GetSummaryAsync(sampleId, sectionIds);
        if (summary is null) return null;
        var doc = await _wordGenerator.GenerateFromLinesAsync(TitleFor(summary), BuildReportLines(summary));
        return (FileStemFor(summary), doc);
    }

    // Builds the document that gets frozen at final decision. Separate
    // from the download path only so the archive service can hand the
    // same ReportDocument to storage.
    public async Task<ReportDocument?> BuildReportDocumentAsync(int sampleId)
    {
        var summary = await GetSummaryAsync(sampleId);
        return summary is null ? null : ReportDocumentMapper.ForSample(summary);
    }

    private static string TitleFor(SampleSummaryDto s) => $"Sample Summary - {s.ReferenceNumber}";
    private static string FileStemFor(SampleSummaryDto s) => $"SampleSummary_{s.ReferenceNumber}";

    // Flattens every section of the summary into the plain lines
    // SimpleDocxWriter expects - same shape as the 5
    // sections SampleSummaryDialog.tsx renders, so the export reads as
    // the same document, just on paper/in Word instead of a floating page.
    public static List<string> BuildReportLines(SampleSummaryDto s)
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
            lines.Add($"Amount: {p.Amount}");
            lines.Add($"Technique: {p.Technique}");
            if (p.Technique == "Filtration")
            {
                lines.Add($"Filtration Volume: {p.FiltrationVolume}");
                lines.Add($"Washing Volume: {p.WashingVolume}");
            }
            lines.Add($"Diluent: {p.Diluent}");
            lines.Add($"Neutralizer: {p.Neutralizer}");
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
                    lines.Add($"    Location: {loc.LocationName}   CFU: {loc.CFUResult?.ToString() ?? "-"}   Reported: {loc.ReportedResult ?? "-"}   Status: {loc.Status?.ToString() ?? "-"}");
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
                    lines.Add($"    Limits (Alert/Action/Spec): {FormatLimit(r.AlertLimit)} / {FormatLimit(r.ActionLimit)} / {FormatLimit(r.SpecLimit)}");
                    lines.Add($"    Entered By: {r.EnteredByName}   Entered At: {FormatDateTime(r.EnteredAt)}");
                }
            }
            else if (order.Analysis is { } analysis)
            {
                lines.Add($"  FINAL RESULT ({analysis.AnalysisType}):");
                lines.Add($"    Analysed At: {FormatDateTime(analysis.AnalysedAt)}   Entered By: {analysis.EnteredByName}   Entered At: {FormatDateTime(analysis.EnteredAt)}");
                foreach (var pr in analysis.ParameterResults)
                {
                    lines.Add($"    {pr.ParameterName}: {pr.ReportedDisplay}   Spec: {FormatLimit(pr.SpecLimit)}   Status: {pr.ComparisonStatus}");
                }
            }
            else if (order.PathogenObservations.Count > 0)
            {
                lines.Add("  FINAL RESULT:");
                foreach (var p in order.PathogenObservations)
                    lines.Add($"    {p.StepName}: Observation = {p.Observation}   Entered By: {p.ObservedByName}   Entered At: {FormatDateTime(p.ObservedAt)}");
            }

            if (order.BiochemicalResults.Count > 0)
            {
                lines.Add("  BIOCHEMICAL IDENTIFICATION:");
                foreach (var b in order.BiochemicalResults)
                {
                    var call = b.OrganismDetected is true ? nameof(ResultStatus.Detected) : b.OrganismDetected is false ? "Not Detected" : "Undetermined";
                    lines.Add($"    {b.StepName}: {b.BiochemicalResultText}   Interpretation: {call}   Entered By: {b.SubmittedByName}   Entered At: {FormatDateTime(b.SubmittedAt)}");
                }
            }
            else if (order.Results.Count > 0 && order.Analysis is null)
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
    private static string FormatLimit(string? limit) => string.IsNullOrWhiteSpace(limit) ? "-" : limit;

    // Postgres numeric columns come back with their full scale (8500.000000);
    // the export shows the value as entered/calculated, without trailing zeros.
    private static string ExportNumber(decimal? d) =>
        d is decimal v ? v.ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture) : "-";

    public static string? FormatSpecificationsText(IReadOnlyList<Specification>? specs)
    {
        if (specs is null || specs.Count == 0)
            return null;
        if (specs.Count == 1)
            return FormatSpecificationText(specs[0]);

        var parts = specs
            .Select(s => (s.ParameterName, Text: FormatSpecificationText(s)))
            .Where(p => p.Text is not null)
            .Select(p => $"{p.ParameterName}: {p.Text}")
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    public static string? FormatSpecificationText(Specification? spec)
    {
        if (spec is null)
            return null;

        static string WithUnit(string text, string? unit) =>
            string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit.Trim()}";

        switch (spec.LimitType)
        {
            case LimitType.NotMoreThan:
            case LimitType.NotLessThan:
            case LimitType.TargetWithTolerance:
                return string.IsNullOrWhiteSpace(spec.SpecLimit) ? null : WithUnit(spec.SpecLimit.Trim(), spec.Unit);
            case LimitType.Qualitative:
                return string.IsNullOrWhiteSpace(spec.ExpectedResultText) ? null : spec.ExpectedResultText.Trim();
            case LimitType.PresenceAbsence:
            {
                var state = spec.ExpectedState == ExpectedPresence.Presence ? "Present" : "Absent";
                return spec.SampleQuantity is decimal qty
                    ? $"{state} in {qty.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} {spec.SampleQuantityUnit}".TrimEnd()
                    : state;
            }
            case LimitType.MultiStage:
                return spec.Stages.Count == 0
                    ? null
                    : string.Join("; ", spec.Stages.OrderBy(s => s.StageNumber).Select(s => $"{s.StageLabel}: {s.AcceptanceCriteriaText}"));
        }

        // Range and Count-Tiered: the original text rules.
        if (string.IsNullOrWhiteSpace(spec.SpecLimit))
            return null;

        var raw = spec.SpecLimit.Trim();
        var parsed = SpecLimitParser.Parse(raw);

        if (parsed.IsRange)
        {
            var minStr = parsed.MinRaw ?? parsed.Min!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var maxStr = parsed.MaxRaw ?? parsed.Max!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var unit = !string.IsNullOrWhiteSpace(spec.Unit) ? spec.Unit.Trim() : parsed.Unit?.Trim();
            return string.IsNullOrWhiteSpace(unit)
                ? $"{minStr} – {maxStr}"
                : $"{minStr} – {maxStr} {unit}";
        }

        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            if (!string.IsNullOrWhiteSpace(spec.Unit))
            {
                return $"NMT {raw}/{spec.Unit.Trim()}";
            }
            return raw;
        }

        return raw;
    }
}
