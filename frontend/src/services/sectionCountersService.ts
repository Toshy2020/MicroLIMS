import { apiClient } from "./apiClient";
import type { StatusTone } from "../theme/statusTokens";

// One live figure on a section card. The rule behind it, and the
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

// A laboratory's section page, or Document Control's.
export type CounterSource = "MICRO" | "FP" | "document-control";

export const sectionCountersService = {
  async get(source: CounterSource): Promise<SectionCardCounters[]> {
    const res = source === "document-control"
      ? await apiClient.get("/section-counters/document-control")
      : await apiClient.get("/section-counters", { params: { lab: source } });
    return res.data.data;
  }
};
