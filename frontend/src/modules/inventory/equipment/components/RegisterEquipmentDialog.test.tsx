import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { RegisterEquipmentDialog } from "./RegisterEquipmentDialog";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";

vi.mock("../../../../hooks/useMyLabs", () => ({
  useMyLabs: () => ({ codes: ["QC"], labs: [{ sectionId: 1, sectionCode: "QC" }] })
}));

vi.mock("../../../../hooks/useLaboratorySections", () => ({
  useLaboratorySections: () => ({
    sections: [{ sectionId: 1, sectionCode: "QC", sectionName: "Quality Control" }],
    loading: false,
    error: null,
    reload: vi.fn()
  })
}));

vi.mock("../services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    create: vi.fn(),
    update: vi.fn()
  }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("RegisterEquipmentDialog", () => {
  it("displays the server error message when creating equipment with a duplicate serial number", async () => {
    vi.mocked(EquipmentInventoryService.create).mockRejectedValue({
      response: {
        data: {
          message: "Serial number 'SN-DUPLICATE-999' is already registered to another instrument."
        }
      }
    });

    render(
      <RegisterEquipmentDialog
        open={true}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
        editingItem={null}
      />
    );

    fireEvent.change(screen.getByLabelText(/Instrument Type/i), {
      target: { value: "Autoclave" }
    });
    fireEvent.change(screen.getByLabelText(/Equipment Code/i), {
      target: { value: "EQ-AC-01" }
    });
    fireEvent.change(screen.getByLabelText(/Location/i), {
      target: { value: "Prep Room" }
    });
    fireEvent.change(screen.getByLabelText(/Serial Number/i), {
      target: { value: "SN-DUPLICATE-999" }
    });

    const submitBtn = screen.getByRole("button", { name: "Register Equipment" });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(
        screen.getByText(
          "Serial number 'SN-DUPLICATE-999' is already registered to another instrument."
        )
      ).toBeTruthy();
    });
  });

  it("displays the server error message when updating equipment with a duplicate serial number", async () => {
    vi.mocked(EquipmentInventoryService.update).mockRejectedValue({
      response: {
        data: {
          message: "Serial number 'SN-EXISTING-123' is already registered to another instrument."
        }
      }
    });

    const existingEquipment = {
      id: 5,
      version: 2,
      instrumentType: "Incubator",
      manufacturerName: "Memmert",
      serialNumber: "SN-OLD",
      firmwareVersion: "1.0",
      code: "EQ-INC-05",
      location: "Room 101",
      calibrationDueDate: "2026-12-31",
      status: "InService" as const,
      isCalibrationOverdue: false,
      sectionId: 1
    };

    render(
      <RegisterEquipmentDialog
        open={true}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
        editingItem={existingEquipment}
      />
    );

    fireEvent.change(screen.getByLabelText(/Serial Number/i), {
      target: { value: "SN-EXISTING-123" }
    });

    const submitBtn = screen.getByRole("button", { name: "Save Changes" });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(
        screen.getByText(
          "Serial number 'SN-EXISTING-123' is already registered to another instrument."
        )
      ).toBeTruthy();
    });
  });
});
