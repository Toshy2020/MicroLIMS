using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

// Pure per-test-order mapping for the Sample Summary: loaded data in, DTO out, no database access.
public static class SampleSummaryTestOrderBuilder
{
    public static TestOrderSummaryDetailDto Build(TestOrder order, SampleSummaryData d, Func<int, string> NameOf)
    {
        var sample = d.Sample;
        var testDefinitions = d.TestDefinitions;
        var incubations = d.Incubations;
        var mediaLookup = d.MediaLookup;
        var locationPathogenObservations = d.LocationPathogenObservations;
        var sampleLocations = d.SampleLocations;
        var specificationsByTestCode = d.SpecificationsByTestCode;
        var sectionRows = d.SectionRows;
        var sourceRefByOrderId = d.SourceRefByOrderId;
        var results = d.Results;
        var countTestReadings = d.CountTestReadings;
        var activeAnalyses = d.ActiveAnalyses;
        var pathogenObservations = d.PathogenObservations;
        var biochemicalResults = d.BiochemicalResults;
        var workflowHistory = d.WorkflowHistory;

        TestDefinition? def = null;
        testDefinitions.TryGetValue(order.TestCode, out def);

        bool usesTsb = def?.Steps.Any(step =>
            step.StepType is StepType.BrothEnrichment or StepType.SelectiveBroth ||
            step.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase)) ?? false;

        var orderIncubations = incubations.Where(i => i.TestOrderId == order.Id).ToList();
        // Effective orders can come from retest descendants, so each is
        // judged by its own sample's status, not this sample's.
        var stateResult = WorkflowStateResolver.Resolve(order, usesTsb, orderIncubations, null, d.Now, def?.Steps, order.Sample?.Status ?? sample.Status, mediaLookup);

        var locationDtos = BuildLocations(order, d, NameOf);
        specificationsByTestCode.TryGetValue(order.TestCode, out var testSpecs);

