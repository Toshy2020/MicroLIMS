# Backend domain enums: status / state / stage / result / outcome / type (raw facts)

Read-only extraction from `backend/MicroLIMS.Domain` (excluding bin/obj). Values are the numeric values the C# compiler assigns: explicit literal, else previous + 1 starting at 0. Enums are listed when their name contains Status, State, Stage, Result, Outcome or Type. `[src]` = line of the `enum` declaration; members are in the same file.

Matched enums: 68. Other enums in Domain (not matched by name): 50.

## ActorType
[src: backend/MicroLIMS.Domain/Enums/ActorType.cs:L3]

Member | Value
--- | ---
User | 1
System | 2

## AdminPasswordRecoveryStatus
[src: backend/MicroLIMS.Domain/Enums/AdminPasswordRecoveryStatus.cs:L3]

Member | Value
--- | ---
Pending | 0
Used | 1
Expired | 2
FailedLimitExceeded | 3

## ApprovalGateStatus
[src: backend/MicroLIMS.Domain/Enums/ApprovalGateStatus.cs:L3]

Member | Value
--- | ---
PendingReview | 0
Approved | 1
Rejected | 2

## ApprovalStatus
[src: backend/MicroLIMS.Domain/Enums/ApprovalStatus.cs:L3]

Member | Value
--- | ---
Pending | 0
InProgress | 1
ResultEntered | 2
Reviewed | 3
Approved | 4
Rejected | 5
RetestRequested | 6
Voided | 7
Cancelled | 8

## AssignmentType
[src: backend/MicroLIMS.Domain/Enums/AssignmentType.cs:L3]

Member | Value
--- | ---
Reading | 1
Training | 2

## CalibrationCheckType
[src: backend/MicroLIMS.Domain/Enums/CalibrationCheckType.cs:L3]

Member | Value
--- | ---
Blank | 0
Icv | 1
Ccv | 2
InternalStandard | 3

## ConfirmatoryResult
[src: backend/MicroLIMS.Domain/Enums/ConfirmatoryResult.cs:L3]

Member | Value
--- | ---
AllConforming | 0
Inconclusive | 1

## DissolutionStageOutcome
[src: backend/MicroLIMS.Domain/Enums/DissolutionStageOutcome.cs:L3]

Member | Value
--- | ---
Complies | 0
NextStageRequired | 1
DoesNotComply | 2

## DocumentApprovalTaskStatus
[src: backend/MicroLIMS.Domain/Enums/DocumentApprovalTaskStatus.cs:L3]

Member | Value
--- | ---
Pending | 1
Approved | 2
ReturnedForCorrection | 3
Declined | 4
Cancelled | 5

## DocumentEscalationStatus
[src: backend/MicroLIMS.Domain/Enums/DocumentEscalationStatus.cs:L3]

Member | Value
--- | ---
Raised | 1
Resolved | 2
Cancelled | 3
Superseded | 4

## DocumentRecordStatus
[src: backend/MicroLIMS.Domain/Enums/DocumentRecordStatus.cs:L3]

Member | Value
--- | ---
Active | 1
Void | 2

## DocumentRevisionStatus
[src: backend/MicroLIMS.Domain/Enums/DocumentRevisionStatus.cs:L3]

Member | Value
--- | ---
Draft | 1
Cancelled | 2
InReview | 3
AwaitingApproval | 4
FutureEffective | 5
Effective | 6
Superseded | 7
Obsolete | 8

## EquationType
[src: backend/MicroLIMS.Domain/Enums/EquationType.cs:L3]

Member | Value
--- | ---
None | 0
HplcAssay | 1
SystemSuitability | 2
CalibrationCurve | 3
Measurement | 4
GravimetricLoss | 5
GravimetricResidue | 6
Qualitative | 7
Dissolution | 8
Disintegration | 9
WeightVariation | 10
HplcMultiAnalyte | 11
StandardComparison | 12
HplcMethodAssay | 13
Titration | 14
IcpMethodAssay | 15

## EquipmentDocumentType
[src: backend/MicroLIMS.Domain/Enums/EquipmentDocumentType.cs:L5]

