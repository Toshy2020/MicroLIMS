import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { RegisterTable, type RegisterColumn } from "../../../components/lab/RegisterTable";
import { useRowSelection } from "../../../components/lab/table/useRowSelection";
import { ReceiveService } from "../services/ReceiveService";
import type { BulkAssignResult, SampleAssignmentOptions } from "../types/receivingTypes";
import { BulkAssignDialog, bulkAssignAction } from "./BulkAssignDialog";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    getAssignmentOptions: vi.fn(),
    bulkAssign: vi.fn()
  }
}));

const options: SampleAssignmentOptions = {
  sampleId: 101,
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
      eligibleUserIds: [1, 3, 4]
    }
  ],
  users: [
    { id: 1, fullName: "Hana Head", username: "head", roleName: "Section Head", roleType: "SectionHead" },
    { id: 3, fullName: "Mona Ali", username: "mona", roleName: "Analyst", roleType: "Analyst" },
    { id: 4, fullName: "Omar Saleh", username: "omar", roleName: "Reviewer", roleType: "Reviewer" }
  ]
};

interface SampleRow {
  sampleId: number;
  referenceNumber: string;
}

const SAMPLE_ROWS: SampleRow[] = [
  { sampleId: 101, referenceNumber: "FP1026001" },
  { sampleId: 102, referenceNumber: "FP1026002" },
  { sampleId: 103, referenceNumber: "FP1026003" }
];

const COLUMNS: RegisterColumn<SampleRow>[] = [
  { key: "referenceNumber", label: "Reference" }
];

function SelectionHarness({
  initialIds = [101, 102, 103],
  onSelectionChange = vi.fn(),
  onAssigned = vi.fn()
}: {
  initialIds?: number[];
  onSelectionChange?: (ids: number[]) => void;
  onAssigned?: (res: BulkAssignResult) => void;
}) {
  const selection = useRowSelection<number | string>("test");
  const [initialized, setInitialized] = useState(false);
  const [open, setOpen] = useState(true);

  if (!initialized) {
    setInitialized(true);
    selection.togglePage(initialIds);
  }

  const selectedIds = [...selection.selected].map(Number);

  return (
    <div>
      <div data-testid="selected-ids">{selectedIds.join(",")}</div>
      <BulkAssignDialog
        open={open}
        sampleIds={selectedIds}
        selection={selection}
        onSelectionChange={onSelectionChange}
        onAssigned={onAssigned}
        onClose={() => setOpen(false)}
      />
    </div>
  );
}

function TableBulkAssignHarness() {
  const selection = useRowSelection<number | string>("register");
  const [dialogIds, setDialogIds] = useState<number[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);

  const bulkActions = [
    bulkAssignAction((ids) => {
      setDialogIds(ids);
      setDialogOpen(true);
    })
  ];

  return (
    <>
      <RegisterTable
        columns={COLUMNS}
        rows={SAMPLE_ROWS}
        getRowId={(r) => r.sampleId}
        empty={{ title: "No samples" }}
        selection={selection}
        bulkActions={bulkActions}
      />
      <BulkAssignDialog
        open={dialogOpen}
        sampleIds={dialogIds}
        selection={selection}
        onClose={() => setDialogOpen(false)}
      />
    </>
  );
}

