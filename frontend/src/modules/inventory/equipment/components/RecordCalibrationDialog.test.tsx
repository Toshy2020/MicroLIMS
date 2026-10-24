import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RecordCalibrationDialog } from "./RecordCalibrationDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import type { EquipmentDocument } from "../types/equipmentTypes";
import { changeDateField } from "../../../../components/DateField.testUtils";

vi.mock("../../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Alice", username: "alice", role: "Analyst" })
}));

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
  it("Continue to sign is disabled until required fields are filled", async () => {
    vi.mocked(EquipmentInventoryService.getDocuments).mockResolvedValue([mockDoc]);

    render(
      <RecordCalibrationDialog
        open={true}
        equipmentId={1}
        equipmentCode="EQ-001"
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(EquipmentInventoryService.getDocuments).toHaveBeenCalledWith(1);
    });

    const continueBtn = screen.getByRole("button", { name: /Continue to sign/i }) as HTMLButtonElement;
    expect(continueBtn.disabled).toBe(true);

    // Select certificate
    const certSelect = screen.getByRole("combobox", { name: /Calibration Certificate/i });
    fireEvent.mouseDown(certSelect);
    const docOption = await screen.findByRole("option", { name: /cert_2026\.pdf/i });
    fireEvent.click(docOption);

    // Still disabled without date
    expect(continueBtn.disabled).toBe(true);

    // Enter Calibration Date
    changeDateField(/Calibration Date/i, "2026-04-14");

    // Enabled now that certificate and date are provided
    expect(continueBtn.disabled).toBe(false);
  });

  it("signing calls the API with the password", async () => {
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
    const onClose = vi.fn();
    render(
      <RecordCalibrationDialog
        open={true}
        equipmentId={1}
        equipmentCode="EQ-001"
        onClose={onClose}
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
    changeDateField(/Calibration Date/i, "2026-04-14");

    // Click Continue to sign
    const continueBtn = screen.getByRole("button", { name: /Continue to sign/i });
    fireEvent.click(continueBtn);

    // SignatureDialog is shown
    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(
      screen.getByText(
        "I confirm the calibration of EQ-001 on 2026-04-14 is recorded with certificate cert_2026.pdf."
      )
    ).toBeTruthy();

    // Fill password in SignatureDialog
    const passwordInput = screen.getByLabelText(/^Password/i);
    await userEvent.type(passwordInput, "ValidPassword123!");

    const signBtn = screen.getByRole("button", { name: "Sign" });
    fireEvent.click(signBtn);

    await waitFor(() => {
      expect(EquipmentPlanService.recordCalibration).toHaveBeenCalledWith(1, {
        certificateDocumentId: 101,
        calibratedOn: "2026-04-14",
        certificateNextDue: null,
        password: "ValidPassword123!"
      });
      expect(onSuccess).toHaveBeenCalled();
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("includes next due date in meaning statement when provided", async () => {
    vi.mocked(EquipmentInventoryService.getDocuments).mockResolvedValue([mockDoc]);

    render(
      <RecordCalibrationDialog
        open={true}
        equipmentId={1}
        equipmentCode="EQ-001"
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: /Calibration Certificate/i })).toBeTruthy();
    });

    const certSelect = screen.getByRole("combobox", { name: /Calibration Certificate/i });
    fireEvent.mouseDown(certSelect);
    const docOption = await screen.findByRole("option", { name: /cert_2026\.pdf/i });
    fireEvent.click(docOption);

    changeDateField(/Calibration Date/i, "2026-04-14");

    changeDateField(/Certificate Next-Due Date/i, "2027-04-14");

    const continueBtn = screen.getByRole("button", { name: /Continue to sign/i });
    fireEvent.click(continueBtn);

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(
      screen.getByText(
        "I confirm the calibration of EQ-001 on 2026-04-14 is recorded with certificate cert_2026.pdf, next due 2027-04-14."
      )
    ).toBeTruthy();
  });
});
