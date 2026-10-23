import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { MonthGrid } from "./MonthGrid";
import type { CalendarEventResponse, EquipmentTaskItem, OccurrenceSummary } from "../types";

const occurrence: OccurrenceSummary = {
  id: 7, version: 1, category: "EnvironmentalMonitoring", target: "Room A", group: "Filling", periodKey: "2026-W43",
  targetDate: "2026-10-28", windowStart: "2026-10-25", windowEnd: "2026-10-29", originalWindowEnd: "2026-10-29",
  status: "Upcoming", late: false, itemsTotal: 1, itemsPerformed: 0, manual: false
};

const task: EquipmentTaskItem = {
  id: 1,
  equipmentId: 10,
  equipmentCode: "EQ-BAL-01",
  instrumentType: "Balance",
  type: "Calibration",
  originalDueDate: "2026-10-15",
  dueDate: "2026-10-15",
  completedOn: null,
  status: "Due",
  evidenceLabel: null
};

const calEvent: CalendarEventResponse = {
  id: 2,
  type: "Audit",
  title: "ISO Internal Audit",
  date: "2026-10-20",
  month: null,
  note: null,
  equipmentInventoryId: null,
  equipmentCode: null,
  status: "Active",
  reportPending: false,
  documents: [],
  version: 1
};

describe("MonthGrid", () => {
  afterEach(() => {
    cleanup();
  });

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

  it("renders an equipment task and an event on their dates; clicking an empty day calls onAddEvent(\"2026-10-25\")", () => {
    const onPickTask = vi.fn();
    const onPickEvent = vi.fn();
    const onAddEvent = vi.fn();

    render(
      <ThemeProvider theme={lightTheme}>
        <MonthGrid
          year={2026}
          month={9} // October (0-indexed 9)
          occurrences={[]}
          tasks={[task]}
          events={[calEvent]}
          today="2026-10-21"
          onPick={vi.fn()}
          onPickTask={onPickTask}
          onPickEvent={onPickEvent}
          onAddEvent={onAddEvent}
        />
      </ThemeProvider>
    );

    // Verify task is rendered
    expect(screen.getByText(/EQ-BAL-01/)).toBeTruthy();
    // Verify event is rendered
    expect(screen.getByText("ISO Internal Audit")).toBeTruthy();

    // Clicking task calls onPickTask
    fireEvent.click(screen.getByText(/EQ-BAL-01/));
    expect(onPickTask).toHaveBeenCalledWith(task);

    // Clicking event calls onPickEvent
    fireEvent.click(screen.getByText("ISO Internal Audit"));
    expect(onPickEvent).toHaveBeenCalledWith(calEvent);

    // Day 25 has no items, clicking it calls onAddEvent("2026-10-25")
    fireEvent.click(screen.getByText("25"));
    expect(onAddEvent).toHaveBeenCalledWith("2026-10-25");
  });
});
