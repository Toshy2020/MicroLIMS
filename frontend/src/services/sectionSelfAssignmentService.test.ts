import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "./apiClient";
import {
  sectionSelfAssignmentService,
  type SectionSelfAssignmentDto
} from "./sectionSelfAssignmentService";

vi.mock("./apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    put: vi.fn()
  }
}));

describe("sectionSelfAssignmentService", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetches /sections/self-assignment with optional AbortSignal", async () => {
    const controller = new AbortController();
    const payload: SectionSelfAssignmentDto[] = [
      {
        sectionId: 1,
        sectionName: "Microbiology",
        enabled: true,
        changedAt: "2026-11-15T08:30:00Z",
        changedByName: "Sam Head",
        version: 4
      }
    ];
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: payload } });

    const res = await sectionSelfAssignmentService.getAll(controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/sections/self-assignment", {
      signal: controller.signal
    });
    expect(res).toEqual(payload);
  });

  it("puts { enabled, reason, version } to /sections/{sectionId}/self-assignment", async () => {
    const updated: SectionSelfAssignmentDto = {
      sectionId: 3,
      sectionName: "Finished Product",
      enabled: false,
      changedAt: "2026-11-15T09:00:00Z",
      changedByName: "Sam Head",
      version: 5
    };
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: updated } });

    const res = await sectionSelfAssignmentService.update(3, {
      enabled: false,
      reason: "Balance analyst workload during shutdown",
      version: 4
    });

    expect(apiClient.put).toHaveBeenCalledWith("/sections/3/self-assignment", {
      enabled: false,
      reason: "Balance analyst workload during shutdown",
      version: 4
    });
    expect(res).toEqual(updated);
  });
});
