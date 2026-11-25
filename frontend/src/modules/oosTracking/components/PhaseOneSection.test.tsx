import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PhaseOneSection } from "./PhaseOneSection";
import type {
  OosCategoryDto,
  OosChecklistFactsDto,
  OosInvestigationDetailDto
} from "../../../services/oosInvestigationService";

const baseDetail: OosInvestigationDetailDto = {
  id: 10,
  number: "OOS1126010",
  kind: "Oos",
  status: "PhaseOne",
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
  phaseOneAssessment: null,
  phaseOneCauseCategoryId: null,
  phaseOneCauseCategoryName: null,
  phaseOneCauseDescription: null,
  phaseOneSignedAtUtc: null,
  phaseOneSignedByUserId: null,
  phaseOneSignedByName: null,
  conclusion: null,
  rootCauseCategoryId: null,
  rootCauseCategoryName: null,
  rootCauseDescription: null,
  conclusionSummary: null,
  concludedAtUtc: null,
  concludedByUserId: null,
  concludedByName: null,
  version: 7,
  checklistItems: [
    { phase: 1, key: "item-1", order: 1, text: "Check incubator temperature" }
  ],
  results: [
    {
      id: 1,
      testOrderId: 55,
      testCode: "TAMC",
      testDisplayName: "Total Aerobic Microbial Count",
      outcomeAtOpening: "Fails",
      resultSummaryAtOpening: "520 CFU/g",
      addedAtUtc: "2026-11-20T09:00:00Z",
      isInvalidated: false,
      invalidatedAtUtc: null
    }
  ],
  entries: [],
  retestPlans: [],
  reopenings: [],
  deviationRequests: [],
  timeline: []
};

const facts: OosChecklistFactsDto = {
  version: 7,
  orders: [
    {
      testOrderId: 55,
      resultAuditCount: 2,
      equipment: [],
      media: [
        {
          mediaId: 3,
          lotNumber: "TSA-2026-09",
          isReleasedForUse: true,
          expiryDate: "2027-01-01T00:00:00Z"
        }
      ],
      solutions: [],
      standards: [],
      incubations: []
    }
  ]
};

const categories: OosCategoryDto[] = [
  { id: 1, name: "Analyst error", isActive: true, isNoCauseFound: false, version: 1 },
  { id: 7, name: "No cause found", isActive: true, isNoCauseFound: true, version: 1 }
];

function setupAuth() {
  localStorage.setItem("microlims_username", "head1");
  localStorage.setItem("microlims_role", "SectionHead");
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_user_id", "1");
  localStorage.setItem("microlims_permissions", JSON.stringify(["Oos.View", "Oos.Contribute", "Oos.Manage"]));
}

