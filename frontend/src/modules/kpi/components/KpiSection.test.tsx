import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { entry, point } from "../kpiFixtures";
import type { KpiEntry } from "../types/kpiTypes";
import { KpiSection } from "./KpiSection";

// Recharts measures 0x0 in jsdom; the tests assert on text, never on the SVG.
// LineChart records its data prop so a test can prove the (scaled) points reach the chart.
const chart = vi.hoisted(() => ({ data: null as { label: string; value: number | null }[] | null }));
vi.mock("recharts", async () => {
  const { createElement, Fragment } = await import("react");
  const Pass = ({ children }: { children?: unknown }) => createElement(Fragment, null, children as never);
  const Nil = () => null;
  const LineChart = ({ data }: { data: { label: string; value: number | null }[] }) => {
    chart.data = data;
    return null;
  };
  return { ResponsiveContainer: Pass, LineChart, Line: Nil, XAxis: Nil, YAxis: Nil, Tooltip: Nil, CartesianGrid: Nil };
});

afterEach(cleanup);

function renderSection(entries: KpiEntry[], onOpenRecords: (key: string) => void = vi.fn()) {
  render(
    <PinnedLightTheme>
      <KpiSection title="Flow" entries={entries} onOpenRecords={onOpenRecords} />
    </PinnedLightTheme>
  );
  return onOpenRecords;
}

describe("KpiSection", () => {
  it("renders nothing for an empty list", () => {
    const { container } = render(
      <PinnedLightTheme><KpiSection title="Flow" entries={[]} onOpenRecords={() => {}} /></PinnedLightTheme>
    );
    expect(container.textContent).toBe("");
  });

  it("shows stage bars with median, P90 and n", () => {
    renderSection([
      entry({ key: "stage-tat", name: "Stage turnaround", group: "Flow", order: 20 }, {
        series: [point("Testing", 2.1, { secondary: 4, n: 12 }), point("Review", 0.5, { secondary: 1, n: 12 })]
      })
    ]);
    expect(screen.getByRole("heading", { name: "Flow" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Stage turnaround" })).toBeTruthy();
    expect(screen.getByText("median 2.1 days · P90 4.0 days · n = 12")).toBeTruthy();
    expect(screen.getByText("Review")).toBeTruthy();
  });

  it("shows backlog aging by stage and bucket, and a dash where a bucket is missing", () => {
    renderSection([
      entry({ key: "backlog-aging", name: "Backlog aging", unit: "tests", group: "Flow", order: 21 }, {
        series: [
          point("within50", 5, { group: "Testing" }),
          point("over100", 2, { group: "Testing" })
        ]
      })
    ]);
    expect(screen.getByText("Testing")).toBeTruthy();
    expect(screen.getByText("5")).toBeTruthy();
    expect(screen.getByText("2")).toBeTruthy();
    expect(screen.getAllByText("—").length).toBe(1);
  });

  it("lists returns by test code", () => {
    renderSection([
      entry({ key: "returns-by-test", name: "Returns by test", unit: "tests", group: "Quality", order: 30 }, {
        series: [point("TAMC", 3)]
      })
    ]);
    expect(screen.getByText("TAMC")).toBeTruthy();
    expect(screen.getByText("3")).toBeTruthy();
  });

  it("asks for more points before drawing a trend", () => {
    renderSection([
      entry({ key: "retest-rate", name: "Retest rate", unit: "%", group: "Headline", order: 4 }, { series: [point("2026-09", 4)] })
    ]);
    expect(screen.getByText("Not enough points for a trend yet.")).toBeTruthy();
  });

  it("feeds a two-point percent series to the chart as scaled percentages", () => {
    chart.data = null;
    renderSection([
      entry({ key: "retest-rate", name: "Retest rate", unit: "%", group: "Headline", order: 4 }, {
        series: [point("2026-08", 0.04), point("2026-09", 0.05)]
      })
    ]);
    expect(chart.data).toEqual([{ label: "2026-08", value: 4 }, { label: "2026-09", value: 5 }]);
  });

  it("shows a count panel with a genuine zero and no series as a value, not as a broken panel", () => {
    renderSection([
      entry({ key: "returns-by-test", name: "Returns by test", unit: "tests", group: "Quality", order: 30 }, {
        value: 0, n: 0, status: "Ok", series: []
      })
    ]);
    expect(screen.getByText("0 tests")).toBeTruthy();
    expect(screen.getByText("None in this period")).toBeTruthy();
    expect(screen.queryByText(/No data/)).toBeNull();
  });

  it("shows alert and action rates and the adverse-trend flag for the EM entry", () => {
    renderSection([
      entry({ key: "em-excursion-rate", name: "EM excursion rate", unit: "%" }, {
        extras: { alertRate: 0.018, actionRate: 0.001 }, flags: ["AdverseTrend"],
        series: [point("2026-08", 1), point("2026-09", 2)]
      })
    ]);
    expect(screen.getByText("Alert rate 1.8%")).toBeTruthy();
    expect(screen.getByText("Action rate 0.1%")).toBeTruthy();
    expect(screen.getByText("Adverse trend")).toBeTruthy();
  });

  it("shows a failed entry as an error and a NoData entry as no data, without a zero", () => {
    renderSection([
      entry({ key: "stage-tat", name: "Stage turnaround", error: "boom" }, null),
      entry({ key: "backlog-aging", name: "Backlog aging", unit: "tests" }, { status: "NoData", value: null, n: 0 })
    ]);
    expect(screen.getByText("Could not be calculated: boom")).toBeTruthy();
    expect(screen.getByText("No data in this period")).toBeTruthy();
    expect(screen.queryByText("0")).toBeNull();
  });

  it("shows a series-only panel whose value is null", () => {
    renderSection([
      entry({ key: "stage-tat", name: "Stage turnaround" }, { value: null, status: "Ok", series: [point("Testing", 2.1, { secondary: 4, n: 12 })] })
    ]);
    expect(screen.getByText("Testing")).toBeTruthy();
    expect(screen.queryByText("No data in this period")).toBeNull();
  });

  it("notes too few records on a rate", () => {
    renderSection([
      entry({ key: "oos-rate", name: "OOS rate", unit: "%" }, { value: 0.2, n: 2, tooFewRecords: true, series: [] })
    ]);
    expect(screen.getByText("too few records to judge")).toBeTruthy();
  });

  it("opens the records for a panel's key", async () => {
    const onOpen = renderSection([entry({ key: "stage-tat", name: "Stage turnaround" })]);
    await userEvent.click(screen.getByRole("button", { name: "Show records for Stage turnaround" }));
    expect(onOpen).toHaveBeenCalledWith("stage-tat");
  });

  it("offers no records button for a failed entry", () => {
    renderSection([entry({ error: "boom" }, null)]);
    expect(screen.queryByRole("button")).toBeNull();
  });
});
