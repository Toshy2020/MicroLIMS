import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { actionAvailabilityService } from "../../../services/actionAvailabilityService";
import { documentApprovalService } from "../services/documentApprovalService";
import type { ApprovalDossierDto } from "../types/documentControlTypes";
import { i18n } from "../../../i18n";
import { ApprovalWorkspaceDialog } from "./ApprovalWorkspaceDialog";

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

function dossier(over: Partial<ApprovalDossierDto["readiness"]> = {}): ApprovalDossierDto {
  return {
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
    reviewHistory: [],
    readiness: {
      isReady: true,
      validationErrors: [],
      requiresElectronicSignature: true,
      hasActiveControlledPdf: true,
      isTechnicalReviewCompleted: true,
      areMandatoryFindingsResolved: true,
      isImpactAssessmentCompleted: true,
      areChangeItemsAddressed: true,
      isSegregationOfDutiesSatisfied: true,
      ...over
    },
    activeSignature: null
  } as unknown as ApprovalDossierDto;
}

async function openDecisionTab(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole("tab", { name: /Decision Controls/ }));
}

function mount() {
  return render(
    <PinnedLightTheme>
      <ApprovalWorkspaceDialog open approvalTaskId={5} currentUserId={9} onClose={vi.fn()} onDecisionExecuted={vi.fn()} />
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  vi.mocked(actionAvailabilityService.get).mockReset();
  vi.mocked(documentApprovalService.getApprovalDossier).mockReset();
  vi.mocked(documentApprovalService.executeApprovalDecision).mockReset();
});
afterEach(async () => {
  cleanup();
  await i18n.changeLanguage("en");
});

describe("ApprovalWorkspaceDialog gated approval", () => {
  it("the author sees Approve Revision disabled with the server's sentence", async () => {
    const sentence = "You authored this revision, so you cannot approve it.";
    vi.mocked(documentApprovalService.getApprovalDossier).mockResolvedValue(dossier({ isSegregationOfDutiesSatisfied: false, isReady: false }));
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([{ action: "document.approve", allowed: false, reason: sentence }]);
    const user = userEvent.setup();
    mount();
    await openDecisionTab(user);

    expect(await screen.findByText(sentence)).toBeTruthy();
    expect(actionAvailabilityService.get).toHaveBeenCalledWith("document-revision", 31, undefined, expect.any(AbortSignal));
    const approve = screen.getByRole("button", { name: "Approve Revision" });
    expect(approve.getAttribute("aria-disabled")).toBe("true");
    await user.click(approve);
    // No decision was chosen, so no confirm button appeared.
    expect(screen.queryByRole("button", { name: /Sign & Approve/ })).toBeNull();
  });

  it("when allowed, Approve Revision can be chosen even though the local readiness flag is off", async () => {
    vi.mocked(documentApprovalService.getApprovalDossier).mockResolvedValue(dossier({ isReady: false }));
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([{ action: "document.approve", allowed: true, reason: null }]);
    const user = userEvent.setup();
    mount();
    await openDecisionTab(user);

    await user.click(await screen.findByRole("button", { name: "Approve Revision" }));
    expect(await screen.findByRole("button", { name: /Sign & Approve/ })).toBeTruthy();
  });

  it("return for correction keeps its request payload", async () => {
    vi.mocked(documentApprovalService.getApprovalDossier).mockResolvedValue(dossier());
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([{ action: "document.approve", allowed: true, reason: null }]);
    vi.mocked(documentApprovalService.executeApprovalDecision).mockResolvedValue(dossier().task);
    const user = userEvent.setup();
    mount();
    await openDecisionTab(user);

    await user.click(await screen.findByRole("button", { name: "Return for Correction" }));
    await user.type(await screen.findByLabelText(/Mandatory Reason for Return/), "Section 3 is unclear.");
    await user.click(screen.getByRole("button", { name: /Confirm ReturnForCorrection/ }));
    await waitFor(() => expect(documentApprovalService.executeApprovalDecision).toHaveBeenCalledWith(5, {
      decision: "ReturnForCorrection",
      decisionNotes: "Section 3 is unclear.",
      effectiveDate: undefined
    }));
  });

  it("renders workspace title and tabs in Arabic when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["documentControl"]);
    vi.mocked(documentApprovalService.getApprovalDossier).mockResolvedValue(dossier());
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([{ action: "document.approve", allowed: true, reason: null }]);

    mount();

    expect(await screen.findByText(/مساحة عمل الاعتماد/)).toBeTruthy();
    expect(screen.getByRole("tab", { name: /ضوابط القرار/ })).toBeTruthy();
  });
});
