import { afterEach, describe, expect, it, vi } from "vitest";
import { dashboard, entry, headlineEntries, point, workload, workloadRow } from "../kpiFixtures";
import { buildKpiReportHtml, exportKpiPdf } from "./exportKpiPdf";
import { formatKpiValue } from "./kpiFormat";
import type { KpiFiltersValue } from "./kpiFilters";

afterEach(() => vi.restoreAllMocks());

const filters: KpiFiltersValue = { from: "2026-09-12", to: "2026-10-11", category: "FinishedProduct", location: null, testCode: null };

describe("buildKpiReportHtml", () => {
  it("prints each figure exactly as the screen formats it", () => {
    const kpis = headlineEntries();
    const html = buildKpiReportHtml(dashboard(kpis), null, filters, "Sam Head");
    for (const k of kpis) {
      expect(html).toContain(formatKpiValue(k.unit, k.result!.value));
      expect(html).toContain(k.name);
    }
    expect(html).toContain("n</th>");
  });

  it("prints the scope, the signer and the server's generation time", () => {
    const html = buildKpiReportHtml(dashboard([entry()]), null, filters, "Sam Head");
    expect(html).toContain("2026-09-12 to 2026-10-11");
    expect(html).toContain("FinishedProduct");
    expect(html).toContain("Sam Head");
    expect(html).toMatch(/2026/);
  });

  it("prints a failed KPI and a no-data KPI as such, never as a number", () => {
    const html = buildKpiReportHtml(dashboard([
      entry({ key: "a", name: "Failed measure", error: "timed out" }, null),
      entry({ key: "b", name: "Empty measure", unit: "%" }, { status: "NoData", value: null, n: 0 })
    ]), null, filters, "Sam Head");
    expect(html).toContain("Could not be calculated");
    expect(html).toContain("timed out");
    expect(html).toContain("No data");
    expect(html).not.toContain("0.0%");
  });

  it("prints a real zero", () => {
    const html = buildKpiReportHtml(dashboard([entry({ key: "w", name: "WIP backlog", unit: "tests" }, { value: 0, n: 0 })]), null, filters, "Sam Head");
    expect(html).toContain("0 tests");
  });

  it("prints prior period, gaps, flags and the too-few-records note", () => {
    const html = buildKpiReportHtml(dashboard([
      entry({ unit: "%" }, {
        value: 0.4, n: 3, tooFewRecords: true, flags: ["AdverseTrend"],
        gaps: [{ code: "g", message: "2 tests unweighted", count: 2 }],
        prior: { value: 0.3, n: 22, numerator: null, denominator: null }
      })
    ]), null, filters, "Sam Head");
    expect(html).toContain("Prior period: 30.0% (n=22)");
    expect(html).toContain("2 tests unweighted");
    expect(html).toContain("Adverse trend");
    expect(html).toContain("too few records to judge");
  });

  it("prints supporting series as tables with median and P90 for stage turnaround", () => {
    const html = buildKpiReportHtml(dashboard([
      entry({ key: "stage-tat", name: "Stage turnaround", group: "Flow", order: 20 }, {
        series: [point("Testing", 2.1, { secondary: 4, n: 12 })]
      })
    ]), null, filters, "Sam Head");
    expect(html).toContain("Median");
    expect(html).toContain("P90");
    expect(html).toContain("Testing");
    expect(html).toContain("2.1 days");
    expect(html).toContain("4.0 days");
  });

  it("prints analysts alphabetically and escapes their names", () => {
    const html = buildKpiReportHtml(dashboard([entry()]), workload([
      workloadRow({ userId: 2, fullName: "Zed Analyst", deliveredUnits: 99 }),
      workloadRow({ userId: 1, fullName: "<b>Amy</b> Analyst", deliveredUnits: 1 })
    ]), filters, "Sam Head");
    expect(html).toContain("&lt;b&gt;Amy&lt;/b&gt; Analyst");
    expect(html).not.toContain("<b>Amy</b>");
    expect(html.indexOf("Amy")).toBeLessThan(html.indexOf("Zed Analyst"));
  });

  it("notes own-numbers-only and leaves the workload block out when there is no workload", () => {
    const own = buildKpiReportHtml(dashboard([entry()]), workload([workloadRow()], { onlyOwnRow: true }), filters, "Sam Head");
    expect(own).toContain("Showing the viewer&#39;s own numbers only.");
    const none = buildKpiReportHtml(dashboard([entry()]), null, filters, "Sam Head");
    expect(none).not.toContain("Workload");
  });

  it("says so when the workload failed to load instead of silently omitting it", () => {
    const html = buildKpiReportHtml(dashboard([entry()]), null, filters, "Sam Head", "Workload refused <now>");
    expect(html).toContain("Workload could not be loaded: Workload refused &lt;now&gt;");
  });
});

describe("exportKpiPdf", () => {
  it("writes the report into a new window", () => {
    const doc = { open: vi.fn(), write: vi.fn(), close: vi.fn() };
    const spy = vi.spyOn(window, "open").mockReturnValue({ document: doc } as unknown as Window);
    exportKpiPdf(dashboard([entry()]), null, filters, "Sam Head");
    expect(spy).toHaveBeenCalledWith("", "_blank");
    expect(doc.write).toHaveBeenCalledTimes(1);
    expect(String(doc.write.mock.calls[0][0])).toContain("Sample TAT");
    expect(doc.close).toHaveBeenCalled();
  });

  it("does nothing when the browser blocks the window", () => {
    vi.spyOn(window, "open").mockReturnValue(null);
    expect(() => exportKpiPdf(dashboard([entry()]), null, filters, "Sam Head")).not.toThrow();
  });
});
