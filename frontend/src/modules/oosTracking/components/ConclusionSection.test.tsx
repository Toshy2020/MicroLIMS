import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { ConclusionSection } from "./ConclusionSection";
import type {
  OosCategoryDto,
  OosInvestigationDetailDto
} from "../../../services/oosInvestigationService";

const baseDetail: OosInvestigationDetailDto = {
  id: 30,
  number: "OOS1126030",
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
  version: 8,
  checklistItems: [],
  results: [],
  entries: [],
  retestPlans: [],
  reopenings: [],
  deviationRequests: [],
  timeline: []
};

const categories: OosCategoryDto[] = [
  { id: 1, name: "Equipment", isActive: true, isNoCauseFound: false, version: 1 },
  { id: 7, name: "No cause found", isActive: true, isNoCauseFound: true, version: 1 }
];

function setupAuth() {
  localStorage.setItem("microlims_username", "head1");
  localStorage.setItem("microlims_role", "SectionHead");
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_user_id", "1");
  localStorage.setItem("microlims_permissions", JSON.stringify(["Oos.View", "Oos.Manage"]));
}

describe("ConclusionSection", () => {
  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("disables Invalidated with visible reason when cause is No cause found", async () => {
    setupAuth();
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <ConclusionSection
            detail={baseDetail}
            categories={categories}
            canManage={true}
            onConclude={vi.fn()}
            onReopen={vi.fn()}
            onCloseExcursion={vi.fn()}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByLabelText("Root Cause Category"));
    await user.click(await screen.findByRole("option", { name: "No cause found" }));

    const invalidatedRadio = screen.getByRole("radio", { name: /OOS Invalidated/i }) as HTMLInputElement;
    expect(invalidatedRadio.disabled).toBe(true);
    expect(screen.getByText(/"No cause found" can only lead to "OOS Confirmed"\./i)).toBeTruthy();
  });

  it("shows server refusal and keeps SignatureDialog open when conclude fails", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onConclude = vi.fn().mockRejectedValue({
      response: { data: { message: "You performed a test in this investigation, so you cannot sign it." } }
    });

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <ConclusionSection
            detail={baseDetail}
            categories={categories}
            canManage={true}
            onConclude={onConclude}
            onReopen={vi.fn()}
            onCloseExcursion={vi.fn()}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByLabelText("Root Cause Category"));
    await user.click(await screen.findByRole("option", { name: "Equipment" }));
    await user.type(screen.getByLabelText("Root Cause Description"), "Sensor drift");
    await user.type(screen.getByLabelText("Conclusion Summary"), "Confirmed OOS result");
    await user.click(screen.getByRole("button", { name: "Sign & Conclude" }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog.textContent).toContain("Investigation concluded");
    await user.type(screen.getByLabelText(/Signing Password|Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(screen.getByText("You performed a test in this investigation, so you cannot sign it.")).toBeTruthy();
    });
    expect(screen.getByRole("dialog")).toBeTruthy();
  });
});
