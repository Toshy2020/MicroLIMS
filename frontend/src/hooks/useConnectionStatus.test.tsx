import { act, renderHook, waitFor } from "@testing-library/react";
import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient, isNetworkError } from "../services/apiClient";
import { healthService } from "../services/healthService";
import { useConnectionStatus } from "./useConnectionStatus";

vi.mock("../services/healthService", () => ({ healthService: { check: vi.fn() } }));

const originalAdapter = apiClient.defaults.adapter;

function failWith(status: number | null): AxiosAdapter {
  return (config: InternalAxiosRequestConfig) => {
    if (status === null) return Promise.reject(new AxiosError("Network Error", "ERR_NETWORK", config));
    const response = { status, statusText: "", data: {}, headers: {}, config };
    return Promise.reject(new AxiosError("Request failed", "ERR_BAD_RESPONSE", config, null, response));
  };
}

function networkError(): AxiosError {
  return new AxiosError("Network Error", "ERR_NETWORK");
}

beforeEach(() => {
  vi.mocked(healthService.check).mockReset();
});
afterEach(() => {
  apiClient.defaults.adapter = originalAdapter;
  vi.useRealTimers();
});

describe("isNetworkError", () => {
  it("is true only for a failure with no HTTP answer", () => {
    expect(isNetworkError(networkError())).toBe(true);
    expect(isNetworkError(new Error("boom"))).toBe(false);
    expect(isNetworkError(null)).toBe(false);
  });
});

describe("useConnectionStatus", () => {
  it("network error flips to offline; successful health check flips back; HTTP 500 does not count as offline", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    const { result } = renderHook(() => useConnectionStatus(true));
    expect(result.current.online).toBe(true);

    // An HTTP 500 is an answer from the server: still online.
    apiClient.defaults.adapter = failWith(500);
    await act(async () => {
      await apiClient.get("/anything").catch(() => undefined);
    });
    expect(result.current.online).toBe(true);

    // No answer at all: offline.
    apiClient.defaults.adapter = failWith(null);
    await act(async () => {
      await apiClient.get("/anything").catch(() => undefined);
    });
    await waitFor(() => expect(result.current.online).toBe(false));

    // The next health check still fails: stays offline.
    vi.mocked(healthService.check).mockRejectedValueOnce(networkError());
    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });
    expect(result.current.online).toBe(false);
    expect(healthService.check).toHaveBeenCalledTimes(1);

    // One successful health check brings it back.
    vi.mocked(healthService.check).mockResolvedValueOnce(undefined);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });
    expect(result.current.online).toBe(true);
    expect(result.current.lastCheckedAt).toBeInstanceOf(Date);
  });

  it("a health check that answers with HTTP 500 does not count as offline", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    const { result } = renderHook(() => useConnectionStatus(true));
    vi.mocked(healthService.check).mockRejectedValueOnce(
      new AxiosError("Request failed", "ERR_BAD_RESPONSE", undefined, null, {
        status: 500, statusText: "", data: {}, headers: {}, config: { headers: {} } as InternalAxiosRequestConfig
      })
    );

    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });

    expect(healthService.check).toHaveBeenCalledTimes(1);
    expect(result.current.online).toBe(true);
  });

  it("a failing health check flips to offline", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    const { result } = renderHook(() => useConnectionStatus(true));
    vi.mocked(healthService.check).mockRejectedValue(networkError());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(15_000);
    });

    expect(result.current.online).toBe(false);
  });

  it("does not poll while inactive and online", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    renderHook(() => useConnectionStatus(false));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(60_000);
    });

    expect(healthService.check).not.toHaveBeenCalled();
  });
});
