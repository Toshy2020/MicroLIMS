import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { NewSampleDialog } from "./NewSampleDialog";
import { ReceiveService } from "../services/ReceiveService";
import { masterDataOptions } from "../../../services/masterDataOptions";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
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

describe("NewSampleDialog After Cleaning Receipt", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (ReceiveService.receiveAfterCleaning as any).mockResolvedValue({ sampleId: 777 });
  });

  it("in Physchem lab: loads Finished Product items, requires item and techniques, and submits them", async () => {
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    render(
      <PinnedLightTheme>
        <NewSampleDialog
          open={true}
          onClose={onClose}
          onSuccess={onSuccess}
          allowedCategories={["product", "ac"]}
          labMode={{ kind: "fixed", sectionId: 2, code: "FP" }}
        />
      </PinnedLightTheme>
    );

    // Step 1: Click "After Cleaning" category tile
    const acTile = await screen.findByText("After Cleaning");
    fireEvent.click(acTile);

    // Click Next
    const nextBtn = screen.getByRole("button", { name: /Next/i });
    fireEvent.click(nextBtn);

    // Step 2: Grid is visible
    await screen.findByText("Previous Product");
    expect(screen.getByText("Techniques")).toBeDefined();

    // Check that getItems was called for FinishedProduct
    expect(masterDataOptions.getItems).toHaveBeenCalledWith("FinishedProduct");

    // Try Save All Samples without filling required fields
    const saveBtn = screen.getByRole("button", { name: /Save All Samples/i });
    fireEvent.click(saveBtn);

    // Validation messages
    await waitFor(() => {
      expect(screen.getByText(/Please fix validation errors before saving/i)).toBeDefined();
    });
    expect(ReceiveService.receiveAfterCleaning).not.toHaveBeenCalled();

    // Fill Previous Product via Autocomplete
    const prevProdInput = screen.getByLabelText("Sample 1 previous product");
    fireEvent.change(prevProdInput, { target: { value: "Paracetamol" } });
    fireEvent.click(await screen.findByText("Paracetamol 500mg Tablets"));

    // Check Techniques: UV and HPLC
    const uvCheckbox = screen.getByLabelText("Sample 1 technique UV");
    const hplcCheckbox = screen.getByLabelText("Sample 1 technique HPLC");
    fireEvent.click(uvCheckbox);
    fireEvent.click(hplcCheckbox);

    // Fill Prev Batch No
    const prevBatchInput = screen.getByLabelText("Sample 1 previous batch number");
    fireEvent.change(prevBatchInput, { target: { value: "BATCH-AC-101" } });

    // Select Machine
    const machineSelect = screen.getByLabelText("Sample 1 machine");
    fireEvent.mouseDown(machineSelect);
    fireEvent.click(await screen.findByText("Blender 01"));

    // Select Cause of Testing
    const causeSelect = screen.getByLabelText("Sample 1 cause of testing");
    fireEvent.mouseDown(causeSelect);
    fireEvent.click(await screen.findByText("Routine"));

    // Fill Sampled By
    const sampledByInput = screen.getByPlaceholderText("Sampled By");
    fireEvent.change(sampledByInput, { target: { value: "Analyst Bob" } });

    // Save All
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(ReceiveService.receiveAfterCleaning).toHaveBeenCalledWith(
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
      );
      expect(onSuccess).toHaveBeenCalledWith(1);
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("in Micro lab: allows free-text previous product, techniques column not shown, submits null techniques", async () => {
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    render(
      <PinnedLightTheme>
        <NewSampleDialog
          open={true}
          onClose={onClose}
          onSuccess={onSuccess}
          allowedCategories={["product", "rm", "pm", "water", "em", "ac"]}
          labMode={{ kind: "fixed", sectionId: 1, code: "MICRO" }}
        />
      </PinnedLightTheme>
    );

    // Step 1: Click "After Cleaning" category tile
    const acTile = await screen.findByText("After Cleaning");
    fireEvent.click(acTile);

    // Click Next
    const nextBtn = screen.getByRole("button", { name: /Next/i });
    fireEvent.click(nextBtn);

    // Step 2: Grid is visible
    await screen.findByText("Previous Product");
    // Techniques column should NOT be present in Micro lab
    expect(screen.queryByText("Techniques")).toBeNull();

    // Fill free text previous product name
    const prevProdInput = screen.getByLabelText("Sample 1 previous product name");
    fireEvent.change(prevProdInput, { target: { value: "Custom Ointment" } });

    // Fill Prev Batch No
    const prevBatchInput = screen.getByLabelText("Sample 1 previous batch number");
    fireEvent.change(prevBatchInput, { target: { value: "BATCH-M-01" } });

    // Select Machine
    const machineSelect = screen.getByLabelText("Sample 1 machine");
    fireEvent.mouseDown(machineSelect);
    fireEvent.click(await screen.findByText("Blender 01"));

    // Select Cause of Testing
    const causeSelect = screen.getByLabelText("Sample 1 cause of testing");
    fireEvent.mouseDown(causeSelect);
    fireEvent.click(await screen.findByText("Routine"));

    // Fill Sampled By
    const sampledByInput = screen.getByPlaceholderText("Sampled By");
    fireEvent.change(sampledByInput, { target: { value: "Micro Analyst" } });

    // Save All
    const saveBtn = screen.getByRole("button", { name: /Save All Samples/i });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(ReceiveService.receiveAfterCleaning).toHaveBeenCalledWith(
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
      );
      expect(onSuccess).toHaveBeenCalledWith(1);
    });
  });
});