describe("PhaseOneSection", () => {
  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("allows analyst to add a statement and answer a checklist item with facts beside it", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onAddStatement = vi.fn().mockResolvedValue(undefined);
    const onAnswerChecklist = vi.fn().mockResolvedValue(undefined);

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <PhaseOneSection
            detail={baseDetail}
            facts={facts}
            factsLoading={false}
            categories={categories}
            canContribute={true}
            canManage={true}
            onAddStatement={onAddStatement}
            onAnswerChecklist={onAnswerChecklist}
            onSignPhaseOne={vi.fn()}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(screen.getByText(/TSA-2026-09/)).toBeTruthy();

    await user.type(screen.getByLabelText("Analyst statement"), "Sample diluted 1:10 as per SOP.");
    await user.click(screen.getByRole("button", { name: "Add Statement" }));
    await waitFor(() => {
      expect(onAddStatement).toHaveBeenCalledWith("Sample diluted 1:10 as per SOP.", 7);
    });

    await user.click(screen.getByRole("radio", { name: "Yes" }));
    await user.type(screen.getByLabelText("Comment (optional)"), "Verified 32.5 C log.");
    await user.click(screen.getByRole("button", { name: "Save Answer" }));
    await waitFor(() => {
      expect(onAnswerChecklist).toHaveBeenCalledWith("item-1", "Yes", "Verified 32.5 C log.", 7);
    });
  });

  it("requires an assignable cause for LaboratoryErrorConfirmed and sends assessment, cause, password and version on sign", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onSignPhaseOne = vi.fn().mockResolvedValue(undefined);

    const readyDetail: OosInvestigationDetailDto = {
      ...baseDetail,
      entries: [
        {
          id: 1,
          phase: 1,
          kind: "AnalystStatement",
          checklistItemKey: null,
          answer: null,
          text: "Observed spill",
          parentEntryId: null,
          recordedByUserId: 3,
          recordedByName: "Ada Analyst",
          recordedAtUtc: "2026-11-20T10:00:00Z"
        },
        {
          id: 2,
          phase: 1,
          kind: "ChecklistAnswer",
          checklistItemKey: "item-1",
          answer: "Yes",
          text: "Checked",
          parentEntryId: null,
          recordedByUserId: 3,
          recordedByName: "Ada Analyst",
          recordedAtUtc: "2026-11-20T10:05:00Z"
        }
      ]
    };

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <PhaseOneSection
            detail={readyDetail}
            facts={facts}
            factsLoading={false}
            categories={categories}
            canContribute={true}
            canManage={true}
            onAddStatement={vi.fn()}
            onAnswerChecklist={vi.fn()}
            onSignPhaseOne={onSignPhaseOne}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    const signBtn = screen.getByRole("button", { name: "Sign Phase I" }) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText(/Select an assignable cause category/i)).toBeTruthy();

    await user.click(screen.getByLabelText("Assignable Cause Category"));
    expect(screen.queryByRole("option", { name: "No cause found" })).toBeNull();
    await user.click(screen.getByRole("option", { name: "Analyst error" }));
    await user.type(screen.getByLabelText("Cause Description"), "Pipette tip leaked during dilution.");

    expect((screen.getByRole("button", { name: "Sign Phase I" }) as HTMLButtonElement).disabled).toBe(false);
    await user.click(screen.getByRole("button", { name: "Sign Phase I" }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog.textContent).toContain("Phase I concluded");
    await user.type(screen.getByLabelText(/Signing Password|Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(onSignPhaseOne).toHaveBeenCalledWith(
        {
          assessment: "LaboratoryErrorConfirmed",
          causeCategoryId: 1,
          causeDescription: "Pipette tip leaked during dilution.",
          password: "Secret123!"
        },
        7
      );
    });
  });

  it("shows server refusal inline and keeps statement and checklist inputs on failure", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onAddStatement = vi.fn().mockRejectedValue({
      response: { data: { message: "Only assigned analysts may add statements." } }
    });
    const onAnswerChecklist = vi.fn().mockResolvedValue("Checklist item already locked.");

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <PhaseOneSection
            detail={baseDetail}
            facts={facts}
            factsLoading={false}
            categories={categories}
            canContribute={true}
            canManage={true}
            onAddStatement={onAddStatement}
            onAnswerChecklist={onAnswerChecklist}
            onSignPhaseOne={vi.fn()}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    const statementField = screen.getByLabelText("Analyst statement") as HTMLTextAreaElement;
    await user.type(statementField, "Sample diluted 1:10.");
    await user.click(screen.getByRole("button", { name: "Add Statement" }));

    expect(await screen.findByText("Only assigned analysts may add statements.")).toBeTruthy();
    expect(statementField.value).toBe("Sample diluted 1:10.");

    await user.click(screen.getByRole("radio", { name: "Yes" }));
    const commentField = screen.getByLabelText("Comment (optional)") as HTMLInputElement;
    await user.type(commentField, "Checked log.");
    await user.click(screen.getByRole("button", { name: "Save Answer" }));

    expect(await screen.findByText("Checklist item already locked.")).toBeTruthy();
    expect(commentField.value).toBe("Checked log.");
  });

  it("keeps SignatureDialog open and shows refusal when Phase I sign fails", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onSignPhaseOne = vi.fn().mockRejectedValue({
      response: { data: { message: "All checklist items must be answered before signing Phase I." } }
    });

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <PhaseOneSection
            detail={baseDetail}
            facts={facts}
            factsLoading={false}
            categories={categories}
            canContribute={true}
            canManage={true}
            onAddStatement={vi.fn()}
            onAnswerChecklist={vi.fn()}
            onSignPhaseOne={onSignPhaseOne}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("radio", { name: /No Laboratory Error Found/i }));
    await user.click(screen.getByRole("button", { name: "Sign Phase I" }));

    await screen.findByRole("dialog");
    await user.type(screen.getByLabelText(/Signing Password|Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(
        screen.getByText("All checklist items must be answered before signing Phase I.")
      ).toBeTruthy();
    });
    expect(screen.getByRole("dialog")).toBeTruthy();
  });
});
