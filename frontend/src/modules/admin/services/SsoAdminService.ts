import { apiClient } from "../../../services/apiClient";

export type SsoMode = "Off" | "OpenIdConnect" | "ActiveDirectory";

export interface SsoUnlinkedUser {
  id: number;
  username: string;
  fullName: string;
  role: string;
}

export interface SsoAdminOverview {
  mode: SsoMode;
  configuration: Record<string, string | boolean | number | null>;
  lastTestPassedAt: string | null;
  blockers: string[];
  unlinkedUsers: SsoUnlinkedUser[];
}

export interface SsoTestResult {
  passed: boolean;
  testedAt: string;
}

export interface DirectoryLinkCsvRow {
  username: string;
  companyUsername: string;
}

export type DirectoryLinkRowStatus = "Ok" | "UnknownUser" | "Duplicate" | "AlreadyLinked" | "Invalid";

export interface DirectoryLinkPreviewRow {
  row: number;
  username: string;
  status: DirectoryLinkRowStatus;
  message: string;
}

export interface DirectoryLinkPreviewResult {
  rows: DirectoryLinkPreviewRow[];
  applied?: number;
}

export const SsoAdminService = {
  async getOverview(signal?: AbortSignal): Promise<SsoAdminOverview> {
    return (await apiClient.get("/admin/sso", { signal })).data.data as SsoAdminOverview;
  },

  async testConnection(): Promise<SsoTestResult> {
    return (await apiClient.post("/admin/sso/test")).data.data as SsoTestResult;
  },

  async previewLinks(rows: DirectoryLinkCsvRow[]): Promise<DirectoryLinkPreviewResult> {
    return (await apiClient.post("/admin/sso/links/preview", { rows })).data.data as DirectoryLinkPreviewResult;
  },

  async importLinks(rows: DirectoryLinkCsvRow[]): Promise<DirectoryLinkPreviewResult> {
    return (await apiClient.post("/admin/sso/links/import", { rows })).data.data as DirectoryLinkPreviewResult;
  }
};
