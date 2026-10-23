import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { EventDialog } from "./EventDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import type { CalendarEventResponse } from "../types";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    updateEvent: vi.fn(),
    cancelEvent: vi.fn(),
    uploadEventDocument: vi.fn(),
    getDocumentContentUrl: (id: number) => `/api/calendar-event-documents/${id}/content`
  }
}));

vi.mock("../../inventory/equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getAll: vi.fn().mockResolvedValue([])
  }
}));

const mockEquipmentList: EquipmentItem[] = [
  {
    id: 3,
    code: "EQ-BAL-01",
    instrumentType: "Analytical Balance",
    manufacturerName: "Mettler",
    serialNumber: "SN123",
    firmwareVersion: "1.0",
    location: "Lab B",
    calibrationDueDate: null,
    status: "InService",
    isCalibrationOverdue: false,
    sectionId: 1
  },
  {
    id: 7,
    code: "EQ-INC-02",
    instrumentType: "Incubator",
    manufacturerName: "Memmert",
    serialNumber: "SN456",
    firmwareVersion: "2.0",
    location: "Lab B",
    calibrationDueDate: null,
    status: "InService",
    isCalibrationOverdue: false,
    sectionId: 1
  }
];

const ptEventNoDocs: CalendarEventResponse = {
  id: 10,
  type: "ProficiencyTesting",
  title: "Microbiology PT Round 4",
  date: "2026-10-30",
  month: null,
  note: "Send samples to external lab",
  equipmentInventoryId: null,
  equipmentCode: null,
  status: "Active",
  reportPending: true,
  documents: [],
  version: 1
};

const vendorEvent: CalendarEventResponse = {
  id: 11,
  type: "VendorVisit",
  title: "Service engineer visit",
  date: "2026-11-05",
  month: null,
  note: null,
  equipmentInventoryId: 3,
  equipmentCode: "EQ-BAL-01",
  status: "Active",
  reportPending: false,
  documents: [],
  version: 1
};

