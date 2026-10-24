import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReportingService } from "../../reports/services/ReportingService";
import { kpiDashboardService } from "../services/kpiDashboardService";
import { kpiWorkloadService } from "../services/kpiWorkloadService";
import { dashboard, entry, headlineEntries, point, workload, workloadRow } from "../kpiFixtures";
import type { KpiRecordPage } from "../types/kpiTypes";
import { KpiPerformanceTab } from "./KpiPerformanceTab";
import { changeDateField } from "../../../components/DateField.testUtils";

vi.mock("../services/kpiDashboardService", () => ({
  kpiDashboardService: { dashboard: vi.fn(), records: vi.fn(), definitions: vi.fn() }
}));
vi.mock("../services/kpiWorkloadService", () => ({
  kpiWorkloadService: { workload: vi.fn(), workloadTests: vi.fn() }
}));
vi.mock("../../reports/services/ReportingService", () => ({
  ReportingService: { getFilterOptions: vi.fn() }
}));
const auth = vi.hoisted(() => ({ canConfigureProfiles: true }));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Sam Head" }),
  useHasPermission: () => auth.canConfigureProfiles
}));
vi.mock("./WorkflowProfilesDialog", async () => {
  const { createElement } = await import("react");
  return { WorkflowProfilesDialog: ({ open }: { open: boolean }) => (open ? createElement("div", null, "profiles dialog") : null) };
});
vi.mock("recharts", async () => {
  const { createElement, Fragment } = await import("react");
  const Pass = ({ children }: { children?: unknown }) => createElement(Fragment, null, children as never);
  const Nil = () => null;
  return { ResponsiveContainer: Pass, LineChart: Pass, Line: Nil, XAxis: Nil, YAxis: Nil, Tooltip: Nil, CartesianGrid: Nil };
});

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

const recordsPage: KpiRecordPage = {
  key: "sample-tat", page: 1, pageSize: 50, total: 1,
  items: [{
    sampleId: 7, testOrderId: 11, controlNumber: "FP-0007", testCode: "TAMC", category: "FinishedProduct",
    analystName: "Amy Analyst", eventAt: "2026-10-01T08:00:00Z", value: 3.5, unit: "days", inclusion: "Numerator", note: null
  }]
};

const allEntries = () => [
  ...headlineEntries(),
  entry({ key: "stage-tat", name: "Stage turnaround", group: "Flow", order: 20 }, { series: [point("Testing", 2.1, { secondary: 4, n: 12 })] }),
  entry({ key: "backlog-aging", name: "Backlog aging", unit: "tests", group: "Flow", order: 21 }, { series: [point("within50", 5, { group: "Testing" })] }),
  entry({ key: "returns-by-test", name: "Returns by test", unit: "tests", group: "Quality", order: 30 }, { series: [point("TAMC", 3)] })
];

beforeEach(() => {
  auth.canConfigureProfiles = true;
  vi.mocked(kpiDashboardService.dashboard).mockReset().mockResolvedValue(dashboard(allEntries()));
  vi.mocked(kpiDashboardService.records).mockReset().mockResolvedValue(recordsPage);
  vi.mocked(kpiDashboardService.definitions).mockReset().mockResolvedValue({ kpis: [], profilesInForce: [] });
  vi.mocked(kpiWorkloadService.workload).mockReset().mockResolvedValue(
    workload([workloadRow({ userId: 1, fullName: "Amy Analyst" }), workloadRow({ userId: 2, fullName: "Bob Analyst" })])
  );
  vi.mocked(kpiWorkloadService.workloadTests).mockReset().mockResolvedValue(recordsPage);
  vi.mocked(ReportingService.getFilterOptions).mockReset().mockResolvedValue(
    { categories: ["FinishedProduct"], testCodes: [], subjectNames: [], units: [] } as never
  );
});

function renderTab() {
  return render(<PinnedLightTheme><KpiPerformanceTab /></PinnedLightTheme>);
}

