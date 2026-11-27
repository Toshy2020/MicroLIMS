import { apiClient, ifMatch } from "./apiClient";
import type { AuditLogItem } from "../modules/auditSearch/types/auditTypes";

// Serialized as strings via ApiJsonOptions (JsonStringEnumConverter).
// Backend AuditReviewScopeKind { All = 0, EntityName = 2, Record = 3 }.
export type AuditReviewScopeKind = "All" | "EntityName" | "Record";
export type AuditTrailReviewStatus = "InProgress" | "Signed";
export type AuditTrailReviewOutcome = "NoFindings" | "FindingsRecorded";
export type AuditFindingStatus = "Open" | "Closed";

export interface StartAuditTrailReviewRequest {
  title: string;
  periodFromUtc: string;
  periodToUtc: string;
  scopeKind: AuditReviewScopeKind;
  entityName?: string | null;
  recordId?: string | null;
}

export interface AddAuditTrailReviewFindingRequest {
  auditLogId: number;
  note: string;
}

export interface SignAuditTrailReviewRequest {
  password: string;
  conclusion: string;
}

export interface CloseAuditTrailReviewFindingRequest {
  comment: string;
}

export interface AuditTrailReviewFindingDto {
  id: number;
  reviewId: number;
  auditLogId: number;
  note: string;
  status: AuditFindingStatus;
  closedComment: string | null;
  closedByUserId: number | null;
  closedByName: string | null;
  closedAtUtc: string | null;
  recordedByUserId: number;
  recordedByName: string | null;
  recordedAtUtc: string;
  // The status as a number (0 Open, 1 Closed): the token a deviation raised from this finding must send.
  version: number;
}

export interface AuditTrailReviewListItemDto {
  id: number;
  title: string;
  periodFromUtc: string;
  periodToUtc: string;
  scopeKind: AuditReviewScopeKind;
  scopeEntityName: string | null;
  scopeRecordId: string | null;
  cutoffAuditLogId: number;
  entryCount: number;
  newerEntriesCount: number;
  findingCount: number;
  openFindingCount: number;
  startedByUserId: number;
  startedByName: string | null;
  startedAtUtc: string;
  status: AuditTrailReviewStatus;
  outcome: AuditTrailReviewOutcome | null;
  signedAtUtc: string | null;
  signerFullName: string | null;
  signatureMeaningText: string | null;
  version: number;
}

export interface AuditTrailReviewDto {
  id: number;
  title: string;
  periodFromUtc: string;
  periodToUtc: string;
  scopeKind: AuditReviewScopeKind;
  scopeEntityName: string | null;
  scopeRecordId: string | null;
  cutoffAuditLogId: number;
  entryCount: number;
  newerEntriesCount: number;
  startedByUserId: number;
  startedByName: string | null;
  startedAtUtc: string;
  status: AuditTrailReviewStatus;
  outcome: AuditTrailReviewOutcome | null;
  conclusion: string | null;
  signedAtUtc: string | null;
  electronicSignatureId: number | null;
  signerFullName: string | null;
  signerUsername: string | null;
  signatureMeaningText: string | null;
  version: number;
  findings: AuditTrailReviewFindingDto[];
}

export interface PagedAuditResult<T> {
  items: T[];
  page: number;
  pageNumber?: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

async function downloadBlob(url: string, fileName: string): Promise<void> {
  const res = await apiClient.get(url, { responseType: "blob" });
  const objectUrl = window.URL.createObjectURL(res.data);
  const a = document.createElement("a");
  a.href = objectUrl;
  a.download = fileName;
  a.click();
  window.URL.revokeObjectURL(objectUrl);
}

export const auditTrailReviewService = {
  async list(page = 1, pageSize = 25, signal?: AbortSignal): Promise<AuditTrailReviewListItemDto[]> {
    const res = await apiClient.get("/audit-trail-reviews", { params: { page, pageSize }, signal });
    return res.data.data;
  },

  async get(id: number, signal?: AbortSignal): Promise<AuditTrailReviewDto> {
    const res = await apiClient.get(`/audit-trail-reviews/${id}`, { signal });
    return res.data.data;
  },

  async getEntries(
    id: number,
    page = 1,
    pageSize = 25,
    signal?: AbortSignal
  ): Promise<PagedAuditResult<AuditLogItem>> {
    const res = await apiClient.get(`/audit-trail-reviews/${id}/entries`, {
      params: { page, pageSize },
      signal
    });
    return res.data.data;
  },

  async start(request: StartAuditTrailReviewRequest): Promise<AuditTrailReviewDto> {
    const res = await apiClient.post("/audit-trail-reviews", request);
    return res.data.data;
  },

  async addFinding(
    id: number,
    request: AddAuditTrailReviewFindingRequest
  ): Promise<AuditTrailReviewFindingDto> {
    const res = await apiClient.post(`/audit-trail-reviews/${id}/findings`, request);
    return res.data.data;
  },

  async sign(
    id: number,
    request: SignAuditTrailReviewRequest,
    version: number
  ): Promise<AuditTrailReviewDto> {
    const res = await apiClient.post(`/audit-trail-reviews/${id}/sign`, request, ifMatch(version));
    return res.data.data;
  },

  async closeFinding(
    findingId: number,
    request: CloseAuditTrailReviewFindingRequest
  ): Promise<AuditTrailReviewFindingDto> {
    const res = await apiClient.post(`/audit-trail-reviews/findings/${findingId}/close`, request);
    return res.data.data;
  },

  exportPdf(id: number): Promise<void> {
    return downloadBlob(`/audit-trail-reviews/${id}/pdf`, `audit-trail-review-${id}.pdf`);
  }
};
