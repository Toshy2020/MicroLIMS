import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { EquipmentDetailsDialog } from "./EquipmentDetailsDialog";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type { EquipmentItem } from "../types/equipmentTypes";

vi.mock("../../../../contexts/AuthContext", () => ({
  useAuth: () => ({ permissions: [], user: { id: 1, fullName: "Alice" } })
}));

vi.mock("../services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getStatusHistory: vi.fn().mockResolvedValue([]),
    getDocuments: vi.fn().mockResolvedValue([])
  }
}));

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    saveSchedule: vi.fn(),
    getMaintenanceLog: vi.fn().mockResolvedValue([]),
    getInstrumentHistory: vi.fn().mockResolvedValue({
      equipmentId: 1,
      equipmentCode: "EQ-001",
      instrumentType: "Incubator",
      manufacturerName: "Memmert",
      serialNumber: "SN-12345",
      location: "Room 101",
      status: "InService",
      calibrationIntervalDescription: null,
      pmIntervalDescription: null,
      tasks: [],
      reschedules: [],
      statusChanges: [],
      logbook: [],
      calibrationBaseline: null,
      pmBaseline: null,
      nextCalibrationDue: null,
      nextPmDue: null
    })
  }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const mockEquipment: EquipmentItem = {
  id: 1,
  version: 1,
  code: "EQ-001",
  instrumentType: "Incubator",
  manufacturerName: "Memmert",
  serialNumber: "SN-12345",
  firmwareVersion: "1.0",
  location: "Room 101",
  calibrationDueDate: "2026-12-31",
  status: "InService",
  isCalibrationOverdue: false,
  sectionId: 1
};

describe("EquipmentDetailsDialog tabs", () => {
  it("renders Details, Schedule, Logbook, and History tabs and switches between them", async () => {
    render(
      <ThemeProvider theme={lightTheme}>
        <EquipmentDetailsDialog
          open={true}
          equipment={mockEquipment}
          onClose={vi.fn()}
          onEditEquipment={vi.fn()}
          onEquipmentUpdated={vi.fn()}
        />
      </ThemeProvider>
    );

    // Initial tab is Details
    expect(screen.getByText("Instrument Information")).toBeTruthy();

    // Click Schedule tab
    const scheduleTab = screen.getByRole("tab", { name: "Schedule" });
    fireEvent.click(scheduleTab);
    await waitFor(() => {
      expect(screen.getByText("Calibration & Maintenance Schedule")).toBeTruthy();
    });

    // Click Logbook tab
    const logbookTab = screen.getByRole("tab", { name: "Logbook" });
    fireEvent.click(logbookTab);
    await waitFor(() => {
      expect(screen.getByText("Equipment Maintenance Logbook")).toBeTruthy();
    });

    // Click History tab
    const historyTab = screen.getByRole("tab", { name: "History" });
    fireEvent.click(historyTab);
    await waitFor(() => {
      expect(screen.getByText("Equipment History & Maintenance Record")).toBeTruthy();
    });
  });
});
