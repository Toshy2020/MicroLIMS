import { apiClient } from "../../../services/apiClient";
import { PagedResult } from "./ReceiveService";

// Mirrors backend SampleTrackingLabDto - a lab absent from a row's `labs`
// means "not requested" (see SampleTrackingService.ToRow).
export interface TrackingLab {
  sectionId: number;
  sectionName: string;
  stage: string;
}

// Mirrors backend SampleTrackingRowDto (GET /samples/tracking).
export interface TrackingRow {
  sampleId: number;
  referenceNumber: string;
  product: string;
  batchNumber: string | null;
  receivedAt: string;
  overallStatus: string;
  labs: TrackingLab[];
}

export interface TrackingFilter {
  labSectionId?: number;
  overall?: string;
  from?: string;
  to?: string;
  itemId?: number;
  page?: number;
  pageSize?: number;
}

// Cross-laboratory tracking board (Samples.TrackAll) - one row per sample,
// with every lab's own stage.
export const TrackingService = {
  async getTracking(filter: TrackingFilter = {}): Promise<PagedResult<TrackingRow>> {
    const params: Record<string, string | number> = {};
    if (filter.labSectionId != null) params.labSectionId = filter.labSectionId;
    if (filter.overall) params.overall = filter.overall;
    if (filter.from) params.from = filter.from;
    if (filter.to) params.to = filter.to;
    if (filter.itemId != null) params.itemId = filter.itemId;
    if (filter.page != null) params.page = filter.page;
    if (filter.pageSize != null) params.pageSize = filter.pageSize;

    const res = await apiClient.get("/samples/tracking", { params });
    return res.data.data;
  }
};
