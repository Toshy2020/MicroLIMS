import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { kpiDashboardService } from "../services/kpiDashboardService";
import type { KpiDefinitions } from "../types/kpiTypes";
import { DefinitionsDrawer } from "./DefinitionsDrawer";

vi.mock("../services/kpiDashboardService", () => ({
  kpiDashboardService: { dashboard: vi.fn(), records: vi.fn(), definitions: vi.fn() }
}));

afterEach(cleanup);
beforeEach(() => vi.mocked(kpiDashboardService.definitions).mockReset());

const defs: KpiDefinitions = {
  kpis: [
    {
      key: "right-first-time", name: "Right first time", unit: "%", direction: "HigherIsBetter", group: "Headline", order: 3,
      definition: "Units of tests approved with no return divided by units approved.",
      clockEvents: "Test approval, return event", population: "Tests approved in the period", source: "TestOrder, TestReturnEvent"
    },
    {
      key: "sample-tat", name: "Sample TAT", unit: "days", direction: "LowerIsBetter", group: "Headline", order: 1,
      definition: "Days from receipt to approval.", clockEvents: "ReceivedAt to ApprovedAt",
      population: "Samples approved in the period", source: "Sample"
    }
  ],
  profilesInForce: [
    { workflowType: "CountTest", testCode: null, workUnits: 1.5, targetAnalystDays: 7, effectiveFrom: "2026-10-01", versionId: 4 },
    { workflowType: "Titration", testCode: "TITR-1", workUnits: null, targetAnalystDays: 5, effectiveFrom: "2026-10-03", versionId: 9 }
  ]
};

function renderDrawer(open = true, onClose: () => void = vi.fn()) {
  render(
    <PinnedLightTheme>
      <DefinitionsDrawer open={open} onClose={onClose} from="2026-09-12" to="2026-10-11" />
    </PinnedLightTheme>
  );
  return onClose;
}

describe("DefinitionsDrawer", () => {
  it("lists each KPI's definition, clock events, population and source in registry order", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue(defs);
    renderDrawer();
    expect(await screen.findByText("Days from receipt to approval.")).toBeTruthy();
    expect(screen.getByText("ReceivedAt to ApprovedAt")).toBeTruthy();
    expect(screen.getByText("Samples approved in the period")).toBeTruthy();
    expect(screen.getByText("TestOrder, TestReturnEvent")).toBeTruthy();
    expect(screen.getByText(/Lower is better/)).toBeTruthy(); // rendered inside "Unit: days · Lower is better"
    const order = screen.getAllByRole("heading", { name: /^(Sample TAT|Right first time)$/ }).map((h) => h.textContent);
    expect(order).toEqual(["Sample TAT", "Right first time"]);
    expect(kpiDashboardService.definitions).toHaveBeenCalledWith("2026-09-12", "2026-10-11");
  });

  it("shows the profile versions in force for the chosen period, with inherited values marked", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue(defs);
    renderDrawer();
    expect(await screen.findByRole("heading", { name: "Profile versions in force on 2026-10-11" })).toBeTruthy();
    expect(screen.getByText("CountTest")).toBeTruthy();
    expect(screen.getByText("Type default")).toBeTruthy();
    expect(screen.getByText("1.5")).toBeTruthy();
    expect(screen.getByText("TITR-1")).toBeTruthy();
    expect(screen.getByText("inherited")).toBeTruthy();
  });

  it("says so when no profile is in force", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue({ ...defs, profilesInForce: [] });
    renderDrawer();
    expect(await screen.findByText(/No effective profile versions/)).toBeTruthy();
  });

  it("does not fetch while closed", () => {
    renderDrawer(false);
    expect(kpiDashboardService.definitions).not.toHaveBeenCalled();
  });

  it("shows the server's message when the definitions cannot be loaded", async () => {
    vi.mocked(kpiDashboardService.definitions).mockRejectedValueOnce({ response: { data: { message: "Definitions unavailable" } } });
    renderDrawer();
    expect(await screen.findByText("Definitions unavailable")).toBeTruthy();
  });

  it("closes from its close button", async () => {
    vi.mocked(kpiDashboardService.definitions).mockResolvedValue(defs);
    const onClose = renderDrawer();
    await screen.findByText("Days from receipt to approval.");
    await userEvent.click(screen.getByRole("button", { name: "Close definitions" }));
    expect(onClose).toHaveBeenCalled();
  });
});
