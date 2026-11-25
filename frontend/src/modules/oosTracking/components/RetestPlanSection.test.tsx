import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { RetestPlanSection } from "./RetestPlanSection";
import type { OosInvestigationDetailDto } from "../../../services/oosInvestigationService";
import type { UserRecord } from "../../users/services/UserService";

const baseDetail: OosInvestigationDetailDto = {
  id: 12,
  number: "OOS1126012",
  kind: "Oos",
  status: "AwaitingRetest",
  sampleId: 5,
  sampleReferenceNumber: "S-2026-0005",
  sampleCategory: "FinishedProduct",
  sampleDisplayName: "Amoxicillin",
  batchNumber: "B-5",
  sectionId: 1,
  sectionName: "Microbiology",
  openedAtUtc: "2026-11-20T09:00:00Z",
  openedByUserId: 2,
  openedByName: "Rita Reviewer",
  phaseOneDueDate: "2026-11-24",
  dueDate: "2026-12-01",
  isOverdue: false,
  phaseOneAssessment: "LaboratoryErrorConfirmed",
  phaseOneCauseCategoryId: 1,
  phaseOneCauseCategoryName: "Analyst error",
  phaseOneCauseDescription: "Dilution error",
  phaseOneSignedAtUtc: "2026-11-20T12:00:00Z",
  phaseOneSignedByUserId: 1,
  phaseOneSignedByName: "Sam Head",
  conclusion: null,
  rootCauseCategoryId: null,
  rootCauseCategoryName: null,
  rootCauseDescription: null,
  conclusionSummary: null,
  concludedAtUtc: null,
  concludedByUserId: null,
  concludedByName: null,
  version: 9,
  checklistItems: [],
  results: [
    {
      id: 1,
      testOrderId: 55,
      testCode: "TAMC",
      testDisplayName: "Total Aerobic Microbial Count",
      outcomeAtOpening: "Fails",
      resultSummaryAtOpening: "520 CFU/g",
      addedAtUtc: "2026-11-20T09:00:00Z",
      isInvalidated: true,
      invalidatedAtUtc: "2026-11-20T12:00:00Z"
    }
  ],
  entries: [],
  retestPlans: [],
  reopenings: [],
  deviationRequests: [],
  timeline: []
};

const analysts: UserRecord[] = [
  {
    id: 10,
    username: "analyst2",
    fullName: "Bob Analyst",
    email: null,
    roleId: 3,
    role: { id: 3, name: "Analyst", type: "Analyst" },
    isActive: true,
    isLocked: false,
    lockedUntil: null,
    mustChangePassword: false,
    createdAt: "2026-01-01T00:00:00Z",
    lastLoginAt: null,
    passwordChangedAt: null
  }
];

function setupAuth() {
  localStorage.setItem("microlims_username", "head1");
  localStorage.setItem("microlims_role", "SectionHead");
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_user_id", "1");
  localStorage.setItem("microlims_permissions", JSON.stringify(["Oos.View", "Oos.Manage"]));
}

