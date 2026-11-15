import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { resetUiStateCache } from "./useUiState";
import { usePersistedLab } from "./usePersistedLab";

const service = vi.hoisted(() => ({ getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }));
vi.mock("../services/uiStateService", () => ({ uiStateService: service }));

const AVAILABLE = ["MICRO", "FP"];

function Probe({ available = AVAILABLE }: { available?: string[] }) {
  const [lab, setLab] = usePersistedLab(available);
  const location = useLocation();
  return (
    <div>
      <output aria-label="lab">{lab}</output>
      <output aria-label="search">{location.search}</output>
      <button type="button" onClick={() => setLab("FP")}>choose FP</button>
    </div>
  );
}

function renderAt(url: string, ui: ReactElement) {
  return render(<MemoryRouter initialEntries={[url]}>{ui}</MemoryRouter>);
}

const settle = () => new Promise((r) => setTimeout(r, 50));

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem("microlims_user_id", "7");
  resetUiStateCache();
  service.set.mockResolvedValue(undefined);
});

afterEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  resetUiStateCache();
});

describe("usePersistedLab", () => {
  it("URL value wins over stored", async () => {
    service.getAll.mockResolvedValue({ "nav.lastLab": "FP" });
    renderAt("/?lab=micro", <Probe />);

    expect(screen.getByLabelText("lab").textContent).toBe("MICRO");
    await waitFor(() => expect(service.getAll).toHaveBeenCalled());
    await settle();
    expect(screen.getByLabelText("lab").textContent).toBe("MICRO");
  });

  it("stored value applied when URL is silent", async () => {
    service.getAll.mockResolvedValue({ "nav.lastLab": "FP" });
    renderAt("/", <Probe />);

    expect(screen.getByLabelText("lab").textContent).toBe("MICRO");
    await waitFor(() => expect(screen.getByLabelText("lab").textContent).toBe("FP"));
  });

  it("stored laboratory the user no longer has falls back to the first available", async () => {
    service.getAll.mockResolvedValue({ "nav.lastLab": "QA" });
    renderAt("/", <Probe />);

    await waitFor(() => expect(service.getAll).toHaveBeenCalled());
    await settle();
    expect(screen.getByLabelText("lab").textContent).toBe("MICRO");
  });

  it("choosing a laboratory stores it and updates ?lab=", async () => {
    const user = userEvent.setup();
    service.getAll.mockResolvedValue({});
    renderAt("/", <Probe />);

    await user.click(screen.getByRole("button", { name: "choose FP" }));

    expect(screen.getByLabelText("lab").textContent).toBe("FP");
    expect(screen.getByLabelText("search").textContent).toBe("?lab=FP");
    await waitFor(() => expect(service.set).toHaveBeenCalledWith("nav.lastLab", "FP"), { timeout: 2000 });
  });

  it("store unavailable uses the browser fallback", async () => {
    service.getAll.mockRejectedValue(new Error("offline"));
    localStorage.setItem("microlims.uiState.7.nav.lastLab", "FP");
    renderAt("/", <Probe />);

    await waitFor(() => expect(screen.getByLabelText("lab").textContent).toBe("FP"));
  });

  it("no laboratories gives an empty value", () => {
    service.getAll.mockResolvedValue({});
    renderAt("/", <Probe available={[]} />);

    expect(screen.getByLabelText("lab").textContent).toBe("");
  });
});
