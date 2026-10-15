import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { entry } from "../kpiFixtures";
import type { KpiDefinition, KpiEntry } from "../types/kpiTypes";
import { KpiTile } from "./KpiTile";

afterEach(cleanup);

function renderTile(e: KpiEntry, over: { definition?: KpiDefinition; onOpenRecords?: (key: string) => void } = {}) {
  const onOpenRecords = over.onOpenRecords ?? vi.fn();
  render(
    <PinnedLightTheme>
      <KpiTile entry={e} definition={over.definition} onOpenRecords={onOpenRecords} />
    </PinnedLightTheme>
  );
  return onOpenRecords;
}

describe("KpiTile", () => {
  it("shows the value and n for an Ok result and no prior line when there is no prior", () => {
    renderTile(entry());
    expect(screen.getByText("4.2 days")).toBeTruthy();
    expect(screen.getByText("n = 31")).toBeTruthy();
    expect(screen.queryByText(/Prior period/)).toBeNull();
  });

  it("shows the prior value with its own n only when a prior exists", () => {
    renderTile(entry({}, { prior: { value: 5, n: 20, numerator: null, denominator: null } }));
    expect(screen.getByText("Prior period: 5.0 days (n=20)")).toBeTruthy();
  });

  it("shows No data and no number for NoData", () => {
    renderTile(entry({ unit: "%" }, { status: "NoData", value: null, n: 0 }));
    expect(screen.getByText("No data")).toBeTruthy();
    expect(screen.queryByText(/%/)).toBeNull();
    expect(screen.queryByText(/^0/)).toBeNull();
  });

  it("shows a real zero as a value, not as no data", () => {
    renderTile(entry({ key: "em-excursion-rate", name: "EM excursion rate", unit: "%" }, { value: 0, n: 40 }));
    expect(screen.getByText("0.0%")).toBeTruthy();
    expect(screen.queryByText("No data")).toBeNull();
  });

  it("shows a fractional percent value as a percentage, not 100x too small", () => {
    renderTile(entry({ key: "right-first-time", name: "Right first time", unit: "%" }, { value: 0.875, n: 40 }));
    expect(screen.getByText("87.5%")).toBeTruthy();
  });

  it("shows gap chips for a Partial result", () => {
    renderTile(entry({}, { status: "Partial", gaps: [{ code: "unweighted", message: "12 tests unweighted", count: 12 }] }));
    expect(screen.getByText("12 tests unweighted")).toBeTruthy();
  });

  it("labels the adverse-trend flag", () => {
    renderTile(entry({ unit: "%" }, { flags: ["AdverseTrend"] }));
    expect(screen.getByText("Adverse trend")).toBeTruthy();
  });

  it("mutes a rate with too few records and says why", () => {
    renderTile(entry({ unit: "%" }, { value: 0.5, n: 3, tooFewRecords: true }));
    expect(screen.getByText("too few records to judge")).toBeTruthy();
    expect(screen.getByText("50.0%").getAttribute("data-muted")).toBe("true");
  });

  it("does not mute a normal value", () => {
    renderTile(entry());
    expect(screen.getByText("4.2 days").getAttribute("data-muted")).toBe("false");
  });

  it("shows known extras such as P90", () => {
    renderTile(entry({}, { extras: { p90: 12 } }));
    expect(screen.getByText("P90 12.0 days")).toBeTruthy();
  });

  it("renders a failed KPI as an error tile that cannot open records", () => {
    renderTile(entry({ error: "Query timed out" }, null));
    expect(screen.getByText("Could not be calculated")).toBeTruthy();
    expect(screen.getByText("Query timed out")).toBeTruthy();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("opens the record list for its key when clicked", async () => {
    const onOpen = renderTile(entry());
    await userEvent.click(screen.getByRole("button", { name: /^Sample TAT: 4\.2 days/ }));
    expect(onOpen).toHaveBeenCalledWith("sample-tat");
  });

  it("shows the registry definition on hover", async () => {
    const definition: KpiDefinition = {
      key: "sample-tat", name: "Sample TAT", unit: "days", direction: "LowerIsBetter", group: "Headline", order: 1,
      definition: "Days from receipt to approval.", clockEvents: "", population: "", source: ""
    };
    renderTile(entry(), { definition });
    await userEvent.hover(screen.getByRole("button", { name: /^Sample TAT/ }));
    expect(await screen.findByText("Days from receipt to approval.")).toBeTruthy();
  });
});
