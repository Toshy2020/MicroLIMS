using MicroLIMS.Domain.Enums;

namespace MicroLIMS.API.Controllers;

// Request bodies for TestWorkflowController, kept in their own file so the
// controller shows only endpoints. Namespace is unchanged on purpose.
// SelectMediaRequest/RecordTestResultRequest are CountTest-only now -
// every pathogen step is submitted through the dedicated Submit*
// endpoints below instead. Their dual-plate fields (Plate2MediaId,
// Plate1Label, Plate2Label, GrowthObserved, Plate1/2GrowthObserved) are
// gone along with the dual-plate model itself; see TestWorkflowEngine.
public record SelectMediaRequest(string StepName, int MediaLotId, int IncubatorId);
public record StartStage2IncubationRequest(string StepName, int IncubatorId);
public record RecordTestResultRequest(string StepName, List<decimal>? PlateReadings, decimal? DilutionFactor, List<string>? RawPlateReadings = null, string? DilutionFactorOverrideNote = null);
public record RecordMeasurementParameterRequest(int SpecificationId, List<decimal> Readings);
public record RecordMeasurementResultRequest(DateTime AnalysedAt, int? EquipmentId, List<RecordMeasurementParameterRequest> Parameters, string Password, string? Comment = null);
public record GravimetricReplicateRequest(decimal? Container, decimal Initial, decimal Final);
public record RecordGravimetricParameterRequest(int SpecificationId, List<GravimetricReplicateRequest> Replicates);
public record RecordGravimetricResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string> Conditions, List<RecordGravimetricParameterRequest> Parameters, string Password, string? Comment = null);
public record RecordQualitativeParameterRequest(int SpecificationId, bool Conforms, string? Observation);
public record RecordQualitativeResultRequest(DateTime AnalysedAt, int? EquipmentId, List<RecordQualitativeParameterRequest> Parameters, string Password, string? Comment = null);
public record RecordDissolutionResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string>? Conditions, decimal MediumVolumeMl, decimal? DilutionFactor, List<decimal> VesselAreas, string Password, string? Comment = null);
public record RecordDissolutionStageRequest(List<decimal> VesselAreas, string Password, string? Comment = null);
public record RecordDisintegrationResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string>? Conditions, List<decimal?> UnitMinutes, string Password, string? Comment = null);
public record RecordDisintegrationStageRequest(List<decimal?> UnitMinutes, string Password, string? Comment = null);
public record RecordWeightVariationUnitRequest(decimal? WeightMg = null, decimal? GrossMg = null, decimal? ShellMg = null);
public record RecordWeightVariationResultRequest(DateTime AnalysedAt, int? EquipmentId, Dictionary<string, string>? Conditions, List<RecordWeightVariationUnitRequest> Units, string Password, string? Comment = null);
public record RecordWeightVariationStageRequest(List<RecordWeightVariationUnitRequest> Units, string Password, string? Comment = null);
public record BatchResultLocationRequest(int SampleLocationId, List<decimal> Readings);
public record BatchResultsRequest(List<BatchResultLocationRequest> Locations);
public record WaterBatchLocationRequest(int SampleLocationId, List<decimal> Readings);
public record WaterBatchReadingsRequest(List<WaterBatchLocationRequest> Locations);
public record BatchPathogenLocationRequest(int SampleLocationId, bool? GrowthObserved);
public record BatchPathogenResultsRequest(List<BatchPathogenLocationRequest> Locations);

public record SubmitBrothRequest(string StepName, string? Observation);

public record StartSelectivePlatingIncubationRequest(string StepName, int MediaLotId, int EquipmentId, DateTime? IncubationStartUtc = null);
public record SubmitSelectivePlatingObservationRequest(string StepName, GrowthObservation Observation, string? ObservedAppearanceNote = null);

[Obsolete("Use StartSelectivePlatingIncubationRequest and SubmitSelectivePlatingObservationRequest.")]
public record SubmitSelectivePlatingRequest(string StepName, int MediaLotId, int EquipmentId,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc, GrowthObservation Observation);

public record ConfirmatorySelectionRequest(int StepMediaId, int MediaLotId, int EquipmentId);
public record SubmitConfirmatorySetupRequest(string StepName, List<ConfirmatorySelectionRequest> Selections,
    DateTime IncubationStartUtc, DateTime IncubationEndUtc);

public record TitrationStandardRequest(int MaterialId, decimal WeightMg, decimal TitreMl);
public record TitrationReplicateRequest(decimal SampleWeightMg, decimal TitrantVolumeMl);
public record RecordTitrationResultRequest(
    DateTime AnalysedAt, int? EquipmentId, int TitrantPreparationId, int? ExcessPreparationId, decimal? BlankVolumeMl,
    decimal? TitrationTemperatureC, decimal? LossOnDryingPercent, decimal? AverageUnitWeightMg,
    List<TitrationStandardRequest>? Standards, List<int> SpecificationIds, List<TitrationReplicateRequest> Replicates,
    string Password, string? Comment = null, bool? DueTitrantAcknowledged = null, string? DueTitrantJustification = null);

public record ConfirmatoryObservationRequest(int MaterialId, GrowthObservation Observation);
public record SubmitConfirmatoryObservationsRequest(string StepName, List<ConfirmatoryObservationRequest> Observations);

public record AnalystDecisionRequest(AnalystDecision Decision);
public record SubmitBiochemicalRequest(string StepName, string BiochemicalResultText, int? AttachmentId, bool OrganismDetected);
public record BiochemicalReviewRequest(bool Approve, string Comment);
