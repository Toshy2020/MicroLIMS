import { describe, expect, it, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { LocationResultsPanel } from "./LocationResultsPanel";
import { TestWorkflowService } from "./services/TestWorkflowService";
import type { CurrentStepResponse } from "./types/testWorkflowTypes";

vi.mock("./services/TestWorkflowService", () => ({
  TestWorkflowService: {
    recordLocationResults: vi.fn()
  }
}));

afterEach(cleanup);

describe("LocationResultsPanel", () => {
  const current: CurrentStepResponse = {
    testOrderId: 42,
    testName: "PH-WATER",
    workflowType: "Measurement" as any,
    step: null,
    stepNumber: 1,
    totalSteps: 1,
    sampleContext: { sampleName: "PW Water Sample" } as any,
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
        name: "PW-01 - Point A",
        limitType: "Range",
        lowerLimit: 6.5,
        upperLimit: 8.5,
        expectedResultText: null,
        unit: "pH",
        specLimit: "6.5 – 8.5",
        value: 7.2,
        text: null
      },
      {
        sampleLocationId: 102,
        name: "PW-02 - Point B",
        limitType: "NotMoreThan",
        lowerLimit: null,
        upperLimit: 1.3,
        expectedResultText: null,
        unit: "µS/cm",
        specLimit: "NMT 1.3",
        value: null,
        text: null
      },
      {
        sampleLocationId: 103,
        name: "PW-03 - Point C",
        limitType: "Qualitative",
        lowerLimit: null,
        upperLimit: null,
        expectedResultText: "Clear liquid",
        unit: null,
        specLimit: "Clear liquid",
        value: null,
        text: "Clear liquid"
      }
    ]
  };

  it("renders table with locations, limits, units and prefilled values", () => {
    render(
      <PinnedLightTheme>
        <LocationResultsPanel
          testOrderId={42}
          displayName="Water Chemistry Test"
          current={current}
          onRecorded={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Water Chemistry Test")).toBeDefined();
    expect(screen.getByText("PW-01 - Point A")).toBeDefined();
    expect(screen.getByText("PW-02 - Point B")).toBeDefined();
    expect(screen.getByText("PW-03 - Point C")).toBeDefined();

    // Check limits and units
    expect(screen.getByText("6.5 – 8.5")).toBeDefined();
    expect(screen.getByText("NMT 1.3")).toBeDefined();
    expect(screen.getByText("pH")).toBeDefined();
    expect(screen.getByText("µS/cm")).toBeDefined();

    // Inputs: PW-01 should be prefilled with 7.2
    const input1 = screen.getByLabelText("Result for PW-01 - Point A") as HTMLInputElement;
    expect(input1.value).toBe("7.2");
    expect(input1.type).toBe("number");

    // PW-02 is empty numeric
    const input2 = screen.getByLabelText("Result for PW-02 - Point B") as HTMLInputElement;
    expect(input2.value).toBe("");
    expect(input2.type).toBe("number");

    // PW-03 is prefilled qualitative text
    const input3 = screen.getByLabelText("Result for PW-03 - Point C") as HTMLInputElement;
    expect(input3.value).toBe("Clear liquid");
    expect(input3.type).toBe("text");
  });

  it("validates empty inputs before submitting", async () => {
    render(
      <PinnedLightTheme>
        <LocationResultsPanel
          testOrderId={42}
          current={current}
          onRecorded={vi.fn()}
        />
      </PinnedLightTheme>
    );

    // Try submit while PW-02 is blank
    const submitBtn = screen.getByRole("button", { name: "Submit" });
    fireEvent.click(submitBtn);

    expect(screen.getByText(/Please enter a valid numeric result for PW-02 - Point B/i)).toBeDefined();
    expect(TestWorkflowService.recordLocationResults).not.toHaveBeenCalled();
  });

  it("submits location results and calls onRecorded", async () => {
    const onRecorded = vi.fn();
    (TestWorkflowService.recordLocationResults as any).mockResolvedValueOnce({ success: true });

    render(
      <PinnedLightTheme>
        <LocationResultsPanel
          testOrderId={42}
          current={current}
          onRecorded={onRecorded}
        />
      </PinnedLightTheme>
    );

    // Fill PW-02
    const input2 = screen.getByLabelText("Result for PW-02 - Point B");
    fireEvent.change(input2, { target: { value: "1.1" } });

    const submitBtn = screen.getByRole("button", { name: "Submit" });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(TestWorkflowService.recordLocationResults).toHaveBeenCalledWith(42, [
        { sampleLocationId: 101, value: 7.2, text: null },
        { sampleLocationId: 102, value: 1.1, text: null },
        { sampleLocationId: 103, value: null, text: "Clear liquid" }
      ]);
      expect(onRecorded).toHaveBeenCalled();
    });
  });

  it("displays server error when submission fails", async () => {
    (TestWorkflowService.recordLocationResults as any).mockRejectedValueOnce({
      response: { data: { message: "Limits for pH at PW-01 are not configured." } }
    });

    render(
      <PinnedLightTheme>
        <LocationResultsPanel
          testOrderId={42}
          current={current}
          onRecorded={vi.fn()}
        />
      </PinnedLightTheme>
    );

    const input2 = screen.getByLabelText("Result for PW-02 - Point B");
    fireEvent.change(input2, { target: { value: "0.9" } });

    const submitBtn = screen.getByRole("button", { name: "Submit" });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText("Limits for pH at PW-01 are not configured.")).toBeDefined();
    });
  });

  it("renders No peaks and Peaks detected buttons for UV qualitative rows and submits selection", async () => {
    const onRecorded = vi.fn();
    (TestWorkflowService.recordLocationResults as any).mockResolvedValueOnce({ success: true });

    const uvCurrent: CurrentStepResponse = {
      testOrderId: 99,
      testName: "UV-CLEAN",
      workflowType: "Qualitative" as any,
      step: null,
      stepNumber: 1,
      totalSteps: 1,
      sampleContext: { sampleName: "AC Swab Sample" } as any,
      incubationLock: null,
      previousSteps: [],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: [],
      locationEntry: true,
      locations: [
        {
          sampleLocationId: 201,
          name: "Chute Swab",
          limitType: "Qualitative",
          lowerLimit: null,
          upperLimit: null,
          expectedResultText: "No peaks",
          unit: null,
          specLimit: "No peaks",
          value: null,
          text: null
        }
      ]
    };

    render(
      <PinnedLightTheme>
        <LocationResultsPanel
          testOrderId={99}
          displayName="UV Scan"
          current={uvCurrent}
          onRecorded={onRecorded}
        />
      </PinnedLightTheme>
    );

    // Verify buttons are rendered and no text field
    const noPeaksBtn = screen.getByRole("button", { name: "No peaks for Chute Swab" });
    const peaksDetectedBtn = screen.getByRole("button", { name: "Peaks detected for Chute Swab" });
    expect(noPeaksBtn).toBeDefined();
    expect(peaksDetectedBtn).toBeDefined();
    expect(screen.queryByPlaceholderText("Result text")).toBeNull();

    // Click No peaks
    fireEvent.click(noPeaksBtn);

    const submitBtn = screen.getByRole("button", { name: "Submit" });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(TestWorkflowService.recordLocationResults).toHaveBeenCalledWith(99, [
        { sampleLocationId: 201, value: null, text: "No peaks" }
      ]);
      expect(onRecorded).toHaveBeenCalled();
    });
  });

  it("submits Peaks detected when Peaks detected button is clicked", async () => {
    const onRecorded = vi.fn();
    (TestWorkflowService.recordLocationResults as any).mockResolvedValueOnce({ success: true });

    const uvCurrent: CurrentStepResponse = {
      testOrderId: 100,
      testName: "UV-CLEAN",
      workflowType: "Qualitative" as any,
      step: null,
      stepNumber: 1,
      totalSteps: 1,
      sampleContext: { sampleName: "AC Swab Sample" } as any,
      incubationLock: null,
      previousSteps: [],
      allStepsComplete: false,
      finalResult: null,
      allSteps: [],
      completedSteps: [],
      locationEntry: true,
      locations: [
        {
          sampleLocationId: 202,
          name: "Needle Rinse",
          limitType: "Qualitative",
          lowerLimit: null,
          upperLimit: null,
          expectedResultText: "No peaks",
          unit: null,
          specLimit: "No peaks",
          value: null,
          text: null
        }
      ]
    };

    render(
      <PinnedLightTheme>
        <LocationResultsPanel
          testOrderId={100}
          displayName="UV Scan"
          current={uvCurrent}
          onRecorded={onRecorded}
        />
      </PinnedLightTheme>
    );

    const peaksDetectedBtn = screen.getByRole("button", { name: "Peaks detected for Needle Rinse" });
    fireEvent.click(peaksDetectedBtn);

    const submitBtn = screen.getByRole("button", { name: "Submit" });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(TestWorkflowService.recordLocationResults).toHaveBeenCalledWith(100, [
        { sampleLocationId: 202, value: null, text: "Peaks detected" }
      ]);
      expect(onRecorded).toHaveBeenCalled();
    });
  });
});
