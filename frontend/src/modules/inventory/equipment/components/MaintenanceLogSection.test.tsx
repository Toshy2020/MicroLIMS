import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MaintenanceLogSection } from "./MaintenanceLogSection";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type { MaintenanceLogEntryResponse } from "../types/equipmentTypes";

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
  it("renders log entries and handles void action with reason and password", async () => {
    vi.mocked(EquipmentPlanService.getMaintenanceLog).mockResolvedValue(mockEntries);
    vi.mocked(EquipmentPlanService.voidLogEntry).mockResolvedValue({ message: "Voided" });

    render(<MaintenanceLogSection equipmentId={1} />);

    await waitFor(() => {
      expect(screen.getByText("Filter change and lubrication")).toBeTruthy();
    });

    // Click void button
    const voidBtn = screen.getByRole("button", { name: /Void this entry/i });
    fireEvent.click(voidBtn);

    // Dialog opens
    await waitFor(() => {
      expect(screen.getByText(/Void Maintenance Log Entry/i)).toBeTruthy();
    });

    const reasonInput = screen.getByLabelText(/Reason for Voiding/i);
    fireEvent.change(reasonInput, { target: { value: "Logged on wrong instrument" } });

    const passwordInput = screen.getByLabelText(/^Password/i);
    fireEvent.change(passwordInput, { target: { value: "ValidPassword123!" } });

    const submitVoidBtn = screen.getByRole("button", { name: /Void Entry/i });
    fireEvent.click(submitVoidBtn);

    await waitFor(() => {
      expect(EquipmentPlanService.voidLogEntry).toHaveBeenCalledWith(1, {
        reason: "Logged on wrong instrument",
        password: "ValidPassword123!"
      });
    });
  });
});
