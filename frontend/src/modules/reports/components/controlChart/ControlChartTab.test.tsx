import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ControlChartTab, presetControlChart } from "./ControlChartTab";
import { ControlChartService } from "../../services/ControlChartService";

vi.mock("../../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));
vi.mock("../../services/ReportingService", () => ({
  ReportingService: { getFilterOptions: vi.fn().mockResolvedValue({ testCodes: [], subjectNames: [], categories: [] }) }
}));
// Never resolves: the test is about which criteria are requested, not the chart drawn.
vi.mock("../../services/ControlChartService", () => ({
  ControlChartService: { getChart: vi.fn(() => new Promise(() => {})), getLimitReview: vi.fn(() => new Promise(() => {})) }
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); sessionStorage.clear(); });

const stored = () => JSON.parse(sessionStorage.getItem("microlims.controlCharts.session") ?? "{}");

describe("ControlChartTab session state", () => {
  it("reopens on the criteria kept for the session, and Reset clears them", async () => {
    presetControlChart({ category: "FinishedProduct", testCode: "TAMC", subjectName: "Osteocare Liquid" });
    const { rerender } = render(<ControlChartTab />);

    await waitFor(() => expect(ControlChartService.getChart).toHaveBeenCalledWith(
      expect.objectContaining({ category: "FinishedProduct", testCode: "TAMC", subjectName: "Osteocare Liquid" }),
      expect.anything()
    ));

    // A re-render with unchanged criteria must not fetch again (it used to loop).
    rerender(<ControlChartTab />);
    await new Promise((r) => setTimeout(r, 600));
    expect(ControlChartService.getChart).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: /Reset/ }));

    await waitFor(() => expect(stored().criteria).toMatchObject({ category: "", testCode: "", subjectName: "" }));
    expect(screen.getByText("Choose a sample category and a test to analyse.")).toBeTruthy();
  });
});
