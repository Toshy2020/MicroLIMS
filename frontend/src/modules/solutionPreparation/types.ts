export type SolutionType = "MobilePhase" | "Diluent" | "Titrant";

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
  materialMasterEntryId: number;
  entryCode: string;
  entryName: string;
  recipeQuantity: number;
  recipeUnit: SolutionComponentUnit;
  materialId: number | null;
  lotBatchNumber: string | null;
  lotUnit: string | null;
  quantityUsed: number | null;
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
}

export interface StartPreparationRequest {
  solutionMasterId: number;
  hplcMethodId?: number | null;
}

export interface PreparationComponentInput {
  componentId: number;
  materialId?: number | null;
  quantityUsed?: number | null;
}

export interface SavePreparationRequest {
  components: PreparationComponentInput[];
  finalVolumeMl?: number | null;
  measuredPh?: number | null;
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
  components?: Array<{
    id: number;
    order: number;
    materialMasterEntryId: number;
    entryCode: string;
    entryName: string;
    quantity: number;
    unit: SolutionComponentUnit;
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
