import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { SampleSummaryDialog } from "./SampleSummaryDialog";
import type { SampleSummary } from "./types/sampleSummaryTypes";
import { PERMISSIONS } from "../../routes/routes";

const mockGetSummary = vi.fn();
const mockCompleteReview = vi.fn();

vi.mock("./services/SampleSummaryService", () => ({
  SampleSummaryService: {
    getSummary: (...args: unknown[]) => mockGetSummary(...args),
    completeReview: (...args: unknown[]) => mockCompleteReview(...args),
    exportPdf: vi.fn(),
    returnTestToAnalyst: vi.fn(),
    decideApproval: vi.fn()
  }
}));

vi.mock("../users/services/UserService", () => ({
  UserService: {
    getEligibleAnalysts: vi.fn().mockResolvedValue([])
  }
}));

vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => ({
    permissions: [PERMISSIONS.SAMPLES_REVIEW, PERMISSIONS.SAMPLES_APPROVE],
    fullName: "QC Reviewer",
    username: "qcreviewer",
    role: "Reviewer"
  })
}));

// Mock SignatureDialog to immediately trigger onConfirm with password
vi.mock("../../components/SignatureDialog", () => ({
  SignatureDialog: ({
    open,
    meaningStatement,
    onConfirm
  }: {
    open: boolean;
    meaningStatement?: string;
    onConfirm: (p: string) => Promise<void>;
  }) =>
    open ? (
      <div data-testid="mock-signature-dialog">
        <span>{meaningStatement}</span>
        <button onClick={() => void onConfirm("secret123")}>confirm-signature</button>
      </div>
    ) : null
}));

afterEach(cleanup);

beforeEach(() => {
  mockGetSummary.mockReset();
  mockCompleteReview.mockReset();
});

const createSample = (over: Partial<SampleSummary> = {}): SampleSummary => ({
  sampleId: 77,
  referenceNumber: "RM0926077",
  category: "RawMaterial",
  displayName: "Microcrystalline Cellulose",
  productionStage: null,
  causeOfTesting: "Routine",
  batchNumber: "LOT-MCC-01",
  grade: "NF",
  retentionQuantity: null,
  retentionQuantityUnit: null,
  controlNumber: "CTRL-01",
  status: "UnderReview",
  statusDisplay: "Under review",
  receivedByName: "Receiver",
  receivedAt: "2026-10-26T08:00:00Z",
  sampledBy: "QC",
  sampleQuantity: "500 g",
  mfgDate: "2026-01-01T00:00:00Z",
  expDate: "2028-01-01T00:00:00Z",
  waterSamplingPointCode: null,
  waterSamplingPointLocation: null,
  storageCondition: null,
  storageTimeHours: null,
  reviewedByName: null,
  reviewedAt: null,
  approvedByName: null,
  approvedAt: null,
  approvalDecision: null,
  certificateRemarks: null,
  preparation: null,
  signatures: [],
  testOrders: [],
  timeline: [],
  certificate: { sample: { matrix: null, simple: null } } as never,
  sections: [
    {
      sectionId: 10,
      sectionCode: "FP",
      sectionName: "Physicochemical",
      status: "UnderReview",
      canView: true,
      reviewedByName: null,
      reviewedAt: null,
      approvedByName: null,
      approvedAt: null,
      approvalDecision: null,
      certificateRemarks: null,
      coaAvailable: false,
      closedByName: null,
      closedAt: null,
      closeReason: null
    }
  ],
  overallStatus: "InProgress",
  combinedCoaAvailable: false,
  allSectionsVisible: true,
  ...over
});

describe("SampleSummaryDialog review flow", () => {
  it("sends retentionQuantity and retentionQuantityUnit when completing review for RawMaterial FP", async () => {
    const sample = createSample({ category: "RawMaterial" });
    mockGetSummary.mockResolvedValue(sample);
    mockCompleteReview.mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(await screen.findByText("Complete overview of test execution, incubation stages, results, and approvals.")).toBeTruthy();
    expect(screen.getByLabelText(/Retention quantity/i)).toBeTruthy();

    // Fill retention quantity and select unit
    fireEvent.change(screen.getByLabelText(/Retention quantity/i), { target: { value: "50" } });
    await userEvent.click(screen.getByRole("combobox", { name: /Unit/i }));
    await userEvent.click(screen.getByRole("option", { name: "g" }));

    // Click Submit Review
    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(submitReviewBtn);

    // Signature dialog appears
    expect(await screen.findByTestId("mock-signature-dialog")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "confirm-signature", hidden: true }));

    await waitFor(() => expect(mockCompleteReview).toHaveBeenCalledTimes(1));
    expect(mockCompleteReview).toHaveBeenCalledWith(
      77,
      "secret123",
      undefined,
      10,
      50,
      "g"
    );
  }, 15000);

  it("does not send retention fields when completing review for FinishedProduct", async () => {
    const sample = createSample({
      category: "FinishedProduct",
      sections: [
        {
          sectionId: 10,
          sectionCode: "FP",
          sectionName: "Physicochemical",
          status: "UnderReview",
          canView: true,
          reviewedByName: null,
          reviewedAt: null,
          approvedByName: null,
          approvedAt: null,
          approvalDecision: null,
          certificateRemarks: null,
          coaAvailable: false,
          closedByName: null,
          closedAt: null,
          closeReason: null
        }
      ]
    });
    mockGetSummary.mockResolvedValue(sample);
    mockCompleteReview.mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <SampleSummaryDialog open={true} sampleId={77} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(await screen.findByText("Complete overview of test execution, incubation stages, results, and approvals.")).toBeTruthy();
    expect(screen.queryByLabelText(/Retention quantity/i)).toBeNull();

    const submitReviewBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitReviewBtn as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(submitReviewBtn);

    expect(await screen.findByTestId("mock-signature-dialog")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "confirm-signature", hidden: true }));

    await waitFor(() => expect(mockCompleteReview).toHaveBeenCalledTimes(1));
    expect(mockCompleteReview).toHaveBeenCalledWith(
      77,
      "secret123",
      undefined,
      10,
      undefined,
      undefined
    );
  }, 15000);
});
