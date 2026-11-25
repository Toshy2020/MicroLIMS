import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, cleanup, act, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { TestWorkflowDialog } from "./TestWorkflowDialog";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";

const service = vi.hoisted(() => ({
  getCurrentStep: vi.fn(),
  recordCountResult: vi.fn(),
  previewCountResult: vi.fn()
}));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../services/lookupCache", () => ({
  lookupCache: { getReleasedMedia: vi.fn().mockResolvedValue([]), getIncubators: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

beforeEach(() => {
  localStorage.clear();
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date("2026-10-11T09:00:00Z"));
  service.previewCountResult.mockResolvedValue({
    reported: "120 CFU/mL",
    average: 12,
    calculated: 120,
    status: "WithinLimits",
    messages: []
  });
});
afterEach(() => { cleanup(); vi.useRealTimers(); vi.clearAllMocks(); localStorage.clear(); });

const step = {
  id: 1, stepOrder: 1, stepName: "TAMC", stepType: "PlateCount", targetOrganismId: null,
  stepMedia: [{ mediaProductId: 5, mediaProductName: "Tryptic soy agar", tempMin: 30, tempMax: 35, incubationMinHours: 72, incubationMaxHours: 120, isRequired: true }],
  incubationMinHours: 72, incubationMaxHours: 120, temperatureMin: 30, temperatureMax: 35, isFinalStep: true
};

const incubating = (remainingMinimumSeconds: number) => ({
  testOrderId: 9, step, stepNumber: 1, totalSteps: 1, testName: "TAMC", workflowType: "CountTest",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct", configuredDilutionFactor: 10 },
  incubationLock: {
    isLocked: true, incubationEndUtc: "2026-10-16T09:00:00Z", remainingSeconds: 120 * 3600,
    minReadyAt: new Date(Date.now() + remainingMinimumSeconds * 1000).toISOString(), remainingMinimumSeconds
  },
  previousSteps: [{ stepNumber: 1, stepName: "TAMC", status: "Incubating", mediaName: "Tryptic soy agar", lotNumber: "TSA-01", incubatorName: "INC-02", incubatorSetTemp: 32.5, incubationStartUtc: "2026-10-11T07:00:00Z", incubationEndUtc: null, observation: null }],
  allStepsComplete: false, finalResult: null,
  allSteps: [{ stepOrder: 1, stepName: "TAMC" }], completedSteps: []
});

