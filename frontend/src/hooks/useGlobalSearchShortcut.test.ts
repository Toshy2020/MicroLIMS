import { describe, expect, it, vi, afterEach } from "vitest";
import { renderHook, act, cleanup } from "@testing-library/react";
import { useGlobalSearchShortcut } from "./useGlobalSearchShortcut";

describe("useGlobalSearchShortcut", () => {
  afterEach(cleanup);

  it("calls callback when Ctrl+K is pressed", () => {
    const onTrigger = vi.fn();
    renderHook(() => useGlobalSearchShortcut(onTrigger));

    act(() => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", ctrlKey: true }));
    });

    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it("calls callback when Cmd+K is pressed (macOS)", () => {
    const onTrigger = vi.fn();
    renderHook(() => useGlobalSearchShortcut(onTrigger));

    act(() => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", metaKey: true }));
    });

    expect(onTrigger).toHaveBeenCalledTimes(1);
  });

  it("does not trigger on plain k press", () => {
    const onTrigger = vi.fn();
    renderHook(() => useGlobalSearchShortcut(onTrigger));

    act(() => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "k" }));
    });

    expect(onTrigger).not.toHaveBeenCalled();
  });
});
