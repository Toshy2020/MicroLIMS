import { apiClient } from "./apiClient";
import type { StatusTone } from "../theme/statusTokens";

// One live figure on a laboratory section card. The rule behind it, and the
// tone it is drawn in, are decided on the server (SectionCountersService).
export interface SectionCounter {
  label: string;
  value: number;
  tone: StatusTone;
}

export interface SectionCardCounters {
  card: string;
  counters: SectionCounter[];
}

export const sectionCountersService = {
  async get(lab: "MICRO" | "FP"): Promise<SectionCardCounters[]> {
    const res = await apiClient.get("/section-counters", { params: { lab } });
    return res.data.data;
  }
};
