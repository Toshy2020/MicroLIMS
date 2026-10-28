import { apiClient } from "../../../services/apiClient";

export interface AccessReviewPermission { code: string; hidden: boolean }

export interface AccessReviewMembership {
  departmentId: number;
  departmentName: string;
  sectionId: number | null;
  sectionName: string | null;
  physchemArea: string | null;
}

export interface AccessReviewStatusChange {
  timestamp: string;
  action: string;
  byUserId: number | null;
  reason: string | null;
  detail: string | null;
}

export interface AccessReviewUserEntry {
  userId: number;
  username: string;
  fullName: string;
  isActive: boolean;
  deactivatedAt: string | null;
  roleId: number;
  roleName: string;
  roleType: string;
  permissions: AccessReviewPermission[];
  sections: AccessReviewMembership[];
  lastSuccessfulLogin: string | null;
  neverLoggedIn: boolean;
  dormant: boolean;
  daysSinceLastActivity: number | null;
  statusHistory: AccessReviewStatusChange[];
  sodConflicts: string[];
}

export interface AccessReviewSummary {
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  dormantUsers: number;
  neverLoggedInUsers: number;
  sodConflictUsers: number;
}

export interface AccessReviewSnapshot {
  reportVersion: number;
  generatedAt: string;
  periodLabel: string;
  dormantDays: number;
  statusHistoryMonths: number;
  summary: AccessReviewSummary;
  users: AccessReviewUserEntry[];
}

export type AccessReviewStatus = "Draft" | "Signed";
export type FindingAction = "NoChange" | "ChangeRequested";

export const FINDING_KINDS = ["SodConflict", "Dormant", "NeverLoggedIn", "RoleReview", "PermissionReview", "Other"] as const;

export interface AccessReviewListItem {
  id: number;
  generatedAt: string;
  generatedByUserId: number;
  generatedByName: string | null;
  periodLabel: string;
  status: AccessReviewStatus;
  reviewerUserId: number | null;
  reviewerName: string | null;
  signedAt: string | null;
  findingCount: number;
  summary: AccessReviewSummary;
}

export interface AccessReviewFinding {
  id: number;
  reviewId: number;
  userId: number;
  username: string | null;
  kind: string;
  detail: string;
  action: FindingAction;
  note: string | null;
  recordedByUserId: number;
  recordedAt: string;
}

export interface AccessReviewDetail {
  id: number;
  generatedAt: string;
  generatedByUserId: number;
  generatedByName: string | null;
  periodLabel: string;
  status: AccessReviewStatus;
  reviewerUserId: number | null;
  reviewerName: string | null;
  signedAt: string | null;
  electronicSignatureId: number | null;
  conclusion: string | null;
  snapshotSha256: string;
  snapshotIntact: boolean;
  snapshot: AccessReviewSnapshot;
  findings: AccessReviewFinding[];
}

export interface SaveFindingRequest {
  userId: number;
  kind: string;
  detail: string;
  action: FindingAction;
  note?: string;
}

export const AccessReviewService = {
  async list(): Promise<AccessReviewListItem[]> {
    return (await apiClient.get("/admin/access-reviews")).data.data;
  },
  async get(id: number): Promise<AccessReviewDetail> {
    return (await apiClient.get(`/admin/access-reviews/${id}`)).data.data;
  },
  async generate(periodLabel: string, reason: string): Promise<AccessReviewDetail> {
    return (await apiClient.post("/admin/access-reviews", { periodLabel, reason })).data.data;
  },
  async addFinding(id: number, request: SaveFindingRequest): Promise<AccessReviewFinding> {
    return (await apiClient.post(`/admin/access-reviews/${id}/findings`, request)).data.data;
  },
  async updateFinding(id: number, findingId: number, request: SaveFindingRequest): Promise<AccessReviewFinding> {
    return (await apiClient.put(`/admin/access-reviews/${id}/findings/${findingId}`, request)).data.data;
  },
  async sign(id: number, password: string, conclusion: string): Promise<AccessReviewDetail> {
    return (await apiClient.post(`/admin/access-reviews/${id}/sign`, { password, conclusion })).data.data;
  },
  async exportCsv(id: number): Promise<void> {
    const res = await apiClient.get(`/admin/access-reviews/${id}/export`, { responseType: "blob" });
    const url = window.URL.createObjectURL(new Blob([res.data], { type: "text/csv" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `access-review-${id}.csv`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  }
};
