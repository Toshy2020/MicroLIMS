import { apiClient } from "./apiClient";
import type { OosInvestigationKind } from "./oosInvestigationService";

// The groupings GET api/oos-investigations/trends accepts (OosTrendService.GetTrendsAsync). Any other
// value is refused by the server with Oos.TrendGroupByInvalid.
export type OosTrendGroupBy = "product" | "test" | "analyst" | "instrument" | "cause" | "conclusion";

export const OOS_TREND_GROUP_BY: readonly OosTrendGroupBy[] = [
  "product",
  "test",
  "analyst",
  "instrument",
  "cause",
  "conclusion"
];

// OosTrendGroupDto (OosInvestigationReadDtos.cs).
export interface OosTrendGroupDto {
  key: string;
  label: string;
  totalCount: number;
  openCount: number;
  concludedCount: number;
  confirmedCount: number;
  invalidatedCount: number;
  excursionClosedCount: number;
  medianWorkingDaysToConclude: number | null;
}

// OosTrendReportDto (OosInvestigationReadDtos.cs). groupBy is the normalised value the server used.
export interface OosTrendReportDto {
  groupBy: string;
  fromUtc: string | null;
  toUtc: string | null;
  groups: OosTrendGroupDto[];
}

export interface OosTrendFilter {
  groupBy: OosTrendGroupBy;
  from?: string;
  to?: string;
  kind?: OosInvestigationKind;
  sectionId?: number;
}

export const oosTrendsService = {
  async getTrends(filter: OosTrendFilter, signal?: AbortSignal): Promise<OosTrendReportDto> {
    return (await apiClient.get("/oos-investigations/trends", { params: filter, signal })).data.data;
  }
};
