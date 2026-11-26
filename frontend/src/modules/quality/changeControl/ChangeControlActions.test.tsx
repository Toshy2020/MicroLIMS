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

import { ChangeControlActions } from "./ChangeControlActions";
import { actionDetail, recordDetail, refusal } from "../testFixtures";
import { QA, SECTION_HEAD, ok, renderStep, serveLookups, versionHeader } from "../steps/stepTestUtils";
import type { ChangeControlStatus, QualityChangeControlAffectedDto, QualityChangeControlDetailDto, QualityRecordDetailDto } from "../../../services/qualityService";

const TEMPLATE = {
  id: 1,
  type: "ChangeControl",
  sectionId: 1,
  version: 1,
  items: [
    { key: "validation", order: 1, text: "Is revalidation needed?", isActive: true },
    { key: "training", order: 2, text: "Is training needed?", isActive: true },
    { key: "retired", order: 3, text: "Retired question", isActive: false }
  ]
};

const ANSWERS = [
  { id: 1, itemKey: "validation", itemText: "Is revalidation needed?", answer: "No", comment: "Same method", answeredByUserId: 8, answeredAtUtc: "2026-11-21T10:00:00Z", recordVersion: 3 },
  { id: 2, itemKey: "training", itemText: "Is training needed?", answer: "Yes", comment: null, answeredByUserId: 8, answeredAtUtc: "2026-11-21T10:00:00Z", recordVersion: 3 }
];

const HITS = {
  query: "Amox",
  totalMatches: 2,
  executionTimeMs: 1,
  categories: [
    {
      categoryName: "Items",
      count: 2,
      items: [
        { id: 12, entityType: "Item", title: "Amoxicillin 500mg", subtitle: "Tablet", status: "", matchedField: "", matchedValue: "", badgeColor: "", navigationTarget: "" },
        { id: 99, entityType: "Sample", title: "S-2026-0010", subtitle: "Sample", status: "", matchedField: "", matchedValue: "", badgeColor: "", navigationTarget: "" }
      ]
    }
  ]
};

function serve(answers: unknown[] = []) {
  serveLookups(http, {
    "/quality/config/templates/ChangeControl": () => ok(TEMPLATE),
    "/quality/1/checklist-answers": () => ok(answers),
    "/search/global": () => ok(HITS)
  });
}

function changeControl(status: ChangeControlStatus, overrides: Partial<QualityChangeControlDetailDto> = {}, record: Partial<QualityRecordDetailDto> = {}) {
  return recordDetail({
    type: "ChangeControl",
    number: "CC-2026-0002",
    status,
    deviation: null,
    changeControl: {
      status,
      reason: "New incubator model",
      impactSummary: null,
      rejectionReason: null,
      postChangeReview: null,
      unchangedJustification: null,
      affected: [],
      ...overrides
    },
    ...record
  });
}

const task = (overrides = {}) => actionDetail({ id: 31, kind: "ChangeTask", description: "Update the SOP", responsibleUserId: 7, dueDate: "2027-01-20", ...overrides });

async function signWith(user: ReturnType<typeof userEvent.setup>, password: string) {
  await user.type(await screen.findByLabelText(/^Password/), password);
  await user.click(screen.getByRole("button", { name: "Sign" }));
}

