import { apiClient } from "../../../services/apiClient";

export interface SystemSetting {
  key: string;
  category: string;
  description: string;
  valueType: "Integer" | "Boolean" | "String";
  value: string;
  defaultValue: string;
  // "Database" (changed here), "Configuration" (legacy appsettings key) or "Default".
  source: string;
  unit: string | null;
  min: number | null;
  max: number | null;
  allowedValues: string[] | null;
  isGxpRelevant: boolean;
  requiresSignature: boolean;
  version: number;
  lastChangedAt: string | null;
  lastChangedByUserId: number | null;
  lastChangedByName: string | null;
}

// Shown for information only: secrets, startup-only values and tables edited elsewhere.
export interface SystemSettingFixedItem {
  key: string;
  label: string;
  value: string;
  source: string;
  note: string;
  editLocation: string | null;
}

export interface SystemSettingsOverview {
  settings: SystemSetting[];
  fixedItems: SystemSettingFixedItem[];
}

export interface SystemSettingVersion {
  version: number;
  value: string;
  previousValue: string;
  changedByUserId: number;
  changedByName: string | null;
  changedAt: string;
  reason: string;
  electronicSignatureId: number | null;
}

export const SettingsService = {
  async getAll(): Promise<SystemSettingsOverview> {
    return (await apiClient.get("/admin/settings")).data.data;
  },
  async update(key: string, value: string, reason: string, password?: string): Promise<SystemSetting> {
    return (await apiClient.put(`/admin/settings/${encodeURIComponent(key)}`, { value, reason, password })).data.data;
  },
  async history(key: string): Promise<SystemSettingVersion[]> {
    return (await apiClient.get(`/admin/settings/${encodeURIComponent(key)}/history`)).data.data;
  }
};
