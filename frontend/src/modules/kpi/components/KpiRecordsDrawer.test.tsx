import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { KpiRecordPage } from "../types/kpiTypes";
import { KpiRecordsDrawer } from "./KpiRecordsDrawer";

afterEach(cleanup);

const page: KpiRecordPage = {
  key: "sample-tat", page: 1, pageSize: 50, total: 120,
  items: [{
    sampleId: 7, testOrderId: 11, controlNumber: "FP-0007", testCode: "TAMC", category: "FinishedProduct",
    analystName: "Amy Analyst", eventAt: "2026-10-01T08:00:00Z", value: 3.5, unit: "days",
    inclusion: "Numerator", note: "Approved on the second review"
  }]
};

function renderDrawer(load: (p: number, s: number) => Promise<KpiRecordPage>, onClose: () => void = vi.fn()) {
  render(
    <PinnedLightTheme>
      <KpiRecordsDrawer title="Sample TAT - records" onClose={onClose} load={load} />
    </PinnedLightTheme>
  );
}

describe("KpiRecordsDrawer", () => {
  it("loads page 1 and shows the record with its role in the figure", async () => {
    const load = vi.fn().mockResolvedValue(page);
    renderDrawer(load);
    expect(await screen.findByText("FP-0007")).toBeTruthy();
    expect(screen.getByText("Numerator")).toBeTruthy();
    expect(screen.getByText("3.5 days")).toBeTruthy();
    expect(screen.getByText("Approved on the second review")).toBeTruthy();
    expect(screen.getByText("Sample TAT - records")).toBeTruthy();
    expect(load).toHaveBeenCalledWith(1, 50);
  });

  it("formats a fractional percent record value as a percentage", async () => {
    const pct = { ...page, items: [{ ...page.items[0], value: 0.875, unit: "%" }] };
    renderDrawer(vi.fn().mockResolvedValue(pct));
    expect(await screen.findByText("87.5%")).toBeTruthy();
  });

  it("asks the server for the next page", async () => {
    const load = vi.fn().mockResolvedValue(page);
    renderDrawer(load);
    await screen.findByText("FP-0007");
    await userEvent.click(screen.getByRole("button", { name: /next page/i }));
    expect(load).toHaveBeenLastCalledWith(2, 50);
  });

  it("shows an empty page as no records", async () => {
    renderDrawer(vi.fn().mockResolvedValue({ ...page, total: 0, items: [] }));
    expect(await screen.findByText("No records.")).toBeTruthy();
  });

  it("shows the server's message on failure", async () => {
    renderDrawer(vi.fn().mockRejectedValue({ response: { data: { message: "Not permitted" } } }));
    expect(await screen.findByText("Not permitted")).toBeTruthy();
  });

  it("closes from its close button", async () => {
    const onClose = vi.fn();
    renderDrawer(vi.fn().mockResolvedValue(page), onClose);
    await screen.findByText("FP-0007");
    await userEvent.click(screen.getByRole("button", { name: "Close records" }));
    expect(onClose).toHaveBeenCalled();
  });
});