        return new TestOrderSummaryDetailDto
        {
            TestOrderId = order.Id,
            SectionId = order.SectionId,
            SectionName = sectionRows.TryGetValue(order.SectionId, out var sectionRow) ? sectionRow.Name : string.Empty,
            TestCode = order.TestCode,
            TestDisplayName = def?.DisplayName ?? order.TestCode,
            SourceSampleReferenceNumber = sourceRefByOrderId.TryGetValue(order.Id, out var sourceRef) ? sourceRef : null,
            SpecificationText = SampleSummaryService.FormatSpecificationsText(testSpecs),
            Status = order.Status.ToString(),
            CurrentStep = order.CurrentStep.ToString(),
            WorkflowState = stateResult.WorkflowState,
            WorkflowStateDisplay = stateResult.WorkflowStateDisplay,
            WorkflowStatus = stateResult.WorkflowStatus,
            UsesSharedTsb = usesTsb,
            IsWorkflowLocked = stateResult.IsWorkflowLocked,
            IsResultEntryAllowed = stateResult.IsResultEntryAllowed,
            ResultLockReason = stateResult.LockReason,
            IsSuperseded = order.IsSuperseded,
            CancelledAtStep = order.CancelledAtStep?.ToString(),
            CancelledAtStage = order.CancelledAtStage,
            Incubations = BuildIncubations(order, d, NameOf),
        Results = results.Where(r => r.TestOrderId == order.Id).Select(r => new ResultDetailDto
        {
            RawValue = r.RawValue,
            InterpretedValue = r.InterpretedValue,
            Type = r.Type.ToString(),
            EnteredByName = NameOf(r.EnteredByUserId),
            EnteredAt = r.EnteredAt
        }).ToList(),
        CountTestReadings = countTestReadings.Where(r => r.TestOrderId == order.Id && r.IsActive).Select(r => new CountTestReadingDetailDto
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
        Analysis = activeAnalyses.Where(e => e.TestOrderId == order.Id).Select(e => new AnalysisDetailDto
        {
            Id = e.Id,
            TestOrderId = e.TestOrderId,
            AnalysisType = e.AnalysisType,
            EquipmentId = e.EquipmentId,
            EquipmentCode = e.EquipmentCode,
            EquipmentName = e.EquipmentName,
            AnalysedAt = e.AnalysedAt,
            UnitAmount = e.UnitAmount,
            ConditionsJson = e.ConditionsJson,
            ValidityRecordType = e.ValidityRecordType,
            ValidityRecordId = e.ValidityRecordId,
            EnteredByName = NameOf(e.EnteredByUserId),
            EnteredAt = e.EnteredAt,
            Comment = e.Comment,
            ParameterResults = e.Results.Select(r => new ParameterResultDetailDto
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
                Readings = r.Readings
            }).ToList()
        }).FirstOrDefault(),
        PathogenObservations = pathogenObservations.Where(p => p.TestOrderId == order.Id).Select(p => new PathogenObservationDetailDto
        {
            StepName = p.StepName,
            StepOrder = p.StepOrder,
            Observation = p.Observation.ToString(),
            ObservedByName = NameOf(p.ObservedByUserId),
            ObservedAt = p.ObservedAt
        }).ToList(),
        BiochemicalResults = biochemicalResults.Where(r => r.TestOrderId == order.Id).Select(r => new BiochemicalResultDetailDto
        {
            StepName = r.StepName,
            BiochemicalResultText = r.BiochemicalResultText ?? string.Empty,
            OrganismDetected = r.BiochemicalOrganismDetected,
            SubmittedByName = NameOf(r.SubmittedByUserId),
            SubmittedAt = r.SubmittedAtUtc
        }).ToList(),
        WorkflowHistory = workflowHistory.Where(w => w.TestOrderId == order.Id).OrderBy(w => w.Timestamp).Select(w => new WorkflowHistoryDetailDto
        {
            FromStep = w.FromStep.ToString(),
            ToStep = w.ToStep.ToString(),
            Note = w.Note,
            PerformedByName = NameOf(w.PerformedByUserId),
            Timestamp = w.Timestamp
        }).ToList(),
        Locations = locationDtos
        };
    }

    private static List<SampleLocationDetailDto> BuildLocations(TestOrder order, SampleSummaryData d, Func<int, string> NameOf)
    {
        var locationPathogenObservations = d.LocationPathogenObservations;
        var sampleLocations = d.SampleLocations;

        var orderPathogenObs = locationPathogenObservations.Where(o => o.TestOrderId == order.Id).ToList();

        List<SampleLocationDetailDto> locationDtos;
        if (orderPathogenObs.Count > 0)
        {
            locationDtos = orderPathogenObs.Select(obs =>
            {
                string reportedResult;
                ResultStatus status;

                if (obs.ConfirmatoryPlateObservations != null && obs.ConfirmatoryPlateObservations.Count > 0)
                {
                    var plates = obs.ConfirmatoryPlateObservations.ToList();
                    var allConforming = plates.All(p => p.Observation == GrowthObservation.GrowthConforming);
                    var allNoGrowth = plates.All(p => p.Observation == GrowthObservation.NoGrowth);

                    if (allConforming)
                    {
                        reportedResult = "Detected (+)";
                        status = ResultStatus.OutOfSpecification;
                    }
                    else if (allNoGrowth)
                    {
                        reportedResult = "Not Detected (-)";
                        status = ResultStatus.WithinLimits;
                    }
                    else
                    {
                        reportedResult = "Inconclusive (Retest)";
                        status = ResultStatus.RequiresReview;
                    }
                }
                else if (obs.SampleLocation != null && !string.IsNullOrWhiteSpace(obs.SampleLocation.ReportedResult) && obs.SampleLocation.Status != ResultStatus.PendingConfirmation)
                {
                    reportedResult = obs.SampleLocation.ReportedResult;
                    status = obs.SampleLocation.Status == ResultStatus.Detected ? ResultStatus.OutOfSpecification : obs.SampleLocation.Status == ResultStatus.Absent ? ResultStatus.WithinLimits : obs.SampleLocation.Status ?? ResultStatus.WithinLimits;
                }
                else
                {
                    reportedResult = obs.GrowthObservation switch
                    {
                        GrowthObservation.NoGrowth => "Not Detected (-)",
                        GrowthObservation.GrowthNonConforming => "Growth Non-Conforming",
                        GrowthObservation.GrowthConforming => "Growth Conforming (Presumptive +)",
                        _ => "—"
                    };
                    status = obs.GrowthObservation == GrowthObservation.NoGrowth
                        ? ResultStatus.WithinLimits
                        : ResultStatus.PendingConfirmation;
                }

                return new SampleLocationDetailDto
                {
                    LocationKey = ResolveLocationKey(obs.SampleLocation),
                    LocationName = ResolveLocationName(obs.SampleLocation),
                    GradeClassification = obs.SampleLocation?.RoomTestConfiguration?.Room?.GradeClassification,
                    AlertLimit = null,
                    ActionLimit = null,
                    SpecLimit = null,
                    CFUResult = null,
                    CalculatedResult = null,
                    ReportedResult = reportedResult,
                    Status = status,
                    EnteredByName = NameOf(obs.ObservedByUserId),
                    EnteredAt = obs.ObservedAt
                };
            }).ToList();
        }
        else
        {
            locationDtos = sampleLocations.Where(l => l.TestOrderId == order.Id).Select(l => new SampleLocationDetailDto
            {
                LocationKey = ResolveLocationKey(l),
                LocationName = ResolveLocationName(l),
                GradeClassification = l.RoomTestConfiguration?.Room?.GradeClassification,
                AlertLimit = l.AlertLimit,
                ActionLimit = l.ActionLimit,
                SpecLimit = l.SpecLimit,
                CFUResult = l.CFUResult,
                CalculatedResult = l.CalculatedResult,
                ReportedResult = l.ReportedResult,
                Status = l.Status,
                Unit = l.Unit,
                EnteredByName = l.EnteredByUserId is not null ? NameOf(l.EnteredByUserId.Value) : null,
                EnteredAt = l.EnteredAt
            }).ToList();
        }

        return locationDtos;
    }

    private static List<IncubationDetailDto> BuildIncubations(TestOrder order, SampleSummaryData d, Func<int, string> NameOf)
    {
        var incubations = d.Incubations;
        var countTestReadings = d.CountTestReadings;
        var sampleLocations = d.SampleLocations;
        var pathogenObservations = d.PathogenObservations;
        var results = d.Results;
        var workflowHistory = d.WorkflowHistory;

        return incubations.Where(i => i.TestOrderId == order.Id)
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
    }).ToList();
    }

    // The physical location's stable identity, independent of
    // ResolveLocationName's display text (which can collide - e.g.
    // several water sampling points sharing the same "WTU" label). Feeds
    // SampleLocationDetailDto.LocationKey. Rooms and machine parts key on
    // the Room/MachinePart itself, not the per-test configuration row:
    // there is one RoomTestConfiguration per room per TestCode, so keying
    // on it gave the same room a separate COA row for every test.
    private static string ResolveLocationKey(SampleLocation? loc)
    {
        if (loc == null) return $"None:{Guid.NewGuid()}";
        if (loc.WaterSamplingPointId is int directWaterId) return $"Water:{directWaterId}";
        if (loc.SamplingConfiguration?.WaterSamplingPointId is int configWaterId) return $"Water:{configWaterId}";
        if (loc.RoomTestConfiguration?.RoomId is int roomId) return $"Room:{roomId}";
        if (loc.RoomTestConfigurationId is int roomConfigId) return $"RoomConfig:{roomConfigId}";
        if (loc.MachinePartConfiguration?.MachinePartId is int partId) return $"MachinePart:{partId}";
        if (loc.MachinePartConfigurationId is int partConfigId) return $"MachinePartConfig:{partConfigId}";
        return $"Loc:{loc.Id}";
    }

    private static string ResolveLocationName(SampleLocation? loc)
    {
        if (loc == null) return "—";
        if (loc.WaterSamplingPoint != null)
            return !string.IsNullOrWhiteSpace(loc.WaterSamplingPoint.Code)
                ? loc.WaterSamplingPoint.Code
                : loc.WaterSamplingPoint.Location ?? "—";
        if (loc.SamplingConfiguration?.WaterSamplingPoint != null)
            return !string.IsNullOrWhiteSpace(loc.SamplingConfiguration.WaterSamplingPoint.Code)
                ? loc.SamplingConfiguration.WaterSamplingPoint.Code
                : loc.SamplingConfiguration.WaterSamplingPoint.Location ?? "—";
        if (loc.RoomTestConfiguration?.Room != null)
            return loc.RoomTestConfiguration.Room.Name ?? "—";
        if (loc.MachinePartConfiguration?.MachinePart != null)
            return loc.MachinePartConfiguration.MachinePart.Name ?? "—";
        return $"Location {loc.Id}";
    }
}
