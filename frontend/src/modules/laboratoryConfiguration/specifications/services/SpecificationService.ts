import { apiClient } from "../../../../services/apiClient";

export type LimitType =
  | "Range"
  | "NotMoreThan"
  | "NotLessThan"
  | "TargetWithTolerance"
  | "CountTiered"
  | "Qualitative"
  | "PresenceAbsence"
  | "MultiStage"
  | "DissolutionQ"
  | "DisintegrationTime"
  | "WeightVariation";

export type DosageForm = "Tablet" | "HardCapsule" | "SoftCapsule";

export type ToleranceMode = "Absolute" | "Percent";

export type ExpectedPresence = "Presence" | "Absence";

export type ResultBasis = "MgPerKg" | "MgPerUnit" | "PercentLabelClaim";

export type SampleMatrix = "Solid" | "Liquid";

export interface SpecificationStageDto {
  id?: number;
  stageNumber: number;
  stageLabel: string;
  acceptanceCriteriaText: string;
}

export interface SpecificationDto {
  id?: number;
  itemId?: number;
  testCode: string;
  parameterName?: string | null;
  displayOrder?: number;
  limitType?: LimitType | string | null;
  referenceStandard?: string | null;
  lowerLimit?: number | string | null;
  upperLimit?: number | string | null;
  lowerInclusive?: boolean | null;
  upperInclusive?: boolean | null;
  target?: number | string | null;
  tolerance?: number | string | null;
  toleranceMode?: ToleranceMode | string | null;
  expectedResultText?: string | null;
  expectedState?: ExpectedPresence | string | null;
  sampleQuantity?: number | string | null;
  sampleQuantityUnit?: string | null;
  alertLimit?: string | null;
  actionLimit?: string | null;
  specLimit?: string | null;
  unit?: string | null;
  dilutionFactor?: number | null;
  stages?: SpecificationStageDto[];
  testAnalyteId?: number | null;
  resultBasis?: ResultBasis | string | null;
  sampleMatrix?: SampleMatrix | string | null;
  labelClaim?: number | string | null;
  labelClaimUnit?: string | null;
  conversionFactor?: number | string | null;
  dosageForm?: DosageForm | string | null;
  // Ownership (design.md §6) - the row's owner is its TestCode's Test
  // Master section, not a column of its own. canEdit is false for a row
  // whose test belongs to a lab other than the caller's; the row still
  // renders (read-only) with sectionName as a chip.
  canEdit?: boolean;
  sectionName?: string;
}

export interface CreateSpecificationPayload {
  itemId: number;
  testCode: string;
  parameterName?: string | null;
  displayOrder?: number;
  limitType?: LimitType;
  referenceStandard?: string | null;
  lowerLimit?: number | null;
  upperLimit?: number | null;
  lowerInclusive?: boolean;
  upperInclusive?: boolean;
  target?: number | null;
  tolerance?: number | null;
  toleranceMode?: ToleranceMode | null;
  expectedResultText?: string | null;
  expectedState?: ExpectedPresence | null;
  sampleQuantity?: number | null;
  sampleQuantityUnit?: string | null;
  alertLimit?: string | null;
  actionLimit?: string | null;
  specLimit?: string | null;
  unit?: string | null;
  dilutionFactor?: number | null;
  stages?: Array<{ stageNumber: number; stageLabel: string; acceptanceCriteriaText: string }>;
  testAnalyteId?: number | null;
  resultBasis?: ResultBasis | null;
  sampleMatrix?: SampleMatrix | null;
  labelClaim?: number | null;
  labelClaimUnit?: string | null;
  conversionFactor?: number | null;
  dosageForm?: DosageForm | null;
}

export type UpdateSpecificationPayload = Omit<CreateSpecificationPayload, "itemId">;

export const SpecificationService = {
  getForItem: (itemId: number): Promise<SpecificationDto[]> =>
    apiClient.get(`/masterdata/specifications?itemId=${itemId}`).then((r) => r.data.data),
  create: (payload: CreateSpecificationPayload): Promise<SpecificationDto> =>
    apiClient.post("/masterdata/specifications", payload).then((r) => r.data.data),
  update: (id: number, payload: UpdateSpecificationPayload): Promise<SpecificationDto> =>
    apiClient.put(`/masterdata/specifications/${id}`, payload).then((r) => r.data.data),
  remove: (id: number) => apiClient.delete(`/masterdata/specifications/${id}`),
  delete: (id: number) => apiClient.delete(`/masterdata/specifications/${id}`)
};

