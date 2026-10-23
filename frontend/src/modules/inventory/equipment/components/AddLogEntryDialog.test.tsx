import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { AddLogEntryDialog } from "./AddLogEntryDialog";
import { EquipmentPlanService } from "../services/EquipmentPlanService";

vi.mock("../../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Alice", username: "alice", role: "Analyst" })
}));

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
  it("Continue to sign is disabled until required fields are filled", async () => {
    render(
      <AddLogEntryDialog
        open={true}
        equipmentId={1}
        equipmentCode="EQ-001"
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const continueBtn = screen.getByRole("button", { name: /Continue to sign/i }) as HTMLButtonElement;
    expect(continueBtn.disabled).toBe(true);

    const workDoneInput = screen.getByLabelText(/Work Done/i);
    fireEvent.change(workDoneInput, { target: { value: "Replaced filter" } });

    expect(continueBtn.disabled).toBe(false);
  });

  it("signing calls the API with the password", async () => {
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
    const onClose = vi.fn();
    render(
      <AddLogEntryDialog
        open={true}
        equipmentId={1}
        equipmentCode="EQ-001"
        onClose={onClose}
        onSuccess={onSuccess}
      />
    );

    const dateInput = screen.getByLabelText(/Date Performed/i);
    fireEvent.change(dateInput, { target: { value: "2026-04-14" } });

    const workDoneInput = screen.getByLabelText(/Work Done/i);
    fireEvent.change(workDoneInput, {
      target: { value: "Replaced HEPA filter and calibrated sensors" }
    });

    const partsInput = screen.getByLabelText(/Parts Replaced/i);
    fireEvent.change(partsInput, { target: { value: "HEPA Filter" } });

    const findingsInput = screen.getByLabelText(/Findings/i);
    fireEvent.change(findingsInput, { target: { value: "Normal operation restored" } });

    const continueBtn = screen.getByRole("button", { name: /Continue to sign/i });
    fireEvent.click(continueBtn);

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(
      screen.getByText(
        "I confirm this preventive maintenance entry for EQ-001, performed on 2026-04-14, is accurate."
      )
    ).toBeTruthy();

    const passwordInput = screen.getByLabelText(/^Password/i);
    fireEvent.change(passwordInput, { target: { value: "ValidPassword123!" } });

    const signBtn = screen.getByRole("button", { name: "Sign" });
    fireEvent.click(signBtn);

    await waitFor(() => {
      expect(EquipmentPlanService.addLogEntry).toHaveBeenCalledWith(
        1,
        expect.objectContaining({
          entryType: "PreventiveMaintenance",
          performedOn: "2026-04-14",
          workDone: "Replaced HEPA filter and calibrated sensors",
          partsReplaced: "HEPA Filter",
          findings: "Normal operation restored",
          password: "ValidPassword123!"
        })
      );
      expect(onSuccess).toHaveBeenCalled();
      expect(onClose).toHaveBeenCalled();
    });
  });
});
