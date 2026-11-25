import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { PhaseTwoSection } from "./PhaseTwoSection";
import type {
  OosChecklistFactsDto,
  OosInvestigationDetailDto
} from "../../../services/oosInvestigationService";

const baseDetail: OosInvestigationDetailDto = {
  id: 20,
  number: "OOS1126020",
  kind: "Oos",
  status: "PhaseTwo",
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
  phaseOneAssessment: "NoLaboratoryErrorFound",
  phaseOneCauseCategoryId: null,
  phaseOneCauseCategoryName: null,
  phaseOneCauseDescription: null,
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
  version: 5,
  checklistItems: [],
  results: [],
  entries: [
    {
      id: 41,
      phase: 2,
      kind: "Hypothesis",
      checklistItemKey: null,
      answer: null,
      text: "Check mixer speed variation during granulation",
      parentEntryId: null,
      recordedByUserId: 1,
      recordedByName: "Sam Head",
      recordedAtUtc: "2026-11-21T08:00:00Z"
    }
  ],
  retestPlans: [],
  reopenings: [],
  deviationRequests: [],
  timeline: []
};

const emptyFacts: OosChecklistFactsDto = { version: 5, orders: [] };

describe("PhaseTwoSection", () => {
  afterEach(() => {
    cleanup();
  });

  it("adds a hypothesis and records an outcome linked to an existing hypothesis", async () => {
    const user = userEvent.setup();
    const onAddEntry = vi.fn().mockResolvedValue(undefined);

    render(
      <PinnedLightTheme>
        <PhaseTwoSection
          detail={baseDetail}
          facts={emptyFacts}
          factsLoading={false}
          canContributeOrManage={true}
          onAddEntry={onAddEntry}
          onAnswerChecklist={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Check mixer speed variation during granulation")).toBeTruthy();

    await user.type(screen.getByLabelText("Outcome for this hypothesis"), "Mixer log confirmed normal speed.");
    await user.click(screen.getByRole("button", { name: "Record Outcome" }));
    await waitFor(() => {
      expect(onAddEntry).toHaveBeenCalledWith("HypothesisOutcome", "Mixer log confirmed normal speed.", 41, 5);
    });

    await user.type(screen.getByLabelText("New hypothesis"), "Check sampling thief cleanliness");
    await user.click(screen.getByRole("button", { name: "Add Hypothesis" }));
    await waitFor(() => {
      expect(onAddEntry).toHaveBeenCalledWith("Hypothesis", "Check sampling thief cleanliness", null, 5);
    });
  });
});
