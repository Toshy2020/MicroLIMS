import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { SampleCoaPage } from "./SampleCoaPage";

const mockGetSummary = vi.fn();

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: { getSummary: (...args: unknown[]) => mockGetSummary(...args) }
}));

const row = (over: Record<string, unknown>) => ({
  kind: "Tested",
  sectionId: null,
  testOrderId: 0,
  testCode: "",
  testDisplayName: "",
  specification: null,
  result: "",
  analystName: null,
  analystAt: null,
  conform: true,
  limitsNotConfigured: false,
  noResult: false,
  ...over
});

const na = (sectionId: number | null, name: string, ref: string) =>
  row({ kind: "NotApplicable", sectionId, testDisplayName: name, result: `Not applicable (${ref})` });
const supplier = row({ kind: "SupplierStated", testDisplayName: "Particle size", result: "20 mesh (stated by supplier)" });

const scope = (testName: string | null, supportingRows: unknown[]) => ({
  matrix: null,
  complies: true,
  conclusionText: "Test results of the sample are Conform according to specification and decision rule.",
  resultDate: null,
  supportingRows,
  simple: testName
    ? { overallComplies: true, rows: [row({ testOrderId: 1, testCode: testName, testDisplayName: testName, result: "Absent" })] }
    : null
});

const base = {
  referenceNumber: "RM-1",
  category: "RawMaterial",
  displayName: "Sorbitol",
  controlNumber: "C1",
  status: "Approved",
  combinedCoaAvailable: true,
  allSectionsVisible: true,
  signatures: [],
  approvedAt: null
};

const section = (sectionId: number, sectionName: string) => ({
  sectionId, sectionCode: sectionName, sectionName, status: "Approved", canView: true, coaAvailable: true,
  reviewedByName: null, reviewedAt: null, approvedByName: null, approvedAt: null, approvalDecision: null,
  certificateRemarks: null, closedByName: null, closedAt: null, closeReason: null
});

const testOrder = (testOrderId: number, sectionId: number, name: string) => ({
  testOrderId, sectionId, sectionName: sectionId === 1 ? "Microbiology" : "Chemistry",
  testCode: name, testDisplayName: name, status: "Approved", isSuperseded: false, locations: []
});

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={["/samples/1/coa"]}>
      <Routes>
        <Route path="/samples/:id/coa" element={<SampleCoaPage />} />
      </Routes>
    </MemoryRouter>
  );

afterEach(() => cleanup());

describe("SampleCoaPage supporting rows", () => {
  it("prints not-applicable and supplier-stated rows below the tested results", async () => {
    mockGetSummary.mockResolvedValue({
      ...base,
      sections: [],
      testOrders: [],
      certificate: { sections: [], sample: scope("Salmonella", [na(2, "Heavy metals", "QC form NA-01"), supplier]) }
    });

    renderPage();

    await waitFor(() => expect(screen.getByText("Salmonella")).toBeTruthy());
    const naCell = screen.getByText("Not applicable (QC form NA-01)");
    const supplierCell = screen.getByText("20 mesh (stated by supplier)");
    expect(screen.getByText("Heavy metals")).toBeTruthy();
    expect(screen.getByText("Particle size")).toBeTruthy();
    // Document order: tested row first, supporting rows after it.
    expect(screen.getByText("Salmonella").compareDocumentPosition(naCell) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(naCell.compareDocumentPosition(supplierCell) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("on a combined certificate prints supplier rows once and each lab's own not-applicable row under that lab", async () => {
    const microRow = na(1, "Mould", "QC form NA-02");
    const chemRow = na(2, "Heavy metals", "QC form NA-01");
    mockGetSummary.mockResolvedValue({
      ...base,
      sections: [section(1, "Microbiology"), section(2, "Chemistry")],
      testOrders: [testOrder(1, 1, "Salmonella"), testOrder(2, 2, "Assay")],
      certificate: {
        // Section scopes carry their own rows plus the unscoped ones (the
        // single-lab certificate); the combined view must not repeat those.
        sample: scope("Salmonella", [microRow, chemRow, supplier]),
        sections: [
          { sectionId: 1, sectionName: "Microbiology", scope: scope("Salmonella", [microRow, supplier]) },
          { sectionId: 2, sectionName: "Chemistry", scope: scope("Assay", [chemRow, supplier]) }
        ]
      }
    });

    renderPage();

    await waitFor(() => expect(screen.getAllByText("Particle size").length).toBeGreaterThan(0));
    expect(screen.getAllByText("20 mesh (stated by supplier)").length).toBe(1);
    expect(screen.getAllByText("Not applicable (QC form NA-02)").length).toBe(1);
    expect(screen.getAllByText("Not applicable (QC form NA-01)").length).toBe(1);
    // The Chemistry row sits after the Chemistry heading, before the once-only supplier row.
    const chemHeading = screen.getByText("Chemistry Test Results");
    const chemNa = screen.getByText("Not applicable (QC form NA-01)");
    const microNa = screen.getByText("Not applicable (QC form NA-02)");
    expect(chemHeading.compareDocumentPosition(chemNa) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(chemNa.compareDocumentPosition(screen.getByText("20 mesh (stated by supplier)")) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(chemHeading.compareDocumentPosition(microNa) & Node.DOCUMENT_POSITION_PRECEDING).toBeTruthy();
  });

  it("keeps a lab's not-applicable row when the lab has no tested rows", async () => {
    const chemRow = na(2, "Heavy metals", "QC form NA-01");
    mockGetSummary.mockResolvedValue({
      ...base,
      sections: [section(1, "Microbiology"), section(2, "Chemistry")],
      testOrders: [testOrder(1, 1, "Salmonella"), testOrder(2, 2, "Assay")],
      certificate: {
        sample: scope("Salmonella", [chemRow]),
        sections: [
          { sectionId: 1, sectionName: "Microbiology", scope: scope("Salmonella", []) },
          { sectionId: 2, sectionName: "Chemistry", scope: scope(null, [chemRow]) }
        ]
      }
    });

    renderPage();

    await waitFor(() => expect(screen.getByText("Not applicable (QC form NA-01)")).toBeTruthy());
    expect(screen.getAllByText("Not applicable (QC form NA-01)").length).toBe(1);
  });
});
