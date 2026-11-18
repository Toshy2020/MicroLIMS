import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { actionAvailabilityService } from "../../../services/actionAvailabilityService";
import { documentApprovalService } from "../services/documentApprovalService";
import type { ApprovalDossierDto } from "../types/documentControlTypes";
import type { AcknowledgementPresentationDto } from "../types/acknowledgementTypes";
import { ApprovalWorkspaceDialog } from "./ApprovalWorkspaceDialog";
import { DocumentAcknowledgementDialog } from "./DocumentAcknowledgementDialog";

// Network boundaries only.
vi.mock("../../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn() }
}));
vi.mock("../services/documentApprovalService", () => ({
  documentApprovalService: {
    getApprovalDossier: vi.fn(),
    executeApprovalDecision: vi.fn(),
    signApproval: vi.fn()
  }
}));

const dossier = {
  task: {
    id: 5,
    documentRevisionId: 31,
    documentMasterId: 8,
    assignedApproverFullName: "Section Head",
    assignedApproverUsername: "sh",
    status: "Pending",
    submissionNotes: null,
    targetEffectiveDate: null
  },
  revision: { id: 31, revisionNumber: "2" },
  master: { id: 8, companyDocumentCode: "SOP-001", title: "Sampling" },
  controlledPdf: null,
  changeItems: [],
  impactAssessment: null,
  reviewHistory: [
    {
      id: 1,
      assignedReviewerFullName: "Rana Reviewer",
      assignedReviewerUsername: "rana",
      decision: "Approved",
      decisionAt: "2026-11-17T09:41:07Z",
      reviewNotes: null,
      findings: []
    }
  ],
  readiness: {
    isReady: true,
    validationErrors: [],
    requiresElectronicSignature: true,
    hasActiveControlledPdf: true,
    isTechnicalReviewCompleted: true,
    areMandatoryFindingsResolved: true,
    isImpactAssessmentCompleted: true,
    areChangeItemsAddressed: true,
    isSegregationOfDutiesSatisfied: true
  },
  activeSignature: null
} as unknown as ApprovalDossierDto;

const acknowledgement = {
  assignmentId: 1,
  documentMasterId: 8,
  companyDocumentCode: "SOP-001",
  documentTitle: "Sampling",
  documentRevisionId: 31,
  revisionNumber: "2",
  assignedUserId: 4,
  assignedUserName: "Rana Reviewer",
  assignmentStatus: "Pending",
  dueDateUtc: "2026-12-01T00:00:00Z",
  legalStatementText: "I have read and understood this document.",
  canAcknowledge: true
} as AcknowledgementPresentationDto;

beforeEach(() => {
  vi.mocked(actionAvailabilityService.get).mockResolvedValue([]);
  vi.mocked(documentApprovalService.getApprovalDossier).mockResolvedValue(dossier);
});

afterEach(() => {
  cleanup();
  vi.unstubAllEnvs();
  vi.clearAllMocks();
});

describe("document control regulated times", () => {
  it("shows the zone beside a reviewer's decision time", async () => {
    render(
      <PinnedLightTheme>
        <ApprovalWorkspaceDialog open approvalTaskId={5} currentUserId={9} onClose={vi.fn()} onDecisionExecuted={vi.fn()} />
      </PinnedLightTheme>
    );
    const decision = await screen.findByText(/Decision: Approved on /);
    expect(decision.textContent).toMatch(/17 Nov 2026, \d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/);
  });

  it.each(["America/Los_Angeles", "Pacific/Honolulu"])("shows the due date as the same calendar day in %s", (zone) => {
    vi.stubEnv("TZ", zone);
    render(
      <PinnedLightTheme>
        <DocumentAcknowledgementDialog
          open
          context={acknowledgement}
          loading={false}
          onClose={vi.fn()}
          onConfirmAcknowledgement={vi.fn()}
        />
      </PinnedLightTheme>
    );
    expect(screen.getByText("01 Dec 2026")).toBeTruthy();
  });
});