describe("KpiPerformanceTab", () => {
  it("renders the eight headline tiles, the sections and the workload table", async () => {
    renderTab();
    for (const name of ["Sample TAT", "On time, all stages", "Right first time", "Retest rate", "OOS rate", "WIP backlog", "Incubation reads on time", "EM excursion rate"]) {
      expect(await screen.findByRole("button", { name: new RegExp(`^${name}: `) })).toBeTruthy();
    }
    for (const title of ["Flow", "Quality", "Micro", "Workload"]) {
      expect(screen.getByRole("heading", { name: title })).toBeTruthy();
    }
    expect(screen.getByRole("heading", { name: "Stage turnaround" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Show tests for Amy Analyst" })).toBeTruthy();
  });

  it("opens the records behind a tile", async () => {
    renderTab();
    await userEvent.click(await screen.findByRole("button", { name: /^Sample TAT: / }));
    expect(await screen.findByText("FP-0007")).toBeTruthy();
    expect(kpiDashboardService.records).toHaveBeenCalledWith("sample-tat", expect.objectContaining({ page: 1, pageSize: 50 }));
  });

  it("opens the tests behind a workload row for that analyst", async () => {
    renderTab();
    await userEvent.click(await screen.findByRole("button", { name: "Show tests for Amy Analyst" }));
    expect(await screen.findByText("FP-0007")).toBeTruthy();
    expect(kpiWorkloadService.workloadTests).toHaveBeenCalledWith(expect.objectContaining({ analystId: 1, page: 1, pageSize: 50 }));
  });

  it("a workload failure leaves the tiles on screen", async () => {
    vi.mocked(kpiWorkloadService.workload).mockRejectedValue({ response: { data: { message: "Workload refused" } } });
    renderTab();
    expect(await screen.findByRole("button", { name: /^Sample TAT: / })).toBeTruthy();
    expect(await screen.findByText("Workload refused")).toBeTruthy();
    expect(screen.queryByText("Could not be calculated")).toBeNull();
  });

  it("shows one failed KPI as an error tile while the others render", async () => {
    const entries = allEntries();
    entries[0] = entry({ key: "sample-tat", name: "Sample TAT", error: "Query timed out" }, null);
    vi.mocked(kpiDashboardService.dashboard).mockResolvedValue(dashboard(entries));
    renderTab();
    expect(await screen.findByText("Could not be calculated")).toBeTruthy();
    expect(screen.getByText("Query timed out")).toBeTruthy();
    expect(screen.getByRole("button", { name: /^Right first time: / })).toBeTruthy();
  });

  it("shows a dashboard failure with a retry that recovers", async () => {
    vi.mocked(kpiDashboardService.dashboard)
      .mockRejectedValueOnce({ response: { data: { message: "Dashboard unavailable" } } })
      .mockResolvedValue(dashboard(allEntries()));
    renderTab();
    expect(await screen.findByText("Dashboard unavailable")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^Sample TAT: / })).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByRole("button", { name: /^Sample TAT: / })).toBeTruthy();
    expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(2);
  });

  it("fetches again when the period changes", async () => {
    renderTab();
    await screen.findByRole("button", { name: /^Sample TAT: / });
    changeDateField("From", "01/01/2020");
    await waitFor(() => expect(kpiDashboardService.dashboard).toHaveBeenCalledTimes(2));
    expect(vi.mocked(kpiDashboardService.dashboard).mock.calls[1][0]).toEqual(expect.objectContaining({ from: "2020-01-01" }));
    expect(vi.mocked(kpiWorkloadService.workload).mock.calls[1][0]).toEqual(expect.objectContaining({ from: "2020-01-01" }));
  });

  it("opens the definitions drawer and the workflow profiles dialog", async () => {
    renderTab();
    await screen.findByRole("button", { name: /^Sample TAT: / });
    await userEvent.click(screen.getByRole("button", { name: "Definitions" }));
    expect(await screen.findByRole("button", { name: "Close definitions" })).toBeTruthy();
    await userEvent.click(screen.getByRole("button", { name: "Close definitions" }));
    await userEvent.click(await screen.findByRole("button", { name: "Workflow Profiles" }));
    expect(await screen.findByText("profiles dialog")).toBeTruthy();
  });

  it("hides the Workflow Profiles button for users without Kpi.ConfigureProfiles", async () => {
    auth.canConfigureProfiles = false;
    renderTab();
    await screen.findByRole("button", { name: /^Sample TAT: / });
    expect(screen.queryByRole("button", { name: "Workflow Profiles" })).toBeNull();
  });

  it("enables the PDF export only once the figures are loaded, then prints them", async () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    renderTab();
    const button = () => screen.getByRole("button", { name: "Export PDF" }) as HTMLButtonElement;
    expect(button().disabled).toBe(true);
    await waitFor(() => expect(button().disabled).toBe(false));
    await userEvent.click(button());
    expect(open).toHaveBeenCalledTimes(1);
  });

  it("prints the workload failure note when exporting after the workload request failed", async () => {
    vi.mocked(kpiWorkloadService.workload).mockRejectedValue({ response: { data: { message: "Workload refused" } } });
    const doc = { open: vi.fn(), write: vi.fn(), close: vi.fn() };
    vi.spyOn(window, "open").mockReturnValue({ document: doc } as unknown as Window);
    renderTab();
    await screen.findByText("Workload refused");
    const button = screen.getByRole("button", { name: "Export PDF" }) as HTMLButtonElement;
    await waitFor(() => expect(button.disabled).toBe(false));
    await userEvent.click(button);
    expect(String(doc.write.mock.calls[0][0])).toContain("Workload could not be loaded: Workload refused");
  });
});
