import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { dashboard, entry } from "../kpiFixtures";
import type { KpiDashboard } from "../types/kpiTypes";
import { useKpiDashboard } from "./useKpiDashboard";

vi.mock("../services/kpiDashboardService", () => ({
  kpiDashboardService: { dashboard: vi.fn(), records: vi.fn(), definitions: vi.fn() }
}));

const base = { from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null };

function deferred<T>() {
  let resolve!: (v: T) => void;
  let reject!: (e: unknown) => void;
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}

beforeEach(() => {
  vi.mocked(kpiDashboardService.dashboard).mockReset();
});

describe("useKpiDashboard", () => {
  it("loads the dashboard for the filters and leaves unset filters out", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard([entry()]));
    const { result } = renderHook(() => useKpiDashboard({ ...base, testCode: "TAMC" }));
    expect(result.current.loading).toBe(true);
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data?.kpis[0].key).toBe("sample-tat");
    expect(result.current.error).toBeNull();
    expect(kpiDashboardService.dashboard).toHaveBeenCalledWith({
      from: "2026-09-12", to: "2026-10-11", category: undefined, location: undefined, testCode: "TAMC"
    });
  });

  it("ignores a slow response for an earlier filter", async () => {
    const first = deferred<KpiDashboard>();
    const second = deferred<KpiDashboard>();
    vi.mocked(kpiDashboardService.dashboard)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);

    const { result, rerender } = renderHook(({ f }) => useKpiDashboard(f), { initialProps: { f: base } });
    rerender({ f: { ...base, from: "2026-09-01" } });

    await act(async () => { second.resolve(dashboard([entry({ key: "newer" })])); });
    await waitFor(() => expect(result.current.data?.kpis[0].key).toBe("newer"));

    await act(async () => { first.resolve(dashboard([entry({ key: "older" })])); });
    expect(result.current.data?.kpis[0].key).toBe("newer");
    expect(result.current.loading).toBe(false);
  });

  it("ignores a late failure from an earlier filter", async () => {
    const first = deferred<KpiDashboard>();
    const second = deferred<KpiDashboard>();
    vi.mocked(kpiDashboardService.dashboard)
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);

    const { result, rerender } = renderHook(({ f }) => useKpiDashboard(f), { initialProps: { f: base } });
    rerender({ f: { ...base, to: "2026-10-10" } });

    await act(async () => { second.resolve(dashboard([entry({ key: "newer" })])); });
    await act(async () => { first.reject({ response: { data: { message: "old request failed" } } }); });

    expect(result.current.error).toBeNull();
    expect(result.current.data?.kpis[0].key).toBe("newer");
  });

  it("surfaces the server message and drops the stale figures on failure", async () => {
    vi.mocked(kpiDashboardService.dashboard)
      .mockResolvedValueOnce(dashboard([entry()]))
      .mockRejectedValueOnce({ response: { data: { message: "Not permitted" } } });
    const { result } = renderHook(() => useKpiDashboard(base));
    await waitFor(() => expect(result.current.data).not.toBeNull());

    await act(async () => { await result.current.reload(); });
    expect(result.current.error).toBe("Not permitted");
    expect(result.current.data).toBeNull();
    expect(result.current.loading).toBe(false);
  });

  it("falls back to a plain message when the failure has no body", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockRejectedValue(new Error("Network Error"));
    const { result } = renderHook(() => useKpiDashboard(base));
    await waitFor(() => expect(result.current.error).toBe("The KPI figures could not be loaded."));
  });

  it("does not refetch when only the filters object identity changes", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard([entry()]));
    const { result, rerender } = renderHook(({ f }) => useKpiDashboard(f), { initialProps: { f: base } });
    await waitFor(() => expect(result.current.loading).toBe(false));
    rerender({ f: { ...base } });
    expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(1);
  });

  it("reload fetches again", async () => {
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard([entry()]));
    const { result } = renderHook(() => useKpiDashboard(base));
    await waitFor(() => expect(result.current.loading).toBe(false));
    await act(async () => { await result.current.reload(); });
    expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(2);
  });
});
