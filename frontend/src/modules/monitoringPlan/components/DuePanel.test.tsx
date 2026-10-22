import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { DuePanel } from "./DuePanel";
import type { DueResult, OccurrenceSummary } from "../types";

const occ = (id: number, target: string, windowEnd: string, status: OccurrenceSummary["status"]): OccurrenceSummary => ({
  id, version: 1, category: "Water", target, group: "Water Treatment Unit", periodKey: `p${id}`, targetDate: null,
  windowStart: windowEnd, windowEnd, originalWindowEnd: windowEnd, status, late: false, itemsTotal: 1, itemsPerformed: 0, manual: false
});

const due: DueResult = {
  today: "2026-10-22", warningDayOfMonth: 20, showWarning: true,
  items: [
    occ(1, "SP 206", "2026-01-08", "Missed"), occ(2, "SP 206", "2026-01-15", "Missed"), occ(3, "SP 303", "2026-01-09", "Missed"),
    occ(4, "SP 206", "2026-10-29", "Upcoming")
  ]
};

const renderPanel = (onPick = vi.fn()) =>
  render(<ThemeProvider theme={lightTheme}><DuePanel due={due} onPick={onPick} /></ThemeProvider>);

describe("DuePanel", () => {
  afterEach(cleanup);

  it("opens on this month and keeps the backlog out of it", () => {
    renderPanel();
    expect(screen.getByText("This month (1)")).toBeTruthy();
    expect(screen.queryByText("Missed on")).toBeNull();
  });

  it("lists earlier months once per sampling point with the dates it was missed", () => {
    const onPick = vi.fn();
    renderPanel(onPick);
    fireEvent.click(screen.getByText("Earlier months (3)"));

    expect(screen.getAllByText("SP 206")).toHaveLength(1);
    expect(screen.getByText("Missed ×2")).toBeTruthy();
    expect(screen.getByText("Missed ×1")).toBeTruthy();

    fireEvent.click(screen.getByText("15 Jan"));
    expect(onPick).toHaveBeenCalledWith(due.items[1]);
  });
});
