using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using System.Text.Json.Serialization;

namespace MicroLIMS.Application.DTOs;

// Request bodies of the master-data endpoints (api/masterdata/...).
// The Application services take them directly.

public record CreateWaterSamplingPointRequest(string Code, string Location, string TestingFrequency, List<string> AssignedTestCodes, int? WaterDepartmentId);
public record UpdateWaterSamplingPointRequest(string Code, string Location, string TestingFrequency, List<string> AssignedTestCodes, int? WaterDepartmentId);
public record CreateWaterDepartmentRequest(string Name);
public record UpdateWaterDepartmentRequest(string Name);
public record CreateWaterSamplingConfigRequest(int WaterSamplingPointId, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record UpdateWaterSamplingConfigRequest(string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateDepartmentRequest(string Name, string Class, string TestingFrequency);
public record CreateRoomRequest(string Name, int DepartmentId, string GradeClassification);
public record UpdateDepartmentRequest(string Name, string Class, string TestingFrequency);
public record UpdateRoomRequest(string Name, int DepartmentId, string GradeClassification);
public record UpdateRoomTestConfigRequest(string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateMachineRequest(string Name);
public record UpdateMachineRequest(string Name);
public record CreateMachinePartRequest(string Name, int MachineId);
public record UpdateMachinePartRequest(string Name, int MachineId);
public record UpdateMachinePartConfigRequest(string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, bool IsPathogenTest, string? Unit = null);
public record SpecificationStageDto(int? Id, int StageNumber, string StageLabel, string AcceptanceCriteriaText);
public record CreateSpecificationRequest(
    int ItemId,
    string TestCode,
    string? AlertLimit = null,
    string? ActionLimit = null,
    string? SpecLimit = null,
    string? Unit = null,
    decimal? DilutionFactor = null,
    string? ParameterName = null,
    int? DisplayOrder = null,
    LimitType? LimitType = null,
    string? ReferenceStandard = null,
    decimal? LowerLimit = null,
    decimal? UpperLimit = null,
    bool? LowerInclusive = null,
    bool? UpperInclusive = null,
    decimal? Target = null,
    decimal? Tolerance = null,
    ToleranceMode? ToleranceMode = null,
    string? ExpectedResultText = null,
    ExpectedPresence? ExpectedState = null,
    decimal? SampleQuantity = null,
    string? SampleQuantityUnit = null,
    List<SpecificationStageDto>? Stages = null,
    ResultBasis? ResultBasis = null,
    decimal? LabelClaim = null,
    string? LabelClaimUnit = null,
    DosageForm? DosageForm = null,
    int? HplcMethodAnalyteId = null,
    ProductionStageRole? ProductionStageRole = null,
    int? IcpMethodElementId = null);

public record UpdateSpecificationRequest(
    string TestCode,
    string? AlertLimit = null,
    string? ActionLimit = null,
    string? SpecLimit = null,
    string? Unit = null,
    decimal? DilutionFactor = null,
    string? ParameterName = null,
    int? DisplayOrder = null,
    LimitType? LimitType = null,
    string? ReferenceStandard = null,
    decimal? LowerLimit = null,
    decimal? UpperLimit = null,
    bool? LowerInclusive = null,
    bool? UpperInclusive = null,
    decimal? Target = null,
    decimal? Tolerance = null,
    ToleranceMode? ToleranceMode = null,
    string? ExpectedResultText = null,
    ExpectedPresence? ExpectedState = null,
    decimal? SampleQuantity = null,
    string? SampleQuantityUnit = null,
    List<SpecificationStageDto>? Stages = null,
    ResultBasis? ResultBasis = null,
    decimal? LabelClaim = null,
    string? LabelClaimUnit = null,
    DosageForm? DosageForm = null,
    int? HplcMethodAnalyteId = null,
    ProductionStageRole? ProductionStageRole = null,
    int? IcpMethodElementId = null);

// GET shape for specifications - every field the frontend already binds to,
// unchanged, plus CanEdit/SectionName so the UI can show the other lab's
// rows read-only (Task 9 - specification rows are owned by their test's lab).
public record SpecificationRowDto(
    int Id,
    int ItemId,
    string TestCode,
    string AlertLimit,
    string ActionLimit,
    string SpecLimit,
    string Unit,
    decimal? DilutionFactor,
    string ParameterName,
    int DisplayOrder,
    LimitType LimitType,
    string? ReferenceStandard,
    decimal? LowerLimit,
    decimal? UpperLimit,
    bool LowerInclusive,
    bool UpperInclusive,
    decimal? Target,
    decimal? Tolerance,
    ToleranceMode? ToleranceMode,
    string? ExpectedResultText,
    ExpectedPresence? ExpectedState,
    decimal? SampleQuantity,
    string? SampleQuantityUnit,
    ResultBasis? ResultBasis,
    decimal? LabelClaim,
    string? LabelClaimUnit,
    DosageForm? DosageForm,
    List<SpecificationStageResponse> Stages,
    bool CanEdit,
    string SectionName,
    int? HplcMethodAnalyteId = null,
    ProductionStageRole? ProductionStageRole = null,
    int? IcpMethodElementId = null)
{
    // The record's row version, sent back as If-Match with an edit.
    public uint Version { get; init; }
}
public record CreateDiluentTypeRequest(string Name, bool RequiresBatchTracking, int? MaterialId);
public record CreateProductionStageRequest(string Name, ProductionStageRole Role);
public record UpdateProductionStageRequest(string Name, ProductionStageRole Role);
public record CreateEquipmentRequest(
    string Name,
    string Code,
    EquipmentType Type,
    string? Location,
    decimal? SetPointTemperature,
    DateTime? CalibrationDueDate,
    string? Vendor = null,
    CdsSoftware? CdsSoftware = null,
    string? ConnectionSettings = null,
    int? SectionId = null);

public record UpdateEquipmentRequest(
    string Name,
    string Code,
    EquipmentType Type,
    string? Location,
    decimal? SetPointTemperature,
    DateTime? CalibrationDueDate,
    string? Vendor = null,
    CdsSoftware? CdsSoftware = null,
    string? ConnectionSettings = null,
    int? SectionId = null);
public record CreateRoomTestConfigRequest(int RoomId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, string? Unit = null);
public record CreateMachinePartConfigRequest(int MachinePartId, string TestType, string TestCode, string AlertLimit, string ActionLimit, string SpecLimit, bool IsPathogenTest, string? Unit = null);
public record CreateMediaProductRequest(string Name, string Code);
public record UpdateMediaProductRequest(string Name);
public record ChangeMediaProductCodeRequest(string Code, string Reason, string Password);
public record CreateMediaIncubationConditionRequest(int MediaProductId, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax);
public record UpdateMediaIncubationConditionRequest(int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax);
public record CreateMediaConfigurationChallengeRequest(int OrganismId, ChallengeRole? ChallengeRole, string? ExpectedDescription, string? InitialInoculum);
public record CreateMediaConfigurationRequest(int MediaProductId, EvaluationType EvaluationType, int MediaIncubationConditionId, decimal? RecoveryPercentMin, decimal? RecoveryPercentMax, List<CreateMediaConfigurationChallengeRequest>? Challenges);
public record UpdateMediaConfigurationRequest(int MediaProductId, EvaluationType EvaluationType, int MediaIncubationConditionId, decimal? RecoveryPercentMin, decimal? RecoveryPercentMax, List<CreateMediaConfigurationChallengeRequest>? Challenges);
public record CreateOrganismRequest(string ScientificName, string? AtccNumber, string? CommonName, string? Description);
public record UpdateOrganismRequest(string ScientificName, string? AtccNumber, string? CommonName, string? Description);
public record CreateTestDefinitionRequest(
    string Code,
    string DisplayName,
    int? SectionId = null,
    WorkflowType WorkflowType = WorkflowType.Observation,
    EquationType EquationType = EquationType.None,
    bool RequiresSystemSuitability = false,
    string? MethodAbbreviation = null,
    decimal? SstMaxRsdPercent = null,
    decimal? SstMinResolution = null,
    decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null,
    int? ReplicateCount = null,
    MeasurementEvaluationBasis? EvaluationBasis = null,
    string? ConditionFields = null,
    bool? UsesTare = null,
    decimal? DissolutionS1Offset = null,
    decimal? DissolutionS2MinOffset = null,
    decimal? DissolutionS3MinOffset = null,
    decimal? DissolutionS3MaxBelowS2Min = null,
    int? DisintegrationStage1Units = null,
    int? DisintegrationStage2Units = null,
    int? DisintegrationMaxStage1Failures = null,
    int? DisintegrationMinPassTotal = null,
    int? WvUnitCount = null,
    decimal? WvTabletBand1MaxMg = null,
    decimal? WvTabletBand1Percent = null,
    decimal? WvTabletBand2MaxMg = null,
    decimal? WvTabletBand2Percent = null,
    decimal? WvTabletBand3Percent = null,
    int? WvTabletMaxOutside = null,
    decimal? WvCapsuleInnerPercent = null,
    decimal? WvCapsuleOuterPercent = null,
    int? WvCapsuleS1MaxOutside = null,
    int? WvCapsuleS1MaxForRetest = null,
    int? WvCapsuleS2ExtraUnits = null,
    int? WvCapsuleS2MaxOutside = null,
    int? HplcMethodId = null,
    TitrationType? TitrationType = null,
    bool? TitrationNonAqueous = null,
    TitrationMode? TitrationMode = null,
    TitrationCalculation? TitrationCalculation = null,
    int? TitrantSolutionMasterId = null,
    decimal? TitrationEquivalencyFactor = null,
    bool? TitrationBlankRequired = null,
    int? TitrationExcessSolutionMasterId = null,
    decimal? TitrationExcessVolumeMl = null,
    decimal? TitrationMaxRsdPercent = null,
    TitrationEndpoint? TitrationEndpoint = null,
    int? TitrationIndicatorEntryId = null,
    bool? TitrationTempCorrection = null,
    decimal? TitrationExpansionCoefficient = null,
    int? TitrationStandardEntryId = null,
    int? IcpMethodId = null)
{
    // System.Text.Json cannot bind a constructor with more than 64 parameters
    // and this one has more, so the body is bound through this constructor and
    // the init setters instead. Without it every POST/PUT fails model binding.
    [JsonConstructor]
    public CreateTestDefinitionRequest() : this(string.Empty, string.Empty) { }
}
// SectionId: move the test to another laboratory section (null = keep). Test
// orders already created keep the section they were created with.
public record UpdateTestDefinitionRequest(
    string Code,
    string DisplayName,
    int? SectionId = null,
    WorkflowType? WorkflowType = null,
    EquationType? EquationType = null,
    bool? RequiresSystemSuitability = null,
    string? MethodAbbreviation = null,
    decimal? SstMaxRsdPercent = null,
    decimal? SstMinResolution = null,
    decimal? SstMaxTailingFactor = null,
    decimal? SstMinTheoreticalPlates = null,
    int? ReplicateCount = null,
    MeasurementEvaluationBasis? EvaluationBasis = null,
    string? ConditionFields = null,
    bool? UsesTare = null,
    decimal? DissolutionS1Offset = null,
    decimal? DissolutionS2MinOffset = null,
    decimal? DissolutionS3MinOffset = null,
    decimal? DissolutionS3MaxBelowS2Min = null,
    int? DisintegrationStage1Units = null,
    int? DisintegrationStage2Units = null,
    int? DisintegrationMaxStage1Failures = null,
    int? DisintegrationMinPassTotal = null,
    int? WvUnitCount = null,
    decimal? WvTabletBand1MaxMg = null,
    decimal? WvTabletBand1Percent = null,
    decimal? WvTabletBand2MaxMg = null,
    decimal? WvTabletBand2Percent = null,
    decimal? WvTabletBand3Percent = null,
    int? WvTabletMaxOutside = null,
    decimal? WvCapsuleInnerPercent = null,
    decimal? WvCapsuleOuterPercent = null,
    int? WvCapsuleS1MaxOutside = null,
    int? WvCapsuleS1MaxForRetest = null,
    int? WvCapsuleS2ExtraUnits = null,
    int? WvCapsuleS2MaxOutside = null,
    int? HplcMethodId = null,
    TitrationType? TitrationType = null,
    bool? TitrationNonAqueous = null,
    TitrationMode? TitrationMode = null,
    TitrationCalculation? TitrationCalculation = null,
    int? TitrantSolutionMasterId = null,
    decimal? TitrationEquivalencyFactor = null,
    bool? TitrationBlankRequired = null,
    int? TitrationExcessSolutionMasterId = null,
    decimal? TitrationExcessVolumeMl = null,
    decimal? TitrationMaxRsdPercent = null,
    TitrationEndpoint? TitrationEndpoint = null,
    int? TitrationIndicatorEntryId = null,
    bool? TitrationTempCorrection = null,
    decimal? TitrationExpansionCoefficient = null,
    int? TitrationStandardEntryId = null,
    int? IcpMethodId = null)
{
    // System.Text.Json cannot bind a constructor with more than 64 parameters
    // and this one has more, so the body is bound through this constructor and
    // the init setters instead. Without it every POST/PUT fails model binding.
    [JsonConstructor]
    public UpdateTestDefinitionRequest() : this(string.Empty, string.Empty) { }
}
public record UpdateWorkflowTypeRequest(WorkflowType WorkflowType);

public record StepMediaRequest(int MaterialId, bool IsRequired, int DisplayOrder, int? MediaIncubationConditionId);
public record IncubationStageRequest(int StageNumber, decimal TempMin, decimal TempMax, int IncubationMinHours, int IncubationMaxHours);
// PhenotypicTestType (single) is kept alongside the new PhenotypicTestTypes
// list for backward compatibility - an older client sending only the single
// field still works. When the list is supplied, it drives the step's
// bundled phenotypic tests; the single field is still stored too (whatever
// the client sends) since some existing chained-step templates rely on it.
public record CreateTestWorkflowStepRequest(string StepName, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount, PhenotypicTestType? PhenotypicTestType, List<PhenotypicTestType>? PhenotypicTestTypes = null);
public record UpdateTestWorkflowStepRequest(string StepName, int IncubationMinHours, int IncubationMaxHours, decimal TemperatureMin, decimal TemperatureMax, bool IsFinalStep, StepType StepType, int? TargetOrganismId, List<StepMediaRequest> StepMedia, bool RequiresIncubationTransfer, List<IncubationStageRequest>? IncubationStages, int? ConfirmatoryMediaCount, PhenotypicTestType? PhenotypicTestType, List<PhenotypicTestType>? PhenotypicTestTypes = null);
public record MoveTestWorkflowStepRequest(string Direction);

// Backs the Items Master's category-dependent dynamic forms: Product ->
// Specification, Water -> Sampling Points, EM -> Rooms, After Cleaning
// -> Machine Parts (gap analysis - "Dynamic Forms").
public record SetAutoclaveProgramStatusHttpRequest(bool IsActive, string? Comment);

public record EquationTypeDto(
    string Code,
    string Name,
    string FormulaText,
    IReadOnlyList<string> RequiredInputs);