Member | Value
--- | ---
CalibrationCertificate | 1

## EquipmentOperationalStatus
[src: backend/MicroLIMS.Domain/Enums/EquipmentOperationalStatus.cs:L5]

Member | Value
--- | ---
InService | 0
OutOfService | 1
Retired | 2

## EquipmentType
[src: backend/MicroLIMS.Domain/Enums/EquipmentType.cs:L3]

Member | Value
--- | ---
Incubator | 0
Autoclave | 1
LafCabinet | 2
BiologicalSafetyCabinet | 3
WaterBath | 4
Other | 5
Hplc | 6
PhMeter | 7
Balance | 8
IcpOes | 9
UvVis | 10
DissolutionTester | 11
DisintegrationTester | 12
KarlFischer | 13
Titrator | 14
Viscometer | 15
Refractometer | 16
Polarimeter | 17
ConductivityMeter | 18
MeltingPoint | 19
Oven | 20
Furnace | 21
Gc | 22
Aas | 23
DigestionMicrowave | 24
Caliper | 25

## EvaluationOutcome
[src: backend/MicroLIMS.Domain/Enums/EvaluationOutcome.cs:L3]

Member | Value
--- | ---
Conform | 0
NonConform | 1

## EvaluationType
[src: backend/MicroLIMS.Domain/Enums/EvaluationType.cs:L5]

Member | Value
--- | ---
GrowthPromotion | 0
IndicationInhibition | 1
EnrichmentCharacteristics | 2

## GptStage
[src: backend/MicroLIMS.Domain/Enums/GptStage.cs:L5]

Member | Value
--- | ---
Preparation | 0
Sterility | 1
Recovery | 2
Release | 3
Rejected | 4

## HplcDetectorType
[src: backend/MicroLIMS.Domain/Enums/HplcDetectorType.cs:L4]

Member | Value
--- | ---
UV | 0
PDA | 1
FLD | 2
RI | 3
ELSD | 4
Other | 5
Fid | 6
Tcd | 7
Ecd | 8
Ms | 9

## HplcResultMode
[src: backend/MicroLIMS.Domain/Enums/HplcResultMode.cs:L4]

Member | Value
--- | ---
Assay | 0
ResidualSolvents | 1

## HplcRunSampleStatus
[src: backend/MicroLIMS.Domain/Enums/HplcRunSampleStatus.cs:L6]

Member | Value
--- | ---
Assigned | 0
Removed | 1

## HplcRunStatus
[src: backend/MicroLIMS.Domain/Enums/HplcRunStatus.cs:L5]

Member | Value
--- | ---
Open | 0
Completed | 1
Abandoned | 2

## HplcSstStatus
[src: backend/MicroLIMS.Domain/Enums/HplcSstStatus.cs:L5]

Member | Value
--- | ---
Pending | 0
Passed | 1
Failed | 2

## IcpCalibrationStatus
[src: backend/MicroLIMS.Domain/Enums/IcpCalibrationStatus.cs:L3]

Member | Value
--- | ---
Pending | 0
Confirmed | 1

## IcpRunSampleStatus
[src: backend/MicroLIMS.Domain/Enums/IcpRunSampleStatus.cs:L3]

Member | Value
--- | ---
Assigned | 0
Removed | 1

## IcpRunStatus
[src: backend/MicroLIMS.Domain/Enums/IcpRunStatus.cs:L3]

Member | Value
--- | ---
Open | 0
Completed | 1
Abandoned | 2

## IncidentStatus
[src: backend/MicroLIMS.Domain/Enums/IncidentStatus.cs:L4]

Member | Value
--- | ---
Open | 0
Resolved | 1

## ItemDocumentType
[src: backend/MicroLIMS.Domain/Enums/ItemDocumentType.cs:L4]

Member | Value
--- | ---
Sop | 0
VerificationReport | 1

## LimitType
[src: backend/MicroLIMS.Domain/Enums/LimitType.cs:L3]

Member | Value
--- | ---
Range | 0
NotMoreThan | 1
NotLessThan | 2
TargetWithTolerance | 3
CountTiered | 4
Qualitative | 5
PresenceAbsence | 6
MultiStage | 7
DissolutionQ | 8
DisintegrationTime | 9
WeightVariation | 10

