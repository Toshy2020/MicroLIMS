import { apiClient } from "../../../../services/apiClient";
import type {
  BiologicalWasteDestruction,
  BiologicalWasteEligible,
  RecordBiologicalWastePayload
} from "../types/biologicalWasteTypes";

export const BiologicalWasteService = {
  getAll: (): Promise<BiologicalWasteDestruction[]> =>
    apiClient.get("/biological-waste-destructions").then((r) => r.data.data),
  getById: (id: number): Promise<BiologicalWasteDestruction> =>
    apiClient.get(`/biological-waste-destructions/${id}`).then((r) => r.data.data),
  getEligible: (): Promise<BiologicalWasteEligible> =>
    apiClient.get("/biological-waste-destructions/eligible").then((r) => r.data.data),
  record: (payload: RecordBiologicalWastePayload): Promise<BiologicalWasteDestruction> =>
    apiClient.post("/biological-waste-destructions", payload).then((r) => r.data.data)
};
