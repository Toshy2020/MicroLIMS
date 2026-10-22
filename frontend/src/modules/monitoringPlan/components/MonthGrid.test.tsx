import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { MonthGrid } from "./MonthGrid";
import type { OccurrenceSummary } from "../types";

const occurrence: OccurrenceSummary = {
  id: 7, version: 1, category: "EnvironmentalMonitoring", target: "Room A", group: "Filling", periodKey: "2026-W43",
  targetDate: "2026-10-28", windowStart: "2026-10-25", windowEnd: "2026-10-29", originalWindowEnd: "2026-10-29",
  status: "Upcoming", late: false, itemsTotal: 1, itemsPerformed: 0, manual: false
};

describe("MonthGrid", () => {
  it("starts the week on Sunday and shows the occurrence on its target day", () => {
    const onPick = vi.fn();
    render(
      <ThemeProvider theme={lightTheme}>
        <MonthGrid year={2026} month={9} occurrences={[occurrence]} today="2026-10-21" onPick={onPick} />
      </ThemeProvider>
    );

    const headers = screen.getAllByText(/^(Sun|Mon|Tue|Wed|Thu|Fri|Sat)$/).map((e) => e.textContent);
    expect(headers).toEqual(["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]);

    fireEvent.click(screen.getByText("Room A"));
    expect(onPick).toHaveBeenCalledWith(occurrence);
  });

  it("lists the days beyond the second occurrence in a popover", () => {
    const onPick = vi.fn();
    const more = [1, 2, 3, 4].map((n) => ({ ...occurrence, id: n, target: `Point ${n}` }));
    render(
      <ThemeProvider theme={lightTheme}>
        <MonthGrid year={2026} month={9} occurrences={more} today="2026-10-21" onPick={onPick} />
      </ThemeProvider>
    );

    expect(screen.queryByText("Point 4")).toBeNull();
    fireEvent.click(screen.getByText("+2 more"));
    fireEvent.click(screen.getByText("Point 4"));
    expect(onPick).toHaveBeenCalledWith(more[3]);
  });
});
