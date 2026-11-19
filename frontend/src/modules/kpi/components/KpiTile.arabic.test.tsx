import { afterEach, describe, expect, it } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import type { KpiEntry } from "../types/kpiTypes";
import { KpiTile } from "./KpiTile";

const entry: KpiEntry = {
  key: "right-first-time",
  name: "Right first time",
  unit: "%",
  direction: "HigherIsBetter",
  group: "Headline",
  order: 1,
  error: null,
  result: {
    value: 0.875,
    numerator: 7,
    denominator: 8,
    n: 8,
    prior: null,
    status: "Ok",
    tooFewRecords: false,
    gaps: [],
    series: [],
    extras: {},
    flags: []
  }
};

describe("KpiTile in Arabic", () => {
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("names the tile's record button in Arabic and keeps the figure as written", async () => {
    await renderInArabic(<KpiTile entry={entry} onOpenRecords={() => {}} />, ["common", "kpi"]);

    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByRole("button", { name: "صحيح من أول مرة: 87.5%. عرض السجلات" })).toBeTruthy();
    expect(screen.getByText("صحيح من أول مرة")).toBeTruthy();
    expect(screen.queryByText("Right first time")).toBeNull();
    expect(screen.getByText("ن = 8")).toBeTruthy();
    expect(screen.getByText("87.5%")).toBeTruthy();
  });

  it("shows the server name for a KPI key the client has no translation for", async () => {
    await renderInArabic(
      <KpiTile entry={{ ...entry, key: "future-kpi", name: "Future measure" }} onOpenRecords={() => {}} />,
      ["common", "kpi"]
    );

    expect(screen.getByText("Future measure")).toBeTruthy();
  });
});
