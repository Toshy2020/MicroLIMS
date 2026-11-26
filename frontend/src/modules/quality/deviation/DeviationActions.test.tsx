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

import { DeviationActions } from "./DeviationActions";
import { recordDetail, refusal } from "../testFixtures";
import { NONE, QA, SECTION_HEAD, ok, renderStep, serveLookups, versionHeader } from "../steps/stepTestUtils";
import type { QualityDeviationDetailDto, QualityDeviationStatus, QualityRecordDetailDto } from "../../../services/qualityService";

// The default fixture: DEV-2026-0001, id 1, version 3, raised by user 7, owned by user 8.
function deviation(status: QualityDeviationStatus, overrides: Partial<QualityDeviationDetailDto> = {}, record: Partial<QualityRecordDetailDto> = {}) {
  const base = recordDetail().deviation as QualityDeviationDetailDto;
  return recordDetail({ status, deviation: { ...base, status, ...overrides }, ...record });
}

const investigated = {
  impactAssessment: "Plates of two batches were affected.",
  rootCauseCategoryId: 4,
  rootCauseCategoryName: "Equipment failure",
  rootCauseDescription: "Probe drifted.",
  proposedActions: "Replace the probe."
};

async function signWith(user: ReturnType<typeof userEvent.setup>, password: string) {
  await user.type(await screen.findByLabelText(/^Password/), password);
  await user.click(screen.getByRole("button", { name: "Sign" }));
}

