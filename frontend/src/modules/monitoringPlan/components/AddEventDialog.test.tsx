import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AddEventDialog } from "./AddEventDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    createEvent: vi.fn()
  }
}));

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
          date: "2026-10-25"
        })
      );
    });

    expect(onClose).toHaveBeenCalled();
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
