import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
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

vi.mock("./services/myWorkService", () => ({
  myWorkService: {
    get: vi.fn(),
    summary: vi.fn(),
    take: vi.fn(),
    release: vi.fn()
  }
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
