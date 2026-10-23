import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AddLogEntryDialog } from "./AddLogEntryDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    addLogEntry: vi.fn()
  }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("AddLogEntryDialog", () => {
  it("requires work done", async () => {
    render(
      <AddLogEntryDialog
        open={true}
        equipmentId={1}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const passwordInput = screen.getByLabelText(/^Password/i);
    fireEvent.change(passwordInput, { target: { value: "ValidPassword123!" } });

    const submitBtn = screen.getByRole("button", { name: /Save Log Entry/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText(/Work done is required/i)).toBeTruthy();
    });

    expect(EquipmentPlanService.addLogEntry).not.toHaveBeenCalled();
  });

  it("submits log entry when all required fields are provided", async () => {
    vi.mocked(EquipmentPlanService.addLogEntry).mockResolvedValue({
      id: 10,
      equipmentInventoryId: 1,
      entryType: "PreventiveMaintenance",
      performedOn: "2026-04-14",
      performedByUserId: 1,
      performedByUserName: "Alice",
      workDone: "Replaced HEPA filter and calibrated sensors",
      partsReplaced: "HEPA Filter",
      findings: "Normal operation restored",
      calendarEventId: null,
      recordedAt: "2026-04-14T10:00:00Z",
      voidedAt: null,
      voidedByUserId: null,
      voidReason: null,
      electronicSignatureId: 99
    });

    const onSuccess = vi.fn();
    render(
      <AddLogEntryDialog
        open={true}
        equipmentId={1}
        onClose={vi.fn()}
        onSuccess={onSuccess}
      />
    );

    const workDoneInput = screen.getByLabelText(/Work Done/i);
    fireEvent.change(workDoneInput, {
      target: { value: "Replaced HEPA filter and calibrated sensors" }
    });

    const partsInput = screen.getByLabelText(/Parts Replaced/i);
    fireEvent.change(partsInput, { target: { value: "HEPA Filter" } });

    const findingsInput = screen.getByLabelText(/Findings/i);
    fireEvent.change(findingsInput, { target: { value: "Normal operation restored" } });

    const passwordInput = screen.getByLabelText(/^Password/i);
    fireEvent.change(passwordInput, { target: { value: "ValidPassword123!" } });

    const submitBtn = screen.getByRole("button", { name: /Save Log Entry/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(EquipmentPlanService.addLogEntry).toHaveBeenCalledWith(
        1,
        expect.objectContaining({
          entryType: "PreventiveMaintenance",
          workDone: "Replaced HEPA filter and calibrated sensors",
          partsReplaced: "HEPA Filter",
          findings: "Normal operation restored",
          password: "ValidPassword123!"
        })
      );
      expect(onSuccess).toHaveBeenCalled();
    });
  });
});
