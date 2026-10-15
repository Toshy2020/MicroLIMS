import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { workload, workloadRow } from "../kpiFixtures";
import { useKpiWorkload } from "./useKpiWorkload";

vi.mock("../services/kpiWorkloadService", () => ({
  kpiWorkloadService: { workload: vi.fn(), workloadTests: vi.fn() }
}));

const base = { from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null };

beforeEach(() => {
  vi.mocked(kpiWorkloadService.workload).mockReset();
});

describe("useKpiWorkload", () => {
  it("loads the workload without an analyst filter by default", async () => {
    vi.mocked(kpiWorkloadService.workload).mockResolvedValue(workload([workloadRow()]));
    const { result } = renderHook(() => useKpiWorkload(base));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data?.rows).toHaveLength(1);
    expect(kpiWorkloadService.workload).toHaveBeenCalledWith({
      from: "2026-09-12", to: "2026-10-11", category: undefined, location: undefined, testCode: undefined, analystId: undefined
    });
  });

  it("passes the analyst when one is given", async () => {
    vi.mocked(kpiWorkloadService.workload).mockResolvedValue(workload([workloadRow()]));
    const { result } = renderHook(() => useKpiWorkload(base, 7));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(kpiWorkloadService.workload).toHaveBeenCalledWith(expect.objectContaining({ analystId: 7 }));
  });

  it("reports a refusal as its own error", async () => {
    vi.mocked(kpiWorkloadService.workload).mockRejectedValue({ response: { data: { message: "Forbidden" } } });
    const { result } = renderHook(() => useKpiWorkload(base));
    await waitFor(() => expect(result.current.error).toBe("Forbidden"));
    expect(result.current.data).toBeNull();
  });
});
