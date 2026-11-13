import { afterEach, describe, expect, it, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useWakeLock } from "./useWakeLock";

function installWakeLock(request: ReturnType<typeof vi.fn>) {
  Object.defineProperty(navigator, "wakeLock", { configurable: true, value: { request } });
}
function makeRequest() {
  const sentinels: { release: ReturnType<typeof vi.fn> }[] = [];
  const request = vi.fn(async () => {
    const s = { release: vi.fn(async () => {}) };
    sentinels.push(s);
    return s;
  });
  return { request, sentinels };
}
function setVisibility(state: "visible" | "hidden") {
  Object.defineProperty(document, "visibilityState", { configurable: true, value: state });
  document.dispatchEvent(new Event("visibilitychange"));
}

afterEach(() => {
  Reflect.deleteProperty(navigator, "wakeLock");
  Object.defineProperty(document, "visibilityState", { configurable: true, value: "visible" });
});

describe("useWakeLock", () => {
  it("requests a screen lock when active and releases when deactivated", async () => {
    const { request, sentinels } = makeRequest();
    installWakeLock(request);
    const { rerender } = renderHook(({ on }) => useWakeLock(on), { initialProps: { on: true } });
    await act(async () => {});
    expect(request).toHaveBeenCalledWith("screen");
    rerender({ on: false });
    await act(async () => {});
    expect(sentinels[0].release).toHaveBeenCalled();
  });

  it("does not request while inactive, and releases on unmount", async () => {
    const { request, sentinels } = makeRequest();
    installWakeLock(request);
    renderHook(() => useWakeLock(false));
    await act(async () => {});
    expect(request).not.toHaveBeenCalled();
    const live = renderHook(() => useWakeLock(true));
    await act(async () => {});
    live.unmount();
    await act(async () => {});
    expect(sentinels[0].release).toHaveBeenCalled();
  });

  it("re-requests when the page becomes visible again", async () => {
    const { request } = makeRequest();
    installWakeLock(request);
    renderHook(() => useWakeLock(true));
    await act(async () => {});
    expect(request).toHaveBeenCalledTimes(1);
    await act(async () => { setVisibility("hidden"); });
    expect(request).toHaveBeenCalledTimes(1);
    await act(async () => { setVisibility("visible"); });
    expect(request).toHaveBeenCalledTimes(2);
  });

  it("does nothing in a browser without wake lock support", async () => {
    expect("wakeLock" in navigator).toBe(false);
    expect(() => renderHook(() => useWakeLock(true))).not.toThrow();
    await act(async () => { setVisibility("visible"); });
  });

  it("stays silent when the request is rejected", async () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    installWakeLock(vi.fn(async () => { throw new Error("denied"); }));
    renderHook(() => useWakeLock(true));
    await act(async () => {});
    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });
});
