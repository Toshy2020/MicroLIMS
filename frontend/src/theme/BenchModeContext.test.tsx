import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { BenchModeProvider, useBenchMode } from "./BenchModeContext";
import { AuthProvider, useAuth } from "../contexts/AuthContext";

const MODE_KEY = "microlims_bench_mode";
const OFFERED_KEY = "microlims_bench_mode_offered";

function setCoarsePointer(coarse: boolean) {
  window.matchMedia = vi.fn().mockImplementation((query: string) => ({
    matches: coarse && query === "(pointer: coarse)",
    media: query,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    onchange: null,
    dispatchEvent: vi.fn()
  })) as unknown as typeof window.matchMedia;
}

function Probe() {
  const { benchMode, setBenchMode, shouldOffer, dismissOffer } = useBenchMode();
  return (
    <div>
      <span data-testid="state">{benchMode ? "on" : "off"}</span>
      <span data-testid="offer">{shouldOffer ? "offer" : "no-offer"}</span>
      <button type="button" onClick={() => setBenchMode(true)}>turn on</button>
      <button type="button" onClick={() => setBenchMode(false)}>turn off</button>
      <button type="button" onClick={dismissOffer}>dismiss</button>
    </div>
  );
}

function renderProbe() {
  return render(<BenchModeProvider><Probe /></BenchModeProvider>);
}

beforeEach(() => {
  localStorage.clear();
  setCoarsePointer(false);
});
afterEach(() => {
  vi.restoreAllMocks();
  localStorage.clear();
});

describe("BenchModeProvider", () => {
  it("defaults to off", () => {
    renderProbe();
    expect(screen.getByTestId("state").textContent).toBe("off");
  });

  it("persists on per device and is read back by a fresh mount", async () => {
    const user = userEvent.setup();
    const first = renderProbe();
    await user.click(screen.getByRole("button", { name: "turn on" }));
    expect(screen.getByTestId("state").textContent).toBe("on");
    expect(localStorage.getItem(MODE_KEY)).toBe("on");
    first.unmount();

    renderProbe();
    expect(screen.getByTestId("state").textContent).toBe("on");

    await user.click(screen.getByRole("button", { name: "turn off" }));
    expect(localStorage.getItem(MODE_KEY)).toBe("off");
  });

  it("offers bench mode once on a coarse pointer and not again after dismissal", async () => {
    const user = userEvent.setup();
    setCoarsePointer(true);
    const first = renderProbe();
    expect(screen.getByTestId("offer").textContent).toBe("offer");
    await user.click(screen.getByRole("button", { name: "dismiss" }));
    expect(screen.getByTestId("offer").textContent).toBe("no-offer");
    expect(localStorage.getItem(OFFERED_KEY)).toBe("true");
    first.unmount();

    renderProbe();
    expect(screen.getByTestId("offer").textContent).toBe("no-offer");
  });

  it("does not offer on a fine pointer", () => {
    renderProbe();
    expect(screen.getByTestId("offer").textContent).toBe("no-offer");
  });

  it("does not offer when bench mode is already on", () => {
    localStorage.setItem(MODE_KEY, "on");
    setCoarsePointer(true);
    renderProbe();
    expect(screen.getByTestId("offer").textContent).toBe("no-offer");
  });

  it("falls back to off without crashing when storage is unavailable", async () => {
    const user = userEvent.setup();
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("blocked"); });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new Error("blocked"); });
    setCoarsePointer(true);
    renderProbe();
    expect(screen.getByTestId("state").textContent).toBe("off");
    // The choice still applies for this page view even though it cannot be saved.
    await user.click(screen.getByRole("button", { name: "turn on" }));
    expect(screen.getByTestId("state").textContent).toBe("on");
    await user.click(screen.getByRole("button", { name: "dismiss" }));
    expect(screen.getByTestId("offer").textContent).toBe("no-offer");
  });

  it("survives logout: auth keys are cleared, the bench keys stay", async () => {
    const user = userEvent.setup();
    localStorage.setItem(MODE_KEY, "on");
    localStorage.setItem(OFFERED_KEY, "true");
    localStorage.setItem("microlims_token", "t");
    localStorage.setItem("microlims_username", "u");

    function LogoutButton() {
      const { logout } = useAuth();
      return <button type="button" onClick={logout}>log out</button>;
    }
    render(
      <BenchModeProvider>
        <AuthProvider>
          <LogoutButton />
          <Probe />
        </AuthProvider>
      </BenchModeProvider>
    );
    await user.click(screen.getByRole("button", { name: "log out" }));

    expect(localStorage.getItem("microlims_token")).toBeNull();
    expect(localStorage.getItem("microlims_username")).toBeNull();
    expect(localStorage.getItem(MODE_KEY)).toBe("on");
    expect(localStorage.getItem(OFFERED_KEY)).toBe("true");
    expect(screen.getByTestId("state").textContent).toBe("on");
  });
});
