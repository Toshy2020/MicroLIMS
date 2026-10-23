import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { DeviationsPanel } from "./DeviationsPanel";
import { MonitoringDeviationApi } from "../services/MonitoringPlanService";
import type { Deviation, DeviationList } from "../types";

vi.mock("../../../contexts/AuthContext", () => ({ useHasPermission: () => true }));
vi.mock("../services/MonitoringPlanService", () => ({
  MonitoringDeviationApi: { list: vi.fn(), scan: vi.fn(), close: vi.fn() }
}));

const missed = (id: number, target: string, windowEnd: string): Deviation => ({
  id, version: 1, number: `MD-2026-0000${id}`, kind: "MissedOccurrence", status: "Open", occurrenceId: 100 + id,
  category: "Water", target, group: "Water Treatment Unit", periodKey: `2026-W0${id}`, targetDate: null,
  windowStart: windowEnd, windowEnd, description: `${target}: nothing was sampled.`, openedAt: "2026-10-23T08:00:00Z",
  dueDate: "2026-11-22", pastDue: false, followUpOccurrenceId: null, closure: null, closureReason: null, closedAt: null
});

const excursion: Deviation = {
  ...missed(9, "SP 303", "2026-10-20"), kind: "ActionLimitExceeded", description: "SP 303: TAMC over the action limit (sample W-9).",
  followUpOccurrenceId: 500
};

const list: DeviationList = {
  openCount: 4, pastDueCount: 0,
  items: [missed(1, "SP 206", "2026-01-08"), missed(2, "SP 206", "2026-01-15"), missed(3, "SP 303", "2026-01-09"), excursion]
};

const renderPanel = (onPick = vi.fn()) =>
  render(<ThemeProvider theme={lightTheme}><DeviationsPanel onPick={onPick} /></ThemeProvider>);

describe("DeviationsPanel", () => {
  beforeEach(() => { vi.mocked(MonitoringDeviationApi.list).mockResolvedValue(list); });
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it("lists missed occurrences once per sampling point, with their dates, apart from excursions", async () => {
    const onPick = vi.fn();
    renderPanel(onPick);

    expect(await screen.findByText("Missed occurrences · 2 sampling point(s)")).toBeTruthy();
    expect(screen.getAllByText("SP 206")).toHaveLength(1);
    expect(screen.getByText("Results over the action limit · 1")).toBeTruthy();

    fireEvent.click(screen.getByLabelText("SP 206, 15 Jan"));
    expect(onPick).toHaveBeenCalledWith(102);
    fireEvent.click(screen.getByText("Open the follow-up sampling"));
    expect(onPick).toHaveBeenCalledWith(500);
  });

  it("closes the selected group with the chosen reason and signatures", async () => {
    vi.mocked(MonitoringDeviationApi.close).mockResolvedValue(2);
    renderPanel();
    await screen.findByText("Missed occurrences · 2 sampling point(s)");

    fireEvent.click(screen.getByLabelText("Select SP 206"));
    fireEvent.click(screen.getByText("Close selected (2)"));
    fireEvent.click(screen.getByLabelText("Not applicable: it was before the plan went live"));
    fireEvent.change(screen.getByLabelText(/^Reason/), { target: { value: "The plan went live in October" } });
    fireEvent.change(screen.getByLabelText(/^Your password/), { target: { value: "pw-1" } });
    fireEvent.change(screen.getByLabelText(/^Second signer username/), { target: { value: "approver" } });
    fireEvent.change(screen.getByLabelText(/^Second signer password/), { target: { value: "pw-2" } });
    fireEvent.click(screen.getByText("Sign and close"));

    await waitFor(() => expect(MonitoringDeviationApi.close).toHaveBeenCalledTimes(1));
    expect(MonitoringDeviationApi.close).toHaveBeenCalledWith([1, 2], "NotApplicableBeforeGoLive", {
      reason: "The plan went live in October", password: "pw-1", secondSignerUsername: "approver", secondSignerPassword: "pw-2"
    });
  });

  it("does not let missed occurrences and excursions be closed together", async () => {
    renderPanel();
    await screen.findByText("Missed occurrences · 2 sampling point(s)");

    fireEvent.click(screen.getByLabelText("Select SP 303"));
    fireEvent.click(screen.getByLabelText("Select MD-2026-00009"));

    expect((screen.getByText("Close selected (2)") as HTMLButtonElement).closest("button")!.disabled).toBe(true);
    expect(screen.getByText(/not both/)).toBeTruthy();
  });

  it("scans on demand and reports what it opened", async () => {
    vi.mocked(MonitoringDeviationApi.scan).mockResolvedValue({ missedOpened: 3, excursionsOpened: 1, followUpsCreated: 1 });
    renderPanel();
    await screen.findByText("Missed occurrences · 2 sampling point(s)");

    fireEvent.click(screen.getByText("Scan now"));

    expect(await screen.findByText("Opened 3 for missed occurrences and 1 for results over the action limit.")).toBeTruthy();
  });
});