describe("RetestPlanSection", () => {
  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("drafts and signs a retest plan", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onSaveDraft = vi.fn().mockResolvedValue(undefined);
    const onSignPlan = vi.fn().mockResolvedValue(undefined);

    const { rerender } = render(
      <PinnedLightTheme>
        <AuthProvider>
          <RetestPlanSection
            detail={baseDetail}
            analysts={analysts}
            canManage={true}
            onSaveDraft={onSaveDraft}
            onSignPlan={onSignPlan}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByLabelText("Assigned Analysts"));
    await user.click(await screen.findByRole("option", { name: "Bob Analyst (analyst2)" }));
    await user.keyboard("{Escape}");
    await user.type(screen.getByLabelText("Judging Rule"), "All retests must conform.");
    await user.click(screen.getByRole("button", { name: "Save Draft Plan" }));

    await waitFor(() => {
      expect(onSaveDraft).toHaveBeenCalledWith(
        {
          source: "RetainedSample",
          retestCount: 1,
          testOrderIds: [55],
          analystUserIds: [10],
          judgingRule: "All retests must conform.",
          amendmentReason: null
        },
        9
      );
    });

    const withDraft: OosInvestigationDetailDto = {
      ...baseDetail,
      version: 10,
      retestPlans: [
        {
          id: 77,
          revision: 1,
          source: "RetainedSample",
          retestCount: 1,
          testOrderIds: [55],
          analystUserIds: [10],
          judgingRule: "All retests must conform.",
          amendmentReason: null,
          signedAtUtc: null,
          signedByUserId: null,
          signedByName: null,
          samplesCreatedAtUtc: null,
          samples: []
        }
      ]
    };

    rerender(
      <PinnedLightTheme>
        <AuthProvider>
          <RetestPlanSection
            detail={withDraft}
            analysts={analysts}
            canManage={true}
            onSaveDraft={onSaveDraft}
            onSignPlan={onSignPlan}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: "Sign & Create Retest Samples" }));
    const dialog = await screen.findByRole("dialog");
    expect(dialog.textContent).toContain("Retest plan approved");
    await user.type(screen.getByLabelText(/Signing Password|Password/i), "Pass123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(onSignPlan).toHaveBeenCalledWith(77, "Pass123!", 10);
    });
  });

  it("requires an amendment reason when a signed plan already exists", async () => {
    setupAuth();
    const withSignedPlan: OosInvestigationDetailDto = {
      ...baseDetail,
      retestPlans: [
        {
          id: 77,
          revision: 1,
          source: "RetainedSample",
          retestCount: 1,
          testOrderIds: [55],
          analystUserIds: [10],
          judgingRule: "All retests must conform.",
          amendmentReason: null,
          signedAtUtc: "2026-11-20T13:00:00Z",
          signedByUserId: 1,
          signedByName: "Sam Head",
          samplesCreatedAtUtc: "2026-11-20T13:00:00Z",
          samples: [{ sampleId: 99, referenceNumber: "S-2026-0099", status: "Pending" }]
        }
      ]
    };

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <RetestPlanSection
            detail={withSignedPlan}
            analysts={analysts}
            canManage={true}
            onSaveDraft={vi.fn()}
            onSignPlan={vi.fn()}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(screen.getByText(/An amendment reason is required for revision 2/i)).toBeTruthy();
    expect((screen.getByRole("button", { name: "Save Draft Plan" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("shows missing-field hint when draft is incomplete and keeps inputs + shows inline alert on save failure", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onSaveDraft = vi.fn().mockResolvedValue("Retest count exceeds allowed maximum.");

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <RetestPlanSection
            detail={baseDetail}
            analysts={analysts}
            canManage={true}
            onSaveDraft={onSaveDraft}
            onSignPlan={vi.fn()}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(
      screen.getByText(/Select at least one test and analyst, set retests \(1–10\), and enter a judging rule to save\./i)
    ).toBeTruthy();

    await user.click(screen.getByLabelText("Assigned Analysts"));
    await user.click(await screen.findByRole("option", { name: "Bob Analyst (analyst2)" }));
    await user.keyboard("{Escape}");
    const ruleField = screen.getByLabelText("Judging Rule") as HTMLTextAreaElement;
    await user.type(ruleField, "All retests must conform.");
    await user.click(screen.getByRole("button", { name: "Save Draft Plan" }));

    expect(await screen.findByText("Retest count exceeds allowed maximum.")).toBeTruthy();
    expect(ruleField.value).toBe("All retests must conform.");
  });

  it("keeps SignatureDialog open on sign refusal and re-initializes form when unsigned draft changes", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onSignPlan = vi.fn().mockRejectedValue({
      response: { data: { message: "Signer cannot be one of the assigned retest analysts." } }
    });

    const withDraft: OosInvestigationDetailDto = {
      ...baseDetail,
      version: 10,
      retestPlans: [
        {
          id: 77,
          revision: 1,
          source: "RetainedSample",
          retestCount: 2,
          testOrderIds: [55],
          analystUserIds: [10],
          judgingRule: "Initial rule",
          amendmentReason: null,
          signedAtUtc: null,
          signedByUserId: null,
          signedByName: null,
          samplesCreatedAtUtc: null,
          samples: []
        }
      ]
    };

    const { rerender } = render(
      <PinnedLightTheme>
        <AuthProvider>
          <RetestPlanSection
            detail={withDraft}
            analysts={analysts}
            canManage={true}
            onSaveDraft={vi.fn()}
            onSignPlan={onSignPlan}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect((screen.getByLabelText("Judging Rule") as HTMLTextAreaElement).value).toBe("Initial rule");

    await user.click(screen.getByRole("button", { name: "Sign & Create Retest Samples" }));
    await screen.findByRole("dialog");
    await user.type(screen.getByLabelText(/Signing Password|Password/i), "Pass123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(screen.getByText("Signer cannot be one of the assigned retest analysts.")).toBeTruthy();
    });
    expect(screen.getByRole("dialog")).toBeTruthy();

    const updatedDraft: OosInvestigationDetailDto = {
      ...withDraft,
      version: 11,
      retestPlans: [
        {
          ...withDraft.retestPlans[0],
          judgingRule: "Updated rule from server"
        }
      ]
    };

    rerender(
      <PinnedLightTheme>
        <AuthProvider>
          <RetestPlanSection
            detail={updatedDraft}
            analysts={analysts}
            canManage={true}
            onSaveDraft={vi.fn()}
            onSignPlan={onSignPlan}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect((screen.getByLabelText("Judging Rule") as HTMLTextAreaElement).value).toBe(
      "Updated rule from server"
    );
  });
});
