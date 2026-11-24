import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AssignAnalystDialog } from "./AssignAnalystDialog";
import { ReceiveService } from "../services/ReceiveService";
import { SampleAssignmentOptions, SampleRecord } from "../types/receivingTypes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    getAssignmentOptions: vi.fn(),
    assignTestAnalysts: vi.fn()
  }
}));

const sample = {
  sampleId: 7,
  referenceNumber: "FP1026001",
  category: "FinishedProduct",
  displayName: "Example Tablet",
  batchNumber: "B-1",
  controlNumber: "C-1",
  assignedTests: [
    { testOrderId: 1, sectionId: 1, testCode: "TAMC", status: "Pending", locationCount: 0, assignedAnalystId: 3, assignedAnalystName: "Mona Ali" },
    { testOrderId: 2, sectionId: 1, testCode: "TYMC", status: "Pending", locationCount: 0, assignedAnalystId: null, assignedAnalystName: null },
    { testOrderId: 3, sectionId: 2, testCode: "ASSAY", status: "Pending", locationCount: 0, assignedAnalystId: 9, assignedAnalystName: "Fady Nabil" }
  ]
} as unknown as SampleRecord;

// Signed in as Section Head 1 (Micro): they appear only as an option for
// themselves; the FP test is outside their laboratory.
const options: SampleAssignmentOptions = {
  sampleId: 7,
  tests: [
    { testOrderId: 1, testCode: "TAMC", sectionId: 1, sectionName: "Microbiology", status: "Pending", assignedAnalystId: 3, assignedAnalystName: "Mona Ali", canAssign: true, eligibleUserIds: [1, 3, 4] },
    { testOrderId: 2, testCode: "TYMC", sectionId: 1, sectionName: "Microbiology", status: "Pending", assignedAnalystId: null, assignedAnalystName: null, canAssign: true, eligibleUserIds: [1, 3] },
    { testOrderId: 3, testCode: "ASSAY", sectionId: 2, sectionName: "Finished Product", status: "Pending", assignedAnalystId: 9, assignedAnalystName: "Fady Nabil", canAssign: false, eligibleUserIds: [] }
  ],
  users: [
    { id: 1, fullName: "Hana Head", username: "head", roleName: "Section Head", roleType: "SectionHead" },
    { id: 3, fullName: "Mona Ali", username: "mona", roleName: "Analyst", roleType: "Analyst" },
    { id: 4, fullName: "Omar Saleh", username: "omar", roleName: "Reviewer", roleType: "Reviewer" }
  ]
};

function renderDialog(onAssigned = vi.fn(), onClose = vi.fn()) {
  render(
    <PinnedLightTheme>
      <AssignAnalystDialog open sample={sample} onClose={onClose} onAssigned={onAssigned} />
    </PinnedLightTheme>
  );
  return { onAssigned, onClose };
}

async function pick(user: ReturnType<typeof userEvent.setup>, comboboxName: string, optionName: string) {
  await user.click(screen.getByRole("combobox", { name: comboboxName }));
  await user.click(within(screen.getByRole("listbox")).getByRole("option", { name: optionName }));
}

describe("AssignAnalystDialog", () => {
  beforeEach(() => {
    vi.mocked(ReceiveService.getAssignmentOptions).mockResolvedValue(options);
    vi.mocked(ReceiveService.assignTestAnalysts).mockResolvedValue(sample);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("offers each test only the people who may take it and locks tests outside the user's laboratory", async () => {
    const user = userEvent.setup();
    renderDialog();
    await screen.findByRole("combobox", { name: "Analyst for TAMC" });

    expect(screen.getByText("Mona Ali, Fady Nabil")).toBeTruthy();
    expect(screen.queryByRole("combobox", { name: "Analyst for ASSAY" })).toBeNull();
    expect(within(screen.getByTestId("assign-row-ASSAY")).getByText("Not your laboratory")).toBeTruthy();

    await user.click(screen.getByRole("combobox", { name: "Analyst for TYMC" }));
    const offered = within(screen.getByRole("listbox")).getAllByRole("option").map((o) => o.textContent);
    expect(offered).toEqual(["Unassigned", "Hana Head (Section Head)", "Mona Ali (Analyst)"]);
  });

  it("orders qualified analysts first and disables refused analysts with the server reason", async () => {
    const user = userEvent.setup();
    vi.mocked(ReceiveService.getAssignmentOptions).mockResolvedValue({
      ...options,
      tests: [
        {
          testOrderId: 1,
          testCode: "TAMC",
          sectionId: 1,
          sectionName: "Microbiology",
          status: "Pending",
          assignedAnalystId: null,
          assignedAnalystName: null,
          canAssign: true,
          eligibleUserIds: [1, 3, 4],
          competencyRefusals: [
            {
              userId: 1,
              code: "Competency.AssigneeNotQualified",
              params: { analyst: "Hana Head", test: "TAMC" },
              reason: "Hana Head is not qualified for TAMC."
            }
          ]
        }
      ]
    });

    renderDialog();
    await screen.findByRole("combobox", { name: "Analyst for TAMC" });
    await user.click(screen.getByRole("combobox", { name: "Analyst for TAMC" }));

    const listbox = screen.getByRole("listbox");
    const opts = within(listbox).getAllByRole("option");
    expect(opts.map((o) => o.textContent)).toEqual([
      "Unassigned",
      "Mona Ali (Analyst)",
      "Omar Saleh (Reviewer)",
      "Hana Head (Section Head)Hana Head is not qualified for TAMC."
    ]);
    const hanaOption = opts[3];
    expect(hanaOption.getAttribute("aria-disabled")).toBe("true");
  });

  it("saves a different analyst per test, sending only the tests that changed", async () => {
    const user = userEvent.setup();
    const { onAssigned, onClose } = renderDialog();
    await screen.findByRole("combobox", { name: "Analyst for TAMC" });

    await pick(user, "Analyst for TAMC", "Omar Saleh (Reviewer)");
    await pick(user, "Analyst for TYMC", "Hana Head (Section Head)");
    await user.type(screen.getByLabelText(/Reason for Assignment/), "Workload");
    await user.click(screen.getByRole("button", { name: "Save Assignment" }));

    await waitFor(() => expect(onAssigned).toHaveBeenCalledWith(sample));
    expect(ReceiveService.assignTestAnalysts).toHaveBeenCalledWith(7, [
      { testOrderId: 1, analystUserId: 4 },
      { testOrderId: 2, analystUserId: 1 }
    ], "Workload");
    expect(onClose).toHaveBeenCalled();
  });

  it("'Assign all tests to' fills only the tests that person may take", async () => {
    const user = userEvent.setup();
    renderDialog();
    await screen.findByRole("combobox", { name: "Analyst for TAMC" });

    await pick(user, "Assign all tests to", "Omar Saleh (Reviewer)");
    await user.click(screen.getByRole("button", { name: "Save Assignment" }));

    await waitFor(() => expect(ReceiveService.assignTestAnalysts).toHaveBeenCalled());
    // Omar is eligible for TAMC only; TYMC stays unassigned and is not sent.
    expect(ReceiveService.assignTestAnalysts).toHaveBeenCalledWith(7, [{ testOrderId: 1, analystUserId: 4 }], undefined);
  });

  it("closes without saving when nothing changed", async () => {
    const user = userEvent.setup();
    const { onClose } = renderDialog();
    await screen.findByRole("combobox", { name: "Analyst for TAMC" });

    await user.click(screen.getByRole("button", { name: "Save Assignment" }));

    expect(onClose).toHaveBeenCalled();
    expect(ReceiveService.assignTestAnalysts).not.toHaveBeenCalled();
  });
});
