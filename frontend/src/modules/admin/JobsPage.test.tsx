import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { JobsPage } from "./JobsPage";
import { BackgroundJobService, BackgroundJobStatus } from "./services/BackgroundJobService";

vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock("./services/BackgroundJobService", () => ({
  BackgroundJobService: { list: vi.fn(), runs: vi.fn(), run: vi.fn() }
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const base = {
  description: "d", classificationReason: "why", schedule: "Every 24 hours", lastRun: null,
  lastSuccessAt: null, lastFailureAt: null, lastPassFinishedAt: null, runningNow: false
};
const jobs: BackgroundJobStatus[] = [
  { ...base, name: "SessionTokenCleanupWorker", classification: "Housekeeping", canRunManually: true },
  { ...base, name: "EquipmentOverdueWorker", classification: "Gxp", canRunManually: false }
];

describe("JobsPage", () => {
  it("offers Run now for housekeeping jobs only", async () => {
    vi.mocked(BackgroundJobService.list).mockResolvedValue(jobs);
    render(<PinnedLightTheme><JobsPage /></PinnedLightTheme>);

    expect(await screen.findByText("EquipmentOverdueWorker")).toBeTruthy();
    expect(screen.getAllByRole("button", { name: "Run now" })).toHaveLength(1);
    expect(screen.getByText("GxP (status only)")).toBeTruthy();
  });

  it("requires a reason before running, then sends it", async () => {
    vi.mocked(BackgroundJobService.list).mockResolvedValue(jobs);
    vi.mocked(BackgroundJobService.run).mockResolvedValue({
      id: 1, jobName: "SessionTokenCleanupWorker", startedAt: "2026-10-27T10:00:00Z", finishedAt: null,
      outcome: "Succeeded", itemsProcessed: 2, error: null, triggeredBy: "Manual", triggeredByUserId: 1, reason: "r"
    });
    render(<PinnedLightTheme><JobsPage /></PinnedLightTheme>);

    fireEvent.click(await screen.findByRole("button", { name: "Run now" }));
    const confirm = screen.getAllByRole("button", { name: "Run now" }).find((b) => b.closest("[role=dialog]"))!;
    expect((confirm as HTMLButtonElement).disabled).toBe(true);

    fireEvent.change(screen.getByRole("textbox", { name: /Reason/ }), { target: { value: "Pre-audit cleanup" } });
    fireEvent.click(confirm);

    await waitFor(() => expect(BackgroundJobService.run).toHaveBeenCalledWith("SessionTokenCleanupWorker", "Pre-audit cleanup"));
  });

  it("opens the run history drawer", async () => {
    vi.mocked(BackgroundJobService.list).mockResolvedValue(jobs);
    vi.mocked(BackgroundJobService.runs).mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    render(<PinnedLightTheme><JobsPage /></PinnedLightTheme>);

    fireEvent.click((await screen.findAllByRole("button", { name: "History" }))[0]);
    expect(await screen.findByText("No runs recorded yet.")).toBeTruthy();
  });
});
