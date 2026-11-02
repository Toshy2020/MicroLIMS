import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { CryovialReportPage } from "./CryovialReportPage";
import type { CryovialSummary } from "./types/cryovialSummaryTypes";

const get = vi.hoisted(() => vi.fn());
vi.mock("../../../services/apiClient", () => ({ apiClient: { get } }));
vi.mock("../../testingWorkspace/services/ArchivedRecordsService", () => ({
  ArchivedRecordsService: { getForEntity: vi.fn().mockResolvedValue([]) }
}));

const summary = (over: Partial<CryovialSummary> = {}): CryovialSummary => ({
  cryovialId: 7, code: "CRY-0007", organismName: "Escherichia coli", manufacturerName: "Mfr", materialName: "Lyophilized E. coli",
  materialBatchNumber: "B1", expiryDate: "2027-01-01T00:00:00Z", numberOfVialsPrepared: 10, vialsRemaining: 8, storageCondition: "-70 C",
  physicalCheckConfirmed: true, physicalCheckText: "", preparedAt: "2026-10-01T00:00:00Z", preparedByName: "Analyst", approvalStatus: "Approved",
  approvedByName: null, approvedAt: null, isDestroyed: false, identityConfirmations: [], unthawedVialsDestruction: null,
  thawHistory: [], timeline: [], signatures: [], ...over
});

const renderReport = () =>
  render(
    <MemoryRouter initialEntries={["/cryovials/7/report"]}>
      <Routes><Route path="/cryovials/:id/report" element={<CryovialReportPage />} /></Routes>
    </MemoryRouter>
  );

beforeEach(() => get.mockReset());
afterEach(cleanup);

describe("CryovialReportPage reference strain", () => {
  it("shows the strain line when the batch has a reference strain", async () => {
    get.mockResolvedValue({ data: { data: summary({ referenceOrganismCode: "ORG-001", cultureCollection: "ATCC", strainNumber: "8739" }) } });
    renderReport();
    expect(await screen.findByText("ORG-001 (ATCC 8739)")).toBeTruthy();
    expect(screen.getByText("Reference strain")).toBeTruthy();
  });

  it("omits the line on old batches without a strain", async () => {
    get.mockResolvedValue({ data: { data: summary() } });
    renderReport();
    await screen.findByText("CRY-0007", { selector: ".sample-id" });
    expect(screen.queryByText("Reference strain")).toBeNull();
  });
});
