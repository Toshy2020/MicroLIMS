import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ApprovalSignaturesCard } from "./ApprovalSignaturesCard";
import type { SampleSummary, SampleSectionSummaryDetail } from "../types/sampleSummaryTypes";

afterEach(cleanup);

const mockSummary = (over: Partial<SampleSummary> = {}): SampleSummary => ({
  sampleId: 101,
  referenceNumber: "RM2610001",
  category: "RawMaterial",
  displayName: "Sodium Chloride USP",
  productionStage: null,
  causeOfTesting: "Routine",
  batchNumber: "BN-999",
  controlNumber: "CN-999",
  status: "UnderReview",
  statusDisplay: "Under review",
  receivedByName: "John Doe",
  receivedAt: "2026-10-26T10:00:00Z",
  sampledBy: "QC",
  sampleQuantity: "100 g",
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
  sections: [],
  overallStatus: "InProgress",
  combinedCoaAvailable: false,
  allSectionsVisible: true,
  ...over
});

const defaultProps = {
  canReview: true,
  canApprove: false,
  comment: "",
  setComment: vi.fn(),
  decision: "Approve" as const,
  setDecision: vi.fn(),
  certificateRemarks: "",
  setCertificateRemarks: vi.fn(),
  selectedTestOrderIds: [],
  setSelectedTestOrderIds: vi.fn(),
  analysts: [],
  analystsLoadFailed: false,
  newSampleAnalystOneId: "" as const,
  setNewSampleAnalystOneId: vi.fn(),
  newSampleAnalystTwoId: "" as const,
  setNewSampleAnalystTwoId: vi.fn(),
  onReviewClick: vi.fn(),
  onApproveClick: vi.fn(),
  approvableSections: [],
  approvalSectionId: "" as const,
  onApprovalSectionChange: vi.fn(),
  effectiveApprovalSectionId: undefined,
  canCloseTesting: false,
  closableSections: [],
  closingSectionId: "" as const,
  onClosingSectionChange: vi.fn(),
  onCloseTestingClick: vi.fn()
};

describe("ApprovalSignaturesCard retention requirement at review", () => {
  const fpSection: SampleSectionSummaryDetail = {
    sectionId: 1,
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
  };

  const microSection: SampleSectionSummaryDetail = {
    sectionId: 2,
    sectionCode: "MICRO",
    sectionName: "Microbiology",
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
  };

  it("shows retention quantity and unit fields for RawMaterial with FP review section", () => {
    const summary = mockSummary({ category: "RawMaterial" });
    render(
      <PinnedLightTheme>
        <ApprovalSignaturesCard
          {...defaultProps}
          summary={summary}
          reviewableSections={[fpSection]}
          reviewSectionId={1}
          onReviewSectionChange={vi.fn()}
          retentionQuantity=""
          setRetentionQuantity={vi.fn()}
          retentionQuantityUnit=""
          setRetentionQuantityUnit={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByLabelText(/Retention quantity/i)).toBeTruthy();
    expect(screen.getByLabelText(/Unit/i)).toBeTruthy();
  });

  it("disables Submit Review button when retention quantity or unit is invalid for RawMaterial FP", () => {
    const summary = mockSummary({ category: "RawMaterial" });
    const { rerender } = render(
      <PinnedLightTheme>
        <ApprovalSignaturesCard
          {...defaultProps}
          summary={summary}
          reviewableSections={[fpSection]}
          reviewSectionId={1}
          onReviewSectionChange={vi.fn()}
          retentionQuantity=""
          setRetentionQuantity={vi.fn()}
          retentionQuantityUnit=""
          setRetentionQuantityUnit={vi.fn()}
        />
      </PinnedLightTheme>
    );

    const submitBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitBtn as HTMLButtonElement).disabled).toBe(true);

    // Quantity <= 0 with valid unit is still disabled
    rerender(
      <PinnedLightTheme>
        <ApprovalSignaturesCard
          {...defaultProps}
          summary={summary}
          reviewableSections={[fpSection]}
          reviewSectionId={1}
          onReviewSectionChange={vi.fn()}
          retentionQuantity="0"
          setRetentionQuantity={vi.fn()}
          retentionQuantityUnit="g"
          setRetentionQuantityUnit={vi.fn()}
        />
      </PinnedLightTheme>
    );
    expect((submitBtn as HTMLButtonElement).disabled).toBe(true);

    // Valid quantity > 0 and valid unit enables Submit Review
    rerender(
      <PinnedLightTheme>
        <ApprovalSignaturesCard
          {...defaultProps}
          summary={summary}
          reviewableSections={[fpSection]}
          reviewSectionId={1}
          onReviewSectionChange={vi.fn()}
          retentionQuantity="50"
          setRetentionQuantity={vi.fn()}
          retentionQuantityUnit="g"
          setRetentionQuantityUnit={vi.fn()}
        />
      </PinnedLightTheme>
    );
    expect((submitBtn as HTMLButtonElement).disabled).toBe(false);
  });

  it("does NOT show retention fields for FinishedProduct even when section is FP", () => {
    const summary = mockSummary({ category: "FinishedProduct" });
    render(
      <PinnedLightTheme>
        <ApprovalSignaturesCard
          {...defaultProps}
          summary={summary}
          reviewableSections={[fpSection]}
          reviewSectionId={1}
          onReviewSectionChange={vi.fn()}
          retentionQuantity=""
          setRetentionQuantity={vi.fn()}
          retentionQuantityUnit=""
          setRetentionQuantityUnit={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.queryByLabelText(/Retention quantity/i)).toBeNull();
    const submitBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitBtn as HTMLButtonElement).disabled).toBe(false);
  });

  it("does NOT show retention fields for RawMaterial when section is MICRO", () => {
    const summary = mockSummary({ category: "RawMaterial" });
    render(
      <PinnedLightTheme>
        <ApprovalSignaturesCard
          {...defaultProps}
          summary={summary}
          reviewableSections={[microSection]}
          reviewSectionId={2}
          onReviewSectionChange={vi.fn()}
          retentionQuantity=""
          setRetentionQuantity={vi.fn()}
          retentionQuantityUnit=""
          setRetentionQuantityUnit={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.queryByLabelText(/Retention quantity/i)).toBeNull();
    const submitBtn = screen.getByRole("button", { name: /Submit Review/i });
    expect((submitBtn as HTMLButtonElement).disabled).toBe(false);
  });

  it("does not display retention under signatures in ApprovalSignaturesCard (kept in SummaryCards)", () => {
    const summary = mockSummary({
      status: "Approved",
      reviewedByName: "Jane Reviewer",
      reviewedAt: "2026-10-26T12:00:00Z",
      retentionQuantity: 25.5,
      retentionQuantityUnit: "g"
    });

    render(
      <PinnedLightTheme>
        <ApprovalSignaturesCard
          {...defaultProps}
          canReview={false}
          summary={summary}
          reviewableSections={[]}
          reviewSectionId=""
          onReviewSectionChange={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.queryByText(/Retention/i)).toBeNull();
  });
});
