import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { GateSettingsCard } from "./GateSettingsCard";
import { competencyService } from "../../services/competencyService";

vi.mock("../../services/competencyService", () => ({
  competencyService: {
    getSettings: vi.fn(),
    getImpact: vi.fn(),
    setGate: vi.fn()
  }
}));

function renderCard() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <GateSettingsCard sectionId={1} />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("GateSettingsCard", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("microlims_full_name", "Hana Head");
    localStorage.setItem("microlims_role", "SectionHead");
    vi.mocked(competencyService.getSettings).mockResolvedValue({
      sectionId: 1,
      gateEnabled: false,
      reminderDaysBeforeExpiry: 30,
      changedAt: null,
      changedByName: null,
      version: 2
    });
    vi.mocked(competencyService.getImpact).mockResolvedValue({
      sectionId: 1,
      totalRefusedOrders: 1,
      analysts: [
        {
          analystUserId: 101,
          analystName: "Mona Ali",
          orders: [
            {
              testOrderId: 501,
              testCode: "TAMC",
              sampleReference: "MICRO26001",
              reasons: [{ code: "Competency.AssigneeNotQualified", text: "Mona Ali is not qualified for TAMC.", params: {} }]
            }
          ]
        }
      ]
    });
    vi.mocked(competencyService.setGate).mockResolvedValue({
      sectionId: 1,
      gateEnabled: true,
      reminderDaysBeforeExpiry: 30,
      changedAt: "2026-11-24T12:00:00Z",
      changedByName: "Hana Head",
      version: 3
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("shows impact preview before the switch dialog confirms", async () => {
    const user = userEvent.setup();
    renderCard();

    expect(await screen.findByText("Gate Disabled")).toBeTruthy();
    expect(screen.getByText(/1 open assigned test order\(s\) would be blocked/i)).toBeTruthy();
    expect(screen.getByText(/MICRO26001 — TAMC/)).toBeTruthy();
    expect(screen.getByText(/Mona Ali is not qualified for TAMC\./)).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Turn Gate On" }));

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    await user.type(screen.getByLabelText(/Reason for changing gate/i), "All analysts qualified");
    await user.type(screen.getByLabelText(/Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() =>
      expect(competencyService.setGate).toHaveBeenCalledWith(1, {
        enabled: true,
        reason: "All analysts qualified",
        password: "Secret123!",
        expectedVersion: 2
      })
    );
  });
});
