import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReportingService } from "../../reports/services/ReportingService";
import type { KpiFiltersValue } from "../utils/kpiFilters";
import { KpiFilters } from "./KpiFilters";

vi.mock("../../reports/services/ReportingService", () => ({
  ReportingService: { getFilterOptions: vi.fn() }
}));

afterEach(cleanup);
beforeEach(() => {
  vi.mocked(ReportingService.getFilterOptions).mockReset();
  vi.mocked(ReportingService.getFilterOptions).mockResolvedValue({
    categories: ["FinishedProduct", "Water"], testCodes: [{ testCode: "TAMC", testDisplayName: "TAMC" }],
    subjectNames: ["Product A"], units: []
  } as never);
});

const value: KpiFiltersValue = { from: "2026-09-12", to: "2026-10-11", category: null, location: null, testCode: null };

function renderFilters(over: {
  onChange?: (v: KpiFiltersValue) => void;
  exportDisabled?: boolean;
  onOpenProfiles?: () => void;
} = {}) {
  const handlers = {
    onChange: over.onChange ?? vi.fn(),
    onOpenDefinitions: vi.fn(),
    onOpenProfiles: "onOpenProfiles" in over ? over.onOpenProfiles : vi.fn(),
    onExport: vi.fn()
  };
  render(
    <PinnedLightTheme>
      <KpiFilters value={value} exportDisabled={over.exportDisabled ?? false} {...handlers} />
    </PinnedLightTheme>
  );
  return handlers;
}

describe("KpiFilters", () => {
  it("reports a valid new range", () => {
    const { onChange } = renderFilters();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-09-20" } });
    expect(onChange).toHaveBeenCalledWith({ ...value, from: "2026-09-20" });
  });

  it("does not call onChange for an inverted or empty range", () => {
    const { onChange } = renderFilters();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-10-20" } });
    expect(screen.getByText("The start date must not be after the end date.")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("From"), { target: { value: "" } });
    expect(screen.getByText("Choose both dates.")).toBeTruthy();
    expect(onChange).not.toHaveBeenCalled();
  });

  it("offers the categories with their readable labels and reports a choice", async () => {
    const { onChange } = renderFilters();
    await userEvent.click(screen.getByRole("combobox", { name: "Category" }));
    await userEvent.click(await screen.findByRole("option", { name: "Finished Product" }));
    expect(onChange).toHaveBeenCalledWith({ ...value, category: "FinishedProduct" });
  });

  it("tells the user when the option lists could not be loaded", async () => {
    vi.mocked(ReportingService.getFilterOptions).mockRejectedValue(new Error("boom"));
    renderFilters();
    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(screen.getByText(/could not be loaded/)).toBeTruthy();
  });

  it("omits the Workflow Profiles button when onOpenProfiles is not provided", () => {
    renderFilters({ onOpenProfiles: undefined });
    expect(screen.queryByRole("button", { name: "Workflow Profiles" })).toBeNull();
  });

  it("wires the three buttons and disables export on request", async () => {
    const h = renderFilters({ exportDisabled: true });
    await userEvent.click(screen.getByRole("button", { name: "Definitions" }));
    await userEvent.click(screen.getByRole("button", { name: "Workflow Profiles" }));
    expect(h.onOpenDefinitions).toHaveBeenCalled();
    expect(h.onOpenProfiles).toHaveBeenCalled();
    expect((screen.getByRole("button", { name: "Export PDF" }) as HTMLButtonElement).disabled).toBe(true);
  });
});