## LocationType
[src: backend/MicroLIMS.Domain/Enums/LocationType.cs:L3]

Member | Value
--- | ---
Room | 0
MachinePart | 1
WaterSamplingPoint | 2

## MasterDataImportStatus
[src: backend/MicroLIMS.Domain/Enums/MasterDataImportStatus.cs:L4]

Member | Value
--- | ---
Loaded | 0
Verified | 1

## MaterialDocumentStatus
[src: backend/MicroLIMS.Domain/Enums/MaterialDocumentStatus.cs:L6]

Member | Value
--- | ---
Current | 0
Superseded | 1
Voided | 2

## MaterialDocumentType
[src: backend/MicroLIMS.Domain/Enums/MaterialDocumentType.cs:L6]

Member | Value
--- | ---
COA | 0
SupplierCertificate | 1
Specification | 2
SDS | 3
Other | 4

## MaterialType
[src: backend/MicroLIMS.Domain/Enums/MaterialType.cs:L10]

Member | Value
--- | ---
DehydratedMedia | 0
LyophilizedMicroorganism | 1
Supplement | 2
AntibioticDisc | 3
IdentificationKit | 4
IdentificationReagent | 5
Chemical | 6
Indicator | 7
ReferenceBuffer | 8
DisposableTool | 9
Other | 10
ReferenceStandard | 11
WorkingStandard | 12
PrimaryStandard | 13

## MediaEvaluationStatus
[src: backend/MicroLIMS.Domain/Enums/MediaEvaluationStatus.cs:L3]

Member | Value
--- | ---
Assigned | 0
InProgress | 1
Completed | 2

## MediaStatus
[src: backend/MicroLIMS.Domain/Enums/MediaStatus.cs:L4]

Member | Value
--- | ---
Prepared | 0
Active | 1
Expired | 2
QuarantineFailed | 3
Destroyed | 4
OutOfStock | 5

## OverallSampleStatus
[src: backend/MicroLIMS.Domain/Enums/OverallSampleStatus.cs:L6]

Member | Value
--- | ---
InProgress | 0
Approved | 1
Rejected | 2
RetestRequested | 3
Voided | 4
Cancelled | 5

## PeriodicReviewFindingStatus
[src: backend/MicroLIMS.Domain/Enums/PeriodicReviewFindingStatus.cs:L3]

Member | Value
--- | ---
Open | 1
Resolved | 2

## PeriodicReviewOutcome
[src: backend/MicroLIMS.Domain/Enums/PeriodicReviewOutcome.cs:L3]

Member | Value
--- | ---
RemainsValid | 1
RevisionRequired | 2
ObsolescenceRecommended | 3

## PeriodicReviewTaskStatus
[src: backend/MicroLIMS.Domain/Enums/PeriodicReviewTaskStatus.cs:L3]

Member | Value
--- | ---
Pending | 1
InProgress | 2
Completed | 3
Cancelled | 4

## PhenotypicTestType
[src: backend/MicroLIMS.Domain/Enums/PhenotypicTestType.cs:L6]

Member | Value
--- | ---
Gram | 0
Catalase | 1
Oxidase | 2
Coagulase | 3
Antibiogram | 4
IdentificationKit | 5

## ProductionStageRole
[src: backend/MicroLIMS.Domain/Enums/ProductionStageRole.cs:L10]

Member | Value
--- | ---
Other | 0
Bulk | 1
InProcess | 2
Finished | 3
Stability | 4

## ResultBasis
[src: backend/MicroLIMS.Domain/Enums/ResultBasis.cs:L3]

Member | Value
--- | ---
MgPerKg | 0
MgPerUnit | 1
PercentLabelClaim | 2
PercentAsIs | 3
PercentDriedBasis | 4
PercentAnhydrousBasis | 5
Ppm | 6

## ResultKind
[src: backend/MicroLIMS.Domain/Enums/ResultKind.cs:L3]

Member | Value
--- | ---
Quantitative | 0
Qualitative | 1

