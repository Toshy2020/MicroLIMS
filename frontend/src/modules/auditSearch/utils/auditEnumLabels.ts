// Human-readable labels for enum fields stored as raw integers in audit
// JSON snapshots (System.Text.Json serializes C# enums as their ordinal by
// default). Arrays are index-aligned to the backend enum definition - do
// not reorder without checking the matching MicroLIMS.Domain.Enums.*.cs
// file, since these positions are the actual stored values.
//
// Keyed by "EntityName.FieldName" (EntityName = the raw C# class name the
// backend's CaptureAuditEntries stamps onto AuditLog.EntityName - the same
// field name can resolve differently per entity, e.g. Status).
//
// This was built by scanning every entity in MicroLIMS.Domain/Entities for
// a property typed as one of MicroLIMS.Domain/Enums/*.cs - keep both in
// sync when either side changes.

const AdminPasswordRecoveryStatus = ["Pending", "Used", "Expired", "Failed - Limit Exceeded"];
const AnalystDecision = ["Submit as Detected", "Proceed to Biochemical"];
const ApprovalDecision = ["Approve", "Reject", "Retest Retained Sample", "New Sample Request", "Investigation", "OOS Investigation"];
const ApprovalGateStatus = ["Pending Review", "Approved", "Rejected"];
const ApprovalStatus = ["Pending", "In Progress", "Result Entered", "Reviewed", "Approved", "Rejected", "Retest Requested"];
const ChallengeRole = ["Inhibition", "Indication"];
const ConfirmatoryResult = ["All Conforming", "Inconclusive"];
const EquipmentDocumentType = ["", "Calibration Certificate"]; // starts at 1 - index 0 unused
const EquipmentOperationalStatus = ["In Service", "Out of Service", "Retired"];
const EquipmentType = ["Incubator", "Autoclave", "LAF Cabinet", "Biological Safety Cabinet", "Water Bath", "Other"];
const EvaluationOutcome = ["Conform", "Non-Conform"];
const EvaluationType = ["Growth Promotion", "Indication / Inhibition", "Enrichment Characteristics"];
const GrowthObservation = ["No Growth", "Growth Non-Conforming", "Growth Conforming"];
const ItemDocumentType = ["SOP", "Verification Report"];
const LocationType = ["Room", "Machine Part", "Water Sampling Point"];
const MaterialDocumentAccessAction = ["View", "Download", "Upload", "Supersede", "Void"];
const MaterialDocumentStatus = ["Current", "Superseded", "Voided"];
const MaterialDocumentType = ["COA", "Supplier Certificate", "Specification", "SDS", "Other"];
const MaterialType = ["Dehydrated Media", "Lyophilized Microorganism", "Supplement", "Antibiotic Disc", "Identification Kit", "Identification Reagent", "Chemical", "Indicator", "Reference Buffer", "Disposable Tool", "Other"];
const MaterialUnit = ["Gram", "Kilogram", "Milliliter", "Liter", "Disc", "Vial", "Kit", "Piece", "Bottle", "Pack"];
const MediaEvaluationStatus = ["Assigned", "In Progress", "Completed"];
const MediaStatus = ["Prepared", "Active", "Expired", "Quarantine / Failed", "Destroyed", "Out of Stock"];
const PhenotypicTestType = ["Gram", "Catalase", "Oxidase", "Coagulase", "Antibiogram", "Identification Kit"];
const ResultKind = ["Quantitative", "Qualitative"];
const ResultLevel = ["Within Limit", "Alert Level", "Action Level", "Out of Specification", "Not Applicable", "Limits Not Configured"];
const ResultType = ["Numeric", "Growth / No Growth", "Interpretive"];
const ReviewWorkflowEventType = ["Submitted for Review", "Review Completed", "Submitted for Approval", "Approval Decision Made"];
const RoleType = ["System Administrator", "Section Head", "Reviewer", "Analyst"];
const SampleCategory = ["Finished Product", "Raw Material", "Packaging Material", "Water", "Environmental Monitoring", "After Cleaning", "GPT"];
const SamplePreparationStatus = ["Needs Preparation", "Ready"];
const SampleStatus = ["Received", "In Testing", "Under Review", "Under Approval", "Approved", "Rejected", "Retest Requested"];
const SignatureMeaning = ["Reviewed", "Approved", "Rejected", "Retest Requested", "Investigation Ordered", "Preparation Steps Confirmed as Configured"];
const StepType = ["Plate Count", "Broth Enrichment", "Selective Broth", "Selective Plating", "Confirmatory Plating", "Biochemical Test"];
const WorkflowStep = ["Waiting", "Running", "Incubating", "Ready", "Reviewed", "Approved"];
const WorkflowType = ["Count Test", "Observation"];

