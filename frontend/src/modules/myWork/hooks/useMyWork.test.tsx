import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook } from "@testing-library/react";
import { myWorkService } from "../services/myWorkService";
import type { MyWorkResponse } from "../myWorkTypes";
import { useMyWork } from "./useMyWork";

vi.mock("../services/myWorkService", () => ({
  myWorkService: {
    get: vi.fn(),
    summary: vi.fn(),
    take: vi.fn(),
    release: vi.fn()
  }
}));

const emptyResponse: MyWorkResponse = {
  serverTimeUtc: "2026-11-14T09:00:00Z",
  timeZoneId: "UTC",
  groups: [],
  unavailableSources: []
};

describe("useMyWork", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      get: () => "visible"
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    cleanup();
  });

  it("aborts an in-flight request on unmount", () => {
    let capturedSignal: AbortSignal | undefined;
    vi.mocked(myWorkService.get).mockImplementation((_lab, signal) => {
      capturedSignal = signal;
      return new Promise(() => {});
    });

    const { unmount } = renderHook(() => useMyWork());
    expect(capturedSignal).toBeDefined();
    expect(capturedSignal?.aborted).toBe(false);

    unmount();
    expect(capturedSignal?.aborted).toBe(true);
  });

  it("refetches on window focus and every 60 s while visible", async () => {
    vi.mocked(myWorkService.get).mockResolvedValue(emptyResponse);

    renderHook(() => useMyWork());

    // Initial mount fetch
    await act(async () => {
      await Promise.resolve();
    });
    expect(myWorkService.get).toHaveBeenCalledTimes(1);

    // 60s interval while visible
    await act(async () => {
      vi.advanceTimersByTime(60_000);
      await Promise.resolve();
    });
    expect(myWorkService.get).toHaveBeenCalledTimes(2);

    // Window focus
    await act(async () => {
      window.dispatchEvent(new Event("focus"));
      await Promise.resolve();
    });
    expect(myWorkService.get).toHaveBeenCalledTimes(3);
  });
});
