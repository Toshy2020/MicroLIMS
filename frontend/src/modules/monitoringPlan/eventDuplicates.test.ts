import { describe, expect, it } from "vitest";
import { isDuplicateCalendarEvent } from "./eventDuplicates";
import type { CalendarEventResponse } from "./types";

const baseEvent: CalendarEventResponse = {
  id: 1,
  type: "VendorVisit",
  title: "Service Visit",
  date: "2026-10-25",
  month: null,
  note: null,
  equipmentInventoryId: 5,
  equipmentCode: "EQ-005",
  status: "Active",
  reportPending: false,
  documents: [],
  version: 1
};

describe("isDuplicateCalendarEvent", () => {
  it("matches same type, title (case-insensitive), instrument, and date", () => {
    expect(
      isDuplicateCalendarEvent(baseEvent, {
        type: "VendorVisit",
        title: "service visit",
        date: "2026-10-25",
        equipmentInventoryId: 5
      })
    ).toBe(true);
  });

  it("does not match itself by id", () => {
    expect(
      isDuplicateCalendarEvent(baseEvent, {
        id: 1,
        type: "VendorVisit",
        title: "Service Visit",
        date: "2026-10-25",
        equipmentInventoryId: 5
      })
    ).toBe(false);
  });

  it("does not match different type", () => {
    expect(
      isDuplicateCalendarEvent(baseEvent, {
        type: "Audit",
        title: "Service Visit",
        date: "2026-10-25",
        equipmentInventoryId: 5
      })
    ).toBe(false);
  });

  it("does not match different instrument", () => {
    expect(
      isDuplicateCalendarEvent(baseEvent, {
        type: "VendorVisit",
        title: "Service Visit",
        date: "2026-10-25",
        equipmentInventoryId: 6
      })
    ).toBe(false);
  });

  it("does not match different date", () => {
    expect(
      isDuplicateCalendarEvent(baseEvent, {
        type: "VendorVisit",
        title: "Service Visit",
        date: "2026-10-26",
        equipmentInventoryId: 5
      })
    ).toBe(false);
  });

  it("does not match cancelled events", () => {
    const cancelledEvent: CalendarEventResponse = {
      ...baseEvent,
      status: "Cancelled"
    };
    expect(
      isDuplicateCalendarEvent(cancelledEvent, {
        type: "VendorVisit",
        title: "Service Visit",
        date: "2026-10-25",
        equipmentInventoryId: 5
      })
    ).toBe(false);
  });

  it("matches month-based events", () => {
    const monthEvent: CalendarEventResponse = {
      ...baseEvent,
      id: 2,
      date: null,
      month: "2026-11"
    };
    expect(
      isDuplicateCalendarEvent(monthEvent, {
        type: "VendorVisit",
        title: "SERVICE VISIT",
        month: "2026-11",
        equipmentInventoryId: 5
      })
    ).toBe(true);
  });
});
