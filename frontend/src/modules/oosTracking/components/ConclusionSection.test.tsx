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
    const alertEl = screen.getByText(/"No cause found" can only lead to "OOS Confirmed"\./i);
    expect(alertEl).toBeTruthy();
    const describedById = invalidatedRadio.getAttribute("aria-describedby");
    expect(describedById).toBeTruthy();
    expect(document.getElementById(describedById!)).toBeTruthy();
    expect(
      screen.getByText(/Select a root cause category, enter a description when invalidating, and enter a conclusion summary to sign\./i)
    ).toBeTruthy();
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

  it("closes an excursion with signature", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onCloseExcursion = vi.fn().mockResolvedValue(undefined);
    const excursionDetail: OosInvestigationDetailDto = {
      ...baseDetail,
      kind: "Excursion",
      status: "PhaseOne",
      phaseOneAssessment: null,
      phaseOneSignedAtUtc: null,
      phaseOneSignedByUserId: null,
      phaseOneSignedByName: null
    };

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <ConclusionSection
            detail={excursionDetail}
            categories={categories}
            canManage={true}
            onConclude={vi.fn()}
            onReopen={vi.fn()}
            onCloseExcursion={onCloseExcursion}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    await user.click(screen.getByLabelText("Root Cause Category"));
    await user.click(await screen.findByRole("option", { name: "Equipment" }));
    await user.type(screen.getByLabelText("Root Cause Description"), "HVAC filter clogged");
    await user.type(screen.getByLabelText("Action Taken"), "Replaced HEPA filter");
    await user.click(screen.getByRole("button", { name: "Sign & Close Excursion" }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog.textContent).toContain("Excursion review closed");
    await user.type(screen.getByLabelText(/Signing Password|Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(onCloseExcursion).toHaveBeenCalledWith(
        {
          causeCategoryId: 1,
          causeDescription: "HVAC filter clogged",
          actionTaken: "Replaced HEPA filter",
          password: "Secret123!"
        },
        8
      );
    });
  });

  it("reopens a concluded investigation and keeps dialog open on server refusal", async () => {
    setupAuth();
    const user = userEvent.setup();
    const onReopen = vi
      .fn()
      .mockRejectedValueOnce({
        response: { data: { message: "Invalid signing password." } }
      })
      .mockResolvedValueOnce(undefined);

    const concludedDetail: OosInvestigationDetailDto = {
      ...baseDetail,
      status: "Concluded",
      conclusion: "Confirmed",
      rootCauseCategoryId: 1,
      rootCauseCategoryName: "Equipment",
      rootCauseDescription: "Sensor drift",
      conclusionSummary: "Confirmed OOS",
      concludedAtUtc: "2026-11-22T10:00:00Z",
      concludedByUserId: 1,
      concludedByName: "Sam Head"
    };

    render(
      <PinnedLightTheme>
        <AuthProvider>
          <ConclusionSection
            detail={concludedDetail}
            categories={categories}
            canManage={true}
            onConclude={vi.fn()}
            onReopen={onReopen}
            onCloseExcursion={vi.fn()}
          />
        </AuthProvider>
      </PinnedLightTheme>
    );

    const reasonInput = screen.getByLabelText("Reason for Reopening") as HTMLInputElement;
    await user.type(reasonInput, "New stability data received");
    await user.click(screen.getByRole("button", { name: "Reopen Investigation" }));

    await screen.findByRole("dialog");
    await user.type(screen.getByLabelText(/Signing Password|Password/i), "WrongPass!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(screen.getByText("Invalid signing password.")).toBeTruthy();
    });
    expect(screen.getByRole("dialog")).toBeTruthy();

    await user.type(screen.getByLabelText(/Signing Password|Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(onReopen).toHaveBeenCalledWith("New stability data received", "Secret123!", 8);
    });
  });
});
