import { describe, expect, it, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { WorkbookReportTable } from "./WorkbookReportTable";
import { canLoad } from "./types";
import type { WorkbookReport } from "./types";

afterEach(cleanup);

const clean: WorkbookReport = { sheets: [{ sheet: "Organisms", created: 3, unchanged: 1, errors: 0 }], errors: [], hasErrors: false };
const failing: WorkbookReport = {
  sheets: [{ sheet: "EmRooms", created: 0, unchanged: 0, errors: 1 }],
  errors: [{ sheet: "EmRooms", row: 4, column: "Department", message: "No EM department named 'X'." }],
  hasErrors: true,
};

describe("WorkbookReportTable", () => {
  it("shows per-sheet counts", () => {
    render(<WorkbookReportTable report={clean} />);
    expect(screen.getByText("Organisms")).toBeTruthy();
    expect(screen.getByText("3")).toBeTruthy();
  });

  it("lists each error with sheet, row and column", () => {
    render(<WorkbookReportTable report={failing} />);
    expect(screen.getByText("No EM department named 'X'.")).toBeTruthy();
    expect(screen.getByText("Department")).toBeTruthy();
    expect(screen.getByText("4")).toBeTruthy();
  });
});

describe("canLoad", () => {
  it("only with a report that has no errors", () => {
    expect(canLoad(null)).toBe(false);
    expect(canLoad(failing)).toBe(false);
    expect(canLoad(clean)).toBe(true);
  });
});
