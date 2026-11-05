export type SolutionType = "MobilePhase" | "Diluent" | "Titrant" | "Reagent" | "Neutralizer";

export type SolutionComponentKind = "Material" | "PreparedSolution" | "Text";

export type ApprovalGateStatus = "PendingReview" | "Approved" | "Rejected";

export const SOLUTION_TYPE_LABELS: Record<SolutionType, string> = {
  MobilePhase: "Mobile Phase",
  Diluent: "Diluent",
  Reagent: "Reagent",
  Neutralizer: "Neutralizer",
  Titrant: "Titrant"
};

export const solutionTypeLabel = (type: SolutionType): string => SOLUTION_TYPE_LABELS[type] ?? type;

export type SolutionPreparationStatus =
  | "InProgress"
  | "Prepared"
  | "Expired"
  | "Discarded"
  | "Cancelled";

export type SolutionComponentUnit =
  | "Gram"
  | "Milligram"
  | "Milliliter"
  | "Liter"
  | "PercentVolume"
  | "Parts";

export interface SolutionPreparationComponentResponse {
  id: number;
  order: number;
  kind?: SolutionComponentKind;
  materialMasterEntryId: number | null;
  entryCode: string;
  entryName: string;
  recipeQuantity: number | null;
  recipeUnit: SolutionComponentUnit | null;
  materialId: number | null;
  lotBatchNumber: string | null;
  lotUnit: string | null;
  quantityUsed: number | null;
  sourceSolutionMasterId?: number | null;
  text?: string | null;
  sourcePreparationId?: number | null;
  sourcePreparationCode?: string | null;
}

export interface SolutionPreparationStatusHistoryResponse {
  fromStatus: SolutionPreparationStatus | null;
  toStatus: SolutionPreparationStatus;
  changedByUserId: number | null;
  changedByUserName: string | null;
  changedAt: string;
  reason: string | null;
}

export interface SolutionPreparationListItem {
  id: number;
  code: string | null;
  type: SolutionType;
  solutionMasterName: string;
  hplcMethodAbbreviation: string | null;
  effectiveStatus: SolutionPreparationStatus;
  preparedAt: string | null;
  expiresAt: string | null;
  preparedByUserName: string | null;
  approvalStatus?: ApprovalGateStatus | null;
}

export interface SolutionPreparationResponse {
  id: number;
  version: number;
  sectionId: number;
  sectionName?: string | null;
  code: string | null;
  solutionMasterId: number;
  solutionMasterName: string;
  type: SolutionType;
  hplcMethodId: number | null;
  hplcMethodAbbreviation: string | null;
  status: SolutionPreparationStatus;
  effectiveStatus: SolutionPreparationStatus;
  startedByUserId: number;
  startedByUserName: string | null;
  startedAt: string;
  preparedByUserId: number | null;
  preparedByUserName: string | null;
  preparedAt: string | null;
  expiresAt: string | null;
  finalVolumeMl: number | null;
  measuredPh: number | null;
  autoclaveEquipmentId?: number | null;
  autoclaveEquipmentName?: string | null;
  autoclaveProgram?: string | null;
  loadType?: string | null;
  autoclaveTemperature?: number | null;
  autoclaveCycleTime?: number | null;
  autoclaveCycleNumber?: number | null;
  // Null when the recipe needs no release.
  approvalStatus?: ApprovalGateStatus | null;
  releasedByUserId?: number | null;
  releasedByUserName?: string | null;
  releasedAt?: string | null;
  releaseComment?: string | null;
  recipeSnapshotJson: string;
  components: SolutionPreparationComponentResponse[];
  statusHistory: SolutionPreparationStatusHistoryResponse[];
  currentFactor?: CurrentFactorDto | null;
}

export type StandardizationMode = "PrimaryStandard" | "AgainstVolumetricSolution";

export type CurrentFactorState =
  | "NotStandardized"
  | "Valid"
  | "Due"
  | "BeforeEachUse";

export interface CurrentFactorDto {
  factor?: number | null;
  standardizedAt?: string | null;
  validUntil?: string | null;
  state: CurrentFactorState;
}

