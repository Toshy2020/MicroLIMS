import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
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

import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ComplaintActions } from "./ComplaintActions";
import { ReceiveComplaintDialog } from "./ReceiveComplaintDialog";
import { recordDetail, refusal } from "../testFixtures";
import { QA, SECTION_HEAD, ok, renderStep, serveLookups, versionHeader } from "../steps/stepTestUtils";
import type { ComplaintStatus, QualityComplaintDetailDto, QualityRecordDetailDto } from "../../../services/qualityService";

const hit = (id: number, entityType: string, title: string) => ({
  id, entityType, title, subtitle: "", status: "", matchedField: "", matchedValue: "", badgeColor: "", navigationTarget: ""
});

const SEARCH = {
  query: "x",
  totalMatches: 3,
  executionTimeMs: 1,
  categories: [
    {
      categoryName: "All",
      count: 3,
      items: [hit(12, "Item", "Amoxicillin 500mg"), hit(40, "Sample", "S-2026-0040"), hit(41, "Sample", "S-2026-0041")]
    }
  ]
};

function complaint(status: ComplaintStatus, overrides: Partial<QualityComplaintDetailDto> = {}, record: Partial<QualityRecordDetailDto> = {}) {
  return recordDetail({
    type: "Complaint",
    number: "CMP-2026-0005",
    status,
    deviation: null,
    complaint: {
      status,
      source: "Customer",
      receivedOn: "2026-11-18",
      externalReference: "EXT-77",
      itemId: 12,
      itemName: "Amoxicillin 500mg",
      lotText: "L-66",
      complaintText: "Tablets arrived discoloured.",
      whatToTest: null,
      findings: null,
      outcome: null,
      conclusionSummary: null,
      ...overrides
    },
    ...record
  });
}

async function signWith(user: ReturnType<typeof userEvent.setup>, password: string) {
  await user.type(await screen.findByLabelText(/^Password/), password);
  await user.click(screen.getByRole("button", { name: "Sign" }));
}