describe("BulkAssignDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(ReceiveService.getAssignmentOptions).mockResolvedValue(options);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("dialog lists selected count, analyst picker and optional reason", async () => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <BulkAssignDialog
          open
          sampleIds={[101, 102, 103]}
          onClose={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(
      await screen.findByText((_c, el) => el?.tagName === "P" && /3 samples selected/i.test(el.textContent ?? ""))
    ).toBeTruthy();

    const picker = await screen.findByRole("combobox", { name: /Assign analyst/i });
    await user.click(picker);

    const listbox = await screen.findByRole("listbox");
    const optionLabels = within(listbox).getAllByRole("option").map((o) => o.textContent);
    expect(optionLabels).toEqual([
      "Hana Head (Section Head)",
      "Mona Ali (Analyst)",
      "Omar Saleh (Reviewer)"
    ]);

    await user.click(within(listbox).getByRole("option", { name: "Mona Ali (Analyst)" }));

    const reasonInput = screen.getByLabelText(/Reason for Assignment/i);
    expect(reasonInput).toBeTruthy();
  });

  it("offers the analysts of the first sample that has any, and says it skipped a voided one", async () => {
    vi.mocked(ReceiveService.getAssignmentOptions).mockImplementation(async (sampleId) =>
      sampleId === 101 ? { sampleId: 101, tests: [], users: [] } : { ...options, sampleId }
    );
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <BulkAssignDialog open sampleIds={[101, 102]} onClose={vi.fn()} />
      </PinnedLightTheme>
    );

    expect(await screen.findByText(/1 selected sample has no open test you can assign/i)).toBeTruthy();
    expect(screen.getByText(/eligible for sample #102/i)).toBeTruthy();
    await user.click(await screen.findByRole("combobox", { name: /Assign analyst/i }));
    expect(within(await screen.findByRole("listbox")).getAllByRole("option")).toHaveLength(3);
  });

  it("explains an empty analyst list when no selected sample has an open test", async () => {
    vi.mocked(ReceiveService.getAssignmentOptions).mockResolvedValue({ sampleId: 101, tests: [], users: [] });
    render(
      <PinnedLightTheme>
        <BulkAssignDialog open sampleIds={[101]} onClose={vi.fn()} />
      </PinnedLightTheme>
    );

    expect(await screen.findByText(/The selected sample has no open test you can assign/i)).toBeTruthy();
    expect(screen.getByText(/no analyst can be offered/i)).toBeTruthy();
  });

  it("result dialog shows assigned count and each refused row with its reason", async () => {
    const user = userEvent.setup();
    const bulkResult: BulkAssignResult = {
      assignedCount: 2,
      refusedCount: 1,
      rows: [
        { sampleId: 101, referenceNumber: "FP1026001", assigned: true, refusedReason: null },
        {
          sampleId: 102,
          referenceNumber: "FP1026002",
          assigned: false,
          refusedReason: "This sample has no open tests you can assign."
        },
        { sampleId: 103, referenceNumber: "FP1026003", assigned: true, refusedReason: null }
      ]
    };
    vi.mocked(ReceiveService.bulkAssign).mockResolvedValue(bulkResult);

    render(
      <PinnedLightTheme>
        <SelectionHarness />
      </PinnedLightTheme>
    );

    const picker = await screen.findByRole("combobox", { name: /Assign analyst/i });
    await user.click(picker);
    const listbox = await screen.findByRole("listbox");
    await user.click(within(listbox).getByRole("option", { name: "Mona Ali (Analyst)" }));

    const reasonInput = screen.getByLabelText(/Reason for Assignment/i);
    await user.type(reasonInput, "Morning shift");

    await user.click(screen.getByRole("button", { name: /Assign Analyst/i }));

    await waitFor(() =>
      expect(ReceiveService.bulkAssign).toHaveBeenCalledWith({
        sampleIds: [101, 102, 103],
        analystUserId: 3,
        reason: "Morning shift"
      })
    );

    expect(
      await screen.findByText((_c, el) => el?.tagName === "P" && /2 assigned/i.test(el.textContent ?? ""))
    ).toBeTruthy();
    expect(
      screen.getByText((_c, el) => el?.tagName === "P" && /1 refused/i.test(el.textContent ?? ""))
    ).toBeTruthy();
    expect(screen.getByText("FP1026002")).toBeTruthy();
    expect(screen.getByText("This sample has no open tests you can assign.")).toBeTruthy();
  });

  it("selection keeps only the refused rows afterwards", async () => {
    const user = userEvent.setup();
    const onSelectionChange = vi.fn();
    const bulkResult: BulkAssignResult = {
      assignedCount: 2,
      refusedCount: 1,
      rows: [
        { sampleId: 101, referenceNumber: "FP1026001", assigned: true, refusedReason: null },
        {
          sampleId: 102,
          referenceNumber: "FP1026002",
          assigned: false,
          refusedReason: "'Mona Ali' is not a member of the Finished Product laboratory section."
        },
        { sampleId: 103, referenceNumber: "FP1026003", assigned: true, refusedReason: null }
      ]
    };
    vi.mocked(ReceiveService.bulkAssign).mockResolvedValue(bulkResult);

    render(
      <PinnedLightTheme>
        <SelectionHarness onSelectionChange={onSelectionChange} />
      </PinnedLightTheme>
    );

    expect(screen.getByTestId("selected-ids").textContent).toBe("101,102,103");

    const picker = await screen.findByRole("combobox", { name: /Assign analyst/i });
    await user.click(picker);
    const listbox = await screen.findByRole("listbox");
    await user.click(within(listbox).getByRole("option", { name: "Mona Ali (Analyst)" }));

    await user.click(screen.getByRole("button", { name: /Assign Analyst/i }));

    await waitFor(() => expect(onSelectionChange).toHaveBeenCalledWith([102]));
    expect(screen.getByTestId("selected-ids").textContent).toBe("102");
  });

  it("wires as a BulkAction on RegisterTable only for users with Samples.AssignAnalyst", async () => {
    const user = userEvent.setup();

    localStorage.setItem("microlims_permissions", JSON.stringify([]));
    const { unmount } = render(
      <PinnedLightTheme>
        <MemoryRouter>
          <AuthProvider>
            <TableBulkAssignHarness />
          </AuthProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.queryByRole("checkbox", { name: "Select row 101" })).toBeNull();
    unmount();

    localStorage.setItem(
      "microlims_permissions",
      JSON.stringify([PERMISSIONS.SAMPLES_ASSIGN_ANALYST])
    );
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <AuthProvider>
            <TableBulkAssignHarness />
          </AuthProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("checkbox", { name: "Select row 101" }));
    await user.click(screen.getByRole("checkbox", { name: "Select row 102" }));

    const assignActionBtn = await screen.findByRole("button", { name: "Assign analyst" });
    await user.click(assignActionBtn);

    expect(
      await screen.findByText((_c, el) => el?.tagName === "P" && /2 samples selected/i.test(el.textContent ?? ""))
    ).toBeTruthy();
  });
});