## ResultLevel
[src: backend/MicroLIMS.Domain/Enums/ResultLevel.cs:L3]

Member | Value
--- | ---
WithinLimit | 0
AlertLevel | 1
ActionLevel | 2
OutOfSpecification | 3
NotApplicable | 4
LimitsNotConfigured | 5

## ResultStatus
[src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L7]

Member | Value
--- | ---
WithinLimits | 1
AlertLimitExceeded | 2
ActionLimitExceeded | 3
OutOfSpecification | 4
LimitsNotConfigured | 5
RequiresReview | 6
Absent | 7
Detected | 8
PendingConfirmation | 9
NotDetected | 10
NextStageRequired | 11
Inconclusive | 12

## ResultType
[src: backend/MicroLIMS.Domain/Enums/ResultType.cs:L3]

Member | Value
--- | ---
Numeric | 0
GrowthNoGrowth | 1
Interpretive | 2

## ReviewFindingStatus
[src: backend/MicroLIMS.Domain/Enums/ReviewFindingStatus.cs:L3]

Member | Value
--- | ---
Open | 1
AuthorResponded | 2
ReviewerVerified | 3
Resolved | 4

## ReviewTaskStatus
[src: backend/MicroLIMS.Domain/Enums/ReviewTaskStatus.cs:L3]

Member | Value
--- | ---
Pending | 1
InProgress | 2
Completed | 3
ReturnedForCorrection | 4
Cancelled | 5

## ReviewWorkflowEventType
[src: backend/MicroLIMS.Domain/Enums/ReviewWorkflowEventType.cs:L6]

Member | Value
--- | ---
SubmittedForReview | 0
ReviewCompleted | 1
SubmittedForApproval | 2
ApprovalDecisionMade | 3
SampleVoided | 4
SampleCorrected | 5
SectionTestingClosed | 6
LaboratoryAdded | 7
StorageLocationChanged | 8

## RevisionChangeItemStatus
[src: backend/MicroLIMS.Domain/Enums/RevisionChangeItemStatus.cs:L4]

Member | Value
--- | ---
Draft | 0
Addressed | 1
Deferred | 2

## RevisionType
[src: backend/MicroLIMS.Domain/Enums/RevisionType.cs:L3]

Member | Value
--- | ---
Major | 1
Minor | 2
Editorial | 3
Administrative | 4

## RoleType
[src: backend/MicroLIMS.Domain/Enums/RoleType.cs:L3]

Member | Value
--- | ---
SystemAdministrator | 0
SectionHead | 1
Reviewer | 2
Analyst | 3

## SamplePreparationStatus
[src: backend/MicroLIMS.Domain/Enums/SamplePreparationStatus.cs:L6]

Member | Value
--- | ---
NeedsPreparation | 0
Ready | 1

## SampleStatus
[src: backend/MicroLIMS.Domain/Enums/SampleStatus.cs:L3]

Member | Value
--- | ---
Received | 0
InTesting | 1
UnderReview | 2
UnderApproval | 3
Approved | 4
Rejected | 5
RetestRequested | 6
Cancelled | 7
Voided | 8

## SamplingType
[src: backend/MicroLIMS.Domain/Enums/SamplingType.cs:L3]

Member | Value
--- | ---
Routine | 0
Investigation | 1
Revalidation | 2
Retest | 3

## SectionSignoffStatus
[src: backend/MicroLIMS.Domain/Enums/SectionSignoffStatus.cs:L5]

Member | Value
--- | ---
InTesting | 0
UnderReview | 1
UnderApproval | 2
Approved | 3
Rejected | 4
RetestRequested | 5
Cancelled | 6
Voided | 7

## SecurityEventOutcome
[src: backend/MicroLIMS.Domain/Enums/SecurityEventOutcome.cs:L7]

Member | Value
--- | ---
Success | 0
Failure | 1

## SolutionPreparationStatus
[src: backend/MicroLIMS.Domain/Enums/SolutionPreparationStatus.cs:L6]

Member | Value
--- | ---
InProgress | 0
Prepared | 1
Expired | 2
Discarded | 3
Cancelled | 4

