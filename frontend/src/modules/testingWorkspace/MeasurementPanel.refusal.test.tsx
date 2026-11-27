import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { MeasurementPanel } from "./MeasurementPanel";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SpecificationService } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { EquipmentConfigurationService } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections } from "../../services/laboratorySectionService";

vi.mock("./services/TestWorkflowService", () => ({
  TestWorkflowService: {
    recordMeasurementResult: vi.fn()
  }
}));
vi.mock("../laboratoryConfiguration/specifications/services/SpecificationService", () => ({
  SpecificationService: {
    getForTestOrder: vi.fn()
  }
}));
vi.mock("../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getTestDefinitions: vi.fn()
  }
}));
vi.mock("../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: vi.fn()
  }
}));
vi.mock("../../services/laboratorySectionService", () => ({
  getSections: vi.fn()
}));

describe("MeasurementPanel recording refusal", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("microlims_full_name", "Mona Ali");
    localStorage.setItem("microlims_role", "Analyst");
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([
      { id: 1, code: "PH", displayName: "pH", replicateCount: 1 } as never
    ]);
    vi.mocked(SpecificationService.getForTestOrder).mockResolvedValue([
      { id: 10, testCode: "PH", parameterName: "pH", specLimit: "6.5 - 7.5", lowerLimit: 6.5, upperLimit: 7.5 } as never
    ]);
    vi.mocked(getSections).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("shows server refusal message beside the recording action when recording is refused", async () => {
    const user = userEvent.setup();
    vi.mocked(TestWorkflowService.recordMeasurementResult).mockRejectedValue(
      new AxiosError("Forbidden", "403", undefined, undefined, {
        status: 403,
        statusText: "Forbidden",
        headers: {},
        config: {} as never,
        data: { message: "You are not qualified for PH." }
      } as AxiosResponse)
    );

    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter>
            <MeasurementPanel
              testOrderId={1}
              displayName="pH"
              testCode="PH"
              itemId={5}
              current={{ testName: "PH", allStepsComplete: false }}
              onRecorded={vi.fn()}
            />
          </MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    const input = await screen.findByRole("textbox", { name: "Rep 1 pH" });
    await user.type(input, "7.0");
    await user.click(screen.getByRole("button", { name: "Sign and record result" }));

    const passwordInput = await screen.findByLabelText(/Password/i);
    await user.type(passwordInput, "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      const alert = screen.getByRole("alert");
      expect(alert.textContent).toBe("You are not qualified for PH.");
    });
    expect(screen.queryByRole("button", { name: /Grant Override/i })).toBeNull();
    expect(screen.queryByText("A Section Head can grant a competency override for this test.")).toBeNull();
  });

  it("shows Section Head hint on Competency.* refusal and never renders Grant Override on recording screen", async () => {
    const user = userEvent.setup();
    localStorage.setItem("microlims_permissions", JSON.stringify(["Competency.Override"]));
    localStorage.setItem("microlims_user_id", "12");
    vi.mocked(TestWorkflowService.recordMeasurementResult).mockRejectedValue(
      new AxiosError("Forbidden", "403", undefined, undefined, {
        status: 403,
        statusText: "Forbidden",
        headers: {},
        config: {} as never,
        data: {
          message: "You are not qualified for PH.",
          errorDetails: [{ code: "Competency.NotQualified", message: "You are not qualified for PH." }]
        }
      } as AxiosResponse)
    );

    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter>
            <MeasurementPanel
              testOrderId={1}
              displayName="pH"
              testCode="PH"
              itemId={5}
              current={{ testName: "PH", allStepsComplete: false }}
              onRecorded={vi.fn()}
            />
          </MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    const input = await screen.findByRole("textbox", { name: "Rep 1 pH" });
    await user.type(input, "7.0");
    await user.click(screen.getByRole("button", { name: "Sign and record result" }));

    const passwordInput = await screen.findByLabelText(/Password/i);
    await user.type(passwordInput, "Secret123!");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      const alert = screen.getByRole("alert");
      expect(alert.textContent).toContain("You are not qualified for PH.");
    });
    expect(await screen.findByText("A Section Head can grant a competency override for this test.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Grant Override/i })).toBeNull();
  });
});
