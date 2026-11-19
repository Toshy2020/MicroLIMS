import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { toast } from "sonner";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { NotificationsPage } from "./NotificationsPage";
import {
  notificationService,
  type NotificationDto,
  type PagedResult
} from "./services/notificationService";

vi.mock("./services/notificationService", () => ({
  notificationService: {
    list: vi.fn(),
    summary: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn(),
    getPreferences: vi.fn(),
    putPreferences: vi.fn(),
    getMandatory: vi.fn(),
    putMandatory: vi.fn(),
    getEmailOutboxSummary: vi.fn()
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

function makeNotification(over: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: 1,
    type: "ReviewWaiting",
    title: "Sample awaiting review",
    message: "Sample SMP-101 is awaiting review.",
    severity: "info",
    createdAt: "2026-11-15T08:30:00Z",
    readAt: null,
    resolvedAt: null,
    target: {
      type: "Sample",
      id: 101,
      sampleId: 101,
      testOrderId: 202
    },
    sectionName: "Microbiology",
    ...over
  };
}

function makePage(
  items: NotificationDto[],
  over: Partial<PagedResult<NotificationDto>> = {}
): PagedResult<NotificationDto> {
  return {
    items,
    page: 1,
    pageNumber: 1,
    pageSize: 25,
    totalCount: items.length,
    totalPages: Math.max(1, Math.ceil(items.length / 25)),
    ...over
  };
}

function renderPage(initialEntry = "/notifications") {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route
            path="/notifications"
            element={
              <>
                <NotificationsPage />
                <LocationEcho />
              </>
            }
          />
          <Route path="/receiving-testing" element={<LocationEcho />} />
          <Route path="/laboratory-configuration/media" element={<LocationEcho />} />
          <Route path="*" element={<LocationEcho />} />
        </Routes>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("NotificationsPage", () => {
  it("lists rows with severity, title, message, section, and formatted time with time zone", async () => {
    const row = makeNotification({
      id: 10,
      severity: "warning",
      type: "MediaExpiry",
      title: "Media lot expiring or expired",
      message: "TSA lot M-2026 expires tomorrow.",
      sectionName: "Media Prep",
      createdAt: "2026-11-15T09:15:00Z",
      target: { type: "Media", id: 5, sampleId: null, testOrderId: null }
    });
    vi.mocked(notificationService.list).mockResolvedValue(makePage([row]));

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("TSA lot M-2026 expires tomorrow.")).toBeTruthy();
    });

    expect(screen.getByText("Warning")).toBeTruthy();
    expect(screen.getByText("Media lot expiring or expired")).toBeTruthy();
    expect(screen.getByText("Media Prep")).toBeTruthy();
    expect(screen.getByText(formatDateTimeWithTimeZone("2026-11-15T09:15:00Z"))).toBeTruthy();
    expect(notificationService.list).toHaveBeenCalledWith(
      { state: "all", type: undefined, q: undefined, page: 1, pageSize: 25 },
      expect.any(AbortSignal)
    );
  });

  it("filters by state tabs including Done for resolved items, and filters by type and search", async () => {
    const user = userEvent.setup();
    const initialRow = makeNotification({ id: 1, message: "Open review task" });
    const doneRow = makeNotification({
      id: 2,
      title: "Test assigned",
      message: "Completed test assignment",
      resolvedAt: "2026-11-15T10:00:00Z"
    });

    vi.mocked(notificationService.list).mockImplementation(async (params) => {
      if (params?.state === "done") {
        return makePage([doneRow]);
      }
      if (params?.type === "MediaExpiry") {
        return makePage([
          makeNotification({
            id: 3,
            type: "MediaExpiry",
            title: "Media lot expiring or expired",
            message: "MacConkey lot expiring",
            severity: "warning",
            target: { type: "Media", id: 9, sampleId: null, testOrderId: null }
          })
        ]);
      }
      if (params?.q === "urgent") {
        return makePage([makeNotification({ id: 4, message: "Urgent calibration due" })]);
      }
      return makePage([initialRow]);
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Open review task")).toBeTruthy();
    });

    // Switch to Done tab -> resolved items appear
    await user.click(screen.getByRole("tab", { name: "Done" }));
    await waitFor(() => {
      expect(screen.getByText("Completed test assignment")).toBeTruthy();
    });
    expect(notificationService.list).toHaveBeenLastCalledWith(
      { state: "done", type: undefined, q: undefined, page: 1, pageSize: 25 },
      expect.any(AbortSignal)
    );

    // Switch to Unread tab
    await user.click(screen.getByRole("tab", { name: "Unread" }));
    await waitFor(() => {
      expect(notificationService.list).toHaveBeenLastCalledWith(
        { state: "unread", type: undefined, q: undefined, page: 1, pageSize: 25 },
        expect.any(AbortSignal)
      );
    });

    // Filter by type
    await user.click(screen.getByRole("combobox", { name: "Type" }));
    const listbox = await screen.findByRole("listbox");
    await user.click(within(listbox).getByRole("option", { name: "Media lot expiring or expired" }));
    await waitFor(() => {
      expect(screen.getByText("MacConkey lot expiring")).toBeTruthy();
    });
    expect(notificationService.list).toHaveBeenLastCalledWith(
      { state: "unread", type: "MediaExpiry", q: undefined, page: 1, pageSize: 25 },
      expect.any(AbortSignal)
    );

    // Filter by search query
    const searchInput = screen.getByRole("textbox", { name: "Search notifications" });
    await user.type(searchInput, "urgent");
    await waitFor(() => {
      expect(notificationService.list).toHaveBeenLastCalledWith(
        { state: "unread", type: "MediaExpiry", q: "urgent", page: 1, pageSize: 25 },
        expect.any(AbortSignal)
      );
    });
  });

  it("pages through server results with page size 25", async () => {
    const user = userEvent.setup();
    const page1Items = Array.from({ length: 25 }, (_, i) =>
      makeNotification({ id: i + 1, message: `Notification row ${i + 1}` })
    );
    const page2Items = [makeNotification({ id: 26, message: "Notification row 26" })];

    vi.mocked(notificationService.list).mockImplementation(async (params) => {
      if (params?.page === 2) {
        return makePage(page2Items, { page: 2, pageNumber: 2, totalCount: 26, totalPages: 2 });
      }
      return makePage(page1Items, { page: 1, pageNumber: 1, totalCount: 26, totalPages: 2 });
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Notification row 1")).toBeTruthy();
    });
    expect(screen.getByText("1–25 of 26")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Go to next page" }));

    await waitFor(() => {
      expect(screen.getByText("Notification row 26")).toBeTruthy();
    });
    expect(notificationService.list).toHaveBeenLastCalledWith(
      { state: "all", type: undefined, q: undefined, page: 2, pageSize: 25 },
      expect.any(AbortSignal)
    );
  });

  it("clicking an unread row marks it read and navigates to its sample or type route", async () => {
    const user = userEvent.setup();
    const sampleNotif = makeNotification({
      id: 77,
      type: "ReviewWaiting",
      message: "Sample SMP-77 ready for review",
      readAt: null,
      target: { type: "Sample", id: 77, sampleId: 501, testOrderId: 602 }
    });
    vi.mocked(notificationService.list).mockResolvedValue(makePage([sampleNotif]));
    vi.mocked(notificationService.markRead).mockResolvedValue(undefined);

    renderPage();

    const messageCell = await screen.findByText("Sample SMP-77 ready for review");
    await user.click(messageCell);

    await waitFor(() => {
      expect(notificationService.markRead).toHaveBeenCalledWith(77);
      expect(screen.getByTestId("location-echo").textContent).toBe("/receiving-testing?sampleId=501&testOrderId=602");
    });
  });

  it("clicking a non-sample notification uses NOTIFICATION_ROUTES after marking read", async () => {
    const user = userEvent.setup();
    const mediaNotif = makeNotification({
      id: 88,
      type: "MediaExpiry",
      title: "Media lot expiring or expired",
      message: "Media lot M-88 expiring",
      readAt: null,
      target: { type: "Media", id: 88, sampleId: null, testOrderId: null }
    });
    vi.mocked(notificationService.list).mockResolvedValue(makePage([mediaNotif]));
    vi.mocked(notificationService.markRead).mockResolvedValue(undefined);

    renderPage();

    const messageCell = await screen.findByText("Media lot M-88 expiring");
    await user.click(messageCell);

    await waitFor(() => {
      expect(notificationService.markRead).toHaveBeenCalledWith(88);
      expect(screen.getByTestId("location-echo").textContent).toBe("/laboratory-configuration/media");
    });
  });

  it("marks all notifications read and reloads the list", async () => {
    const user = userEvent.setup();
    vi.mocked(notificationService.list)
      .mockResolvedValueOnce(makePage([makeNotification({ id: 1, readAt: null, message: "Unread item" })]))
      .mockResolvedValueOnce(
        makePage([makeNotification({ id: 1, readAt: "2026-11-15T11:00:00Z", message: "Unread item" })])
      );
    vi.mocked(notificationService.markAllRead).mockResolvedValue(undefined);

    renderPage();

    await waitFor(() => {
      expect(screen.getByText("Unread item")).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: "Mark all read" }));

    await waitFor(() => {
      expect(notificationService.markAllRead).toHaveBeenCalledTimes(1);
      expect(toast.success).toHaveBeenCalledWith("All notifications marked as read.");
      expect(notificationService.list).toHaveBeenCalledTimes(2);
    });
  });

  it("renders loading, empty, and error states with toast.error and retry", async () => {
    const user = userEvent.setup();

    // 1. Loading state
    vi.mocked(notificationService.list).mockImplementationOnce(() => new Promise(() => {}));
    const { unmount } = renderPage();
    expect(screen.getByRole("button", { name: "Mark all read" })).toHaveProperty("disabled", true);
    unmount();

    // 2. Empty state
    vi.mocked(notificationService.list).mockResolvedValueOnce(makePage([], { totalCount: 0 }));
    const emptyView = renderPage();
    await waitFor(() => {
      expect(screen.getByText("No notifications")).toBeTruthy();
    });
    emptyView.unmount();

    // 3. Error state + toast.error + retry
    vi.mocked(notificationService.list)
      .mockRejectedValueOnce(new Error("Server error"))
      .mockResolvedValueOnce(makePage([makeNotification({ id: 9, message: "Recovered row" })]));

    renderPage();

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith("Could not load notifications.");
      expect(screen.getByRole("alert")).toBeTruthy();
    });

    await user.click(screen.getAllByRole("button", { name: "Retry" })[0]!);

    await waitFor(() => {
      expect(screen.getByText("Recovered row")).toBeTruthy();
    });
  });
});