## SolutionType
[src: backend/MicroLIMS.Domain/Enums/SolutionType.cs:L4]

Member | Value
--- | ---
MobilePhase | 0
Diluent | 1
Titrant | 2

## StepType
[src: backend/MicroLIMS.Domain/Enums/StepType.cs:L6]

Member | Value
--- | ---
PlateCount | 0
BrothEnrichment | 1
SelectiveBroth | 2
SelectivePlating | 3
ConfirmatoryPlating | 4
BiochemicalTest | 5

## StockStatus
[src: backend/MicroLIMS.Domain/Enums/StockStatus.cs:L6]

Member | Value
--- | ---
InStock | 0
Depleted | 1
Expired | 2

## TitrationType
[src: backend/MicroLIMS.Domain/Enums/TitrationType.cs:L4]

Member | Value
--- | ---
AcidBase | 0
Redox | 1
Complexometric | 2
Precipitation | 3
KarlFischer | 4

## TrainingAssignmentStatus
[src: backend/MicroLIMS.Domain/Enums/TrainingAssignmentStatus.cs:L3]

Member | Value
--- | ---
NotAssigned | 1
Assigned | 2
Reading | 3
Acknowledged | 4
KafPending | 5
CompletedPassed | 6
Failed | 7
Overdue | 8
SupersededIncomplete | 9
TrainedOnSupersededOnly | 10
Cancelled | 11

## WorkflowProfileStatus
[src: backend/MicroLIMS.Domain/Enums/WorkflowProfileStatus.cs:L4]

Member | Value
--- | ---
Draft | 0
Effective | 1

## WorkflowType
[src: backend/MicroLIMS.Domain/Enums/WorkflowType.cs:L3]

Member | Value
--- | ---
CountTest | 0
Observation | 1
HplcAssay | 2
ElementalAssay | 3
Measurement | 4
Gravimetric | 5
Qualitative | 6
Dissolution | 7
Disintegration | 8
WeightVariation | 9
HplcMultiAnalyte | 10
StandardComparison | 11
HplcMethodAssay | 12
Titration | 13
IcpMethodAssay | 14

## WorkingStandardQualificationStatus
[src: backend/MicroLIMS.Domain/Enums/WorkingStandardQualificationStatus.cs:L4]

Member | Value
--- | ---
Draft | 0
Assayed | 1
Reviewed | 2
Approved | 3
Rejected | 4

## Other domain enums (names only, not matched by the filter above)

