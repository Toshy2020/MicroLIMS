import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuthProvider, useAuth } from "./AuthContext";
import { LiveProvider, useLive } from "./LiveContext";
import { createFakeLiveConnection } from "./testing/fakeLiveConnection";

vi.mock("../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(() => Promise.resolve({ data: {} }))
  }
}));

function StatusProbe() {
  const { status } = useLive();
  const { logout } = useAuth();
  return (
    <div>
      <span data-testid="status">{status}</span>
      <button type="button" onClick={logout}>Sign out</button>
    </div>
  );
}

function WatchProbe() {
  const { watchRecord } = useLive();
  return (
    <button type="button" onClick={() => { watchRecord("sample", 7); }}>Watch</button>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

async function settle() {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

function renderWith(connection: ReturnType<typeof createFakeLiveConnection>, child: React.ReactNode) {
  localStorage.setItem("microlims_token", "h.e30=.s");
  render(
    <AuthProvider>
      <LiveProvider connection={connection}>{child}</LiveProvider>
    </AuthProvider>
  );
}

describe("LiveProvider", () => {
  it("connects when signed in and reports connected", async () => {
    const connection = createFakeLiveConnection();
    renderWith(connection, <StatusProbe />);
    await settle();
    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("status").textContent).toBe("connected");
  });

  it("logout stops the connection", async () => {
    const user = userEvent.setup();
    const connection = createFakeLiveConnection();
    renderWith(connection, <StatusProbe />);
    await settle();
    expect(connection.stop).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Sign out" }));
    await settle();

    expect(connection.stop).toHaveBeenCalled();
    expect(screen.getByTestId("status").textContent).toBe("offline");
  });

  it("reports offline when the hub cannot be reached", async () => {
    const connection = createFakeLiveConnection({ startFails: true });
    renderWith(connection, <StatusProbe />);
    await settle();
    expect(screen.getByTestId("status").textContent).toBe("offline");
  });

  it("watches a record on the hub once connected", async () => {
    const user = userEvent.setup();
    const connection = createFakeLiveConnection();
    renderWith(connection, <WatchProbe />);
    await settle();
    await user.click(screen.getByRole("button", { name: "Watch" }));
    expect(connection.invoke).toHaveBeenCalledWith("WatchRecord", "sample", 7);
  });
});
