import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { kpiDashboardService } from "./kpiDashboardService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn() },
}));

afterEach(() => vi.clearAllMocks());

describe("kpiDashboardService", () => {
  it("sends only the filters that are set and unwraps the envelope", async () => {
    const dashboard = { from: "2026-10-01T00:00:00Z", kpis: [] };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: dashboard } } as never);

    const result = await kpiDashboardService.dashboard({
      from: "2026-10-01", to: "2026-10-31", category: "Water", location: "", testCode: undefined,
    });

    expect(result).toBe(dashboard);
    expect(apiClient.get).toHaveBeenCalledWith("/kpi/dashboard", {
      params: { from: "2026-10-01", to: "2026-10-31", category: "Water" },
    });
  });

  it("sends analystId only when it is set", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: {} } } as never);

    await kpiDashboardService.dashboard({ from: "2026-10-01", to: "2026-10-31", analystId: 7 });
    expect(vi.mocked(apiClient.get).mock.calls[0][1]).toEqual({ params: { from: "2026-10-01", to: "2026-10-31", analystId: 7 } });

    await kpiDashboardService.dashboard({ from: "2026-10-01", to: "2026-10-31" });
    expect(vi.mocked(apiClient.get).mock.calls[1][1]).toEqual({ params: { from: "2026-10-01", to: "2026-10-31" } });
  });

  it("requests the records of one KPI with paging", async () => {
    const page = { key: "right-first-time", page: 2, pageSize: 25, total: 60, items: [] };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: page } } as never);

    const result = await kpiDashboardService.records("right-first-time", {
      from: "2026-10-01", to: "2026-10-31", testCode: "TAMC", page: 2, pageSize: 25,
    });

    expect(result).toBe(page);
    expect(apiClient.get).toHaveBeenCalledWith("/kpi/right-first-time/records", {
      params: { from: "2026-10-01", to: "2026-10-31", testCode: "TAMC", page: 2, pageSize: 25 },
    });
  });

  it("requests the definitions for the chosen period", async () => {
    const defs = { kpis: [], profilesInForce: [] };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: defs } } as never);

    expect(await kpiDashboardService.definitions("2026-10-01", "2026-10-31")).toBe(defs);
    expect(apiClient.get).toHaveBeenCalledWith("/kpi/definitions", { params: { from: "2026-10-01", to: "2026-10-31" } });
  });
});
