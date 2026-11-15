import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { resetUiStateCache } from "./useUiState";
import { usePersistedTab } from "./usePersistedTab";

const service = vi.hoisted(() => ({ getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }));
vi.mock("../services/uiStateService", () => ({ uiStateService: service }));

const TABS = ["lots", "approved", "history"] as const;

function Probe({ pageKey, urlParam }: { pageKey: string; urlParam?: string }) {
  const [tab, setTab] = usePersistedTab(pageKey, TABS, urlParam);
  const location = useLocation();
  return (
    <div>
      <output aria-label={`tab of ${pageKey}`}>{tab}</output>
      <output aria-label={`search of ${pageKey}`}>{location.search}</output>
      {TABS.map((t) => (
        <button key={t} type="button" onClick={() => setTab(t)}>{`${pageKey} ${t}`}</button>
      ))}
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

describe("usePersistedTab", () => {
  it("URL value wins over stored", async () => {
    service.getAll.mockResolvedValue({ "nav.tab.media": "history" });
    renderAt("/?tab=approved", <Probe pageKey="media" urlParam="tab" />);

    // The address names a tab: it is shown at once and stays after the store answers.
    expect(screen.getByLabelText("tab of media").textContent).toBe("approved");
    await waitFor(() => expect(service.getAll).toHaveBeenCalled());
    await settle();
    expect(screen.getByLabelText("tab of media").textContent).toBe("approved");
  });

  it("stored value applied when URL is silent", async () => {
    service.getAll.mockResolvedValue({ "nav.tab.media": "history" });
    renderAt("/", <Probe pageKey="media" urlParam="tab" />);

    expect(screen.getByLabelText("tab of media").textContent).toBe("lots");
    await waitFor(() => expect(screen.getByLabelText("tab of media").textContent).toBe("history"));
  });

  it("stored value not in available tabs falls back to the default", async () => {
    service.getAll.mockResolvedValue({ "nav.tab.media": "removed-feature" });
    renderAt("/", <Probe pageKey="media" urlParam="tab" />);

    await waitFor(() => expect(service.getAll).toHaveBeenCalled());
    await settle();
    expect(screen.getByLabelText("tab of media").textContent).toBe("lots");
  });

  it("choosing a tab stores it and updates the URL parameter when one is configured", async () => {
    const user = userEvent.setup();
    service.getAll.mockResolvedValue({});
    renderAt("/?other=1", <Probe pageKey="media" urlParam="tab" />);

    await user.click(screen.getByRole("button", { name: "media approved" }));

    expect(screen.getByLabelText("tab of media").textContent).toBe("approved");
    expect(screen.getByLabelText("search of media").textContent).toBe("?other=1&tab=approved");
    await waitFor(() => expect(service.set).toHaveBeenCalledWith("nav.tab.media", "approved"), { timeout: 2000 });
  });

  it("choosing a tab without a URL parameter leaves the address alone", async () => {
    const user = userEvent.setup();
    service.getAll.mockResolvedValue({});
    renderAt("/", <Probe pageKey="media" />);

    await user.click(screen.getByRole("button", { name: "media history" }));

    expect(screen.getByLabelText("tab of media").textContent).toBe("history");
    expect(screen.getByLabelText("search of media").textContent).toBe("");
    await waitFor(() => expect(service.set).toHaveBeenCalledWith("nav.tab.media", "history"), { timeout: 2000 });
  });

  it("a choice made just before leaving the page is still saved", async () => {
    const user = userEvent.setup();
    service.getAll.mockResolvedValue({});
    const { unmount } = renderAt("/", <Probe pageKey="media" />);
    await waitFor(() => expect(service.getAll).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: "media approved" }));
    // Leave before the debounced write would run: the pending choice is sent on the way out.
    expect(service.set).not.toHaveBeenCalled();
    unmount();

    expect(service.set).toHaveBeenCalledWith("nav.tab.media", "approved");
  });

  it("store unavailable uses the browser fallback", async () => {
    service.getAll.mockRejectedValue(new Error("offline"));
    localStorage.setItem("microlims.uiState.7.nav.tab.media", "approved");
    renderAt("/", <Probe pageKey="media" />);

    await waitFor(() => expect(screen.getByLabelText("tab of media").textContent).toBe("approved"));
  });

  it("two pages with different pageKeys do not share a tab", async () => {
    service.getAll.mockResolvedValue({ "nav.tab.media": "approved" });
    renderAt("/", <><Probe pageKey="media" /><Probe pageKey="cryovials" /></>);

    await waitFor(() => expect(screen.getByLabelText("tab of media").textContent).toBe("approved"));
    expect(screen.getByLabelText("tab of cryovials").textContent).toBe("lots");
  });
});
