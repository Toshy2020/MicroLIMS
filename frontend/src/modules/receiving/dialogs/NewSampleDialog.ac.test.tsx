import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { NewSampleDialog } from "./NewSampleDialog";
import { ReceiveService } from "../services/ReceiveService";
import { masterDataOptions } from "../../../services/masterDataOptions";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    receiveBatch: vi.fn(),
    receiveAfterCleaning: vi.fn(),
    receiveItemBased: vi.fn(),
    receiveWater: vi.fn(),
    receiveEM: vi.fn(),
    receiptLabs: vi.fn().mockResolvedValue([]),
    itemGrades: vi.fn().mockResolvedValue([])
  }
}));

vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getCausesOfTesting: vi.fn().mockResolvedValue([{ id: 1, name: "Routine" }]),
    getSamplers: vi.fn().mockResolvedValue([{ id: 1, name: "John Doe" }]),
    getProductionStages: vi.fn().mockResolvedValue([]),
    getWaterSamplingPoints: vi.fn().mockResolvedValue([]),
    getWaterDepartments: vi.fn().mockResolvedValue([]),
    getDepartments: vi.fn().mockResolvedValue([]),
    getMachines: vi.fn().mockResolvedValue([{ id: 10, name: "Blender 01" }]),
    getItems: vi.fn().mockResolvedValue([
      { id: 42, name: "Paracetamol 500mg Tablets", category: "FinishedProduct" }
    ])
  }
}));

afterEach(cleanup);

function renderDialog(props: React.ComponentProps<typeof NewSampleDialog>) {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <NewSampleDialog {...props} />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("NewSampleDialog After Cleaning Receipt", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(ReceiveService.receiveBatch).mockResolvedValue({
      samples: [{ row: 0, sampleId: 777, referenceNumber: "AC-2026-0001" }]
    });
  });

  it("in Physchem lab: loads Finished Product items, requires item and techniques, and submits them", async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    renderDialog({
      open: true,
      onClose,
      onSuccess,
      allowedCategories: ["product", "ac"],
      labMode: { kind: "fixed", sectionId: 2, code: "FP" }
    });

    await user.click(await screen.findByText("After Cleaning"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await screen.findByText("Previous Product");
    expect(screen.getByText("Techniques")).toBeDefined();
    expect(masterDataOptions.getItems).toHaveBeenCalledWith("FinishedProduct");

    const saveBtn = screen.getByRole("button", { name: /Save All Samples/i });
    await user.click(saveBtn);

    await waitFor(() => {
      expect(screen.getByText(/Please fix validation errors before saving/i)).toBeDefined();
    });
    expect(ReceiveService.receiveBatch).not.toHaveBeenCalled();

    const prevProdInput = screen.getByLabelText("Sample 1 previous product");
    await user.type(prevProdInput, "Paracetamol");
    await user.click(await screen.findByRole("option", { name: "Paracetamol 500mg Tablets" }));

    await user.click(screen.getByLabelText("Sample 1 technique UV"));
    await user.click(screen.getByLabelText("Sample 1 technique HPLC"));

    await user.type(screen.getByLabelText("Sample 1 previous batch number"), "BATCH-AC-101");

    await user.click(screen.getByLabelText("Sample 1 machine"));
    await user.click(await screen.findByRole("option", { name: "Blender 01" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Bob");

    await user.click(saveBtn);

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "AfterCleaning",
        afterCleaning: [
          expect.objectContaining({
            machineId: 10,
            causeOfTestingId: 1,
            sampledBy: "Analyst Bob",
            previousProductItemId: 42,
            previousProductName: "Paracetamol 500mg Tablets",
            previousProductBatchNumber: "BATCH-AC-101",
            techniques: ["UV", "HPLC"],
            labSectionId: 2
          })
        ]
      });
      expect(onSuccess).toHaveBeenCalledWith([{ id: 777, referenceNumber: "AC-2026-0001" }]);
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("in Micro lab: allows free-text previous product, techniques column not shown, submits null techniques", async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    renderDialog({
      open: true,
      onClose,
      onSuccess,
      allowedCategories: ["product", "rm", "pm", "water", "em", "ac"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("After Cleaning"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await screen.findByText("Previous Product");
    expect(screen.queryByText("Techniques")).toBeNull();

    await user.type(screen.getByLabelText("Sample 1 previous product name"), "Custom Ointment");
    await user.type(screen.getByLabelText("Sample 1 previous batch number"), "BATCH-M-01");

    await user.click(screen.getByLabelText("Sample 1 machine"));
    await user.click(await screen.findByRole("option", { name: "Blender 01" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByPlaceholderText("Sampled By"), "Micro Analyst");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "AfterCleaning",
        afterCleaning: [
          expect.objectContaining({
            machineId: 10,
            causeOfTestingId: 1,
            sampledBy: "Micro Analyst",
            previousProductItemId: null,
            previousProductName: "Custom Ointment",
            previousProductBatchNumber: "BATCH-M-01",
            techniques: null,
            labSectionId: 1
          })
        ]
      });
      expect(onSuccess).toHaveBeenCalledWith([{ id: 777, referenceNumber: "AC-2026-0001" }]);
    });
  });

  it("after a successful receipt the parent receives the created samples", async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    vi.mocked(masterDataOptions.getItems).mockResolvedValueOnce([
      { id: 51, name: "Lactose Monohydrate", category: "RawMaterial" }
    ]);
    vi.mocked(ReceiveService.receiveBatch).mockResolvedValueOnce({
      samples: [
        { row: 0, sampleId: 101, referenceNumber: "RM-2026-001" },
        { row: 1, sampleId: 102, referenceNumber: "RM-2026-002" }
      ]
    });

    renderDialog({
      open: true,
      onClose,
      onSuccess,
      allowedCategories: ["rm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("Raw Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await screen.findByText("Sampled By");

    const row1Combos = screen.getAllByRole("combobox");
    await user.click(row1Combos[0]);
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));

    await user.click(row1Combos[1]);
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByPlaceholderText("Sampled By"), "Alice");

    await user.click(screen.getByRole("button", { name: /Add Another Sample/i }));
    const allCombos = screen.getAllByRole("combobox");
    await user.click(allCombos[3]);
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledTimes(1);
      expect(onSuccess).toHaveBeenCalledWith([
        { id: 101, referenceNumber: "RM-2026-001" },
        { id: 102, referenceNumber: "RM-2026-002" }
      ]);
      expect(onClose).toHaveBeenCalled();
    });
  });
});
