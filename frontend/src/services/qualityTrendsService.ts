import { apiClient } from "./apiClient";
import type { QualityLinkTargetType } from "./qualityService";

// The groupings GET api/quality/trends accepts (QualityQueryService.GetTrendsAsync). Any other value is
// refused with Quality.TrendGroupByInvalid.
export type QualityTrendGroupBy = "type" | "classification" | "cause" | "section" | "item" | "source";

export const QUALITY_TREND_GROUP_BY: readonly QualityTrendGroupBy[] = [
  "type",
  "classification",
  "cause",
  "section",
  "item",
  "source"
];

// The server clamps repeatWindowDays to 1..3650 and defaults it to 90.
export const QUALITY_REPEAT_WINDOW_DEFAULT_DAYS = 90;

// QualityTrendGroupDto (QualityQueryDtos.cs). The rates are fractions (0..1) or null when nothing qualifies.
export interface QualityTrendGroupDto {
  key: string;
  label: string;
  totalCount: number;
  openCount: number;
  closedCount: number;
  averageTimeToCloseDays: number | null;
  onTimeRate: number | null;
  capaEffectivenessRate: number | null;
  cancelledCount: number;
  rejectedCount: number;
}

// QualityRepeatCauseDto (QualityQueryDtos.cs).
export interface QualityRepeatCauseDto {
  causeCategoryId: number;
  causeCategoryName: string;
  targetType: QualityLinkTargetType;
  targetId: number;
  targetLabel: string;
  occurrenceCount: number;
  firstRaisedAtUtc: string;
  lastRaisedAtUtc: string;
  recordNumbers: string[];
}

// QualityTrendReportDto (QualityQueryDtos.cs).
export interface QualityTrendReportDto {
  groupBy: string;
  fromUtc: string | null;
  toUtc: string | null;
  repeatWindowDays: number;
  groups: QualityTrendGroupDto[];
  repeatCauses: QualityRepeatCauseDto[];
}

export interface QualityTrendFilter {
  groupBy: QualityTrendGroupBy;
  from?: string;
  to?: string;
  repeatWindowDays?: number;
}

export const qualityTrendsService = {
  async getTrends(filter: QualityTrendFilter, signal?: AbortSignal): Promise<QualityTrendReportDto> {
    return (await apiClient.get("/quality/trends", { params: filter, signal })).data.data;
  }
};
