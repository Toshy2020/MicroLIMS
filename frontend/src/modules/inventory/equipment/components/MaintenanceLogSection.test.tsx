import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MaintenanceLogSection } from "./MaintenanceLogSection";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type { MaintenanceLogEntryResponse } from "../types/equipmentTypes";

vi.mock("../../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Alice", username: "alice", role: "Analyst" })
}));

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    getMaintenanceLog: vi.fn(),
    voidLogEntry: vi.fn(),
    addLogEntry: vi.fn()
  }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const mockEntries: MaintenanceLogEntryResponse[] = [
  {
    id: 1,
    equipmentInventoryId: 1,
    entryType: "PreventiveMaintenance",
    performedOn: "2026-04-14",
    performedByUserId: 1,
    performedByUserName: "Alice",
    workDone: "Filter change and lubrication",
    partsReplaced: "Filter",
    findings: "Normal operation",
    calendarEventId: null,
    recordedAt: "2026-04-14T10:00:00Z",
    voidedAt: null,
    voidedByUserId: null,
    voidReason: null,
    electronicSignatureId: 101
  }
];

describe("MaintenanceLogSection", () => {
  it("renders the updated subtitle copy", () => {
    vi.mocked(EquipmentPlanService.getMaintenanceLog).mockResolvedValue([]);
    render(<MaintenanceLogSection equipmentId={1} equipmentCode="EQ-001" />);
    expect(
      screen.getByText("Every entry is signed and kept. A wrong entry is voided with a reason, never deleted.")
    ).toBeTruthy();
  });

  it("Continue to sign is disabled until required fields are filled", async () => {
    vi.mocked(EquipmentPlanService.getMaintenanceLog).mockResolvedValue(mockEntries);

    render(<MaintenanceLogSection equipmentId={1} equipmentCode="EQ-001" />);

    await waitFor(() => {
      expect(screen.getByText("Filter change and lubrication")).toBeTruthy();
    });

    const voidBtn = screen.getByRole("button", { name: /Void this entry/i });
    fireEvent.click(voidBtn);

    await waitFor(() => {
      expect(screen.getByText(/Void Maintenance Log Entry/i)).toBeTruthy();
    });

    const continueBtn = screen.getByRole("button", { name: /Continue to sign/i }) as HTMLButtonElement;
    expect(continueBtn.disabled).toBe(true);

    const reasonInput = screen.getByLabelText(/Reason for Voiding/i);
    fireEvent.change(reasonInput, { target: { value: "Logged on wrong instrument" } });

    expect(continueBtn.disabled).toBe(false);
  });

  it("signing calls the API with the password", async () => {
    vi.mocked(EquipmentPlanService.getMaintenanceLog).mockResolvedValue(mockEntries);
    vi.mocked(EquipmentPlanService.voidLogEntry).mockResolvedValue({ message: "Voided" });

    render(<MaintenanceLogSection equipmentId={1} equipmentCode="EQ-001" />);

    await waitFor(() => {
      expect(screen.getByText("Filter change and lubrication")).toBeTruthy();
    });

    const voidBtn = screen.getByRole("button", { name: /Void this entry/i });
    fireEvent.click(voidBtn);

    await waitFor(() => {
      expect(screen.getByText(/Void Maintenance Log Entry/i)).toBeTruthy();
    });

    const reasonInput = screen.getByLabelText(/Reason for Voiding/i);
    fireEvent.change(reasonInput, { target: { value: "Logged on wrong instrument" } });

    const continueBtn = screen.getByRole("button", { name: /Continue to sign/i });
    fireEvent.click(continueBtn);

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(
      screen.getByText("I am voiding logbook entry #1 for EQ-001. Reason: Logged on wrong instrument.")
    ).toBeTruthy();

    const passwordInput = screen.getByLabelText(/^Password/i);
    fireEvent.change(passwordInput, { target: { value: "ValidPassword123!" } });

    const signBtn = screen.getByRole("button", { name: "Sign" });
    fireEvent.click(signBtn);

    await waitFor(() => {
      expect(EquipmentPlanService.voidLogEntry).toHaveBeenCalledWith(1, {
        reason: "Logged on wrong instrument",
        password: "ValidPassword123!"
      });
    });
  });
});
