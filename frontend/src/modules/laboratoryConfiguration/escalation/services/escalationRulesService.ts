import { apiClient } from "../../../../services/apiClient";

interface ApiEnvelope<T> {
  data: T;
}

export type EscalationTrigger =
  | "ReviewWaiting"
  | "ApprovalWaiting"
  | "ReturnedNotCorrected"
  | "IncubationNotRead"
  | "TestingNotStarted"
  | "DocumentTaskOverdue";

export type EscalationNotifyRole =
  | "SystemAdministrator"
  | "SectionHead"
  | "Reviewer"
  | "Analyst"
  | "QcManager"
  | "Qa";

export interface EscalationRuleDto {
  id: number;
  sectionId: number;
  sectionName: string;
  trigger: EscalationTrigger;
  thresholdWorkingHours: number;
  notifyRoleType: EscalationNotifyRole;
  isActive: boolean;
  createdByUserId: number;
  createdByName: string | null;
  createdAt: string;
  retiredAt: string | null;
  retiredByUserId: number | null;
  retiredByName: string | null;
  replacesRuleId: number | null;
}

export interface CreateEscalationRuleRequest {
  sectionId: number;
  trigger: EscalationTrigger;
  thresholdWorkingHours: number;
  notifyRoleType: EscalationNotifyRole;
}

export interface ReplaceEscalationRuleRequest {
  thresholdWorkingHours: number;
  notifyRoleType: EscalationNotifyRole;
}

export interface ListEscalationRulesParams {
  sectionId?: number;
  includeRetired?: boolean;
}

export const TRIGGER_OPTIONS: ReadonlyArray<{ value: EscalationTrigger; label: string }> = [
  { value: "ReviewWaiting", label: "Review waiting" },
  { value: "ApprovalWaiting", label: "Approval waiting" },
  { value: "ReturnedNotCorrected", label: "Returned test not corrected" },
  { value: "IncubationNotRead", label: "Incubation not read" },
  { value: "TestingNotStarted", label: "Testing not started" },
  { value: "DocumentTaskOverdue", label: "Document task overdue" }
];

export const NOTIFY_ROLE_OPTIONS: ReadonlyArray<EscalationNotifyRole> = [
  "SectionHead",
  "QcManager",
  "Reviewer",
  "Analyst",
  "Qa"
];

export function formatTriggerLabel(trigger: EscalationTrigger): string {
  return TRIGGER_OPTIONS.find((opt) => opt.value === trigger)?.label ?? trigger;
}

export const escalationRulesService = {
  async list(params: ListEscalationRulesParams = {}, signal?: AbortSignal): Promise<EscalationRuleDto[]> {
    const res = await apiClient.get<ApiEnvelope<EscalationRuleDto[]>>("/escalation-rules", {
      params,
      signal
    });
    return res.data.data;
  },

  async create(request: CreateEscalationRuleRequest): Promise<EscalationRuleDto> {
    const res = await apiClient.post<ApiEnvelope<EscalationRuleDto>>("/escalation-rules", request);
    return res.data.data;
  },

  async replace(id: number, request: ReplaceEscalationRuleRequest): Promise<EscalationRuleDto> {
    const res = await apiClient.post<ApiEnvelope<EscalationRuleDto>>(`/escalation-rules/${id}/replace`, request);
    return res.data.data;
  },

  async retire(id: number): Promise<void> {
    await apiClient.post(`/escalation-rules/${id}/retire`);
  }
};
