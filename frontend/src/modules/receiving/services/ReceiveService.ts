import { apiClient } from "../../../services/apiClient";
import {
  ItemBasedReceiveRequest,
  WaterReceiveRequest,
  EMReceiveRequest,
  AfterCleaningReceiveRequest,
  SampleRecord
} from "../types/receivingTypes";

export interface TestingWorkspaceFilter {
  search?: string;
  category?: string;
  status?: string;
  sampleStatus?: string;
  testStatus?: string;
  analystId?: number | null;
  urgency?: string;
  fromDate?: string;
  toDate?: string;
  workloadFilter?: string | null;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface WorkspaceTileCounts {
  needsPreparation: number;
  readyToRead: number;
  awaitingReview: number;
  overdue: number;
  mine: number;
  unassigned: number;
}

// One receiving endpoint per category, matching the distinct backend receive shapes.
export const ReceiveService = {
  receiveItemBased: (r: ItemBasedReceiveRequest) =>
    apiClient.post("/samples", r).then((res) => res.data.data),

  receiveWater: (r: WaterReceiveRequest) =>
    apiClient.post("/water/receive", r).then((res) => res.data.data),

  receiveEM: (r: EMReceiveRequest) =>
    apiClient.post("/em/receive", r).then((res) => res.data.data),

  receiveAfterCleaning: (r: AfterCleaningReceiveRequest) =>
    apiClient.post("/aftercleaning/receive", r).then((res) => res.data.data),

  async getRecords(): Promise<SampleRecord[]> {
    const res = await apiClient.get("/testorders");
    return res.data.data;
  },

  async getRecordsPaged(filter: TestingWorkspaceFilter = {}): Promise<PagedResult<SampleRecord>> {
    const params: Record<string, string | number> = {};
    if (filter.search?.trim()) params.search = filter.search.trim();
    if (filter.category && filter.category !== "ALL") params.category = filter.category;
    if (filter.status && filter.status !== "ALL") params.status = filter.status;
    if (filter.sampleStatus && filter.sampleStatus !== "ALL") params.sampleStatus = filter.sampleStatus;
    if (filter.testStatus && filter.testStatus !== "ALL") params.testStatus = filter.testStatus;
    if (filter.analystId != null) params.analystId = filter.analystId;
    if (filter.urgency) params.urgency = filter.urgency;
    if (filter.fromDate) params.fromDate = filter.fromDate;
    if (filter.toDate) params.toDate = filter.toDate;
    if (filter.workloadFilter) params.workloadFilter = filter.workloadFilter;
    if (filter.page != null) params.page = filter.page;
    if (filter.pageSize != null) params.pageSize = filter.pageSize;

    const res = await apiClient.get("/testorders/page", { params });
    return res.data.data;
  },

  async getWorkloadCounts(): Promise<WorkspaceTileCounts> {
    const res = await apiClient.get("/testorders/counts");
    return res.data.data;
  },

  async getSample(sampleId: number): Promise<SampleRecord | null> {
    const res = await apiClient.get(`/testorders/${sampleId}`);
    return res.data.data;
  },

  async correctSample(sampleId: number, batchNumber?: string, controlNumber?: string): Promise<SampleRecord> {
    const res = await apiClient.put(`/samples/${sampleId}/correct`, { batchNumber, controlNumber });
    return res.data.data;
  },

  async assignAnalyst(sampleId: number, analystUserId: number | null, reason?: string): Promise<SampleRecord> {
    const res = await apiClient.put(`/samples/${sampleId}/assign-analyst`, { analystUserId, reason });
    return res.data.data;
  },

  async voidSample(sampleId: number, reason: string): Promise<SampleRecord> {
    const res = await apiClient.post(`/samples/${sampleId}/void`, { reason });
    return res.data.data;
  }
};
