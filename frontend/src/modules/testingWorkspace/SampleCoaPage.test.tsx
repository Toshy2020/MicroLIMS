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

const summary = {
  referenceNumber: "RM-1",
  category: "RawMaterial",
  displayName: "Sorbitol",
  controlNumber: "C1",
  status: "Approved",
  combinedCoaAvailable: true,
  sections: [],
  signatures: [],
  testOrders: [],
  certificate: {
    sections: [],
    sample: {
      matrix: null,
      complies: true,
      conclusionText: "Test results of the sample are Conform according to specification and decision rule.",
      resultDate: null,
      simple: {
        overallComplies: true,
        rows: [row({ testOrderId: 1, testCode: "SALM", testDisplayName: "Salmonella", result: "Absent" })],
        supportingRows: [
          row({ kind: "NotApplicable", testDisplayName: "Heavy metals", result: "Not applicable (QC form NA-01)" }),
          row({ kind: "SupplierStated", testDisplayName: "Particle size", result: "20 mesh (stated by supplier)" })
        ]
      }
    }
  }
};

afterEach(() => cleanup());

describe("SampleCoaPage supporting rows", () => {
  it("prints not-applicable and supplier-stated rows below the tested results", async () => {
    mockGetSummary.mockResolvedValue(summary);

    render(
      <MemoryRouter initialEntries={["/samples/1/coa"]}>
        <Routes>
          <Route path="/samples/:id/coa" element={<SampleCoaPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => expect(screen.getByText("Salmonella")).toBeTruthy());
    const na = screen.getByText("Not applicable (QC form NA-01)");
    const supplier = screen.getByText("20 mesh (stated by supplier)");
    const tested = screen.getByText("Salmonella");
    expect(screen.getByText("Heavy metals")).toBeTruthy();
    expect(screen.getByText("Particle size")).toBeTruthy();
    // Document order: tested row first, supporting rows after it.
    expect(tested.compareDocumentPosition(na) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(na.compareDocumentPosition(supplier) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});
