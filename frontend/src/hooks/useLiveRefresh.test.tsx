import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { LiveProvider } from "../contexts/LiveContext";
import { createFakeLiveConnection, type FakeLiveConnection } from "../contexts/testing/fakeLiveConnection";
import { useLiveRefresh } from "./useLiveRefresh";

function Probe({ refetch, fallbackMs }: { refetch: () => void; fallbackMs: number }) {
  useLiveRefresh({ kind: "notifications", refetch, fallbackMs });
  return <div>probe</div>;
}

function mount(connection: FakeLiveConnection, refetch: () => void, fallbackMs = 60_000) {
  render(
    <LiveProvider connection={connection}>
      <Probe refetch={refetch} fallbackMs={fallbackMs} />
    </LiveProvider>
  );
  expect(screen.getByText("probe")).toBeTruthy();
}

async function settle() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

function setVisibility(state: "visible" | "hidden") {
  Object.defineProperty(document, "visibilityState", { configurable: true, get: () => state });
  Object.defineProperty(document, "hidden", { configurable: true, get: () => state === "hidden" });
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  setVisibility("visible");
});

describe("useLiveRefresh", () => {
  it("connected: a matching signal triggers refetch; a non-matching one does not", async () => {
    vi.useFakeTimers();
    const connection = createFakeLiveConnection();
    const refetch = vi.fn();
    mount(connection, refetch);
    await settle();

    act(() => connection.emitSignal({ kind: "counters", key: "user:1" }));
    expect(refetch).not.toHaveBeenCalled();

    act(() => connection.emitSignal({ kind: "notifications", key: "user:1" }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });

  it("burst of 10 signals results in one refetch", async () => {
    vi.useFakeTimers();
    const connection = createFakeLiveConnection();
    const refetch = vi.fn();
    mount(connection, refetch);
    await settle();

    act(() => {
      for (let i = 0; i < 10; i++) connection.emitSignal({ kind: "notifications", key: "user:1" });
    });
    expect(refetch).toHaveBeenCalledTimes(1);

    act(() => {
      vi.advanceTimersByTime(5_000);
    });
    expect(refetch).toHaveBeenCalledTimes(1);
  });

  it("signals within one second of a refetch are coalesced into one trailing refetch", async () => {
    vi.useFakeTimers();
    const connection = createFakeLiveConnection();
    const refetch = vi.fn();
    mount(connection, refetch);
    await settle();

    act(() => connection.emitSignal({ kind: "notifications", key: "user:1" }));
    act(() => {
      vi.advanceTimersByTime(200);
      connection.emitSignal({ kind: "notifications", key: "user:1" });
      vi.advanceTimersByTime(200);
      connection.emitSignal({ kind: "notifications", key: "user:1" });
    });
    expect(refetch).toHaveBeenCalledTimes(1);

    act(() => {
      vi.advanceTimersByTime(1_000);
    });
    expect(refetch).toHaveBeenCalledTimes(2);
  });

  it("offline: polls at fallbackMs; a hidden tab does not poll", async () => {
    vi.useFakeTimers();
    const connection = createFakeLiveConnection({ startFails: true });
    const refetch = vi.fn();
    mount(connection, refetch, 10_000);
    await settle();

    act(() => {
      vi.advanceTimersByTime(9_999);
    });
    expect(refetch).not.toHaveBeenCalled();
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(refetch).toHaveBeenCalledTimes(1);

    setVisibility("hidden");
    act(() => {
      vi.advanceTimersByTime(30_000);
    });
    expect(refetch).toHaveBeenCalledTimes(1);

    setVisibility("visible");
    act(() => {
      vi.advanceTimersByTime(10_000);
    });
    expect(refetch).toHaveBeenCalledTimes(2);
  });

  it("reconnect: refetches once and stops polling", async () => {
    vi.useFakeTimers();
    const connection = createFakeLiveConnection();
    const refetch = vi.fn();
    mount(connection, refetch, 10_000);
    await settle();

    act(() => connection.triggerReconnecting());
    act(() => {
      vi.advanceTimersByTime(10_000);
    });
    expect(refetch).toHaveBeenCalledTimes(1);

    act(() => connection.triggerReconnected());
    expect(refetch).toHaveBeenCalledTimes(2);

    act(() => {
      vi.advanceTimersByTime(60_000);
    });
    expect(refetch).toHaveBeenCalledTimes(2);
  });
});