// EquipmentDocumentAccessLog / MaterialDocumentAccessLog are excluded from
// audit capture entirely (see MicroLimsDbContext.CaptureAuditEntries), so
// their Action fields never reach this viewer - only intentionally absent.

export const AUDIT_ENUM_LABELS: Record<string, string[]> = {
  "AdminPasswordRecovery.Status": AdminPasswordRecoveryStatus,

  "Cryovial.ApprovalStatus": ApprovalGateStatus,

  "ConfirmatoryPlateObservation.Observation": GrowthObservation,

  "ElectronicSignature.MeaningOfSignature": SignatureMeaning,

  "Equipment.Type": EquipmentType,
  "EquipmentInventory.Status": EquipmentOperationalStatus,
  "EquipmentDocument.DocumentType": EquipmentDocumentType,
  "EquipmentDocument.Status": MaterialDocumentStatus,
  "EquipmentStatusHistory.PreviousStatus": EquipmentOperationalStatus,
  "EquipmentStatusHistory.NewStatus": EquipmentOperationalStatus,

  "Item.Category": SampleCategory,
  "ItemDocument.DocumentType": ItemDocumentType,
  "ItemDocument.Status": MaterialDocumentStatus,
  "ItemDocumentAccessLog.Action": MaterialDocumentAccessAction,
  "ItemPreparationConfiguration.ApprovalStatus": ApprovalGateStatus,

  "LocationPathogenObservation.GrowthObservation": GrowthObservation,

  "Material.MaterialType": MaterialType,
  "Material.Unit": MaterialUnit,
  "MaterialDocument.DocumentType": MaterialDocumentType,
  "MaterialDocument.Status": MaterialDocumentStatus,

  "Media.Status": MediaStatus,
  "Media.ApprovalStatus": ApprovalGateStatus,
  "MediaConfiguration.EvaluationType": EvaluationType,
  "MediaConfigurationChallenge.ChallengeRole": ChallengeRole,
  "MediaEvaluation.EvaluationType": EvaluationType,
  "MediaEvaluation.Status": MediaEvaluationStatus,
  "MediaEvaluation.Outcome": EvaluationOutcome,
  "MediaEvaluationChallenge.ChallengeRole": ChallengeRole,
  "MediaEvaluationChallenge.Outcome": EvaluationOutcome,

  "OosInvestigationDocument.Status": MaterialDocumentStatus,

  "PathogenObservation.Observation": GrowthObservation,

  "Report.Category": SampleCategory,
  "ReportSnapshot.Category": SampleCategory,
  "Result.Type": ResultType,
  "ResultRecord.Category": SampleCategory,
  "ResultRecord.ResultKind": ResultKind,
  "ResultRecord.ResultLevel": ResultLevel,
  "ResultRecord.SampleStatus": SampleStatus,
  "ReviewWorkflowEvent.EventType": ReviewWorkflowEventType,
  "ReviewWorkflowEvent.Decision": ApprovalDecision,
  "Role.Type": RoleType,

  "Sample.Category": SampleCategory,
  "Sample.Status": SampleStatus,
  "Sample.PreparationStatus": SamplePreparationStatus,
  "Sample.ApprovalDecision": ApprovalDecision,
  "SampleLocation.LocationType": LocationType,

  "TestDefinition.WorkflowType": WorkflowType,
  "TestOrder.Status": ApprovalStatus,
  "TestOrder.CurrentStep": WorkflowStep,
  "TestWorkflowStep.PhenotypicTestType": PhenotypicTestType,
  "TestWorkflowStep.StepType": StepType,
  "TestWorkflowStepPhenotypicTest.PhenotypicTestType": PhenotypicTestType,

  "WorkflowHistory.FromStep": WorkflowStep,
  "WorkflowHistory.ToStep": WorkflowStep,
  "WorkflowStepResult.StepType": StepType,
  "WorkflowStepResult.SelectivePlatingObservation": GrowthObservation,
  "WorkflowStepResult.ConfirmatoryResult": ConfirmatoryResult,
  "WorkflowStepResult.AnalystDecision": AnalystDecision,
  "WorkloadWeight.Category": SampleCategory
};

export function lookupEnumLabel(entityName: string, fieldKey: string, value: number): string | null {
  const labels = AUDIT_ENUM_LABELS[`${entityName}.${fieldKey}`];
  if (!labels) return null;
  return labels[value] ?? `Unknown (${value})`;
}
