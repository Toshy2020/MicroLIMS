import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { CapaActions } from "./CapaActions";
import { actionDetail, recordDetail, refusal } from "../testFixtures";
import { QA, SECTION_HEAD, ok, renderStep, serveLookups, versionHeader } from "../steps/stepTestUtils";
import type { CapaStatus, QualityCapaDetailDto, QualityRecordDetailDto } from "../../../services/qualityService";

const OWNER = { raise: true, investigate: true, labConclude: false, approve: false };

function capa(status: CapaStatus, overrides: Partial<QualityCapaDetailDto> = {}, record: Partial<QualityRecordDetailDto> = {}) {
  return recordDetail({
    type: "Capa",
    number: "CAPA-2026-0003",
    status,
    deviation: null,
    capa: {
      status,
      sourceRecordId: 5,
      sourceRecordNumber: "DEV-2026-0001",
      effectivenessCriteria: null,
      effectivenessMethod: null,
      effectivenessPlannedDate: null,
      effectivenessResult: null,
      effectivenessFindings: null,
      successorCapaId: null,
      successorCapaNumber: null,
      ...overrides
    },
    ...record
  });
}

const plan = {
  effectivenessCriteria: "No drift for 90 days",
  effectivenessMethod: "Read the incubator log weekly",
  effectivenessPlannedDate: "2027-03-01"
};

async function signWith(user: ReturnType<typeof userEvent.setup>, password: string) {
  await user.type(await screen.findByLabelText(/^Password/), password);
  await user.click(screen.getByRole("button", { name: "Sign" }));
}