describe("ChangeControlActions", () => {
  beforeEach(() => {
    serve();
    http.post.mockImplementation(() => ok({}));
    http.put.mockImplementation(() => ok({}));
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  describe("proposed: affected records", () => {
    it("adds an existing affected record found with the search, sending the record version", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(ChangeControlActions, changeControl("Proposed"), { permissions: SECTION_HEAD, currentUserId: 8 });

      await user.type(await screen.findByLabelText("Search for the record"), "Amox");
      expect(screen.queryByRole("button", { name: "Choose S-2026-0010" })).toBeNull();
      await user.click(await screen.findByRole("button", { name: "Choose Amoxicillin 500mg" }));

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/change-controls/1/affected",
          { targetType: "Item", targetId: 12, newRecordCode: null },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("adds a new record by its planned code", async () => {
      const user = userEvent.setup();
      renderStep(ChangeControlActions, changeControl("Proposed"), { permissions: SECTION_HEAD, currentUserId: 8 });

      await user.click(await screen.findByRole("radio", { name: "New record" }));
      await user.type(screen.getByLabelText("Planned code or name"), "TD-NEW-PH");
      await user.click(screen.getByRole("button", { name: "Add new record" }));

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/change-controls/1/affected",
          { targetType: "TestDefinition", targetId: null, newRecordCode: "TD-NEW-PH" },
          versionHeader(3)
        );
      });
    });

    it("shows the server's refusal as text beside the picker", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValue(refusal("This affected record is already listed."));
      const { onChanged } = renderStep(ChangeControlActions, changeControl("Proposed"), { permissions: SECTION_HEAD, currentUserId: 8 });

      await user.type(await screen.findByLabelText("Search for the record"), "Amox");
      await user.click(await screen.findByRole("button", { name: "Choose Amoxicillin 500mg" }));

      expect((await screen.findByRole("alert")).textContent).toBe("This affected record is already listed.");
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("offers no affected-record editor to a person without Quality.Raise", async () => {
      renderStep(ChangeControlActions, changeControl("Proposed"), { permissions: { raise: false, investigate: false, labConclude: false, approve: false }, currentUserId: 11 });

      await screen.findByText("The Section Head records and signs the impact assessment.");
      expect(screen.queryByLabelText("Search for the record")).toBeNull();
    });
  });

  describe("proposed: impact assessment", () => {
    it("saves the summary, the checklist answers and the tasks with the exact request and version", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(ChangeControlActions, changeControl("Proposed"), { permissions: SECTION_HEAD, currentUserId: 8 });

      await user.type(await screen.findByLabelText(/^Impact summary/), "Two incubators move rooms");
      const revalidation = await screen.findByRole("group", { name: "Is revalidation needed?" });
      expect(screen.queryByText("Retired question")).toBeNull();
      await user.click(within(revalidation).getByRole("radio", { name: "No" }));
      await user.type(screen.getByLabelText("Comment for: Is revalidation needed?"), "Same method");
      await user.click(within(screen.getByRole("group", { name: "Is training needed?" })).getByRole("radio", { name: "Not applicable" }));

      await user.click(screen.getByRole("button", { name: "Add task" }));
      const first = screen.getByRole("group", { name: "Task 1" });
      await user.type(within(first).getByLabelText(/^Description/), "Update the SOP");
      await user.click(within(first).getByRole("combobox", { name: /^Responsible person/ }));
      await user.click(await screen.findByRole("option", { name: "Ann Analyst (aanalyst)" }));
      await user.type(within(first).getByLabelText(/^Due date/), "2027-01-20");
      await user.click(screen.getByRole("button", { name: "Save assessment" }));

      await waitFor(() => {
        expect(http.put).toHaveBeenCalledWith(
          "/quality/change-controls/1/assessment",
          {
            impactSummary: "Two incubators move rooms",
            tasks: [{ description: "Update the SOP", responsibleUserId: 7, dueDate: "2027-01-20" }],
            answers: [
              { itemKey: "validation", answer: "No", comment: "Same method" },
              { itemKey: "training", answer: "NotApplicable", comment: null }
            ]
          },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks signing until the assessment is saved, and says why", async () => {
      renderStep(ChangeControlActions, changeControl("Proposed"), { permissions: SECTION_HEAD, currentUserId: 8 });

      const sign = await screen.findByRole("button", { name: "Sign impact assessment" });
      expect((sign as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Save the impact assessment before signing.")).toBeTruthy();
    });

    it("locks signing while checklist items are unanswered", async () => {
      renderStep(ChangeControlActions, changeControl("Proposed", { impactSummary: "Two incubators move rooms" }), {
        permissions: SECTION_HEAD,
        currentUserId: 8
      });

      expect(await screen.findByText("Answer every checklist item first (2 left).")).toBeTruthy();
      expect((screen.getByRole("button", { name: "Sign impact assessment" }) as HTMLButtonElement).disabled).toBe(true);
    });

    it("signs the saved assessment with the server's meaning; a wrong password keeps the dialog open", async () => {
      const user = userEvent.setup();
      serve(ANSWERS);
      http.post.mockRejectedValueOnce(refusal("The password is incorrect."));
      const { onChanged } = renderStep(
        ChangeControlActions,
        changeControl("Proposed", { impactSummary: "Two incubators move rooms" }, { actions: [task()] }),
        { permissions: SECTION_HEAD, currentUserId: 8 }
      );

      const group = await screen.findByRole("group", { name: "Is revalidation needed?" });
      await waitFor(() => expect((within(group).getByRole("radio", { name: "No" }) as HTMLInputElement).checked).toBe(true));
      await user.click(screen.getByRole("button", { name: "Sign impact assessment" }));
      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("Change impact assessed")).toBeTruthy();
      await signWith(user, "wrong");
      expect(await screen.findByText("The password is incorrect.")).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();

      await user.type(screen.getByLabelText(/^Password/), "right");
      await user.click(screen.getByRole("button", { name: "Sign" }));
      await waitFor(() => {
        expect(http.post).toHaveBeenLastCalledWith("/quality/change-controls/1/sign-assessment", { password: "right" }, versionHeader(3));
      });
    });

    it("shows the server's refusal to save as text beside the save button", async () => {
      const user = userEvent.setup();
      http.put.mockRejectedValue(refusal("Existing change control tasks cannot be removed."));
      renderStep(ChangeControlActions, changeControl("Proposed", { impactSummary: "Two incubators move rooms" }, { actions: [task()] }), {
        permissions: SECTION_HEAD,
        currentUserId: 8
      });

      await user.click(await screen.findByRole("button", { name: "Save assessment" }));

      const alerts = await screen.findAllByRole("alert");
      expect(alerts.map((a) => a.textContent)).toContain("Existing change control tasks cannot be removed.");
    });

    it("lets saved tasks be changed but not removed", async () => {
      renderStep(ChangeControlActions, changeControl("Proposed", { impactSummary: "x" }, { actions: [task()] }), {
        permissions: SECTION_HEAD,
        currentUserId: 8
      });

      const first = await screen.findByRole("group", { name: "Task 1" });
      expect((within(first).getByLabelText(/^Description/) as HTMLTextAreaElement).value).toBe("Update the SOP");
      expect(within(first).queryByRole("button", { name: /^Remove/ })).toBeNull();
    });

    it("shows QA the reject action but no assessment editing; rejecting is confirmed, reasoned and signed", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(ChangeControlActions, changeControl("Proposed"), { permissions: QA, currentUserId: 9 });

      expect(await screen.findByText("The Section Head records and signs the impact assessment.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Save assessment" })).toBeNull();
      await user.click(screen.getByRole("button", { name: "Reject" }));
      const confirm = await screen.findByRole("alertdialog");
      await waitFor(() => expect(document.activeElement).toBe(within(confirm).getByRole("button", { name: "Cancel" })));
      await user.click(within(confirm).getByRole("button", { name: "Reject" }));
      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("Change control rejected")).toBeTruthy();
      await user.type(screen.getByLabelText("Reason for rejecting"), "No validation plan");
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/change-controls/1/reject",
          { reason: "No validation plan", password: "pw" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });
  });

  describe("assessed: approval", () => {
    it("approves with the server's meaning", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(ChangeControlActions, changeControl("Assessed", { impactSummary: "Two incubators move rooms" }), {
        permissions: QA,
        currentUserId: 9
      });

      await user.click(screen.getByRole("button", { name: "Approve and sign" }));
      expect(within(await screen.findByRole("dialog", { name: /sign/i })).getByText("Change control approved")).toBeTruthy();
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith("/quality/change-controls/1/approve", { password: "pw" }, versionHeader(3));
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks approval and rejection for the raiser or owner with the server's rule as the reason", async () => {
      renderStep(ChangeControlActions, changeControl("Assessed"), { permissions: QA, currentUserId: 8 });

      expect((screen.getByRole("button", { name: "Approve and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect((screen.getByRole("button", { name: "Reject" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getAllByText("You raised or own this record, so you cannot approve or close it.").length).toBeGreaterThan(0);
    });

    it("shows people without QA rights only the waiting text", async () => {
      renderStep(ChangeControlActions, changeControl("Assessed"), { permissions: SECTION_HEAD, currentUserId: 11 });

      expect(await screen.findByText("Waiting for QA to approve or reject the change.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Approve and sign" })).toBeNull();
    });
  });

  describe("approved for implementation: closure", () => {
    const approved = (tasks = [task({ doneAtUtc: "2026-12-01T09:00:00Z" })]) =>
      changeControl("ApprovedForImplementation", { impactSummary: "x" }, { actions: tasks });

    it("locks closure while a task is unfinished and names it", async () => {
      renderStep(ChangeControlActions, approved([task()]), { permissions: QA, currentUserId: 9 });

      expect((screen.getByRole("button", { name: "Close and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Complete every task first. Unfinished: Update the SOP")).toBeTruthy();
    });

    it("locks closure until the post-change review is written", async () => {
      renderStep(ChangeControlActions, approved(), { permissions: QA, currentUserId: 9 });

      expect((screen.getByRole("button", { name: "Close and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Write the post-change review first.")).toBeTruthy();
    });

    const affected = (overrides: Partial<QualityChangeControlAffectedDto> = {}): QualityChangeControlAffectedDto => ({
      linkId: 71,
      targetType: "Specification",
      targetId: 5,
      number: "TAMC",
      label: "Total aerobic count limit",
      isRestricted: false,
      changedAtUtc: null,
      changeAuditLogId: null,
      unchanged: true,
      ...overrides
    });
    const approvedWith = (list: QualityChangeControlAffectedDto[]) =>
      changeControl("ApprovedForImplementation", { impactSummary: "x", affected: list }, { actions: [task({ doneAtUtc: "2026-12-01T09:00:00Z" })] });

    it("asks for the justification only when an affected specification or test was not changed, and names them", async () => {
      renderStep(
        ChangeControlActions,
        approvedWith([
          affected(),
          affected({ linkId: 72, targetType: "TestDefinition", targetId: 6, number: "BIOBURDEN", label: "Bioburden test", unchanged: true }),
          affected({ linkId: 73, targetType: "Specification", targetId: 8, number: "PH", label: "pH limit", unchanged: false, changedAtUtc: "2026-12-01T08:00:00Z", changeAuditLogId: 40 }),
          affected({ linkId: 74, targetType: "Specification", targetId: 9, number: "SECRET", label: null, isRestricted: true, unchanged: true })
        ]),
        { permissions: QA, currentUserId: 9 }
      );

      const list = screen.getByRole("list", { name: "Affected records not changed" });
      expect(within(list).getByText(/Specification: TAMC/)).toBeTruthy();
      expect(within(list).getByText(/Total aerobic count limit/)).toBeTruthy();
      expect(within(list).getByText(/Test definition: BIOBURDEN/)).toBeTruthy();
      expect(within(list).getByText(/Bioburden test/)).toBeTruthy();
      expect(within(list).getByText(/SECRET/)).toBeTruthy();
      expect(within(list).getByText(/Restricted/)).toBeTruthy();
      expect(within(list).queryByText(/pH limit/)).toBeNull();
      expect(screen.getByLabelText(/^Why affected specifications or tests were not changed/)).toBeTruthy();
    });

    it("shows no justification field when every affected specification and test was changed or none is listed", async () => {
      renderStep(
        ChangeControlActions,
        approvedWith([
          affected({ unchanged: false, changedAtUtc: "2026-12-01T08:00:00Z", changeAuditLogId: 41 }),
          affected({ linkId: 75, targetType: "Item", targetId: 3, number: "ITM-3", label: "Plate", unchanged: false })
        ]),
        { permissions: QA, currentUserId: 9 }
      );

      expect(screen.queryByLabelText(/^Why affected specifications or tests were not changed/)).toBeNull();
      expect(screen.queryByRole("list", { name: "Affected records not changed" })).toBeNull();
      cleanup();
      renderStep(ChangeControlActions, approved(), { permissions: QA, currentUserId: 9 });
      expect(screen.queryByLabelText(/^Why affected specifications or tests were not changed/)).toBeNull();
    });

    it("closes with the review and the unchanged justification, signed with the server's meaning", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(ChangeControlActions, approvedWith([affected()]), { permissions: QA, currentUserId: 9 });

      await user.type(screen.getByLabelText(/^Post-change review/), "Both incubators qualified");
      await user.type(screen.getByLabelText(/^Why affected specifications or tests were not changed/), "The limits did not move");
      await user.click(screen.getByRole("button", { name: "Close and sign" }));
      expect(within(await screen.findByRole("dialog", { name: /sign/i })).getByText("Quality record closed")).toBeTruthy();
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/change-controls/1/close",
          { postChangeReview: "Both incubators qualified", unchangedJustification: "The limits did not move", password: "pw" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("sends no justification as null and shows the server's refusal in the dialog, which stays open", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValue(
        refusal("Record a justification for affected specifications or test definitions that were not changed.")
      );
      const { onChanged } = renderStep(ChangeControlActions, approved(), { permissions: QA, currentUserId: 9 });

      await user.type(screen.getByLabelText(/^Post-change review/), "Done");
      await user.click(screen.getByRole("button", { name: "Close and sign" }));
      await signWith(user, "pw");

      expect(
        await screen.findByText("Record a justification for affected specifications or test definitions that were not changed.")
      ).toBeTruthy();
      expect(http.post).toHaveBeenCalledWith(
        "/quality/change-controls/1/close",
        { postChangeReview: "Done", unchangedJustification: null, password: "pw" },
        versionHeader(3)
      );
      expect(screen.getByRole("dialog", { name: /sign/i })).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("shows task owners that the tasks are being carried out", async () => {
      renderStep(ChangeControlActions, approved([task()]), { permissions: SECTION_HEAD, currentUserId: 11 });

      expect(await screen.findByText(/The task owners are carrying out the tasks/)).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Close and sign" })).toBeNull();
    });
  });

  describe("cancelling and final states", () => {
    it("cancels through the change-control route", async () => {
      const user = userEvent.setup();
      renderStep(ChangeControlActions, changeControl("Assessed"), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Cancel record" }));
      await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Cancel record" }));
      await user.type(await screen.findByLabelText("Reason for cancelling"), "Project dropped");
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/change-controls/1/cancel",
          { reason: "Project dropped", password: "pw" },
          versionHeader(3)
        );
      });
    });

    it("offers no step on a rejected change control", async () => {
      renderStep(ChangeControlActions, changeControl("Rejected"), { permissions: QA, currentUserId: 9 });

      expect(await screen.findByText("No further steps are open on this record.")).toBeTruthy();
    });
  });
});
