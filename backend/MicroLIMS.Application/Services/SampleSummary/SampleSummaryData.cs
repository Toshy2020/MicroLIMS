using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services;

public sealed record SampleSectionRow(string Code, string Name);

public sealed class ActiveParameterResultRow
{
    public int Id { get; set; }
    public int SpecificationId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public decimal? ReportedValue { get; set; }
    public string ReportedDisplay { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? SpecLimit { get; set; }
    public ResultBasis? ResultBasis { get; set; }
    public ResultStatus ComparisonStatus { get; set; }
    public bool OverRange { get; set; }
    public bool BelowLoq { get; set; }
    public string? CalculationJson { get; set; }
    public int? StageReached { get; set; }
    public List<ResultReadingDetailDto> Readings { get; set; } = new();
}

public sealed class ActiveAnalysisRow
{
    public int Id { get; set; }
    public int TestOrderId { get; set; }
    public WorkflowType AnalysisType { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public DateTime AnalysedAt { get; set; }
    public decimal? UnitAmount { get; set; }
    public string? ConditionsJson { get; set; }
    public string? ValidityRecordType { get; set; }
    public int? ValidityRecordId { get; set; }
    public int EnteredByUserId { get; set; }
    public DateTime EnteredAt { get; set; }
    public string? Comment { get; set; }
    public List<ActiveParameterResultRow> Results { get; set; } = new();
}

// Everything GetSummaryAsync loads for one sample, handed to the pure builders.
public sealed class SampleSummaryData
{
    public required Sample Sample { get; init; }
    public required List<TestOrder> EffectiveTestOrders { get; init; }
    public required Dictionary<int, string> SourceRefByOrderId { get; init; }
    public required Dictionary<int, SampleSectionRow> SectionRows { get; init; }
    public required List<Incubation> Incubations { get; init; }
    public required List<Result> Results { get; init; }
    public required List<CountTestReading> CountTestReadings { get; init; }
    public required List<ActiveAnalysisRow> ActiveAnalyses { get; init; }
    public required List<PathogenObservation> PathogenObservations { get; init; }
    public required List<WorkflowStepResult> BiochemicalResults { get; init; }
    public required List<WorkflowHistory> WorkflowHistory { get; init; }
    public required List<SampleLocation> SampleLocations { get; init; }
    public required List<LocationPathogenObservation> LocationPathogenObservations { get; init; }
    public required Dictionary<string, TestDefinition> TestDefinitions { get; init; }
    public required Dictionary<int, (int MaterialId, int? MediaProductId)> MediaLookup { get; init; }
    public required Dictionary<string, List<Specification>> SpecificationsByTestCode { get; init; }
    public required DateTime Now { get; init; }
}