describe("ComplaintActions", () => {
  beforeEach(() => {
    serveLookups(http, { "/search/global": () => ok(SEARCH) });
    http.post.mockImplementation(() => ok({}));
    http.put.mockImplementation(() => ok({}));
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.clearAllMocks();
  });

  describe("request investigation (Received)", () => {
    it("requests the laboratory investigation with the exact request and version, without a signature", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(ComplaintActions, complaint("Received"), { permissions: QA, currentUserId: 9 });

      await user.click(await screen.findByRole("combobox", { name: /^Classification/ }));
      await user.click(await screen.findByRole("option", { name: "Major" }));
      await user.click(screen.getByRole("combobox", { name: /^Owner/ }));
      await user.click(await screen.findByRole("option", { name: "Sam Head (shead)" }));
      await user.type(screen.getByLabelText("Due date"), "2026-12-30");
      await user.type(screen.getByLabelText(/^What to test/), "The retained sample of the lot");
      await user.click(screen.getByRole("button", { name: "Request investigation" }));

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/complaints/1/request-investigation",
          { classificationId: 2, ownerUserId: 8, dueDate: "2026-12-30", whatToTest: "The retained sample of the lot" },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
      expect(screen.queryByRole("dialog", { name: /sign/i })).toBeNull();
    });

    it("locks the request until a classification, an owner and the test are given, and says why", async () => {
      renderStep(ComplaintActions, complaint("Received"), { permissions: QA, currentUserId: 9 });

      const submit = await screen.findByRole("button", { name: "Request investigation" });
      expect((submit as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Choose a classification and an owner, and say what to test.")).toBeTruthy();
    });

    it("shows the server's refusal as text beside the action", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValue(refusal("The due date cannot be before the current laboratory date."));
      const { onChanged } = renderStep(ComplaintActions, complaint("Received"), { permissions: QA, currentUserId: 9 });

      await user.click(await screen.findByRole("combobox", { name: /^Classification/ }));
      await user.click(await screen.findByRole("option", { name: "Minor" }));
      await user.click(screen.getByRole("combobox", { name: /^Owner/ }));
      await user.click(await screen.findByRole("option", { name: "Sam Head (shead)" }));
      await user.type(screen.getByLabelText("Due date"), "2020-01-01");
      await user.type(screen.getByLabelText(/^What to test/), "Retained sample");
      await user.click(screen.getByRole("button", { name: "Request investigation" }));

      expect((await screen.findByRole("alert")).textContent).toBe("The due date cannot be before the current laboratory date.");
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("shows people without QA rights only the waiting text", async () => {
      renderStep(ComplaintActions, complaint("Received"), { permissions: SECTION_HEAD, currentUserId: 11 });

      expect(await screen.findByText("Waiting for QA to request the laboratory investigation.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Request investigation" })).toBeNull();
    });
  });

  describe("investigation and laboratory report", () => {
    const owner = { raise: true, investigate: true, labConclude: false, approve: false };

    it("saves the findings and the linked samples with the exact request and version", async () => {
      const user = userEvent.setup();
      const { onChanged } = renderStep(ComplaintActions, complaint("Investigation", { whatToTest: "The retained sample" }), {
        permissions: owner,
        currentUserId: 8
      });

      expect(screen.getByText("To test: The retained sample")).toBeTruthy();
      await user.type(screen.getByLabelText(/^Findings/), "Discolouration confirmed in the retained sample");
      await user.type(screen.getByLabelText("Search for a sample"), "S-2026");
      await user.click(await screen.findByRole("button", { name: "Choose S-2026-0040" }));
      await user.type(screen.getByLabelText("Search for a sample"), "S-2026");
      await user.click(await screen.findByRole("button", { name: "Choose S-2026-0041" }));
      expect(screen.queryByRole("button", { name: "Choose Amoxicillin 500mg" })).toBeNull();
      await user.click(screen.getByRole("button", { name: "Remove S-2026-0041" }));
      await user.click(screen.getByRole("button", { name: "Save investigation" }));

      await waitFor(() => {
        expect(http.put).toHaveBeenCalledWith(
          "/quality/complaints/1/investigation",
          { findings: "Discolouration confirmed in the retained sample", sampleIds: [40] },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("sends no sample list when no sample is linked", async () => {
      const user = userEvent.setup();
      renderStep(ComplaintActions, complaint("Investigation"), { permissions: owner, currentUserId: 8 });

      await user.type(screen.getByLabelText(/^Findings/), "No defect found");
      await user.click(screen.getByRole("button", { name: "Save investigation" }));

      await waitFor(() => {
        expect(http.put).toHaveBeenCalledWith(
          "/quality/complaints/1/investigation",
          { findings: "No defect found", sampleIds: null },
          versionHeader(3)
        );
      });
    });

    it("shows the server's refusal to save as text beside the save button", async () => {
      const user = userEvent.setup();
      http.put.mockRejectedValue(refusal("You cannot read this sample."));
      const { onChanged } = renderStep(ComplaintActions, complaint("Investigation", { findings: "Saved findings" }), {
        permissions: owner,
        currentUserId: 8
      });

      await user.click(screen.getByRole("button", { name: "Save investigation" }));

      expect((await screen.findByRole("alert")).textContent).toBe("You cannot read this sample.");
      expect(onChanged).not.toHaveBeenCalled();
    });

    it("signs the laboratory report with the server's meaning; a wrong password keeps the dialog open", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValueOnce(refusal("The password is incorrect."));
      const { onChanged } = renderStep(ComplaintActions, complaint("Investigation", { findings: "Confirmed" }), {
        permissions: SECTION_HEAD,
        currentUserId: 11
      });

      await user.click(screen.getByRole("button", { name: "Sign laboratory report" }));
      const dialog = await screen.findByRole("dialog", { name: /sign/i });
      expect(within(dialog).getByText("Laboratory conclusion signed")).toBeTruthy();
      await signWith(user, "wrong");
      expect(await screen.findByText("The password is incorrect.")).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();

      await user.type(screen.getByLabelText(/^Password/), "right");
      await user.click(screen.getByRole("button", { name: "Sign" }));
      await waitFor(() => {
        expect(http.post).toHaveBeenLastCalledWith("/quality/complaints/1/lab-report", { password: "right" }, versionHeader(3));
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks the report for the owner and until the findings are saved", async () => {
      renderStep(ComplaintActions, complaint("Investigation", { findings: "Confirmed" }), { permissions: SECTION_HEAD, currentUserId: 8 });
      expect((screen.getByRole("button", { name: "Sign laboratory report" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("The owner cannot sign the laboratory conclusion.")).toBeTruthy();

      cleanup();
      renderStep(ComplaintActions, complaint("Investigation"), { permissions: SECTION_HEAD, currentUserId: 11 });
      expect((screen.getByRole("button", { name: "Sign laboratory report" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("Save the findings before signing the laboratory report.")).toBeTruthy();
    });

    it("does not offer the investigation to QA", async () => {
      renderStep(ComplaintActions, complaint("Investigation", { findings: "Confirmed" }), { permissions: QA, currentUserId: 9 });

      expect(await screen.findByText("The investigation is with the owner.")).toBeTruthy();
      expect(screen.queryByRole("button", { name: "Save investigation" })).toBeNull();
    });
  });

  describe("conclusion and closure (QA)", () => {
    it("concludes as NotJustified without a follow-up, and requires a follow-up deviation or CAPA for Justified", async () => {
      const user = userEvent.setup();
      serveLookups(http, {
        "/search/global": () => ok(SEARCH),
        "/quality/records": () =>
          ok({
            items: [
              { id: 71, number: "DEV-2026-0071", type: "Deviation", title: "Seal defect", status: "Investigation", isCancelled: false }
            ],
            totalCount: 1,
            page: 1,
            pageSize: 100
          })
      });
      const { onChanged } = renderStep(ComplaintActions, complaint("LabReported", { findings: "Confirmed" }), { permissions: QA, currentUserId: 9 });

      const sign = screen.getByRole("button", { name: "Conclude and sign" });
      expect((sign as HTMLButtonElement).disabled).toBe(true);
      await user.click(screen.getByRole("combobox", { name: /^Outcome/ }));
      await user.click(await screen.findByRole("option", { name: "Justified" }));
      await user.type(screen.getByLabelText(/^Summary of the conclusion/), "Storage fault at the supplier");
      expect((sign as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getByText("A justified complaint needs a linked deviation or CAPA.")).toBeTruthy();

      await user.click(await screen.findByRole("combobox", { name: /^Follow-up deviation or CAPA/ }));
      await user.click(await screen.findByRole("option", { name: /DEV-2026-0071/ }));
      await user.click(screen.getByRole("button", { name: "Conclude and sign" }));
      expect(within(await screen.findByRole("dialog", { name: /sign/i })).getByText("Complaint concluded")).toBeTruthy();
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/complaints/1/conclude",
          { outcome: "Justified", summary: "Storage fault at the supplier", password: "pw", followUpRecordId: 71 },
          versionHeader(3)
        );
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("locks the conclusion for the person who raised or owns the complaint", async () => {
      renderStep(ComplaintActions, complaint("LabReported"), { permissions: QA, currentUserId: 7 });

      expect((screen.getByRole("button", { name: "Conclude and sign" }) as HTMLButtonElement).disabled).toBe(true);
      expect(screen.getAllByText("You raised or own this record, so you cannot approve or close it.").length).toBeGreaterThan(0);
    });

    it("closes with a signature and shows the server's refusal in the dialog, which stays open", async () => {
      const user = userEvent.setup();
      http.post.mockRejectedValueOnce(refusal("Complete the unfinished action before closure: Notify the supplier"));
      const { onChanged } = renderStep(ComplaintActions, complaint("Concluded", { outcome: "Justified" }), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Close and sign" }));
      expect(within(await screen.findByRole("dialog", { name: /sign/i })).getByText("Quality record closed")).toBeTruthy();
      await signWith(user, "pw");
      expect(await screen.findByText("Complete the unfinished action before closure: Notify the supplier")).toBeTruthy();
      expect(onChanged).not.toHaveBeenCalled();

      await user.type(screen.getByLabelText(/^Password/), "pw2");
      await user.click(screen.getByRole("button", { name: "Sign" }));
      await waitFor(() => {
        expect(http.post).toHaveBeenLastCalledWith("/quality/complaints/1/close", { password: "pw2" }, versionHeader(3));
      });
      await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    });

    it("shows people without QA rights only the waiting text for conclusion and closure", async () => {
      renderStep(ComplaintActions, complaint("LabReported"), { permissions: SECTION_HEAD, currentUserId: 11 });
      expect(await screen.findByText("Waiting for QA to conclude the complaint.")).toBeTruthy();

      cleanup();
      renderStep(ComplaintActions, complaint("Concluded"), { permissions: SECTION_HEAD, currentUserId: 11 });
      expect(await screen.findByText("Waiting for QA to close the complaint.")).toBeTruthy();
    });
  });

  describe("cancelling and final states", () => {
    it("cancels through the complaint route", async () => {
      const user = userEvent.setup();
      renderStep(ComplaintActions, complaint("Received"), { permissions: QA, currentUserId: 9 });

      await user.click(screen.getByRole("button", { name: "Cancel record" }));
      await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Cancel record" }));
      await user.type(await screen.findByLabelText("Reason for cancelling"), "Duplicate of CMP-2026-0004");
      await signWith(user, "pw");

      await waitFor(() => {
        expect(http.post).toHaveBeenCalledWith(
          "/quality/complaints/1/cancel",
          { reason: "Duplicate of CMP-2026-0004", password: "pw" },
          versionHeader(3)
        );
      });
    });

    it("offers no step on a closed complaint", async () => {
      renderStep(ComplaintActions, complaint("Closed"), { permissions: QA, currentUserId: 9 });

      expect(await screen.findByText("No further steps are open on this record.")).toBeTruthy();
    });
  });
});

describe("ReceiveComplaintDialog", () => {
  beforeEach(() => {
    serveLookups(http, {
      "/org/my-sections": () => ok([{ sectionId: 1, sectionCode: "MIC", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" }]),
      "/search/global": () => ok(SEARCH)
    });
    http.post.mockImplementation(() => ok({ id: 30, version: 1, record: { id: 30, number: "CMP-2026-0006" } }));
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  function renderDialog() {
    const onCreated = vi.fn().mockResolvedValue(undefined);
    render(
      <PinnedLightTheme>
        <ReceiveComplaintDialog open onClose={vi.fn()} onCreated={onCreated} />
      </PinnedLightTheme>
    );
    return onCreated;
  }

  it("records the complaint with the product chosen from the search and sends the exact request", async () => {
    const user = userEvent.setup();
    const onCreated = renderDialog();

    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Title/), "Discoloured tablets");
    await user.type(within(dialog).getByLabelText(/^Source/), "Customer");
    await user.clear(within(dialog).getByLabelText(/^Date received/));
    await user.type(within(dialog).getByLabelText(/^Date received/), "2026-11-18");
    await user.type(within(dialog).getByLabelText("External reference"), "EXT-77");
    await user.type(within(dialog).getByLabelText("Search for the product"), "Amox");
    await user.click(await within(dialog).findByRole("button", { name: "Choose Amoxicillin 500mg" }));
    expect(within(dialog).getByText("Product: Amoxicillin 500mg")).toBeTruthy();
    expect(within(dialog).queryByRole("button", { name: "Choose S-2026-0040" })).toBeNull();
    await user.type(within(dialog).getByLabelText("Lot"), "L-66");
    await user.type(within(dialog).getByLabelText(/^Complaint text/), "Tablets arrived discoloured.");
    await user.click(within(dialog).getByRole("button", { name: "Receive" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith("/quality/complaints", {
        title: "Discoloured tablets",
        source: "Customer",
        receivedOn: "2026-11-18",
        externalReference: "EXT-77",
        itemId: 12,
        lotText: "L-66",
        complaintText: "Tablets arrived discoloured.",
        sectionId: null
      });
    });
    await waitFor(() => expect(onCreated).toHaveBeenCalledTimes(1));
    expect(onCreated.mock.calls[0][0].record.number).toBe("CMP-2026-0006");
  });

  it("cannot be submitted without a title, a source and the complaint text, and shows the server's refusal", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("The complaint received date cannot be in the future."));
    const onCreated = renderDialog();

    const dialog = await screen.findByRole("dialog");
    const submit = within(dialog).getByRole("button", { name: "Receive" });
    expect((submit as HTMLButtonElement).disabled).toBe(true);
    await user.type(within(dialog).getByLabelText(/^Title/), "T");
    await user.type(within(dialog).getByLabelText(/^Source/), "S");
    await user.type(within(dialog).getByLabelText(/^Complaint text/), "C");
    await user.click(submit);

    expect(await within(dialog).findByText("The complaint received date cannot be in the future.")).toBeTruthy();
    expect(onCreated).not.toHaveBeenCalled();
  });
});