async function choose(user: ReturnType<typeof userEvent.setup>, field: RegExp, option: string) {
  await user.click(screen.getByRole("combobox", { name: field }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("DeviationActions", () => {
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

  describe("assessment (Raised)", () => {
    it("accepts with classification, owner and due date, signed with the server's meaning, sending the record version", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(DeviationActions, deviation("Raised"), { permissions: QA });

      await choose(user, /^Classification/, "Major");
      await choose(user, /^Owner/, "Sam Head (shead)");
      await user.type(screen.getByLabelText("Due date"), "2026-12-15");
      await user.click(screen.getByRole("button", { name: "Accept and sign" }));

      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("Deviation assessed")).toBeTruthy();
      expect(within(dialog).getByText("Quinn QA")).toBeTruthy();
      expect(http.post).not.toHaveBeenCalled();
      await signWith(user, "correct-password");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/deviations/1/assess",
          { accept: true, classificationId: 2, ownerUserId: 8, dueDate: "2026-12-15", password: "correct-password" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("leaves the due date empty so the server computes it, and offers only active classifications and users", async () => {
      const user = userEvent.setup();
      renderStep(DeviationActions, deviation("Raised"), { permissions: QA });

      await user.click(screen.getByRole("combobox", { name: /^Classification/ }));
      expect(screen.queryByRole("option", { name: "Retired" })).toBeNull();
      await user.click(await screen.findByRole("option", { name: "Minor" }));
      await user.click(screen.getByRole("combobox", { name: /^Owner/ }));
      expect(screen.queryByRole("option", { name: /Left Person/ })).toBeNull();
      await user.click(await screen.findByRole("option", { name: "Ann Analyst (aanalyst)" }));
      await user.click(screen.getByRole("button", { name: "Accept and sign" }));
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/deviations/1/assess",
          { accept: true, classificationId: 1, ownerUserId: 7, dueDate: null, password: "pw" },
          versionHeader(3)
        );
      });
    });

    it("locks accepting until a classification and an owner are chosen and says why", async () => {
      renderStep(DeviationActions, deviation("Raised"), { permissions: QA });

      const accept = await screen.findByRole("button", { name: "Accept and sign" });
      expect((accept as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Choose a classification and an owner first.")).toBeTruthy();
    });

    it("keeps the signature dialog open and shows the server's message after a wrong password", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValue(refusal("The password is incorrect."));
      const { onChanged } = renderStep(DeviationActions, deviation("Raised"), { permissions: QA });

      await choose(user, /^Classification/, "Major");
      await choose(user, /^Owner/, "Sam Head (shead)");
      await user.click(screen.getByRole("button", { name: "Accept and sign" }));
      await signWith(user, "wrong");

      expect(await screen.findByText("The password is incorrect.")).toBeTruthy();
      expect(screen.getByRole("dialog", { name: /sign/i })).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("rejects as not a deviation: a confirmation that starts on Cancel, then a signature with a required reason", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(DeviationActions, deviation("Raised"), { permissions: QA });

      await user.click(screen.getByRole("button", { name: "Reject as not a deviation" }));
      const confirm = await screen.findByRole("alertdialog");
      await waitFor(() => expect(document.activeElement).toBe(within(confirm).getByRole("button", { name: "Cancel" })));
      await user.click(within(confirm).getByRole("button", { name: "Reject as not a deviation" }));

      const signature = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(signature).getByText("Deviation rejected")).toBeTruthy();
      await user.type(screen.getByLabelText("Reason for rejecting"), "Normal seasonal variation");
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/deviations/1/reject",
          { reason: "Normal seasonal variation", password: "pw" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("shows only the waiting text to a person who cannot assess", async () => {
      renderStep(DeviationActions, deviation("Raised"), { permissions: SECTION_HEAD, currentUserId: 8 });

      expect(await screen.findByText("Waiting for QA to assess this deviation.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Accept and sign" })).toBeNull();
      expect(screen.queryByRole("button", { name: "Cancel record" })).toBeNull();
    });
  });

  describe("investigation and laboratory conclusion", () => {
    it("saves the investigation with the exact request and the record version", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(DeviationActions, deviation("Investigation", { impactAssessment: null }), {
        permissions: { raise: true, investigate: true, labConclude: false, approve: false },
        currentUserId: 8
      });

      await user.type(screen.getByLabelText(/^Impact assessment/), "Two batches affected");
      await choose(user, /^Root cause category/, "Operator error");
      await user.type(screen.getByLabelText(/^Root cause(?! category)/), "Wrong media lot used");
      await user.type(screen.getByLabelText(/^Proposed actions/), "Retrain the operator");
      await user.click(screen.getByRole("button", { name: "Save investigation" }));

      await waitFor(() => {
        expect(http.put).toHaveBeenCalledWith(
          "/quality/deviations/1/investigation",
          {
            impactAssessment: "Two batches affected",
            rootCauseCategoryId: 5,
            rootCauseDescription: "Wrong media lot used",
            proposedActions: "Retrain the operator"
          },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("shows the server's refusal as text beside the save button", async () => {
      const user = userEvent.setup();
      http.put.mockRejectedValue(refusal("Select an active root cause category."));
      const { onChanged } = renderStep(DeviationActions, deviation("Investigation", investigated), {
        permissions: { raise: true, investigate: true, labConclude: false, approve: false },
        currentUserId: 8
      });

      await user.click(await screen.findByRole("button", { name: "Save investigation" }));

      const alert = await screen.findByRole("alert");
      expect(alert.textContent).toBe("Select an active root cause category.");
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("locks saving until every field is filled and says why", async () => {
      renderStep(DeviationActions, deviation("Investigation"), {
        permissions: { raise: true, investigate: true, labConclude: false, approve: false },
        currentUserId: 8
      });

      const save = await screen.findByRole("button", { name: "Save investigation" });
      expect((save as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Fill in every field to save the investigation.")).toBeTruthy();
    });

    it("does not offer the investigation to QA, which never investigates", async () => {
      renderStep(DeviationActions, deviation("Investigation", investigated), { permissions: QA, currentUserId: 9 });

      expect(await screen.findByText("The investigation is with the owner.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Save investigation" })).toBeNull();
      expect((screen.getByLabelText(/^Root cause(?! category)/) as HTMLTextAreaElement).disabled).toBe(true);
    });

    it("signs the laboratory conclusion with the server's meaning; a wrong password keeps the dialog open", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValueOnce(refusal("The password is incorrect."));
      const { onChanged } = renderStep(DeviationActions, deviation("Investigation", investigated), {
        permissions: SECTION_HEAD,
        currentUserId: 11
      });

      await user.click(await screen.findByRole("button", { name: "Sign laboratory conclusion" }));
      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("Laboratory conclusion signed")).toBeTruthy();
      await signWith(user, "wrong");
      expect(await screen.findByText("The password is incorrect.")).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();

      await user.type(screen.getByLabelText(/^Password/), "right");
      await user.click(screen.getByRole("button", { name: "Sign" }));
      await waitFor(() => {
        expect(http.post).toHaveBeenLastCalledWith("/quality/deviations/1/lab-conclusion", { password: "right" }, versionHeader(3));
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks the laboratory conclusion for the owner, with the server's rule as the reason", async () => {
      renderStep(DeviationActions, deviation("Investigation", investigated), { permissions: SECTION_HEAD, currentUserId: 8 });

      const sign = await screen.findByRole("button", { name: "Sign laboratory conclusion" });
      expect((sign as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("The owner cannot sign the laboratory conclusion.")).toBeTruthy();
    });

    it("locks the laboratory conclusion until the investigation is saved", async () => {
      renderStep(DeviationActions, deviation("Investigation"), { permissions: SECTION_HEAD, currentUserId: 11 });

      const sign = await screen.findByRole("button", { name: "Sign laboratory conclusion" });
      expect((sign as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Save the complete investigation before signing the laboratory conclusion.")).toBeTruthy();
    });
  });

  describe("QA approval (LabConcluded)", () => {
    const concluded = () => deviation("LabConcluded", investigated);

    it("approves with the CAPA decision and a comment, signed with the server's meaning", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(DeviationActions, concluded(), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("checkbox", { name: "A CAPA is required" }));
      await user.type(screen.getByLabelText(/^Comment/), "Plan the probe replacement");
      await user.click(screen.getByRole("button", { name: "Approve and sign" }));

      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("Quality record approved")).toBeTruthy();
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/deviations/1/approve",
          { capaRequired: true, comment: "Plan the probe replacement", password: "pw" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("approves without a comment as null and without a CAPA", async () => {
      const user = userEvent.setup();
      renderStep(DeviationActions, concluded(), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Approve and sign" }));
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/deviations/1/approve",
          { capaRequired: false, comment: null, password: "pw" },
          versionHeader(3)
        );
      });
    });

    it("returns to the investigation with a comment, no signature, and shows the refusal beside the action", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValueOnce(refusal("This step is unavailable in the deviation's current state."));
      const { onChanged } = renderStep(DeviationActions, concluded(), { permissions: QA, currentUserId: 9 });

      const returnButton = screen.getByRole("button", { name: "Return to investigation" });
      expect((returnButton as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Write a comment to return the conclusion.")).toBeTruthy();

      await user.type(screen.getByLabelText(/^Comment/), "Root cause is not supported by data");
      await user.click(returnButton);

      expect((await screen.findByRole("alert")).textContent).toBe("This step is unavailable in the deviation's current state.");
      expect(onChanged).not.toHaveBeenCalled();
      expect(screen.queryByRole("dialog", { name: /sign/i })).toBeNull();

      await user.click(screen.getByRole("button", { name: "Return to investigation" }));
      await waitFor(() => {
        expect(http.post).toHaveBeenLastCalledWith(
          "/quality/deviations/1/return",
          { comment: "Root cause is not supported by data" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks approval and return for the person who raised the record, with the server's rule as the reason", async () => {
      renderStep(DeviationActions, concluded(), { permissions: QA, currentUserId: 7 });

      expect((screen.getByRole("button", { name: "Approve and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect((screen.getByRole("button", { name: "Return to investigation" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getAllByText("You raised or own this record, so you cannot approve or close it.").length).toBeGreaterThan(0);
    });

    it("shows a person without QA rights only the waiting text", async () => {
      renderStep(DeviationActions, concluded(), { permissions: SECTION_HEAD, currentUserId: 11 });

      expect(await screen.findByText("Waiting for QA to approve or return the laboratory conclusion.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Approve and sign" })).toBeNull();
    });
  });

  describe("closure (Approved)", () => {
    const approved = (capaRequired: boolean) => deviation("Approved", { ...investigated, capaRequired });

    it("shows the server's 'CAPA required' refusal as text in the signature dialog, which stays open", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValue(refusal("A CAPA with an approved plan is required before closure."));
      const { onChanged } = renderStep(DeviationActions, approved(true), { permissions: QA, currentUserId: 9 });

      expect(screen.getByText("A CAPA is required. Its plan must be approved before this deviation can be closed.")).toBeTruthy();
      await user.click(screen.getByRole("button", { name: "Close and sign" }));
      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("Quality record closed")).toBeTruthy();
      await signWith(user, "pw");

      expect(await within(dialog).findByText("A CAPA with an approved plan is required before closure.")).toBeTruthy();
      expect(screen.getByRole("dialog", { name: /sign/i })).toBeTruthy();
      expect(http.post).toHaveBeenCalledWith("/quality/deviations/1/close", { password: "pw" }, versionHeader(3));
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("closes a deviation that needs no CAPA", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(DeviationActions, approved(false), { permissions: QA, currentUserId: 9 });

      expect(screen.getByText("No CAPA is required.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Create CAPA" })).toBeNull();
      await user.click(screen.getByRole("button", { name: "Close and sign" }));
      await signWith(user, "pw");

      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks closure for the person who owns the record", async () => {
      renderStep(DeviationActions, approved(false), { permissions: QA, currentUserId: 8 });

      expect((screen.getByRole("button", { name: "Close and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("You raised or own this record, so you cannot approve or close it.")).toBeTruthy();
    });

    it("creates the CAPA from the deviation with the deviation as its source, and opens it", async () => {
      const user = userEvent.setup();
      http.post.mockImplementation(() => ok({ id: 21, version: 1, record: { id: 21, number: "CAPA-2026-0001" } }));
      renderStep(DeviationActions, approved(true), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Create CAPA" }));
      const dialog = await screen.findByRole("dialog", { name: "Create CAPA" });
      expect(within(dialog).getByText("Raised from DEV-2026-0001.")).toBeTruthy();
      await user.type(within(dialog).getByLabelText(/^Title/), "Replace the incubator probe");
      await user.type(within(dialog).getByLabelText(/^Description/), "Probe drift caused the deviation");
      await user.click(within(dialog).getByRole("combobox", { name: /^Owner/ }));
      await user.click(await screen.findByRole("option", { name: "Sam Head (shead)" }));
      await user.click(within(dialog).getByRole("button", { name: "Create CAPA" }));

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith("/quality/capas", {
          title: "Replace the incubator probe",
          description: "Probe drift caused the deviation",
          sectionId: 1,
          sourceRecordId: 1,
          links: null,
          ownerUserId: 8
        });
      });
      await waitFor(() => expect(screen.getByTestId("location").textContent).toBe("/quality/21"));
    });
  });

  describe("cancelling and final states", () => {
    it("cancels with a confirmation, a reason and a signature", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(DeviationActions, deviation("Investigation", investigated), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Cancel record" }));
      const confirm = await screen.findByRole("alertdialog");
      expect(within(confirm).getByText("DEV-2026-0001 stays visible as cancelled. This cannot be undone.")).toBeTruthy();
      await user.click(within(confirm).getByRole("button", { name: "Cancel record" }));

      const signature = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(signature).getByText("Quality record cancelled")).toBeTruthy();
      await user.type(screen.getByLabelText("Reason for cancelling"), "Raised against the wrong incubator");
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/deviations/1/cancel",
          { reason: "Raised against the wrong incubator", password: "pw" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("offers no cancellation to a person without QA rights", async () => {
      renderStep(DeviationActions, deviation("Investigation", investigated), { permissions: SECTION_HEAD, currentUserId: 11 });

      await screen.findByRole("button", { name: "Save investigation" });
      expect(screen.queryByRole("button", { name: "Cancel record" })).toBeNull();
    });

    it.each<[string, Partial<QualityRecordDetailDto>, QualityDeviationStatus]>([
      ["closed", { status: "Closed" }, "Closed"],
      ["rejected", { status: "Rejected" }, "Rejected"],
      ["cancelled", { isCancelled: true, status: "Cancelled" }, "Cancelled"]
    ])("offers no step on a %s deviation", async (_name, record, status) => {
      renderStep(DeviationActions, deviation(status, investigated, record), { permissions: QA, currentUserId: 9 });

      expect(await screen.findByText("No further steps are open on this record.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Cancel record" })).toBeNull();
    });

    it("renders nothing for a record without deviation details", () => {
      renderStep(DeviationActions, recordDetail({ deviation: null }), { permissions: NONE });

      expect(screen.queryByRole("button")).toBeNull();
    });
  });
});