async function choose(user: ReturnType<typeof userEvent.setup>, scope: HTMLElement, field: RegExp, option: string) {
  await user.click(within(scope).getByRole("combobox", { name: field }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("CapaActions", () => {
  beforeEach(() => {
    serveLookups(http);
    http.post.mockImplementation(() => ok({}));
    http.put.mockImplementation(() => ok({}));
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  describe("plan (owner)", () => {
    it("saves the plan with the exact request and the record version", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(CapaActions, capa("Plan", {}, { actions: [] }), { permissions: OWNER, currentUserId: 8 });

      const first = await screen.findByRole("group", { name: "Action 1" });
      await choose(user, first, /^Kind/, "Preventive");
      await user.type(within(first).getByLabelText(/^Description/), "Install a second probe");
      await choose(user, first, /^Responsible person/, "Ann Analyst (aanalyst)");
      await user.type(within(first).getByLabelText(/^Due date/), "2027-01-15");
      await user.type(screen.getByLabelText(/^What will be checked/), "No drift for 90 days");
      await user.type(screen.getByLabelText(/^How it will be checked/), "Read the incubator log weekly");
      await user.type(screen.getByLabelText(/^Planned date of the effectiveness check/), "2027-03-01");
      await user.click(screen.getByRole("button", { name: "Save plan" }));

      await waitFor(() => {
        expect(http.put).toHaveBeenCalledWith(
          "/quality/capas/1/plan",
          {
            actions: [{ kind: "Preventive", description: "Install a second probe", responsibleUserId: 7, dueDate: "2027-01-15" }],
            effectivenessCriteria: "No drift for 90 days",
            effectivenessMethod: "Read the incubator log weekly",
            effectivenessPlannedDate: "2027-03-01"
          },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks saving until every action and the effectiveness check are filled, and says why", async () => {
      renderStep(CapaActions, capa("Plan", {}, { actions: [] }), { permissions: OWNER, currentUserId: 8 });

      const save = await screen.findByRole("button", { name: "Save plan" });
      expect((save as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Fill in every action and the effectiveness check to save the plan.")).toBeTruthy();
    });

    it("shows the saved plan, lets saved actions be changed but not removed, and new ones be removed", async () => {
      const user = userEvent.setup();
      renderStep(CapaActions, capa("Plan", plan, { actions: [actionDetail({ id: 11, description: "Recalibrate the probe", dueDate: "2027-01-10" })] }), {
        permissions: OWNER,
        currentUserId: 8
      });

      const first = await screen.findByRole("group", { name: "Action 1" });
      expect((within(first).getByLabelText(/^Description/) as HTMLTextAreaElement).value).toBe("Recalibrate the probe");
      expect((screen.getByLabelText(/^What will be checked/) as HTMLTextAreaElement).value).toBe("No drift for 90 days");
      expect(within(first).queryByRole("button", { name: /^Remove/ })).toBeNull();

      await user.click(screen.getByRole("button", { name: "Add action" }));
      const second = screen.getByRole("group", { name: "Action 2" });
      await user.click(within(second).getByRole("button", { name: "Remove Action 2" }));
      expect(screen.queryByRole("group", { name: "Action 2" })).toBeNull();
    });

    it("shows the server's refusal as text beside the save button and does not reload", async () => {
      const user = userEvent.setup();
      http.put.mockRejectedValue(refusal("The effectiveness check planned date must be after the last action due date."));
      const { onChanged } = renderStep(CapaActions, capa("Plan", plan, { actions: [actionDetail({ dueDate: "2027-04-01" })] }), {
        permissions: OWNER,
        currentUserId: 8
      });

      await user.click(await screen.findByRole("button", { name: "Save plan" }));

      expect((await screen.findByRole("alert")).textContent).toBe(
        "The effectiveness check planned date must be after the last action due date."
      );
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("shows a person who is not the owner the plan without a save button", async () => {
      renderStep(CapaActions, capa("Plan", plan, { actions: [actionDetail()] }), { permissions: SECTION_HEAD, currentUserId: 11 });

      expect(await screen.findByText("The plan is written by the owner of this CAPA.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Save plan" })).toBeNull();
      expect((screen.getByLabelText(/^What will be checked/) as HTMLTextAreaElement).disabled).toBe(true);
    });
  });

  describe("plan approval (QA)", () => {
    it("approves the saved plan, signed with the server's meaning; a wrong password keeps the dialog open", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValueOnce(refusal("The password is incorrect."));
      const { onChanged } = renderStep(CapaActions, capa("Plan", plan, { actions: [actionDetail()] }), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Approve plan and sign" }));
      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("CAPA plan approved")).toBeTruthy();
      await signWith(user, "wrong");
      expect(await screen.findByText("The password is incorrect.")).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();

      await user.type(screen.getByLabelText(/^Password/), "right");
      await user.click(screen.getByRole("button", { name: "Sign" }));
      await waitFor(() => {
        expect(http.post).toHaveBeenLastCalledWith("/quality/capas/1/approve-plan", { password: "right" }, versionHeader(3));
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks approval until the owner has saved a complete plan", async () => {
      renderStep(CapaActions, capa("Plan", {}, { actions: [] }), { permissions: QA, currentUserId: 9 });

      expect((screen.getByRole("button", { name: "Approve plan and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("The owner has not saved a complete plan yet.")).toBeTruthy();
    });

    it("returns the plan with a comment and no signature", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(CapaActions, capa("Plan", plan, { actions: [actionDetail()] }), { permissions: QA, currentUserId: 9 });

      const returnButton = screen.getByRole("button", { name: "Return plan to owner" });
      expect((returnButton as HTMLButtonElement).disabled).toBe(true);
      await user.type(screen.getByLabelText(/^Comment/), "Name a person for each action");
      await user.click(returnButton);

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith("/quality/capas/1/return-plan", { comment: "Name a person for each action" }, versionHeader(3));
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
      expect(screen.queryByRole("dialog", { name: /sign/i })).toBeNull();
    });

    it("locks approval for the person who raised or owns the CAPA", async () => {
      renderStep(CapaActions, capa("Plan", plan, { actions: [actionDetail()] }), { permissions: QA, currentUserId: 7 });

      expect((screen.getByRole("button", { name: "Approve plan and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getAllByText("You raised or own this record, so you cannot approve or close it.").length).toBeGreaterThan(0);
    });
  });

  describe("completion verification (QA)", () => {
    it("locks verification while an action is unfinished and names it", async () => {
      renderStep(CapaActions, capa("PlanApproved", plan, { actions: [actionDetail({ description: "Recalibrate the probe" })] }), {
        permissions: QA,
        currentUserId: 9
      });

      expect((screen.getByRole("button", { name: "Verify completion and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Complete every action first. Unfinished: Recalibrate the probe")).toBeTruthy();
    });

    it("verifies completion once every action is done, signed with the server's meaning", async () => {
      const user = userEvent.setup();
      const done = actionDetail({ doneAtUtc: "2026-11-25T09:00:00Z", doneByUserId: 7, evidence: "Certificate attached" });
      const { onChanged } = renderStep(CapaActions, capa("PlanApproved", plan, { actions: [done] }), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Verify completion and sign" }));
      expect(within(await screen.findByRole("dialog", { name: /sign/i })).getByText("CAPA completion verified")).toBeTruthy();
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith("/quality/capas/1/verify-completion", { password: "pw" }, versionHeader(3));
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("shows people without QA rights that the actions are being carried out", async () => {
      renderStep(CapaActions, capa("PlanApproved", plan, { actions: [actionDetail()] }), { permissions: SECTION_HEAD, currentUserId: 11 });

      expect(await screen.findByText(/The responsible people are carrying out the actions/)).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Verify completion and sign" })).toBeNull();
    });
  });

  describe("effectiveness check (QA)", () => {
    const verified = (plannedDate: string) =>
      capa("CompletionVerified", { ...plan, effectivenessPlannedDate: plannedDate }, { actions: [actionDetail({ doneAtUtc: "2026-11-25T09:00:00Z" })] });

    it("is disabled before the planned date and says why", async () => {
      renderStep(CapaActions, verified("2999-12-31"), { permissions: QA, currentUserId: 9 });

      expect(
        (await screen.findAllByText("The effectiveness check cannot be signed before its planned date (2999-12-31).")).length
      ).toBeGreaterThan(0);
      expect((screen.getByRole("button", { name: "Sign effectiveness check" }) as HTMLButtonElement).disabled).toBe(true);
      expect((screen.getByRole("radio", { name: "Effective" }) as HTMLInputElement).disabled).toBe(true);
      expect((screen.getByLabelText(/^Findings/) as HTMLTextAreaElement).disabled).toBe(true);
      expect(http.post).not.toHaveBeenCalled();
    });

    it("signs 'effective' on or after the planned date with the exact request", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(CapaActions, verified("2000-01-01"), { permissions: QA, currentUserId: 9 });

      expect(screen.getByText("To be checked: No drift for 90 days")).toBeTruthy();
      await user.click(screen.getByRole("radio", { name: "Effective" }));
      await user.type(screen.getByLabelText(/^Findings/), "No drift in 90 days");
      await user.click(screen.getByRole("button", { name: "Sign effectiveness check" }));
      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("CAPA effectiveness checked")).toBeTruthy();
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/capas/1/effectiveness",
          { result: "Effective", findings: "No drift in 90 days", newCapaId: null, password: "pw" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("needs a successor CAPA for 'not effective': it is created from this CAPA and named in the signed request", async () => {
      const user = userEvent.setup();
      renderStep(CapaActions, verified("2000-01-01"), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("radio", { name: "Not effective" }));
      await user.type(screen.getByLabelText(/^Findings/), "Drift came back");
      const sign = screen.getByRole("button", { name: "Sign effectiveness check" });
      expect((sign as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("A not-effective CAPA needs a new CAPA created from this one. Create it first.")).toBeTruthy();

      http.post.mockImplementation(() => ok({ id: 21, version: 1, record: { id: 21, number: "CAPA-2026-0004" } }));
      await user.click(screen.getByRole("button", { name: "Create successor CAPA" }));
      const dialog = await screen.findByRole("dialog", { name: "Create CAPA" });
      expect(within(dialog).getByText("Raised from CAPA-2026-0003.")).toBeTruthy();
      await user.type(within(dialog).getByLabelText(/^Title/), "Fix the drift for good");
      await user.type(within(dialog).getByLabelText(/^Description/), "The first CAPA did not hold");
      await choose(user, dialog, /^Owner/, "Sam Head (shead)");
      await user.click(within(dialog).getByRole("button", { name: "Create CAPA" }));

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith("/quality/capas", {
          title: "Fix the drift for good",
          description: "The first CAPA did not hold",
          sectionId: 1,
          sourceRecordId: 1,
          links: null,
          ownerUserId: 8
        });
      });
      expect(await screen.findByText("Successor CAPA: CAPA-2026-0004")).toBeTruthy();

      http.post.mockImplementation(() => ok({}));
      await user.click(await screen.findByRole("button", { name: "Sign effectiveness check" }));
      await signWith(user, "pw");
      await waitFor(() => {
        expect(http.post).toHaveBeenLastCalledWith(
          "/quality/capas/1/effectiveness",
          { result: "NotEffective", findings: "Drift came back", newCapaId: 21, password: "pw" },
          versionHeader(3)
        );
      });
    });

    it("shows the server's refusal in the signature dialog, which stays open", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValue(refusal("The effectiveness check cannot be signed before its planned date."));
      const { onChanged } = renderStep(CapaActions, verified("2000-01-01"), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("radio", { name: "Effective" }));
      await user.type(screen.getByLabelText(/^Findings/), "Fine");
      await user.click(screen.getByRole("button", { name: "Sign effectiveness check" }));
      await signWith(user, "pw");

      expect(await screen.findByText("The effectiveness check cannot be signed before its planned date.")).toBeTruthy();
      expect(screen.getByRole("dialog", { name: /sign/i })).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("locks the check for the person who owns the CAPA", async () => {
      renderStep(CapaActions, verified("2000-01-01"), { permissions: QA, currentUserId: 8 });

      expect((screen.getByRole("button", { name: "Sign effectiveness check" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getAllByText("You raised or own this record, so you cannot approve or close it.").length).toBeGreaterThan(0);
    });
  });

  describe("cancelling and final states", () => {
    it("cancels through the CAPA route with a reason and a signature", async () => {
      const user = userEvent.setup();
      renderStep(CapaActions, capa("PlanApproved", plan, { actions: [actionDetail()] }), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Cancel record" }));
      await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Cancel record" }));
      await user.type(await screen.findByLabelText("Reason for cancelling"), "Superseded by CAPA-2026-0009");
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/capas/1/cancel",
          { reason: "Superseded by CAPA-2026-0009", password: "pw" },
          versionHeader(3)
        );
      });
    });

    it("offers no step on a closed CAPA", async () => {
      renderStep(CapaActions, capa("Closed", plan), { permissions: QA, currentUserId: 9 });

      expect(await screen.findByText("No further steps are open on this record.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Cancel record" })).toBeNull();
    });
  });
});
