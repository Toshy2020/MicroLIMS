import { apiClient, withChangeControl, withChangeReason } from "../../../../services/apiClient";
import type { FieldError } from "../../../../services/apiTypes";

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
export type RelatedSubstanceRow = "Named" | "UnspecifiedEach" | "Total" | "TotalOthers";

export type ResultBasis =
  | "MgPerKg"
  | "MgPerUnit"
  | "PercentLabelClaim"
  | "PercentAsIs"
  | "PercentDriedBasis"
  | "PercentAnhydrousBasis"
  | "Ppm";


// The production stage a specification row applies to; null/absent =
// every stage. A sample at a stage with rows of its own uses those rows,
// any other sample the every-stage rows - decided on the server.
export type ProductionStageRole = "Bulk" | "InProcess" | "Finished" | "Stability";

export const PRODUCTION_STAGE_ROLE_OPTIONS: { value: ProductionStageRole; label: string }[] = [
  { value: "Bulk", label: "Bulk" },
  { value: "InProcess", label: "In-Process" },
  { value: "Finished", label: "Finished Product" },
  { value: "Stability", label: "Stability" }
];

export function productionStageRoleLabel(role: ProductionStageRole | string | null | undefined): string {
  return PRODUCTION_STAGE_ROLE_OPTIONS.find((o) => o.value === role)?.label ?? "All stages";
}

// Suggested grades for raw-material specifications; any text up to 20 characters is accepted.
export const GRADE_SUGGESTIONS = ["BP", "USP", "In-house"];

export interface SpecificationStageDto {
  id?: number;
  stageNumber: number;
  stageLabel: string;
  acceptanceCriteriaText: string;
}

export interface SpecificationDto {
  id?: number;
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
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
  resultBasis?: ResultBasis | string | null;
  labelClaim?: number | string | null;
  labelClaimUnit?: string | null;
  dosageForm?: DosageForm | string | null;
  // Ownership (design.md §6) - the row's owner is its TestCode's Test
  // Master section, not a column of its own. canEdit is false for a row
  // whose test belongs to a lab other than the caller's; the row still
  // renders (read-only) with sectionName as a chip.
  canEdit?: boolean;
  sectionName?: string;
  hplcMethodAnalyteId?: number | null;
  hplcMethodImpurityId?: number | null;
  relatedSubstanceRow?: RelatedSubstanceRow | null;
  icpTestElementId?: number | null;
  productionStageRole?: ProductionStageRole | string | null;
  grade?: string | null;
  isRetired?: boolean;
  retiredAt?: string | null;
  retiredByUserId?: number | null;
}

export interface SpecificationDeleteOutcome {
  outcome: "retired" | "deleted" | string;
  id: number;
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
  resultBasis?: ResultBasis | null;
  labelClaim?: number | null;
  labelClaimUnit?: string | null;
  dosageForm?: DosageForm | null;
  hplcMethodAnalyteId?: number | null;
  hplcMethodImpurityId?: number | null;
  relatedSubstanceRow?: RelatedSubstanceRow | null;
  icpTestElementId?: number | null;
  productionStageRole?: ProductionStageRole | null;
  grade?: string | null;
}

export type UpdateSpecificationPayload = Omit<CreateSpecificationPayload, "itemId">;

export interface CopySpecificationsRequest {
  sourceItemId: number;
  targetItemId: number;
  specificationIds: number[];
}

export type CopySpecificationsPreviewStatus = "Ok" | "AlreadyPresent" | "Invalid";

export interface CopySpecificationsPreviewRow {
  sourceSpecificationId: number;
  testCode: string;
  parameterName: string;
  status: CopySpecificationsPreviewStatus;
  message: string | null;
}

export const SpecificationService = {
  getForItem: (itemId: number): Promise<SpecificationDto[]> =>
    apiClient.get(`/masterdata/specifications?itemId=${itemId}`).then((r) => r.data.data),
  // The rows that apply to this test order's sample (its production
  // stage's rows, else the every-stage rows) - for result entry.
  getForTestOrder: (testOrderId: number): Promise<SpecificationDto[]> =>
    apiClient.get(`/masterdata/specifications/for-test-order/${testOrderId}`).then((r) => r.data.data),
  // The trailing changeControl is the approved change control number for a laboratory whose
  // change-control gate is on; it travels in the X-Change-Control header of this one request.
  create: (payload: CreateSpecificationPayload, changeControl?: string): Promise<SpecificationDto> =>
    (changeControl
      ? apiClient.post("/masterdata/specifications", payload, withChangeControl(undefined, changeControl))
      : apiClient.post("/masterdata/specifications", payload)
    ).then((r) => r.data.data),
  createSpecificationsBatch: (payload: CreateSpecificationPayload[]): Promise<SpecificationDto[]> =>
    apiClient.post("/masterdata/specifications/batch", payload).then((r) => r.data.data),
  update: (id: number, payload: UpdateSpecificationPayload, version?: number, reason?: string, changeControl?: string): Promise<SpecificationDto> =>
    apiClient.put(`/masterdata/specifications/${id}`, payload, withChangeControl(withChangeReason(version, reason), changeControl)).then((r) => r.data.data),
  remove: async (id: number, reason?: string, changeControl?: string): Promise<SpecificationDeleteOutcome> => {
    const res = await apiClient.delete(`/masterdata/specifications/${id}`, withChangeControl(withChangeReason(undefined, reason), changeControl));
    return (res?.data?.data ?? { outcome: "deleted", id }) as SpecificationDeleteOutcome;
  },
  delete: async (id: number, reason?: string, changeControl?: string): Promise<SpecificationDeleteOutcome> => {
    const res = await apiClient.delete(`/masterdata/specifications/${id}`, withChangeControl(withChangeReason(undefined, reason), changeControl));
    return (res?.data?.data ?? { outcome: "deleted", id }) as SpecificationDeleteOutcome;
  },
  reinstate: (id: number, reason: string, changeControl?: string): Promise<SpecificationDto> =>
    apiClient.post(`/masterdata/specifications/${id}/reinstate`, undefined, withChangeControl(withChangeReason(undefined, reason), changeControl)).then((r) => r.data.data),
  validate: (
    payload: (CreateSpecificationPayload | (UpdateSpecificationPayload & { itemId?: number })) & { id?: number },
    id?: number
  ): Promise<FieldError[]> =>
    apiClient
      .post(id != null ? `/masterdata/specifications/validate?id=${id}` : "/masterdata/specifications/validate", payload)
      .then((r) => r.data.data ?? []),
  previewCopy: (request: CopySpecificationsRequest): Promise<CopySpecificationsPreviewRow[]> =>
    apiClient.post("/masterdata/specifications/copy/preview", request).then((r) => r.data.data ?? []),
  copySpecifications: (request: CopySpecificationsRequest): Promise<number> =>
    apiClient.post("/masterdata/specifications/copy", request).then((r) => r.data.data)
};

