import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { notificationService } from "./notificationService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn()
  }
}));

beforeEach(() => {
  vi.clearAllMocks();
});

describe("notificationService", () => {
  it("list passes filters, paging, and signal to GET /notifications", async () => {
    const payload = {
      items: [],
      page: 2,
      pageNumber: 2,
      pageSize: 25,
      totalCount: 30,
      totalPages: 2
    };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: payload } });
    const controller = new AbortController();

    const result = await notificationService.list(
      { state: "unread", type: "ReviewWaiting", sectionId: 4, q: "SMP", page: 2, pageSize: 25 },
      controller.signal
    );

    expect(apiClient.get).toHaveBeenCalledWith("/notifications", {
      params: { state: "unread", type: "ReviewWaiting", sectionId: 4, q: "SMP", page: 2, pageSize: 25 },
      signal: controller.signal
    });
    expect(result).toEqual(payload);
  });

  it("summary calls GET /notifications/summary with signal", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: { unread: 7 } } });
    const controller = new AbortController();

    const result = await notificationService.summary(controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/notifications/summary", { signal: controller.signal });
    expect(result).toEqual({ unread: 7 });
  });

  it("markRead and markAllRead post to the notification read endpoints", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { data: {} } });

    await notificationService.markRead(42);
    expect(apiClient.post).toHaveBeenCalledWith("/notifications/42/read");

    await notificationService.markAllRead();
    expect(apiClient.post).toHaveBeenCalledWith("/notifications/read-all");
  });

  it("getPreferences and putPreferences call /notification-preferences", async () => {
    const rows = [
      {
        type: "MediaExpiry",
        title: "Media lot expiring or expired",
        emailAllowed: true,
        emailMode: "Digest",
        mandatory: false,
        mandatoryReason: null
      }
    ];
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: rows } });
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: rows } });
    const controller = new AbortController();

    const fetched = await notificationService.getPreferences(controller.signal);
    expect(apiClient.get).toHaveBeenCalledWith("/notification-preferences", { signal: controller.signal });
    expect(fetched).toEqual(rows);

    const updated = await notificationService.putPreferences([{ type: "MediaExpiry", emailMode: "Digest" }]);
    expect(apiClient.put).toHaveBeenCalledWith("/notification-preferences", {
      preferences: [{ type: "MediaExpiry", emailMode: "Digest" }]
    });
    expect(updated).toEqual(rows);
  });

  it("getMandatory and putMandatory pass sectionId query parameter", async () => {
    const rows = [
      {
        type: "MediaExpiry",
        title: "Media lot expiring or expired",
        emailAllowed: true,
        mandatory: true,
        setByUserId: 1,
        setAt: "2026-11-15T08:00:00Z"
      }
    ];
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: rows } });
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: rows } });
    const controller = new AbortController();

    const fetched = await notificationService.getMandatory(9, controller.signal);
    expect(apiClient.get).toHaveBeenCalledWith("/notification-preferences/mandatory", {
      params: { sectionId: 9 },
      signal: controller.signal
    });
    expect(fetched).toEqual(rows);

    const updated = await notificationService.putMandatory(9, ["MediaExpiry"]);
    expect(apiClient.put).toHaveBeenCalledWith(
      "/notification-preferences/mandatory",
      { types: ["MediaExpiry"] },
      { params: { sectionId: 9 } }
    );
    expect(updated).toEqual(rows);
  });

  it("getEmailOutboxSummary calls GET /admin/email-outbox/summary with signal", async () => {
    const summary = {
      waiting: 2,
      failed: 1,
      lastSentAt: "2026-11-15T07:30:00Z",
      usersWithoutEmail: []
    };
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: summary } });
    const controller = new AbortController();

    const result = await notificationService.getEmailOutboxSummary(controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/admin/email-outbox/summary", { signal: controller.signal });
    expect(result).toEqual(summary);
  });
});
