import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { EquipmentHistoryPrint } from "./EquipmentHistoryPrint";
import type { InstrumentHistoryResponse } from "../types/equipmentTypes";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const mockHistory: InstrumentHistoryResponse = {
  equipmentId: 1,
  equipmentCode: "EQ-001",
  instrumentType: "Incubator",
  manufacturerName: "Memmert",
  serialNumber: "SN-12345",
  location: "Cleanroom 2",
  status: "InService",
  calibrationIntervalDescription: "Every 12 months",
  pmIntervalDescription: "Every 6 months",
  tasks: [
    {
      id: 1,
      equipmentId: 1,
      equipmentCode: "EQ-001",
      instrumentType: "Incubator",
      type: "Calibration",
      originalDueDate: "2026-03-10",
      dueDate: "2026-04-14",
      completedOn: "2026-04-14",
      status: "Done",
      evidenceLabel: "cert_2026.pdf"
    }
  ],
  reschedules: [
    {
      taskId: 1,
      taskType: "Calibration",
      oldValue: "2026-03-10",
      newValue: "2026-04-14",
      reason: "Vendor delay",
      performedAt: "2026-03-09T08:00:00Z",
      performedByUserId: 1,
      performedByName: "Alice"
    }
  ],
  statusChanges: [
    {
      id: 1,
      previousStatus: "OutOfService",
      newStatus: "InService",
      comment: "Calibration recorded; returned to service",
      changedAt: "2026-04-14T11:00:00Z",
      changedByUserId: 1,
      changedByName: "Alice"
    }
  ],
  logbook: [
    {
      id: 1,
      equipmentInventoryId: 1,
      entryType: "PreventiveMaintenance",
      performedOn: "2026-04-14",
      performedByUserId: 1,
      performedByUserName: "Alice",
      workDone: "Routine inspection and cleaning",
      partsReplaced: null,
      findings: "Good condition",
      calendarEventId: null,
      recordedAt: "2026-04-14T11:30:00Z",
      voidedAt: null,
      voidedByUserId: null,
      voidReason: null,
      electronicSignatureId: 10
    }
  ],
  calibrationBaseline: {
    date: "2025-03-10",
    label: "Baseline"
  },
  pmBaseline: {
    date: "2025-10-15",
    label: "Baseline"
  },
  nextCalibrationDue: "2027-04-14",
  nextPmDue: "2026-10-14"
};

describe("EquipmentHistoryPrint", () => {
  it("shows \"Planned 2026-03-10 · Done 2026-04-14\" and \"Baseline\" label", () => {
    render(<EquipmentHistoryPrint history={mockHistory} />);

    expect(screen.getByText(/Planned 2026-03-10 · Done 2026-04-14/i)).toBeTruthy();
    expect(screen.getAllByText(/Baseline/i).length).toBeGreaterThan(0);
    expect(screen.getByText("EQ-001")).toBeTruthy();
  });

  it("shows an automatic status change as System (automatic), not a user", () => {
    const automatic: InstrumentHistoryResponse = {
      ...mockHistory,
      statusChanges: [
        {
          id: 2,
          previousStatus: "InService",
          newStatus: "OutOfService",
          comment: "Calibration overdue since 2026-03-10 (automatic)",
          changedAt: "2026-04-15T00:05:00Z",
          changedByUserId: null,
          changedByName: null
        }
      ]
    };
    render(<EquipmentHistoryPrint history={automatic} />);

    expect(screen.getByText("System (automatic)")).toBeTruthy();
    expect(screen.queryByText(/User #/)).toBeNull();
  });
});
