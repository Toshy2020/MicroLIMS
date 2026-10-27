import { apiClient } from "../../../services/apiClient";

export interface SecurityPolicySetting {
  key: string;
  label: string;
  value: string;
  source: string;
}

export interface SecurityPolicyGroup {
  key: string;
  title: string;
  settings: SecurityPolicySetting[];
}

export interface SecurityPolicy {
  isReadOnly: boolean;
  groups: SecurityPolicyGroup[];
}

export const SecurityPolicyService = {
  async get(): Promise<SecurityPolicy> {
    return (await apiClient.get("/admin/security-policy")).data.data;
  }
};
