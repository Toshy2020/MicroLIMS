import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { EquipmentScheduleSection } from "./EquipmentScheduleSection";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type { EquipmentItem } from "../types/equipmentTypes";

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    saveSchedule: vi.fn(),
    recordCalibration: vi.fn(),
    getMaintenanceLog: vi.fn(),
    addLogEntry: vi.fn(),
    voidLogEntry: vi.fn(),
    getInstrumentHistory: vi.fn()
  }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const mockEquipment: EquipmentItem = {
  id: 1,
  version: 4,
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

describe("EquipmentScheduleSection", () => {
  it("blocks save when value set without unit", async () => {
    render(<EquipmentScheduleSection equipment={mockEquipment} />);

    const calValueInput = screen.getByLabelText(/Calibration Interval/i);
    fireEvent.change(calValueInput, { target: { value: "6" } });

    const saveBtn = screen.getByRole("button", { name: /Save Schedule/i });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(
        screen.getByText(/Calibration interval unit is required when value is specified/i)
      ).toBeTruthy();
    });

    expect(EquipmentPlanService.saveSchedule).not.toHaveBeenCalled();
  });

  it("saves successfully when value and unit are both provided", async () => {
    vi.mocked(EquipmentPlanService.saveSchedule).mockResolvedValue({
      id: 1,
      equipmentId: 1,
      calibrationIntervalValue: 6,
      calibrationIntervalUnit: "Months",
      lastCalibratedOn: "2026-01-15",
      pmIntervalValue: 1,
      pmIntervalUnit: "Months",
      lastPmOn: "2026-01-20",
      version: 5
    });

    render(<EquipmentScheduleSection equipment={mockEquipment} />);

    const calValueInput = screen.getByLabelText(/Calibration Interval/i);
    fireEvent.change(calValueInput, { target: { value: "6" } });

    // Select Calibration Unit: "Months"
    const calUnitSelect = screen.getByLabelText(/Calibration Unit/i);
    fireEvent.mouseDown(calUnitSelect);
    const monthsOption = await screen.findByRole("option", { name: "Months" });
    fireEvent.click(monthsOption);

    const saveBtn = screen.getByRole("button", { name: /Save Schedule/i });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(EquipmentPlanService.saveSchedule).toHaveBeenCalledWith(
        1,
        expect.objectContaining({
          calibrationIntervalValue: 6,
          calibrationIntervalUnit: "Months"
        }),
        4
      );
    });
  });

  it("renders the updated maintenance baseline helper text", () => {
    render(<EquipmentScheduleSection equipment={mockEquipment} />);
    expect(
      screen.getByText("Last maintenance date from the paper logbook. Kept in the audit trail; not signed.")
    ).toBeTruthy();
  });

  it("initializes from equipment fields without as any and updates when reloaded equipment is passed", () => {
    const equipWithSchedule: EquipmentItem = {
      ...mockEquipment,
      calibrationIntervalValue: 12,
      calibrationIntervalUnit: "Months",
      lastCalibratedOn: "2026-02-01",
      pmIntervalValue: 3,
      pmIntervalUnit: "Months",
      lastPmOn: "2026-02-15"
    };

    const { rerender } = render(<EquipmentScheduleSection equipment={equipWithSchedule} />);
    expect(screen.getByDisplayValue("12")).toBeTruthy();
    expect(screen.getByDisplayValue("2026-02-01")).toBeTruthy();
    expect(screen.getByDisplayValue("3")).toBeTruthy();
    expect(screen.getByDisplayValue("2026-02-15")).toBeTruthy();

    const reloaded: EquipmentItem = {
      ...equipWithSchedule,
      calibrationIntervalValue: 24,
      lastCalibratedOn: "2026-05-10"
    };
    rerender(<EquipmentScheduleSection equipment={reloaded} />);
    expect(screen.getByDisplayValue("24")).toBeTruthy();
    expect(screen.getByDisplayValue("2026-05-10")).toBeTruthy();
  });
});