function renderDialog() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <TestWorkflowDialog testOrderId={9} testCode="TAMC" category="FinishedProduct" displayName="Tablet" />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("TestWorkflowDialog count test", () => {
  it("counts down to the plate reading and unlocks Record Result at zero", async () => {
    service.getCurrentStep.mockResolvedValue(incubating(60));
    renderDialog();

    expect(await screen.findByText("00:01:00")).toBeTruthy();
    expect(screen.getByText("TSA-01")).toBeTruthy();
    const record = screen.getByRole("button", { name: "Record Result" }) as HTMLButtonElement;
    expect(record.disabled).toBe(true);

    service.getCurrentStep.mockResolvedValue(incubating(0));
    await act(async () => { vi.advanceTimersByTime(61_000); });
    expect(await screen.findByText("Plate reading is available now")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Record Result" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("renders LocationResultsPanel when current.locationEntry is true", async () => {
    service.getCurrentStep.mockResolvedValue({
      testOrderId: 42,
      testName: "PH",
      workflowType: "Measurement",
      step: null,
      stepNumber: 1,
      totalSteps: 1,
      sampleContext: { sampleName: "Water Sample" },
      incubationLock: null,
      previousSteps: [],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: [],
      locationEntry: true,
      locations: [
        {
          sampleLocationId: 101,
          name: "PW-01 - Point 1",
          limitType: "Range",
          lowerLimit: 6.5,
          upperLimit: 8.5,
          expectedResultText: null,
          unit: "pH",
          specLimit: "6.5 – 8.5",
          value: null,
          text: null
        }
      ]
    });

    render(
      <ThemeProvider theme={lightTheme}>
        <AuthProvider>
          <MemoryRouter>
            <TestWorkflowDialog testOrderId={42} testCode="PH" category="Water" displayName="Purified Water pH" />
          </MemoryRouter>
        </AuthProvider>
      </ThemeProvider>
    );

    expect(await screen.findByText("PW-01 - Point 1")).toBeTruthy();
    expect(screen.getByText("6.5 – 8.5")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Submit" })).toBeTruthy();
  });

  // Payload pin: written against the code before the bench work and unchanged since.
  describe("payload to TestWorkflowService.recordCountResult", () => {
    it("count: payload is exactly { stepName, rawPlateReadings, dilutionFactor, dilutionFactorOverrideNote? } as before", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.recordCountResult.mockResolvedValue({ allStepsComplete: true, isDefinitive: true, outcomeSummary: "ok" });
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "12");
      await user.type(screen.getByLabelText("Plate 2"), "TNTC");
      await user.click(screen.getByRole("button", { name: "Calculate" }));

      await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
      const [orderId, payload, ...rest] = service.recordCountResult.mock.calls[0];
      expect(orderId).toBe(9);
      expect(rest).toEqual([]);
      expect(payload).toEqual({ stepName: "TAMC", rawPlateReadings: ["12", "TNTC"], dilutionFactor: 10, dilutionFactorOverrideNote: undefined });
      expect(Object.keys(payload).sort()).toEqual(["dilutionFactor", "dilutionFactorOverrideNote", "rawPlateReadings", "stepName"]);
    });

    it("count: an overridden dilution factor sends the trimmed justification note", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.recordCountResult.mockResolvedValue({ allStepsComplete: true, isDefinitive: true, outcomeSummary: "ok" });
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.click(screen.getByRole("button", { name: "Override dilution factor" }));
      const factor = screen.getByLabelText("Dilution factor");
      await user.clear(factor);
      await user.type(factor, "100");
      await user.type(screen.getByLabelText(/Justification for override/), "  Sample too concentrated  ");
      await user.type(screen.getByLabelText("Plate 1"), "7");
      await user.click(screen.getByRole("button", { name: "Calculate" }));

      await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
      expect(service.recordCountResult.mock.calls[0]).toEqual([
        9,
        { stepName: "TAMC", rawPlateReadings: ["7"], dilutionFactor: 100, dilutionFactorOverrideNote: "Sample too concentrated" }
      ]);
    });
  });

  describe("server count preview", () => {
    const fieldValidationError = (fieldErrors: Array<{ field: string; message: string; row?: number | null }>) =>
      new AxiosError(
        "Request failed with status code 400",
        "ERR_BAD_REQUEST",
        undefined,
        undefined,
        {
          status: 400,
          statusText: "Bad Request",
          data: { success: false, message: "One or more plate readings are not valid.", fieldErrors },
          headers: {},
          config: {}
        } as AxiosResponse
      );

    it('the dialog shows the server\'s reported value labelled "Preview — not recorded"', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.previewCountResult.mockResolvedValue({
        reported: "120 CFU/mL",
        average: 12,
        calculated: 120,
        status: "WithinLimits",
        messages: ["Within alert limit"]
      });
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "12");

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      expect(service.previewCountResult).toHaveBeenCalledWith(
        9,
        { stepName: "TAMC", rawPlateReadings: ["12"], dilutionFactor: 10, dilutionFactorOverrideNote: undefined },
        expect.any(AbortSignal)
      );
      expect(await screen.findByText("Preview — not recorded")).toBeTruthy();
      expect(screen.getByText("120 CFU/mL")).toBeTruthy();
      expect(screen.getByText(/Average:\s*12/)).toBeTruthy();
      expect(screen.getByText(/Calculated:\s*120/)).toBeTruthy();
      expect(screen.getByText("Within Limits")).toBeTruthy();
      expect(screen.getByText("Within alert limit")).toBeTruthy();
    });

    it("no arithmetic remains in the component", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.previewCountResult.mockResolvedValue({
        reported: "999 CFU/g",
        average: 99.9,
        calculated: 999,
        status: "AlertLimitExceeded",
        messages: []
      });
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "12");

      expect(screen.queryByText(/120\s*CFU/)).toBeNull();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      expect(await screen.findByText("999 CFU/g")).toBeTruthy();
      expect(screen.getByText("Alert Limit Exceeded")).toBeTruthy();
      expect(screen.queryByText(/120\s*CFU/)).toBeNull();
    });

    it("typing debounces the preview call (400 ms) and aborts the previous one", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));

      let resolveFirst: ((value: unknown) => void) | undefined;
      let resolveSecond: ((value: unknown) => void) | undefined;
      service.previewCountResult
        .mockImplementationOnce(() => new Promise((resolve) => { resolveFirst = resolve; }))
        .mockImplementationOnce(() => new Promise((resolve) => { resolveSecond = resolve; }));

      renderDialog();
      await user.click(await screen.findByRole("button", { name: "Record Result" }));

      const plate1 = screen.getByLabelText("Plate 1");
      await user.type(plate1, "1");
      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });
      expect(service.previewCountResult).not.toHaveBeenCalled();

      await user.type(plate1, "2");
      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });
      expect(service.previewCountResult).not.toHaveBeenCalled();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(200);
      });
      expect(service.previewCountResult).toHaveBeenCalledTimes(1);
      const firstSignal = service.previewCountResult.mock.calls[0][2] as AbortSignal;
      expect(firstSignal).toBeInstanceOf(AbortSignal);
      expect(firstSignal.aborted).toBe(false);

      await user.type(screen.getByLabelText("Plate 2"), "5");
      expect(firstSignal.aborted).toBe(true);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });
      expect(service.previewCountResult).toHaveBeenCalledTimes(2);
      const secondSignal = service.previewCountResult.mock.calls[1][2] as AbortSignal;
      expect(secondSignal.aborted).toBe(false);

      await act(async () => {
        resolveSecond?.({ reported: "85 CFU/mL", average: 8.5, calculated: 85, status: "WithinLimits", messages: [] });
        resolveFirst?.({ reported: "120 CFU/mL", average: 12, calculated: 120, status: "WithinLimits", messages: [] });
      });

      expect(await screen.findByText("85 CFU/mL")).toBeTruthy();
      expect(screen.queryByText("120 CFU/mL")).toBeNull();
    });

    it('preview failure shows "Preview unavailable" and Save still works', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.previewCountResult.mockRejectedValue(new Error("Service unavailable"));
      service.recordCountResult.mockResolvedValue({
        allStepsComplete: true,
        isDefinitive: true,
        outcomeSummary: "120 CFU/mL",
        finalResult: "WithinLimits"
      });
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "12");

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      expect(await screen.findByText("Preview unavailable")).toBeTruthy();

      await user.click(screen.getByRole("button", { name: "Calculate" }));
      await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
      expect(service.recordCountResult.mock.calls[0]).toEqual([
        9,
        { stepName: "TAMC", rawPlateReadings: ["12"], dilutionFactor: 10, dilutionFactorOverrideNote: undefined }
      ]);
    });

    it("field error from the preview shows on the reading", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.previewCountResult.mockRejectedValue(
        fieldValidationError([
          { field: "rawPlateReadings", message: "Server rejected reading.", row: 1 }
        ])
      );
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "12");
      await user.type(screen.getByLabelText("Plate 2"), "99");

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      expect(await screen.findByText("Server rejected reading.")).toBeTruthy();
      expect(screen.getByLabelText("Plate 2").getAttribute("aria-invalid")).toBe("true");
      expect(screen.getByLabelText("Plate 1").getAttribute("aria-invalid")).not.toBe("true");
      expect(screen.queryByText("Preview unavailable")).toBeNull();
    });

    it("non-digit reading such as 1,000 shows digits-only message before preview or save", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "1,000");

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      expect(await screen.findByText("Enter a whole number using digits only, or TNTC or Uncountable.")).toBeTruthy();
      expect(screen.getByLabelText("Plate 1").getAttribute("aria-invalid")).toBe("true");
      expect(service.previewCountResult).not.toHaveBeenCalled();

      await user.click(screen.getByRole("button", { name: "Calculate" }));
      expect(service.recordCountResult).not.toHaveBeenCalled();
    });

    it("both TNTC and Uncountable in one reading set are refused before preview or save", async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      service.getCurrentStep.mockResolvedValue(incubating(0));
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "TNTC");
      await user.type(screen.getByLabelText("Plate 2"), "Uncountable");

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      expect(await screen.findByText("A reading set cannot contain both TNTC and Uncountable. Choose one.")).toBeTruthy();
      expect(service.previewCountResult).not.toHaveBeenCalled();

      await user.click(screen.getByRole("button", { name: "Calculate" }));
      expect(service.recordCountResult).not.toHaveBeenCalled();
    });

    it("shows the server competency refusal message when recording a count result is refused", async () => {
      const user = userEvent.setup();
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.recordCountResult.mockRejectedValue(
        new AxiosError("Forbidden", "403", undefined, undefined, {
          status: 403,
          statusText: "Forbidden",
          headers: {},
          config: {} as never,
          data: { message: "You are not qualified for TAMC." }
        } as AxiosResponse)
      );
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "25");

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      await user.click(screen.getByRole("button", { name: "Calculate" }));
      const alerts = await screen.findAllByText("You are not qualified for TAMC.");
      expect(alerts.length).toBeGreaterThanOrEqual(1);
      expect(screen.queryByRole("button", { name: /Grant override/i })).toBeNull();
      expect(screen.queryByText("A Section Head can grant a competency override for this test.")).toBeNull();
    });

    it("shows Section Head hint on Competency.* refusal and never renders Grant override on recording screen", async () => {
      const user = userEvent.setup();
      localStorage.setItem("microlims_user_id", "12");
      localStorage.setItem("microlims_full_name", "Hana Head");
      localStorage.setItem("microlims_permissions", JSON.stringify(["Competency.Override"]));
      service.getCurrentStep.mockResolvedValue(incubating(0));
      service.recordCountResult.mockRejectedValue(
        new AxiosError("Forbidden", "403", undefined, undefined, {
          status: 403,
          statusText: "Forbidden",
          headers: {},
          config: {} as never,
          data: { code: "Competency.NotQualified", message: "You are not qualified for TAMC." }
        } as AxiosResponse)
      );
      renderDialog();

      await user.click(await screen.findByRole("button", { name: "Record Result" }));
      await user.type(screen.getByLabelText("Plate 1"), "25");

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      await user.click(screen.getByRole("button", { name: "Calculate" }));
      expect((await screen.findAllByText("You are not qualified for TAMC.")).length).toBeGreaterThanOrEqual(1);
      expect((await screen.findAllByText("A Section Head can grant a competency override for this test.")).length).toBeGreaterThanOrEqual(1);
      expect(screen.queryByRole("button", { name: /Grant override/i })).toBeNull();
    });
  });

  describe("Arabic RTL", () => {
    afterEach(async () => {
      await resetLanguage();
    });

    it("renders RTL layout, Arabic headings/buttons, and LTR batch identifier", async () => {
      service.getCurrentStep.mockResolvedValue(incubating(60));
      await renderInArabic(
        <AuthProvider>
          <MemoryRouter>
            <TestWorkflowDialog testOrderId={9} testCode="TAMC" category="FinishedProduct" displayName="Tablet" />
          </MemoryRouter>
        </AuthProvider>,
        ["common", "testingWorkspace"]
      );

      expect(document.documentElement.dir).toBe("rtl");
      expect(await screen.findByText("قيد الحضانة")).toBeTruthy();
      expect(screen.getByRole("button", { name: "تسجيل النتيجة" })).toBeTruthy();
      expect(screen.getByText("TSA-01").closest("bdi")?.getAttribute("dir")).toBe("ltr");
    });
  });
});
