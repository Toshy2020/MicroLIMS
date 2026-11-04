import { apiClient } from "./apiClient";

export interface SystemStatus {
  maintenance: boolean;
  message: string;
  banner: string;
  severity: "info" | "warning";
}

export const NO_STATUS: SystemStatus = { maintenance: false, message: "", banner: "", severity: "info" };

export const SystemStatusService = {
  // Public endpoint: works before sign-in and returns only these four values.
  async get(): Promise<SystemStatus> {
    const data = (await apiClient.get("/system/status")).data.data as Partial<SystemStatus>;
    return {
      maintenance: Boolean(data.maintenance),
      message: data.message ?? "",
      banner: data.banner ?? "",
      severity: data.severity === "warning" ? "warning" : "info"
    };
  }
};
