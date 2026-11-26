import { apiClient } from "./apiClient";

export type QualificationBasis =
  | "ObservedPractice"
  | "ProficiencySample"
  | "PreviousQualification"
  | "Other";

export type MatrixCellStatus =
  | "NotQualified"
  | "Qualified"
  | "ExpiringSoon"
  | "Expired"
  | "Withdrawn";

/** The cell status used when the matrix has no record for an analyst and a test. */
export const DEFAULT_MATRIX_CELL_STATUS: MatrixCellStatus = "NotQualified";

export interface SignatureManifestDto {
  userFullName: string;
  username: string;
  roleName: string;
  meaningOfSignature: string;
  signedAt: string;
  comment: string | null;
}

export interface QualificationAttachmentDto {
  id: number;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
}

export interface AnalystQualificationDto {
  id: number;
  version: number;
  userId: number;
  testDefinitionId: number;
  basis: QualificationBasis;
  evidence: string;
  evidenceSampleId: number | null;
  qualifiedOn: string;
  expiresOn: string | null;
  signature: SignatureManifestDto;
  withdrawnOn: string | null;
  withdrawnReason: string | null;
  withdrawalSignature: SignatureManifestDto | null;
  attachments: QualificationAttachmentDto[];
}

export interface QualifyPayload {
  userId: number;
  testDefinitionId: number;
  basis: QualificationBasis;
  evidence: string;
  evidenceSampleId?: number | null;
  qualifiedOn: string;
  password: string;
  attachments?: File[];
}

export interface WithdrawPayload {
  reason: string;
  password: string;
}

export interface CompetencySettingDto {
  sectionId: number;
  gateEnabled: boolean;
  reminderDaysBeforeExpiry: number | null;
  changedAt: string | null;
  changedByName: string | null;
  version: number;
}

export interface CompetencyReasonDto {
  code: string;
  text: string;
  params: Record<string, string>;
}

export interface ImpactOrderDto {
  testOrderId: number;
  testCode: string;
  sampleReference: string;
  reasons: CompetencyReasonDto[];
}

export interface AnalystImpactDto {
  analystUserId: number;
  analystName: string;
  orders: ImpactOrderDto[];
}

export interface GateImpactDto {
  sectionId: number;
  analysts: AnalystImpactDto[];
  totalRefusedOrders: number;
}

export interface GateChangePayload {
  enabled: boolean;
  reason: string;
  password: string;
  expectedVersion: number | null;
}

export interface ReminderDaysPayload {
  reminderDaysBeforeExpiry: number | null;
  expectedVersion: number | null;
}

export interface GrantOverridePayload {
  analystUserId: number;
  reason: string;
  password: string;
}

export interface MatrixCellDto {
  userId: number;
  testDefinitionId: number;
  status: MatrixCellStatus;
  expiresOn: string | null;
  qualificationId: number | null;
  version: number | null;
}

export interface MatrixSopDto {
  documentCode: string;
  revisionNumber: string;
}

export interface MatrixTestDto {
  testDefinitionId: number;
  code: string;
  displayName: string;
  sop: MatrixSopDto | null;
}

export interface MatrixRowDto {
  userId: number;
  analystName: string;
  cells: MatrixCellDto[];
}

export interface MatrixFilters {
  analystSearch?: string;
  testSearch?: string;
  status?: MatrixCellStatus;
  page?: number;
  pageSize?: number;
}

export interface CompetencyMatrixDto {
  sectionId: number;
  page: number;
  pageSize: number;
  totalCount: number;
  tests: MatrixTestDto[];
  analysts: MatrixRowDto[];
}

export const competencyService = {
  qualify: async (payload: QualifyPayload, signal?: AbortSignal): Promise<AnalystQualificationDto> => {
    const form = new FormData();
    form.append("UserId", String(payload.userId));
    form.append("TestDefinitionId", String(payload.testDefinitionId));
    form.append("Basis", payload.basis);
    form.append("Evidence", payload.evidence);
    if (payload.evidenceSampleId != null) {
      form.append("EvidenceSampleId", String(payload.evidenceSampleId));
    }
    form.append("QualifiedOn", payload.qualifiedOn);
    form.append("Password", payload.password);
    for (const file of payload.attachments ?? []) {
      form.append("Attachments", file);
    }
    const res = await apiClient.post("/competency/qualifications", form, { signal });
    return res.data.data;
  },

  withdraw: async (
    id: number,
    payload: WithdrawPayload,
    version?: number | null,
    signal?: AbortSignal
  ): Promise<AnalystQualificationDto> => {
    const res = await apiClient.post(`/competency/qualifications/${id}/withdraw`, payload, {
      ...(version != null ? { headers: { "If-Match": `"${version}"` } } : {}),
      signal
    });
    return res.data.data;
  },

  getSettings: async (sectionId: number, signal?: AbortSignal): Promise<CompetencySettingDto> => {
    const res = await apiClient.get(`/competency/settings/${sectionId}`, { signal });
    return res.data.data;
  },

  getImpact: async (sectionId: number, signal?: AbortSignal): Promise<GateImpactDto> => {
    const res = await apiClient.get(`/competency/settings/${sectionId}/impact`, { signal });
    return res.data.data;
  },

  setGate: async (
    sectionId: number,
    payload: GateChangePayload,
    signal?: AbortSignal
  ): Promise<CompetencySettingDto> => {
    const res = await apiClient.post(`/competency/settings/${sectionId}/gate`, payload, { signal });
    return res.data.data;
  },

  setReminderDays: async (
    sectionId: number,
    payload: ReminderDaysPayload,
    signal?: AbortSignal
  ): Promise<CompetencySettingDto> => {
    const res = await apiClient.put(`/competency/settings/${sectionId}/reminder-days`, payload, { signal });
    return res.data.data;
  },

  getMatrix: async (
    sectionId: number,
    filters?: MatrixFilters,
    signal?: AbortSignal
  ): Promise<CompetencyMatrixDto> => {
    const res = await apiClient.get(`/competency/matrix/${sectionId}`, {
      params: filters,
      signal
    });
    return res.data.data;
  },

  exportMatrix: async (
    sectionId: number,
    filters?: MatrixFilters,
    signal?: AbortSignal
  ): Promise<Blob> => {
    const res = await apiClient.get(`/competency/matrix/${sectionId}/export`, {
      params: filters,
      responseType: "blob",
      signal
    });
    return res.data;
  },

  getMyMatrix: async (signal?: AbortSignal): Promise<CompetencyMatrixDto> => {
    const res = await apiClient.get("/competency/matrix/my", { signal });
    return res.data.data;
  },

  grantOverride: async (
    testOrderId: number,
    payload: GrantOverridePayload,
    signal?: AbortSignal
  ): Promise<number> => {
    const res = await apiClient.post(`/competency/test-orders/${testOrderId}/override`, payload, { signal });
    return res.data.data;
  }
};
