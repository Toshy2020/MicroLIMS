import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { myWorkService } from "./myWorkService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

describe("myWorkService", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetches /my-work with optional lab and AbortSignal", async () => {
    const controller = new AbortController();
    const payload = { serverTimeUtc: "2026-11-14T09:00:00Z", timeZoneId: "UTC", groups: [], unavailableSources: [] };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: payload } });

    const res = await myWorkService.get("MICRO", controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/my-work", {
      params: { lab: "MICRO" },
      signal: controller.signal
    });
    expect(res).toEqual(payload);
  });

  it("fetches /my-work/summary with AbortSignal", async () => {
    const controller = new AbortController();
    const summary = { total: 5, overdue: 1, now: 2, complete: true };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: summary } });

    const res = await myWorkService.summary(controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/my-work/summary", { signal: controller.signal });
    expect(res).toEqual(summary);
  });

  it("posts take and release to testorders self-assignment endpoints", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { data: {} } });

    await myWorkService.take(42);
    expect(apiClient.post).toHaveBeenCalledWith("/testorders/42/take");

    await myWorkService.release(42);
    expect(apiClient.post).toHaveBeenCalledWith("/testorders/42/release");
  });
});
