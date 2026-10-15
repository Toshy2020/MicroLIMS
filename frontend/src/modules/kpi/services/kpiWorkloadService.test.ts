import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup } from "@testing-library/react";
import { apiClient } from "../../../services/apiClient";
import { kpiWorkloadService } from "./kpiWorkloadService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
  ifMatch: (version: number) => ({ headers: { "If-Match": `"${version}"` } }),
}));

const ok = (data: unknown) => ({ data: { success: true, data } }) as never;

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("kpiWorkloadService", () => {
  it("omits analystId and blank filters, so the server applies the caller's own scope", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok({ rows: [] }));

    await kpiWorkloadService.workload({ from: "2026-10-05", to: "2026-10-11", analystId: null, category: "", location: undefined });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workload", { params: { from: "2026-10-05", to: "2026-10-11" } });
  });

  it("sends analystId only when set", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok({ rows: [] }));

    await kpiWorkloadService.workload({ from: "2026-10-05", to: "2026-10-11", analystId: 7 });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workload", { params: { from: "2026-10-05", to: "2026-10-11", analystId: 7 } });
  });

  it("pages the tests behind the figures", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok({ items: [], total: 0 }));

    await kpiWorkloadService.workloadTests({ from: "2026-10-05", to: "2026-10-11", analystId: 7, page: 2, pageSize: 25 });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workload/tests", {
      params: { from: "2026-10-05", to: "2026-10-11", analystId: 7, page: 2, pageSize: 25 },
    });
  });

  it("sends the settings version as If-Match and the exact request field names", async () => {
    vi.mocked(apiClient.put).mockResolvedValue(ok({ sectionId: 3 }));
    const req = { hoursPerAnalystDay: 8, workingWeekdays: 31, loadHorizonDays: 3, reason: "Site works 8 h days" };

    await kpiWorkloadService.updateSettings(3, 4, req);

    expect(apiClient.put).toHaveBeenCalledWith("/kpi/settings/3", req, { headers: { "If-Match": '"4"' } });
    expect(Object.keys(req).sort()).toEqual(["hoursPerAnalystDay", "loadHorizonDays", "reason", "workingWeekdays"]);
  });

  it("lists availability without undefined params, and writes with POST and PUT", async () => {
    vi.mocked(apiClient.get).mockResolvedValue(ok([]));
    vi.mocked(apiClient.post).mockResolvedValue(ok({ id: 1 }));
    vi.mocked(apiClient.put).mockResolvedValue(ok({ id: 1 }));
    const req = { userId: 7, fromDate: "2026-10-14", toDate: "2026-10-16", fractionAvailable: 0.5, reason: "Training course" };

    await kpiWorkloadService.listAvailability({ from: "2026-10-01", userId: undefined });
    await kpiWorkloadService.createAvailability(req);
    await kpiWorkloadService.updateAvailability(1, req);

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/availability", { params: { from: "2026-10-01" } });
    expect(apiClient.post).toHaveBeenCalledWith("/kpi/availability", req);
    expect(apiClient.put).toHaveBeenCalledWith("/kpi/availability/1", req);
  });
});
