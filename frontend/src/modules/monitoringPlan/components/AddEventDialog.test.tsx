import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AddEventDialog } from "./AddEventDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    createEvent: vi.fn()
  }
}));

vi.mock("../../inventory/equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getAll: vi.fn().mockResolvedValue([])
  }
}));

const mockEquipmentList: EquipmentItem[] = [
  {
    id: 42,
    code: "EQ-HPLC-01",
    instrumentType: "HPLC System",
    manufacturerName: "Agilent",
    serialNumber: "SN999",
    firmwareVersion: "2.1",
    location: "Lab A",
    calibrationDueDate: null,
    status: "InService",
    isCalibrationOverdue: false,
    sectionId: 1
  }
];

describe("AddEventDialog", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("requires a title and submits {type, title, date}", async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();

    render(
      <AddEventDialog
        open={true}
        onClose={onClose}
        initialDate="2026-10-25"
        onSubmit={onSubmit}
      />
    );

    const submitBtn = screen.getByRole("button", { name: "Add event" }) as HTMLButtonElement;
    // Title is empty initially -> button disabled
    expect(submitBtn.disabled).toBe(true);

    // On a day is selected by default when initialDate is passed
    expect(screen.getByLabelText("Specific date")).toBeTruthy();
    expect(screen.queryByLabelText("Whole month")).toBeNull();

    // Type a title
    const titleInput = screen.getByLabelText(/^Title/);
    fireEvent.change(titleInput, { target: { value: "Vendor service visit" } });

    expect(submitBtn.disabled).toBe(false);

    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          type: "VendorVisit",
          title: "Vendor service visit",
          date: "2026-10-25",
          month: null
        })
      );
    });

    expect(onClose).toHaveBeenCalled();
  });

  it("presets to Whole month when opened with initialMonth and submits month", async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(
      <AddEventDialog
        open={true}
        onClose={vi.fn()}
        initialMonth="2026-11"
        onSubmit={onSubmit}
      />
    );

    // Whole month is visible and preset
    expect(screen.getByLabelText("Whole month")).toBeTruthy();
    expect(screen.queryByLabelText("Specific date")).toBeNull();

    fireEvent.change(screen.getByLabelText(/^Title/), { target: { value: "Monthly audit" } });
    fireEvent.click(screen.getByRole("button", { name: "Add event" }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          type: "VendorVisit",
          title: "Monthly audit",
          month: "2026-11",
          date: null
        })
      );
    });
  });

  it("allows selecting an optional instrument from Autocomplete and sends equipmentInventoryId", async () => {
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue(mockEquipmentList);
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(
      <AddEventDialog
        open={true}
        onClose={vi.fn()}
        initialDate="2026-10-25"
        onSubmit={onSubmit}
      />
    );

    fireEvent.change(screen.getByLabelText(/^Title/), { target: { value: "HPLC PM" } });

    // Open Autocomplete
    const autocomplete = screen.getByLabelText(/Instrument \(optional\)/i);
    fireEvent.mouseDown(autocomplete);

    const option = await screen.findByText("EQ-HPLC-01 — HPLC System");
    fireEvent.click(option);

    fireEvent.click(screen.getByRole("button", { name: "Add event" }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "HPLC PM",
          equipmentInventoryId: 42
        })
      );
    });
  });

  it("submits using EquipmentPlanService when onSubmit is not provided", async () => {
    vi.mocked(EquipmentPlanService.createEvent).mockResolvedValue({} as any);
    const onClose = vi.fn();

    render(
      <AddEventDialog
        open={true}
        onClose={onClose}
        initialDate="2026-10-25"
      />
    );

    fireEvent.change(screen.getByLabelText(/^Title/), { target: { value: "Audit check" } });
    fireEvent.click(screen.getByRole("button", { name: "Add event" }));

    await waitFor(() => {
      expect(EquipmentPlanService.createEvent).toHaveBeenCalledWith(
        expect.objectContaining({
          type: "VendorVisit",
          title: "Audit check",
          date: "2026-10-25"
        })
      );
    });
  });
});
