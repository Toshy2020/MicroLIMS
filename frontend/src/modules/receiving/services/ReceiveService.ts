import { apiClient } from "../../../services/apiClient";
import {
  ItemBasedReceiveRequest,
  WaterReceiveRequest,
  EMReceiveRequest,
  AfterCleaningReceiveRequest,
  SampleRecord
} from "../types/receivingTypes";

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

  async correctSample(sampleId: number, batchNumber?: string, controlNumber?: string): Promise<SampleRecord> {
    const res = await apiClient.put(`/samples/${sampleId}/correct`, { batchNumber, controlNumber });
    return res.data.data;
  },

  async assignAnalyst(sampleId: number, analystUserId: number | null, reason?: string): Promise<SampleRecord> {
    const res = await apiClient.put(`/samples/${sampleId}/assign-analyst`, { analystUserId, reason });
    return res.data.data;
  }
};