describe("EventDialog", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("shows \"Report pending\" for PT without documents", () => {
    render(
      <EventDialog
        event={ptEventNoDocs}
        open={true}
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />
    );

    expect(screen.getByText("Report pending")).toBeTruthy();
    expect(screen.getByDisplayValue("Microbiology PT Round 4")).toBeTruthy();
  });

  it("cancels an event with reason", async () => {
    vi.mocked(EquipmentPlanService.cancelEvent).mockResolvedValue({
      ...vendorEvent,
      status: "Cancelled"
    });
    const onChanged = vi.fn();
    const onClose = vi.fn();

    render(
      <EventDialog
        event={vendorEvent}
        open={true}
        onClose={onClose}
        onChanged={onChanged}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Cancel event" }));

    const reasonInput = screen.getByLabelText(/^Reason for cancellation/);
    fireEvent.change(reasonInput, { target: { value: "Vendor postponed to next week" } });

    fireEvent.click(screen.getByRole("button", { name: "Confirm cancellation" }));

    await waitFor(() => {
      expect(EquipmentPlanService.cancelEvent).toHaveBeenCalledWith(
        11,
        "Vendor postponed to next week"
      );
    });
    expect(onChanged).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("shows instrument code and allows updating date mode and picking a different instrument", async () => {
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue(mockEquipmentList);
    vi.mocked(EquipmentPlanService.updateEvent).mockResolvedValue({
      ...vendorEvent,
      equipmentInventoryId: 7,
      equipmentCode: "EQ-INC-02",
      month: "2026-12",
      date: null
    });

    const onChanged = vi.fn();
    const onClose = vi.fn();

    render(
      <EventDialog
        event={vendorEvent}
        open={true}
        onClose={onClose}
        onChanged={onChanged}
      />
    );

    // Instrument code is displayed
    expect(screen.getByText("EQ-BAL-01")).toBeTruthy();

    // Specific date is shown, whole month is not
    expect(screen.getByLabelText("Specific date")).toBeTruthy();
    expect(screen.queryByLabelText("Whole month")).toBeNull();

    // Switch date mode to Whole month
    const monthBtn = screen.getByRole("button", { name: "Whole month" });
    fireEvent.click(monthBtn);

    // Now whole month is visible
    const monthInput = screen.getByLabelText("Whole month");
    fireEvent.change(monthInput, { target: { value: "2026-12" } });

    // Change instrument via Autocomplete
    const autocomplete = screen.getByLabelText(/Instrument \(optional\)/i);
    fireEvent.mouseDown(autocomplete);
    const incOption = await screen.findByText("EQ-INC-02 — Incubator");
    fireEvent.click(incOption);

    // Save changes
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => {
      expect(EquipmentPlanService.updateEvent).toHaveBeenCalledWith(
        11,
        1,
        expect.objectContaining({
          type: "VendorVisit",
          title: "Service engineer visit",
          month: "2026-12",
          date: null,
          equipmentInventoryId: 7
        })
      );
    });

    expect(onChanged).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("warns when updating event to match an existing event, allows Back, and Add anyway submits with confirmDuplicate", async () => {
    const existingEvents: CalendarEventResponse[] = [
      vendorEvent,
      {
        id: 99,
        type: "VendorVisit",
        title: "Annual Calibration Visit",
        date: "2026-11-05",
        month: null,
        note: null,
        equipmentInventoryId: 3,
        equipmentCode: "EQ-BAL-01",
        status: "Active",
        reportPending: false,
        documents: [],
        version: 1
      }
    ];

    vi.mocked(EquipmentPlanService.updateEvent).mockResolvedValue({
      ...vendorEvent,
      title: "Annual Calibration Visit",
      version: 2
    });

    const onChanged = vi.fn();
    const onClose = vi.fn();

    render(
      <EventDialog
        event={vendorEvent}
        open={true}
        onClose={onClose}
        onChanged={onChanged}
        existingEvents={existingEvents}
      />
    );

    // Change title to match event 99
    fireEvent.change(screen.getByLabelText(/^Title/), {
      target: { value: "Annual Calibration Visit" }
    });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    // Warning alert appears
    expect(
      await screen.findByText(
        'An event "Annual Calibration Visit" (Vendor visit) is already on 2026-11-05.'
      )
    ).toBeTruthy();
    expect(EquipmentPlanService.updateEvent).not.toHaveBeenCalled();

    // Clicking Back dismisses alert
    fireEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(
      screen.queryByText(
        'An event "Annual Calibration Visit" (Vendor visit) is already on 2026-11-05.'
      )
    ).toBeNull();

    // Click Save changes again and then Add anyway
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    expect(
      await screen.findByText(
        'An event "Annual Calibration Visit" (Vendor visit) is already on 2026-11-05.'
      )
    ).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Add anyway" }));

    await waitFor(() => {
      expect(EquipmentPlanService.updateEvent).toHaveBeenCalledWith(
        11,
        1,
        expect.objectContaining({
          type: "VendorVisit",
          title: "Annual Calibration Visit",
          date: "2026-11-05",
          confirmDuplicate: true
        })
      );
    });
    expect(onChanged).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("shows duplicate alert when server rejects update with 'A matching event already exists' and allows Add anyway", async () => {
    vi.mocked(EquipmentPlanService.updateEvent)
      .mockRejectedValueOnce({
        response: {
          data: {
            message: "A matching event already exists for this date."
          }
        }
      })
      .mockResolvedValueOnce({
        ...vendorEvent,
        version: 2
      });

    const onChanged = vi.fn();
    const onClose = vi.fn();

    render(
      <EventDialog
        event={vendorEvent}
        open={true}
        onClose={onClose}
        onChanged={onChanged}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    // Duplicate alert is displayed
    expect(
      await screen.findByText(
        'An event "Service engineer visit" (Vendor visit) is already on 2026-11-05.'
      )
    ).toBeTruthy();

    // Click Add anyway
    fireEvent.click(screen.getByRole("button", { name: "Add anyway" }));

    await waitFor(() => {
      expect(EquipmentPlanService.updateEvent).toHaveBeenLastCalledWith(
        11,
        1,
        expect.objectContaining({
          confirmDuplicate: true
        })
      );
    });
    expect(onChanged).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });
});