export interface TitrantStandardizationReplicateResponse {
  id: number;
  replicateNo: number;
  standardMaterialId: number | null;
  standardLotBatchNumber: string | null;
  standardWeightMg: number | null;
  standardPurityPercent: number | null;
  referencePreparationId: number | null;
  referencePreparationCode: string | null;
  referenceVolumeMl: number | null;
  referenceFactor: number | null;
  titrantVolumeMl: number;
  blankMl: number | null;
  factor: number;
}

export interface TitrantStandardizationResponse {
  id: number;
  solutionPreparationId: number;
  mode: StandardizationMode;
  meanFactor: number;
  rsdPercent: number | null;
  passed: boolean;
  failureReasons: string | null;
  standardizedByUserId: number;
  standardizedByUserName: string | null;
  standardizedAt: string;
  validUntil: string | null;
  temperatureC?: number | null;
  replicates: TitrantStandardizationReplicateResponse[];
}

export interface StandardizationReplicateInput {
  standardMaterialId?: number | null;
  standardWeightMg?: number | null;
  referencePreparationId?: number | null;
  referenceVolumeMl?: number | null;
  titrantVolumeMl: number;
  blankMl?: number | null;
}

export interface StandardizeRequest {
  replicates: StandardizationReplicateInput[];
  temperatureC?: number | null;
  password: string;
  comment?: string | null;
}

export interface LotOption {
  materialId: number;
  batchNumber: string;
  expiryDate: string | null;
  quantityRemaining: number;
  unit: string;
  usable: boolean;
  reason: string | null;
  purity?: number | null;
}

export interface StartPreparationRequest {
  solutionMasterId: number;
  hplcMethodId?: number | null;
}

export interface PreparationComponentInput {
  componentId: number;
  materialId?: number | null;
  quantityUsed?: number | null;
  sourcePreparationId?: number | null;
}

export interface SavePreparationRequest {
  components: PreparationComponentInput[];
  finalVolumeMl?: number | null;
  measuredPh?: number | null;
  autoclaveEquipmentId?: number | null;
  autoclaveProgram?: string | null;
  loadType?: string | null;
  autoclaveTemperature?: number | null;
  autoclaveCycleTime?: number | null;
  autoclaveCycleNumber?: number | null;
}

export interface AutoclaveRecordInput {
  autoclaveEquipmentId: number | null;
  autoclaveProgram: string | null;
  loadType: string | null;
  autoclaveTemperature: number | null;
  autoclaveCycleTime: number | null;
  autoclaveCycleNumber: number | null;
}

export interface SourcePreparationOption {
  preparationId: number;
  code: string | null;
  preparedAt: string | null;
  expiresAt: string | null;
  approvalStatus: ApprovalGateStatus | null;
  usable: boolean;
  reason: string | null;
}

export interface ReleasePreparationRequest {
  approved: boolean;
  comment?: string | null;
  password: string;
}

export interface CompletePreparationRequest {
  password: string;
  comment?: string | null;
}

export interface RecipeSnapshot {
  id: number;
  name: string;
  type: SolutionType;
  shelfLifeValue: number;
  shelfLifeUnit: string;
  storageCondition: string;
  finalVolumeMl: number;
  phTarget?: number | null;
  phTolerance?: number | null;
  phAdjustingEntryId?: number | null;
  phAdjustingEntryCode?: string | null;
  instructions: string;
  sterilizationRequired?: boolean;
  releaseRequired?: boolean;
  components?: Array<{
    id: number;
    order: number;
    kind?: SolutionComponentKind;
    materialMasterEntryId?: number | null;
    entryCode: string;
    entryName: string;
    quantity?: number | null;
    unit?: SolutionComponentUnit | null;
    sourceSolutionMasterId?: number | null;
    sourceSolutionName?: string | null;
    text?: string | null;
  }>;
  nominalStrength?: number | null;
  strengthUnit?: "Normal" | "Molar" | null;
  standardizationMode?: StandardizationMode | null;
  standardEntryId?: number | null;
  standardEntryCode?: string | null;
  equivalenceMgPerMl?: number | null;
  referenceSolutionId?: number | null;
  referenceSolutionName?: string | null;
  blankRequired?: boolean;
  replicateCount?: number | null;
  factorMin?: number | null;
  factorMax?: number | null;
  maxRsdPercent?: number | null;
  validityDays?: number | null;
}
