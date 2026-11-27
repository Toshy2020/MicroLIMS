import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { AssignedTestCard } from "./AssignedTestCard";
import { competencyService } from "../../../services/competencyService";
import type { SampleCard, TestOrderSummary } from "../types/workspaceTypes";

vi.mock("../../../services/competencyService", () => ({
  competencyService: {
    grantOverride: vi.fn()
  }
}));

const sample: SampleCard = {
  sampleId: 77,
  referenceNumber: "SMP-2026-077",
  displayName: "Amoxicillin 500mg",
  category: "FinishedProduct",
  departmentId: null,
  machineId: null,
  waterDepartmentId: null,
  productionStage: "Final",
  causeOfTesting: "Routine",
  batchNumber: "B-101",
  controlNumber: "C-101",
  status: "InTesting",
  preparationStatus: "Prepared",
  receivedAt: "2026-11-24T08:00:00Z",
  sampleQuantity: "10",
  sampledBy: "Sam",
  mfgDate: null,
  expDate: null,
  waterSamplingPointCode: null,
  waterSamplingPointLocation: null,
  storageCondition: null,
  storageTimeHours: null,
  incubationStarted: false,
  assignedTests: []
};

const baseTest: TestOrderSummary = {
  testOrderId: 501,
  testCode: "TAMC",
  status: "Pending",
  locationCount: 0,
  assignedAnalystId: 42,
  assignedAnalystName: "Mona Ali"
};

function renderCard(test: TestOrderSummary = baseTest, onActionComplete = vi.fn()) {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <AssignedTestCard
            test={test}
            sample={sample}
            onTestClick={vi.fn()}
            onActionComplete={onActionComplete}
          />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("AssignedTestCard competency override entry point", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("microlims_user_id", "12");
    localStorage.setItem("microlims_full_name", "Hana Head");
    localStorage.setItem("microlims_role", "SectionHead");
    vi.mocked(competencyService.grantOverride).mockResolvedValue(88);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("hides Grant Override when user lacks Competency.Override or test has no assigned analyst", () => {
    localStorage.setItem("microlims_permissions", JSON.stringify([]));
    renderCard();
    expect(screen.queryByRole("button", { name: /Grant competency override for TAMC/i })).toBeNull();

    cleanup();
    localStorage.setItem("microlims_permissions", JSON.stringify(["Competency.Override"]));
    renderCard({ ...baseTest, assignedAnalystId: null, assignedAnalystName: null });
    expect(screen.queryByRole("button", { name: /Grant competency override for TAMC/i })).toBeNull();
  });

  it("grants override for the test order's assigned analyst (never auth.userId)", async () => {
    const user = userEvent.setup();
    const onActionComplete = vi.fn();
    localStorage.setItem("microlims_permissions", JSON.stringify(["Competency.Override"]));

    renderCard(baseTest, onActionComplete);

    const btn = screen.getByRole("button", { name: /Grant competency override for TAMC/i });
    await user.click(btn);

    await user.type(await screen.findByRole("textbox", { name: /Reason for override/i }), "Supervised execution");
    await user.click(screen.getByRole("button", { name: "Continue to Sign" }));
    await user.type(await screen.findByLabelText(/Password/i), "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() =>
      expect(competencyService.grantOverride).toHaveBeenCalledWith(501, {
        analystUserId: 42,
        reason: "Supervised execution",
        password: "Secret123!"
      })
    );
    expect(onActionComplete).toHaveBeenCalledTimes(1);
  });
});
