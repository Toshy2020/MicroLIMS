import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RecordCalibrationDialog } from "./RecordCalibrationDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type { EquipmentDocument } from "../types/equipmentTypes";

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    recordCalibration: vi.fn()
  }
}));

vi.mock("../services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getDocuments: vi.fn()
  }
}));

const mockDoc: EquipmentDocument = {
  id: 101,
  equipmentInventoryId: 1,
  documentType: "CalibrationCertificate",
  originalFileName: "cert_2026.pdf",
  fileExtension: ".pdf",
  contentType: "application/pdf",
  fileSizeBytes: 1024,
  contentSha256: "hash123",
  uploadedByUserId: 1,
  uploadedByName: "Alice",
  uploadedAt: "2026-04-14T10:00:00Z",
  status: "Current",
  supersededByDocumentId: null,
  supersededAt: null,
  supersededByUserId: null,
  supersessionReason: null,
  voidedAt: null,
  voidedByUserId: null,
  voidReason: null
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("RecordCalibrationDialog", () => {
  it("requires certificate, date and password", async () => {
    vi.mocked(EquipmentInventoryService.getDocuments).mockResolvedValue([mockDoc]);

    render(
      <RecordCalibrationDialog
        open={true}
        equipmentId={1}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(EquipmentInventoryService.getDocuments).toHaveBeenCalledWith(1);
    });

    const submitBtn = screen.getByRole("button", { name: /Record Calibration/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(
        screen.getByText(/Please select a calibration certificate/i)
      ).toBeTruthy();
    });

    expect(EquipmentPlanService.recordCalibration).not.toHaveBeenCalled();
  });

  it("submits calibration when certificate, date and password are provided", async () => {
    vi.mocked(EquipmentInventoryService.getDocuments).mockResolvedValue([mockDoc]);
    vi.mocked(EquipmentPlanService.recordCalibration).mockResolvedValue({
      id: 50,
      equipmentInventoryId: 1,
      taskType: "Calibration",
      year: 2026,
      sequence: 1,
      isUnplanned: false,
      createdByRevisionId: 1,
      retiredByRevisionId: null,
      originalDueDate: "2026-04-10",
      dueDate: "2026-04-14",
      completedOn: "2026-04-14",
      calibrationDocumentId: 101,
      maintenanceLogEntryId: null,
      intervalSnapshot: "Every 6 months",
      version: 2
    });

    const onSuccess = vi.fn();
    render(
      <RecordCalibrationDialog
        open={true}
        equipmentId={1}
        onClose={vi.fn()}
        onSuccess={onSuccess}
      />
    );

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: /Calibration Certificate/i })).toBeTruthy();
    });

    // Select certificate
    const certSelect = screen.getByRole("combobox", { name: /Calibration Certificate/i });
    fireEvent.mouseDown(certSelect);
    const docOption = await screen.findByRole("option", { name: /cert_2026\.pdf/i });
    fireEvent.click(docOption);

    // Enter Calibration Date
    const dateInput = screen.getByLabelText(/Calibration Date/i);
    await userEvent.type(dateInput, "2026-04-14");

    // Enter Password
    const passwordInput = screen.getByLabelText(/^Password/i);
    await userEvent.type(passwordInput, "ValidPassword123!");

    const submitBtn = screen.getByRole("button", { name: /Record Calibration/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(EquipmentPlanService.recordCalibration).toHaveBeenCalledWith(1, {
        certificateDocumentId: 101,
        calibratedOn: "2026-04-14",
        certificateNextDue: null,
        password: "ValidPassword123!"
      });
      expect(onSuccess).toHaveBeenCalled();
    });
  });
});
