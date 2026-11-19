import { vi } from "vitest";
import type { LiveConnection, LiveSignal } from "../LiveContext";

export interface FakeLiveConnection extends LiveConnection {
  emitSignal(signal: LiveSignal): void;
  triggerReconnecting(): void;
  triggerReconnected(): void;
  triggerClose(): void;
  start: ReturnType<typeof vi.fn<() => Promise<void>>>;
  stop: ReturnType<typeof vi.fn<() => Promise<void>>>;
  invoke: ReturnType<typeof vi.fn<(methodName: string, ...args: unknown[]) => Promise<unknown>>>;
}

// A stand-in for the SignalR hub connection, injected through LiveProvider's
// `connection` prop. `startFails` models a hub that is unreachable.
export function createFakeLiveConnection(options: { startFails?: boolean } = {}): FakeLiveConnection {
  const signalHandlers = new Set<(signal: LiveSignal) => void>();
  let reconnecting: () => void = () => undefined;
  let reconnected: () => void = () => undefined;
  let closed: () => void = () => undefined;

  return {
    start: vi.fn(() => (options.startFails ? Promise.reject(new Error("hub unreachable")) : Promise.resolve())),
    stop: vi.fn(() => Promise.resolve()),
    invoke: vi.fn(() => Promise.resolve(undefined)),
    on: (_method: string, handler: (signal: LiveSignal) => void) => {
      signalHandlers.add(handler);
    },
    off: (_method: string, handler: (signal: LiveSignal) => void) => {
      signalHandlers.delete(handler);
    },
    onreconnecting: (cb: () => void) => {
      reconnecting = cb;
    },
    onreconnected: (cb: () => void) => {
      reconnected = cb;
    },
    onclose: (cb: () => void) => {
      closed = cb;
    },
    emitSignal: (signal) => {
      for (const handler of [...signalHandlers]) handler(signal);
    },
    triggerReconnecting: () => reconnecting(),
    triggerReconnected: () => reconnected(),
    triggerClose: () => closed()
  };
}
