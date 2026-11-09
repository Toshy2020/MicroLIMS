import { act, renderHook, waitFor } from "@testing-library/react";
import { AxiosError, CanceledError } from "axios";
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../services/apiClient";
import { useApi } from "./useApi";

vi.mock("../services/apiClient", () => ({
  apiClient: { get: vi.fn() }
}));

afterEach(() => vi.clearAllMocks());

interface Pending {
  url: string;
  signal: AbortSignal;
  resolve: (data: unknown) => void;
  reject: (err: unknown) => void;
}

// Each call to apiClient.get stays pending until the test settles it, and -
// like axios - rejects with a CanceledError once its signal aborts.
function controllableGet(): Pending[] {
  const calls: Pending[] = [];
  vi.mocked(apiClient.get).mockImplementation(((url: string, config?: { signal?: AbortSignal }) =>
    new Promise((resolve, reject) => {
      const signal = config?.signal as AbortSignal;
      calls.push({ url, signal, resolve: (data) => resolve({ data: { data } }), reject });
      signal?.addEventListener("abort", () => reject(new CanceledError()));
    })) as never);
  return calls;
}

describe("useApi", () => {
  it("cancels the old request when the URL changes and never shows its late answer", async () => {
    const calls = controllableGet();
    const { result, rerender } = renderHook(({ url }) => useApi<string>(url), { initialProps: { url: "/samples/1" } });

    rerender({ url: "/samples/2" });

    expect(calls).toHaveLength(2);
    expect(calls[0].signal.aborted).toBe(true);
    expect(calls[1].signal.aborted).toBe(false);

    await act(async () => calls[1].resolve("sample two"));
    await act(async () => calls[0].resolve("sample one"));

    expect(result.current.data).toBe("sample two");
    expect(result.current.loading).toBe(false);
    expect(result.current.error).toBeNull();
  });

  it("does not report a cancelled request as an error", async () => {
    const calls = controllableGet();
    const { result, rerender } = renderHook(({ url }) => useApi<string>(url), { initialProps: { url: "/a" } });

    rerender({ url: "/b" });
    await act(async () => { await Promise.resolve(); });

    expect(result.current.error).toBeNull();
    expect(result.current.loading).toBe(true);
    expect(calls[1].signal.aborted).toBe(false);
  });

  it("aborts the request in flight when the component unmounts", () => {
    const calls = controllableGet();
    const { unmount } = renderHook(() => useApi<string>("/dashboard"));

    unmount();

    expect(calls[0].signal.aborted).toBe(true);
  });

  it("shows the server's message on failure and recovers on reload", async () => {
    const calls = controllableGet();
    const { result } = renderHook(() => useApi<string>("/dashboard"));

    const failure = new AxiosError("Request failed");
    failure.response = { data: { message: "You are not assigned to this section." } } as AxiosError["response"];
    await act(async () => calls[0].reject(failure));

    expect(result.current.error).toBe("You are not assigned to this section.");
    expect(result.current.loading).toBe(false);

    act(() => result.current.reload());
    expect(result.current.error).toBeNull();
    await act(async () => calls[1].resolve("summary"));

    await waitFor(() => expect(result.current.data).toBe("summary"));
    expect(calls[1].url).toBe("/dashboard");
  });
});
