import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { AxiosError, AxiosHeaders, InternalAxiosRequestConfig } from "axios";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { SystemStatusBanner } from "./SystemStatusBanner";
import { SystemStatusProvider } from "../contexts/SystemStatusContext";
import { SystemStatus, SystemStatusService } from "../services/systemStatusService";
import { apiClient } from "../services/apiClient";
import { MAINTENANCE_EVENT } from "../services/maintenanceSignal";

const toastWarning = vi.fn();
vi.mock("sonner", () => ({ toast: { warning: (...a: unknown[]) => toastWarning(...a), success: vi.fn(), error: vi.fn() } }));

afterEach(() => { cleanup(); vi.restoreAllMocks(); toastWarning.mockClear(); });

const status = (over: Partial<SystemStatus>): SystemStatus =>
  ({ maintenance: false, message: "", banner: "", severity: "info", ...over });

function renderBanner(s: SystemStatus) {
  vi.spyOn(SystemStatusService, "get").mockResolvedValue(s);
  return render(
    <PinnedLightTheme>
      <SystemStatusProvider><SystemStatusBanner /></SystemStatusProvider>
    </PinnedLightTheme>
  );
}

describe("SystemStatusBanner", () => {
  it("shows nothing when there is no announcement and maintenance is off", async () => {
    renderBanner(status({}));
    await waitFor(() => expect(SystemStatusService.get).toHaveBeenCalled());
    expect(screen.queryByRole("status")).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("shows the announcement with its text", async () => {
    renderBanner(status({ banner: "Planned downtime on Friday", severity: "warning" }));
    const banner = await screen.findByRole("status", { name: "Announcement" });
    expect(banner.textContent).toContain("Planned downtime on Friday");
  });

  it("shows a full-width maintenance notice with the message", async () => {
    renderBanner(status({ maintenance: true, message: "Back at 18:00 UTC" }));
    const notice = await screen.findByRole("alert", { name: "Maintenance mode" });
    expect(notice.textContent).toContain("Back at 18:00 UTC");
  });

  it("shows both when maintenance is on and an announcement is set", async () => {
    renderBanner(status({ maintenance: true, message: "Upgrading", banner: "Read-only until 18:00" }));
    expect(await screen.findByRole("alert", { name: "Maintenance mode" })).toBeTruthy();
    expect(screen.getByRole("status", { name: "Announcement" })).toBeTruthy();
  });

  it("tolerates an unreachable status endpoint", async () => {
    vi.spyOn(SystemStatusService, "get").mockRejectedValue(new Error("offline"));
    render(<PinnedLightTheme><SystemStatusProvider><SystemStatusBanner /></SystemStatusProvider></PinnedLightTheme>);
    await waitFor(() => expect(SystemStatusService.get).toHaveBeenCalled());
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("shows the server's message and refreshes the banner when a write is refused with 503", async () => {
    const get = vi.spyOn(SystemStatusService, "get").mockResolvedValue(status({}));
    render(<PinnedLightTheme><SystemStatusProvider><SystemStatusBanner /></SystemStatusProvider></PinnedLightTheme>);
    await waitFor(() => expect(get).toHaveBeenCalledTimes(1));
    get.mockResolvedValue(status({ maintenance: true, message: "Upgrading the database" }));

    const config = { headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
    const refused = new AxiosError("Request failed with status code 503", "ERR_BAD_RESPONSE", config, null, {
      status: 503, statusText: "Service Unavailable", config,
      headers: { "x-maintenance-mode": "on" },
      data: { success: false, message: "Upgrading the database" }
    });
    // Run the refusal through the real client interceptors.
    const handlers = (apiClient.interceptors.response as unknown as { handlers: Array<{ rejected: (e: AxiosError) => Promise<unknown> }> }).handlers;
    let caught: AxiosError | undefined;
    await act(async () => {
      await handlers.find((h) => h?.rejected)!.rejected(refused).catch((e: AxiosError) => { caught = e; });
    });

    expect(caught?.message).toBe("Upgrading the database");
    expect(toastWarning).toHaveBeenCalledWith("Upgrading the database", expect.objectContaining({ id: "maintenance-refusal" }));
    expect((await screen.findByRole("alert", { name: "Maintenance mode" })).textContent).toContain("Upgrading the database");
  });

  it("does not treat an ordinary 503 as maintenance", async () => {
    const config = { headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
    const plain = new AxiosError("Request failed with status code 503", "ERR_BAD_RESPONSE", config, null, {
      status: 503, statusText: "Service Unavailable", config, headers: {}, data: { message: "upstream" }
    });
    const listener = vi.fn();
    window.addEventListener(MAINTENANCE_EVENT, listener);
    const handlers = (apiClient.interceptors.response as unknown as { handlers: Array<{ rejected: (e: AxiosError) => Promise<unknown> }> }).handlers;
    await handlers.find((h) => h?.rejected)!.rejected(plain).catch(() => undefined);
    window.removeEventListener(MAINTENANCE_EVENT, listener);
    expect(listener).not.toHaveBeenCalled();
  });
});
