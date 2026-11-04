import { apiClient } from "../../../services/apiClient";

export interface IntegrationAgent {
  id: number;
  code: string;
  name: string;
  equipmentId: number;
  equipmentCode: string | null;
  keyPrefix: string;
  // "Active", "Never seen" or "Revoked".
  status: string;
  lastSeenAt: string | null;
  createdAt: string;
  revokedAt: string | null;
  revokeReason: string | null;
}

export interface SmtpStatus {
  isConfigured: boolean;
  host: string;
  port: number;
  fromAddress: string;
  enableSsl: boolean;
  authenticationConfigured: boolean;
  // Always masked by the server.
  password: string;
  lastSendAtUtc: string | null;
  lastSendOutcome: string | null;
  lastSendError: string | null;
}

export interface StorageStatus {
  provider: string;
  location: string;
  freeBytes: number | null;
  totalBytes: number | null;
}

export interface ProbeStep { step: string; succeeded: boolean; elapsedMs: number; error: string | null }
export interface StorageProbeResult { succeeded: boolean; steps: ProbeStep[]; warning: string | null }

export interface DeliveryOutcome { recipient: string; outcome: "Sent" | "Skipped" | "Failed"; error: string | null }
export interface TestSendResult { recipientCount: number; sent: number; outcomes: DeliveryOutcome[] }

export interface AgentKeyResponse { agent: IntegrationAgent; plainKey: string }

export const IntegrationsService = {
  async agents(): Promise<IntegrationAgent[]> {
    return (await apiClient.get("/admin/integrations/instrument-agents")).data.data;
  },
  async smtp(): Promise<SmtpStatus> {
    return (await apiClient.get("/admin/integrations/smtp")).data.data;
  },
  async sendTestEmail(to: string, reason: string): Promise<TestSendResult> {
    return (await apiClient.post("/admin/integrations/smtp/test", { to, reason })).data.data;
  },
  async storage(): Promise<StorageStatus> {
    return (await apiClient.get("/admin/integrations/storage")).data.data;
  },
  async probeStorage(reason: string): Promise<StorageProbeResult> {
    return (await apiClient.post("/admin/integrations/storage/probe", { reason })).data.data;
  },
  async sendTestAlert(reason: string): Promise<TestSendResult> {
    return (await apiClient.post("/admin/alerts/test", { reason })).data.data;
  },
  // The existing agent lifecycle endpoints (MasterData.Manage): not re-implemented here.
  async rotateAgentKey(id: number): Promise<AgentKeyResponse> {
    return (await apiClient.post(`/instrument-agents/${id}/rotate-key`)).data.data;
  },
  async revokeAgent(id: number, reason: string): Promise<void> {
    await apiClient.post(`/instrument-agents/${id}/revoke`, { reason });
  }
};
