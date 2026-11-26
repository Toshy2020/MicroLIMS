import { apiClient } from "./apiClient";
import type {
  QualityActionKind,
  QualityRecordListItemDto,
  QualityRecordType
} from "./qualityService";

export interface QualityDashboardActionDto {
  recordId: number;
  recordNumber: string;
  recordType: QualityRecordType;
  recordTitle: string;
  actionId: number;
  kind: QualityActionKind;
  description: string;
  dueDate: string;
  isOverdue: boolean;
}

export interface QualityDashboardDto {
  waitingForMyDecision: QualityRecordListItemDto[];
  myOpenRecords: QualityRecordListItemDto[];
  myOpenActions: QualityDashboardActionDto[];
  overdueRecords: QualityRecordListItemDto[];
  overdueActions: QualityDashboardActionDto[];
  effectivenessChecksDue: QualityRecordListItemDto[];
  waitingForMyDecisionTotalCount: number;
  myOpenRecordsTotalCount: number;
  myOpenActionsTotalCount: number;
  overdueRecordsTotalCount: number;
  overdueActionsTotalCount: number;
  effectivenessChecksDueTotalCount: number;
  laboratoryOpenRecords: QualityRecordListItemDto[] | null;
  laboratoryOpenRecordsTotalCount: number;
  laboratoryOpenActions: QualityDashboardActionDto[] | null;
  laboratoryOpenActionsTotalCount: number;
}

export const qualityDashboardService = {
  async getDashboard(signal?: AbortSignal): Promise<QualityDashboardDto> {
    return (await apiClient.get("/quality/dashboard", { signal })).data.data;
  }
};
