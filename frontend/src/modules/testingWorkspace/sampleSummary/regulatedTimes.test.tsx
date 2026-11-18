import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ApprovalSignaturesCard } from "./ApprovalSignaturesCard";
import { SampleIdentityCard } from "./SummaryCards";
import type { SampleSummary } from "../types/sampleSummaryTypes";

afterEach(() => {
  cleanup();
  vi.unstubAllEnvs();
});

const summary = (over: Partial<SampleSummary> = {}): SampleSummary =>
  ({
    sampleId: 1,
    referenceNumber: "FP1126005",
    category: "FinishedProduct",
    displayName: "Product",
    status: "Approved",
    receivedAt: "2026-10-26T10:00:00Z",
    mfgDate: "2026-01-01T00:00:00Z",
    expDate: "2028-10-01T00:00:00Z",
    reviewedByName: null,
    reviewedAt: null,
    approvedByName: null,
    approvedAt: null,
    signatures: [],
    testOrders: [],
    timeline: [],
    certificate: { sample: { matrix: null, simple: null } },
    ...over
  }) as unknown as SampleSummary;

const unknown = { get: () => null, loading: false, failed: false, refresh: vi.fn() };

function renderSignatures(over: Partial<SampleSummary>) {
  render(
    <PinnedLightTheme>
      <ApprovalSignaturesCard
        summary={summary(over)}
        canReview={false}
        canApprove={false}
        reviewAvailability={unknown}
        approvalAvailability={unknown}
        comment=""
        setComment={vi.fn()}
        decision="Approve"
        setDecision={vi.fn()}
        certificateRemarks=""
        setCertificateRemarks={vi.fn()}
        selectedTestOrderIds={[]}
        setSelectedTestOrderIds={vi.fn()}
        analysts={[]}
        analystsLoadFailed={false}
        newSampleAnalystOneId=""
        setNewSampleAnalystOneId={vi.fn()}
        newSampleAnalystTwoId=""
        setNewSampleAnalystTwoId={vi.fn()}
        onReviewClick={vi.fn()}
        onApproveClick={vi.fn()}
        reviewableSections={[]}
        reviewSectionId=""
        onReviewSectionChange={vi.fn()}
        approvableSections={[]}
        approvalSectionId=""
        onApprovalSectionChange={vi.fn()}
        effectiveApprovalSectionId={undefined}
        canCloseTesting={false}
        closableSections={[]}
        closingSectionId=""
        onClosingSectionChange={vi.fn()}
        onCloseTestingClick={vi.fn()}
      />
    </PinnedLightTheme>
  );
}

// A time of day, then a zone name or offset after it (for example "09:41:07 UTC" or "12:41:07 GMT+3").
const WITH_ZONE = /\d{2}:\d{2}:07 [A-Za-z][A-Za-z0-9+:-]*/;

describe("regulated times show their time zone", () => {
  it("shows the zone beside a recorded signature time", () => {
    renderSignatures({
      signatures: [
        {
          role: "Reviewer",
          printedName: "Rana Reviewer",
          username: "rana",
          meaning: "Reviewed",
          meaningText: "I reviewed this record",
          signedAt: "2026-11-17T09:41:07Z",
          comment: null
        }
      ] as SampleSummary["signatures"]
    });
    expect(screen.getByText(WITH_ZONE)).toBeTruthy();
  });

  it("shows the zone beside the reviewer and approver times of an approved sample", () => {
    renderSignatures({
      reviewedByName: "Rana Reviewer",
      reviewedAt: "2026-11-17T09:41:07Z",
      approvedByName: "Sami Head",
      approvedAt: "2026-11-17T10:41:07Z"
    });
    expect(
      screen.getAllByText(
        (_, el) => el?.tagName === "P" && new RegExp(WITH_ZONE.source + ".*Verified").test(el.textContent ?? "")
      )
    ).toHaveLength(2);
  });
});

describe("expiry and manufacture dates are the same calendar day in every time zone", () => {
  it.each(["America/Los_Angeles", "Pacific/Auckland", "Asia/Riyadh"])("in %s", (zone) => {
    vi.stubEnv("TZ", zone);
    render(
      <PinnedLightTheme>
        <SampleIdentityCard summary={summary()} />
      </PinnedLightTheme>
    );
    const exp = screen.getByText("Exp Date").parentElement as HTMLElement;
    expect(exp.textContent).toContain("01 Oct 2028");
    expect(exp.textContent).not.toMatch(/\d{2}:\d{2}/);
  });
});
