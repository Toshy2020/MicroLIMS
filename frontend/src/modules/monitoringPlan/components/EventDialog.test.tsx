import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { EventDialog } from "./EventDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type { CalendarEventResponse } from "../types";

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    updateEvent: vi.fn(),
    cancelEvent: vi.fn(),
    uploadEventDocument: vi.fn(),
    getDocumentContentUrl: (id: number) => `/api/calendar-event-documents/${id}/content`
  }
}));

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
});
