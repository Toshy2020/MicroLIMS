import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Link, MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import axios, { AxiosHeaders } from "axios";
import { toast } from "sonner";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { myWorkService } from "./services/myWorkService";
import type { MyWorkResponse } from "./myWorkTypes";
import { MyWorkPage } from "./MyWorkPage";
import { i18n } from "../../i18n";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";

// The myWork texts load on first use; the English file is in place before the tests query it.
beforeAll(async () => {
  await i18n.loadNamespaces("myWork");
});

vi.mock("./services/myWorkService", () => ({
  myWorkService: {
    get: vi.fn(),
    summary: vi.fn(),
    take: vi.fn(),
    release: vi.fn()
  }
}));

// Network boundary: the server's answer about take and release.
vi.mock("../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn().mockResolvedValue([]) }
}));

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn()
  }
}));

function LocationEcho() {
  const loc = useLocation();
  return <div data-testid="location-echo">{`${loc.pathname}${loc.search}`}</div>;
}

function makeResponse(over: Partial<MyWorkResponse> = {}): MyWorkResponse {
  return {
    serverTimeUtc: "2026-11-14T09:00:00Z",
    timeZoneId: "UTC",
    unavailableSources: [],
    groups: [
      {
        key: "readyToRead",
        title: "Ready to read now",
        count: 1,
        lockedCount: 0,
        hasMore: false,
        items: [
          {
            key: "inc-1",
            groupKey: "readyToRead",
            identifier: "SMP-1001",
            title: "TAMC Plate Read",
            dueAtUtc: "2026-11-14T08:00:00Z",
            urgency: "Overdue",
            locked: false,
            target: { type: "incubation-read", sampleId: 1, testOrderId: 11, incubationId: 21 },
            actions: ["read"],
            lab: "MICRO"
          }
        ]
      },
      {
        key: "available",
        title: "Available in my section",
        count: 1,
        lockedCount: 0,
        hasMore: false,
        items: [
          {
            key: "avail-22",
            groupKey: "available",
            identifier: "SMP-2002",
            title: "Bioburden",
            urgency: "Now",
            locked: false,
            target: { type: "test-step", sampleId: 2, testOrderId: 22 },
            actions: ["take"],
            lab: "MICRO"
          }
        ]
      },
      {
        key: "toPerform",
        title: "Tests to perform",
        count: 1,
        lockedCount: 0,
        hasMore: false,
        items: [
          {
            key: "mine-33",
            groupKey: "toPerform",
            identifier: "SMP-3003",
            title: "Sterility Test",
            urgency: "Today",
            locked: false,
            target: { type: "test-step", sampleId: 3, testOrderId: 33 },
            actions: ["release"],
            lab: "MICRO"
          }
        ]
      },
      {
        key: "emptyGroup",
        title: "Empty Group Should Not Render",
        count: 0,
        lockedCount: 0,
        hasMore: false,
        items: []
      }
    ],
    ...over
  };
}

function renderPage(initialEntry = "/my-work") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialEntry]}>
          <Routes>
            <Route
              path="/my-work"
              element={
                <>
                  <MyWorkPage />
                  <LocationEcho />
                </>
              }
            />
            <Route
              path="/dashboard"
              element={
                <div>
                  <div>Dashboard</div>
                  <Link to="/my-work">Open My Work (0)</Link>
                </div>
              }
            />
            <Route path="/microbiology/workspace" element={<LocationEcho />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  sessionStorage.clear();
});

