import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { ACTIVITY_EVENTS, useIdleTimeout } from "./useIdleTimeout";

const MIN = 60 * 1000;

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("useIdleTimeout", () => {
  it("lists touch and pointer events as activity", () => {
    expect(ACTIVITY_EVENTS).toContain("touchstart");
    expect(ACTIVITY_EVENTS).toContain("pointerdown");
  });

  it.each(["touchstart", "pointerdown"])("%s resets the idle timer", (evt) => {
    const { result } = renderHook(() => useIdleTimeout(vi.fn()));
    act(() => { vi.advanceTimersByTime(19 * MIN); });
    act(() => { window.dispatchEvent(new Event(evt)); });
    act(() => { vi.advanceTimersByTime(19 * MIN); });
    expect(result.current.showWarning).toBe(false);
    act(() => { vi.advanceTimersByTime(2 * MIN); });
    expect(result.current.showWarning).toBe(true);
  });

  it("no activity for 20 min shows the warning, then logs out after 60 s", () => {
    const onTimeout = vi.fn();
    const { result } = renderHook(() => useIdleTimeout(onTimeout));
    act(() => { vi.advanceTimersByTime(20 * MIN - 1); });
    expect(result.current.showWarning).toBe(false);
    act(() => { vi.advanceTimersByTime(1); });
    expect(result.current.showWarning).toBe(true);
    expect(result.current.secondsRemaining).toBe(60);
    act(() => { vi.advanceTimersByTime(59 * 1000); });
    expect(onTimeout).not.toHaveBeenCalled();
    act(() => { vi.advanceTimersByTime(1000); });
    expect(onTimeout).toHaveBeenCalledTimes(1);
  });
});
