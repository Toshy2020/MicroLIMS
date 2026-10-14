import { describe, it, expect } from "vitest";
import { entry, point } from "../kpiFixtures";
import {
  entryView, formatExtras, formatKpiValue, formatPrior, formatRatio, formatUnits, sortByName
} from "./kpiFormat";

describe("formatKpiValue", () => {
  it("formats each unit", () => {
    expect(formatKpiValue("days", 4.2)).toBe("4.2 days");
    expect(formatKpiValue("%", 0.875)).toBe("87.5%");
    expect(formatKpiValue("tests", 12)).toBe("12 tests");
    expect(formatKpiValue("units", 6)).toBe("6.0 units");
  });

  it("keeps a real zero", () => {
    expect(formatKpiValue("%", 0)).toBe("0.0%");
    expect(formatKpiValue("tests", 0)).toBe("0 tests");
  });

  it("shows a dash only for a missing value", () => {
    expect(formatKpiValue("days", null)).toBe("—");
    expect(formatKpiValue("days", undefined)).toBe("—");
  });
});

describe("wire scales", () => {
  // These two pin the scales confirmed against the backend (KpiScope.Rate returns 4-decimal fractions).
  it("treats KPI percent values as fractions (percent shown 100x too small guard)", () => {
    expect(formatKpiValue("%", 0.5)).toBe("50.0%");
    expect(formatKpiValue("%", 0.875)).toBe("87.5%");
    expect(formatKpiValue("%", 1)).toBe("100.0%");
  });
  it("treats workload ratios as fractions", () => {
    expect(formatRatio(0.5)).toBe("50%");
    expect(formatRatio(1.2)).toBe("120%");
    expect(formatRatio(null)).toBe("—");
    expect(formatRatio(0)).toBe("0%");
  });
});

describe("formatUnits", () => {
  it("shows one decimal and a dash for null", () => {
    expect(formatUnits(24)).toBe("24.0");
    expect(formatUnits(null)).toBe("—");
  });
});

describe("entryView", () => {
  it("reports a failed entry", () => {
    expect(entryView(entry({ error: "timed out" }, null))).toEqual({ kind: "error", message: "timed out" });
  });

  it("treats a missing result with no error as an error, not as zero", () => {
    expect(entryView(entry({}, null)).kind).toBe("error");
  });

  it("reports NoData and a Partial entry with no value as no data", () => {
    expect(entryView(entry({}, { status: "NoData", value: null, n: 0 })).kind).toBe("noData");
    expect(entryView(entry({}, { status: "Partial", value: null })).kind).toBe("noData");
  });

  it("treats a null value with a populated series as data, not as no data", () => {
    const view = entryView(entry({ key: "stage-tat" }, { value: null, status: "Ok", series: [point("Testing", 2.1, { secondary: 4, n: 12 })] }));
    expect(view.kind).toBe("value");
  });

  it("keeps a zero as a value", () => {
    const view = entryView(entry({ unit: "%" }, { value: 0, n: 40 }));
    expect(view.kind).toBe("value");
    if (view.kind === "value") expect(view.text).toBe("0.0%");
  });

  it("mutes a value that has too few records", () => {
    const view = entryView(entry({ unit: "%" }, { value: 0.5, n: 3, tooFewRecords: true }));
    expect(view.kind === "value" && view.muted).toBe(true);
  });
});

describe("formatPrior and formatExtras", () => {
  it("states the prior value and its own n", () => {
    expect(formatPrior("days", { value: 5, n: 20, numerator: null, denominator: null })).toBe("Prior period: 5.0 days (n=20)");
  });

  it("formats known extras only, percent extras as percent", () => {
    const e = entry({ unit: "days" }, { extras: { p90: 12, alertRate: 0.018, actionRate: null, mystery: 9 } });
    expect(formatExtras(e)).toEqual([
      { label: "P90", text: "12.0 days" },
      { label: "Alert rate", text: "1.8%" },
      { label: "Action rate", text: "—" }
    ]);
  });
});

describe("sortByName", () => {
  it("sorts case-insensitively and does not mutate the input", () => {
    const rows = [{ fullName: "zed" }, { fullName: "Amy" }, { fullName: "bob" }];
    expect(sortByName(rows).map((r) => r.fullName)).toEqual(["Amy", "bob", "zed"]);
    expect(rows[0].fullName).toBe("zed");
  });
});