describe("MyWorkPage", () => {
  it("renders groups in server order with headings and counts and omits empty groups", async () => {
    vi.mocked(myWorkService.get).mockResolvedValue(makeResponse());

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /Ready to read now/i })).toBeTruthy();
    });

    const headings = screen
      .getAllByRole("heading", { level: 2 })
      .map((h) => h.textContent);
    expect(headings).toEqual([
      expect.stringContaining("Ready to read now"),
      expect.stringContaining("Available in my section"),
      expect.stringContaining("Tests to perform")
    ]);
    expect(screen.queryByText("Empty Group Should Not Render")).toBeNull();
  });

  it("keeps a locked item out of the All work and urgency tiles and says how many shown items are locked", async () => {
    const item = (key: string, locked: boolean) => ({
      key,
      groupKey: "review",
      identifier: key.toUpperCase(),
      title: `Review ${key}`,
      urgency: "Overdue" as const,
      locked,
      lockReason: locked ? "You cannot review a sample you tested." : undefined,
      target: { type: "sample-review", sampleId: 1 },
      actions: locked ? [] : ["review"],
      lab: "MICRO"
    });
    vi.mocked(myWorkService.get).mockResolvedValue(
      makeResponse({
        groups: [
          { key: "review", title: "Awaiting my review", count: 1, lockedCount: 1, hasMore: false, items: [item("open-1", false), item("locked-1", true)] }
        ]
      })
    );

    renderPage();

    await screen.findByRole("heading", { name: /Awaiting my review/i });
    expect(screen.getByRole("button", { name: /All work/i }).textContent).toMatch(/1/);
    expect(screen.getByRole("button", { name: /Overdue/i }).textContent).toMatch(/1/);
    expect(screen.getByRole("button", { name: /Overdue/i }).textContent).not.toMatch(/2/);
    const shown = screen.getByText(/items shown/i).textContent ?? "";
    expect(shown).toMatch(/2 items shown/);
    expect(shown).toMatch(/1 locked, not counted in All work/);
  });

  it("shows loading skeletons while fetching", () => {
    vi.mocked(myWorkService.get).mockImplementation(() => new Promise(() => {}));

    renderPage();

    expect(screen.getByTestId("my-work-loading")).toBeTruthy();
  });

  it("shows empty state linking to dashboard when no groups exist and user has no dashboard permission", async () => {
    vi.mocked(myWorkService.get).mockResolvedValue(makeResponse({ groups: [] }));

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole("status")).toBeTruthy();
    });
    const dashLink = screen.getByRole("link", { name: /dashboard/i });
    expect(dashLink.getAttribute("href")).toBe("/dashboard");
  });

  it("redirects a user with no groups and a dashboard permission to /dashboard once without looping", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims_permissions", JSON.stringify([PERMISSIONS.DASHBOARDS_LAB_OVERVIEW]));
    vi.mocked(myWorkService.get).mockResolvedValue(makeResponse({ groups: [] }));

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Dashboard")).toBeTruthy();
    });

    await user.click(screen.getByRole("link", { name: "Open My Work (0)" }));

    await waitFor(() => {
      expect(screen.getByText("Nothing waiting for you")).toBeTruthy();
    });
    expect(screen.getByTestId("location-echo").textContent).toBe("/my-work");
  });

  it("shows error state with Retry button that refetches", async () => {
    const user = userEvent.setup();
    vi.mocked(myWorkService.get)
      .mockRejectedValueOnce(new Error("Network failure"))
      .mockResolvedValueOnce(makeResponse());

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Retry/i })).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /Retry/i }));

    await waitFor(() => {
      expect(screen.getByText("SMP-1001")).toBeTruthy();
    });
    expect(myWorkService.get).toHaveBeenCalledTimes(2);
  });

  it("shows PartialOutageBanner naming unavailable sources and Retry refetches", async () => {
    const user = userEvent.setup();
    vi.mocked(myWorkService.get).mockResolvedValue(
      makeResponse({ unavailableSources: ["IncubationSource", "EquipmentSource"] })
    );

    renderPage();

    await waitFor(() => {
      expect(screen.getByText(/IncubationSource, EquipmentSource/)).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /Retry/i }));
    expect(myWorkService.get).toHaveBeenCalledTimes(2);
  });

  it("filters by urgency when a KPI tile is clicked and by identifier search, keeping both in URL", async () => {
    const user = userEvent.setup();
    vi.mocked(myWorkService.get).mockResolvedValue(makeResponse());

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("SMP-1001")).toBeTruthy();
    });

    const overdueTile = screen.getByRole("button", { name: /Overdue/i });
    await user.click(overdueTile);

    expect(screen.getByText("SMP-1001")).toBeTruthy();
    expect(screen.queryByText("SMP-2002")).toBeNull();
    expect(screen.getByTestId("location-echo").textContent).toContain("urgency=Overdue");

    const searchInput = screen.getByLabelText(/Search by identifier/i);
    await user.type(searchInput, "1001");

    expect(screen.getByTestId("location-echo").textContent).toContain("urgency=Overdue");
    expect(screen.getByTestId("location-echo").textContent).toContain("q=1001");
  });

  it("Take calls service, refreshes on success, and shows server message toast on refusal", async () => {
    const user = userEvent.setup();
    vi.mocked(myWorkService.get).mockResolvedValue(makeResponse());

    const axiosErr = new axios.AxiosError(
      "Conflict",
      "409",
      undefined,
      undefined,
      {
        status: 409,
        statusText: "Conflict",
        headers: new AxiosHeaders(),
        config: { headers: new AxiosHeaders() },
        data: { message: "This test was already taken by another analyst." }
      }
    );
    vi.mocked(myWorkService.take).mockRejectedValueOnce(axiosErr).mockResolvedValueOnce(undefined);

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Take/i })).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /Take/i }));

    await waitFor(() => {
      expect(myWorkService.take).toHaveBeenCalledWith(22);
      expect(toast.error).toHaveBeenCalledWith("This test was already taken by another analyst.");
    });

    await user.click(screen.getByRole("button", { name: /Take/i }));
    await waitFor(() => {
      expect(myWorkService.get).toHaveBeenCalledTimes(2);
    });
  });

  it("Release opens confirmation dialog with Cancel focused and calls service on confirm", async () => {
    const user = userEvent.setup();
    vi.mocked(myWorkService.get).mockResolvedValue(makeResponse());
    vi.mocked(myWorkService.release).mockResolvedValue(undefined);

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Release/i })).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /Release/i }));

    const dialog = await screen.findByRole("alertdialog");
    const cancelBtn = within(dialog).getByRole("button", { name: /Cancel/i });
    expect(document.activeElement).toBe(cancelBtn);

    const confirmBtn = within(dialog).getByRole("button", { name: /Release/i });
    await user.click(confirmBtn);

    await waitFor(() => {
      expect(myWorkService.release).toHaveBeenCalledWith(33);
    });
  });

  it("after a keyboard Take, focus lands on the Release button of the row that moved", async () => {
    const user = userEvent.setup();
    const taken = makeResponse();
    // After the take, SMP-2002 sits in the analyst's own list and offers Release.
    const availableGroup = taken.groups[1];
    const moved = { ...availableGroup.items[0], key: "mine-22", groupKey: "toPerform", actions: ["release" as const] };
    const after = makeResponse({
      groups: [
        taken.groups[0],
        { ...taken.groups[2], count: 2, items: [moved, ...taken.groups[2].items] }
      ]
    });
    vi.mocked(myWorkService.get).mockResolvedValueOnce(taken).mockResolvedValue(after);
    vi.mocked(myWorkService.take).mockResolvedValue(undefined);

    renderPage();
    const take = await screen.findByRole("button", { name: /Take/i });
    take.focus();
    await user.keyboard("{Enter}");

    await waitFor(() => {
      const row = screen.getByTestId("my-work-row-mine-22");
      expect(document.activeElement).toBe(within(row).getByRole("button", { name: /Release/i }));
    });
  });

  it("after a keyboard Release, focus lands on the Take button of the row that moved", async () => {
    const user = userEvent.setup();
    const before = makeResponse();
    const mine = before.groups[2].items[0];
    const moved = { ...mine, key: "avail-33", groupKey: "available", actions: ["take" as const] };
    const after = makeResponse({
      groups: [
        before.groups[0],
        { ...before.groups[1], count: 2, items: [...before.groups[1].items, moved] }
      ]
    });
    vi.mocked(myWorkService.get).mockResolvedValueOnce(before).mockResolvedValue(after);
    vi.mocked(myWorkService.release).mockResolvedValue(undefined);

    renderPage();
    const release = await screen.findByRole("button", { name: /Release/i });
    release.focus();
    await user.keyboard("{Enter}");
    const dialog = await screen.findByRole("alertdialog");
    await user.tab();
    expect(document.activeElement).toBe(within(dialog).getByRole("button", { name: /^Release/i }));
    await user.keyboard("{Enter}");

    await waitFor(() => {
      const row = screen.getByTestId("my-work-row-avail-33");
      expect(document.activeElement).toBe(within(row).getByRole("button", { name: /Take/i }));
    });
  });

  it("preserves keyboard focus on the same row key across refresh", async () => {
    vi.mocked(myWorkService.get)
      .mockResolvedValueOnce(makeResponse())
      .mockResolvedValueOnce(makeResponse({ serverTimeUtc: "2026-11-14T09:01:00Z" }));

    renderPage();

    await waitFor(() => {
      expect(screen.getByRole("link", { name: /SMP-1001/i })).toBeTruthy();
    });

    const rowLink = screen.getByRole("link", { name: /SMP-1001/i });
    rowLink.focus();
    expect(document.activeElement).toBe(rowLink);

    window.dispatchEvent(new Event("focus"));

    await waitFor(() => {
      expect(myWorkService.get).toHaveBeenCalledTimes(2);
    });

    const updatedLink = screen.getByRole("link", { name: /SMP-1001/i });
    expect(document.activeElement).toBe(updatedLink);
  });

  it("renders next24Hours group with title and count and navigates like endingToday on item click", async () => {
    const user = userEvent.setup();
    vi.mocked(myWorkService.get).mockResolvedValue(
      makeResponse({
        groups: [
          {
            key: "endingToday",
            title: "Incubation ending today",
            count: 1,
            lockedCount: 0,
            hasMore: false,
            items: [
              {
                key: "incubation:101",
                groupKey: "endingToday",
                identifier: "SMP-TODAY",
                title: "Read Bioburden",
                dueAtUtc: "2026-11-14T21:30:00Z",
                urgency: "Today",
                locked: false,
                target: { type: "incubation-read", sampleId: 10, testOrderId: 101, incubationId: 201 },
                actions: [],
                lab: "MICRO"
              }
            ]
          },
          {
            key: "next24Hours",
            title: "Incubation ending in the next 24 hours",
            count: 2,
            lockedCount: 0,
            hasMore: false,
            items: [
              {
                key: "incubation:102",
                groupKey: "next24Hours",
                identifier: "SMP-NEXT24",
                title: "Read Sterility",
                dueAtUtc: "2026-11-15T06:00:00Z",
                urgency: "Later",
                locked: false,
                target: { type: "incubation-read", sampleId: 20, testOrderId: 102, incubationId: 202 },
                actions: [],
                lab: "MICRO"
              }
            ]
          }
        ]
      })
    );

    renderPage();

    const heading = await screen.findByRole("heading", { name: /Incubation ending in the next 24 hours/i });
    const section = heading.closest("section");
    expect(section).not.toBeNull();
    expect(within(section!).getByText("2")).toBeTruthy();

    const todayLink = screen.getByRole("link", { name: /SMP-TODAY/i });
    expect(todayLink.getAttribute("href")).toBe(
      "/microbiology/workspace?sampleId=10&testOrderId=101&open=incubation&incubationId=201"
    );

    const next24Link = screen.getByRole("link", { name: /SMP-NEXT24/i });
    expect(next24Link.getAttribute("href")).toBe(
      "/microbiology/workspace?sampleId=20&testOrderId=102&open=incubation&incubationId=202"
    );

    await user.click(next24Link);
    expect(screen.getByTestId("location-echo").textContent).toBe(
      "/microbiology/workspace?sampleId=20&testOrderId=102&open=incubation&incubationId=202"
    );
  });
});

describe("MyWorkPage in Arabic", () => {
  afterEach(async () => {
    await resetLanguage();
  });

  it("lays the page out right to left and keeps the sample identifier left to right", async () => {
    vi.mocked(myWorkService.get).mockResolvedValue(makeResponse());

    await renderInArabic(
      <AuthProvider>
        <MemoryRouter initialEntries={["/my-work"]}>
          <MyWorkPage />
        </MemoryRouter>
      </AuthProvider>,
      ["common", "myWork"]
    );

    expect(document.documentElement.dir).toBe("rtl");
    expect(await screen.findByRole("heading", { name: "عملي" })).toBeTruthy();
    const identifier = await screen.findByText("SMP-1001");
    expect(identifier.closest("bdi")?.getAttribute("dir")).toBe("ltr");
  });
});