AnalystDecision [src: backend/MicroLIMS.Domain/Enums/AnalystDecision.cs:L3]; AnalyteView [src: backend/MicroLIMS.Domain/Enums/AnalyteView.cs:L3]; ApprovalDecision [src: backend/MicroLIMS.Domain/Enums/ApprovalDecision.cs:L4]; AssignmentRole [src: backend/MicroLIMS.Domain/Enums/AssignmentRole.cs:L3]; AuditActionCategory [src: backend/MicroLIMS.Domain/Enums/AuditActionCategory.cs:L3]; CarrierGas [src: backend/MicroLIMS.Domain/Enums/CarrierGas.cs:L4]; CdsSoftware [src: backend/MicroLIMS.Domain/Enums/CdsSoftware.cs:L4]; ChallengeRole [src: backend/MicroLIMS.Domain/Enums/ChallengeRole.cs:L5]; DiscussionCategory [src: backend/MicroLIMS.Domain/Enums/DiscussionCategory.cs:L4]; DocumentApprovalDecision [src: backend/MicroLIMS.Domain/Enums/DocumentApprovalDecision.cs:L3]; DocumentConfidentiality [src: backend/MicroLIMS.Domain/Enums/DocumentConfidentiality.cs:L3]; DocumentEscalationLevel [src: backend/MicroLIMS.Domain/Enums/DocumentEscalationLevel.cs:L3]; DosageForm [src: backend/MicroLIMS.Domain/Enums/DosageForm.cs:L3]; ElutionMode [src: backend/MicroLIMS.Domain/Enums/ElutionMode.cs:L4]; EquipmentDocumentAccessAction [src: backend/MicroLIMS.Domain/Enums/EquipmentDocumentAccessAction.cs:L6]; ErrorSeverity [src: backend/MicroLIMS.Domain/Enums/ErrorSeverity.cs:L6]; ErrorSource [src: backend/MicroLIMS.Domain/Enums/ErrorSource.cs:L4]; ExpectedPresence [src: backend/MicroLIMS.Domain/Enums/ExpectedPresence.cs:L3]; FileRole [src: backend/MicroLIMS.Domain/Enums/FileRole.cs:L3]; GrowthObservation [src: backend/MicroLIMS.Domain/Enums/GrowthObservation.cs:L6]; HplcEvidenceContext [src: backend/MicroLIMS.Domain/Enums/HplcEvidenceContext.cs:L4]; HplcEvidenceKind [src: backend/MicroLIMS.Domain/Enums/HplcEvidenceKind.cs:L4]; HplcTechnique [src: backend/MicroLIMS.Domain/Enums/HplcTechnique.cs:L4]; IcpAmountUnit [src: backend/MicroLIMS.Domain/Enums/IcpAmountUnit.cs:L3]; IcpEvidenceContext [src: backend/MicroLIMS.Domain/Enums/IcpEvidenceContext.cs:L3]; IcpEvidenceKind [src: backend/MicroLIMS.Domain/Enums/IcpEvidenceKind.cs:L3]; IcpMethodMode [src: backend/MicroLIMS.Domain/Enums/IcpMethodMode.cs:L4]; MaterialDocumentAccessAction [src: backend/MicroLIMS.Domain/Enums/MaterialDocumentAccessAction.cs:L7]; MaterialMasterCategory [src: backend/MicroLIMS.Domain/Enums/MaterialMasterCategory.cs:L3]; MaterialUnit [src: backend/MicroLIMS.Domain/Enums/MaterialUnit.cs:L6]; MeasurementEvaluationBasis [src: backend/MicroLIMS.Domain/Enums/MeasurementEvaluationBasis.cs:L3]; MediaClass [src: backend/MicroLIMS.Domain/Enums/MediaClass.cs:L4]; MediaStorageForm [src: backend/MicroLIMS.Domain/Enums/MediaStorageForm.cs:L3]; PhyschemArea [src: backend/MicroLIMS.Domain/Enums/PhyschemArea.cs:L4]; ReadingKind [src: backend/MicroLIMS.Domain/Enums/ReadingKind.cs:L3]; RecordOrigin [src: backend/MicroLIMS.Domain/Enums/RecordOrigin.cs:L3]; ReviewDecision [src: backend/MicroLIMS.Domain/Enums/ReviewDecision.cs:L3]; SampleCategory [src: backend/MicroLIMS.Domain/Enums/SampleCategory.cs:L3]; ShelfLifeUnit [src: backend/MicroLIMS.Domain/Enums/ShelfLifeUnit.cs:L4]; SignatureMeaning [src: backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs:L3]; SolutionComponentUnit [src: backend/MicroLIMS.Domain/Enums/SolutionComponentUnit.cs:L4]; StandardizationMode [src: backend/MicroLIMS.Domain/Enums/StandardizationMode.cs:L4]; TitrantStrengthUnit [src: backend/MicroLIMS.Domain/Enums/TitrantStrengthUnit.cs:L4]; TitrationCalculation [src: backend/MicroLIMS.Domain/Enums/TitrationCalculation.cs:L4]; TitrationEndpoint [src: backend/MicroLIMS.Domain/Enums/TitrationEndpoint.cs:L3]; TitrationMode [src: backend/MicroLIMS.Domain/Enums/TitrationMode.cs:L3]; ToleranceMode [src: backend/MicroLIMS.Domain/Enums/ToleranceMode.cs:L3]; WorkflowStep [src: backend/MicroLIMS.Domain/Enums/WorkflowStep.cs:L7]; WorkingStandardDocumentKind [src: backend/MicroLIMS.Domain/Enums/WorkingStandardDocumentKind.cs:L3]; WorkingStandardQualificationKind [src: backend/MicroLIMS.Domain/Enums/WorkingStandardQualificationKind.cs:L3]

