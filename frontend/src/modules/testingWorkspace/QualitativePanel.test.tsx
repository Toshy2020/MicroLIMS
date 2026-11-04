import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, cleanup } from "@testing-library/react";
import { QualitativePanel } from "./QualitativePanel";
import { SpecificationService } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { EquipmentConfigurationService } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import * as laboratorySectionService from "../../services/laboratorySectionService";
import { TestWorkflowService } from "./services/TestWorkflowService";

vi.mock("../../components/SignatureDialog", () => ({
  SignatureDialog: ({ open, onConfirm }: { open: boolean; onConfirm: (pw: string) => Promise<void> }) =>
    open ? (
      <div data-testid="mock-signature-dialog">
        <button onClick={() => void onConfirm("secret123")}>confirm-signature</button>
      </div>
    ) : null
}));

vi.mock("../laboratoryConfiguration/specifications/services/SpecificationService", () => ({
  SpecificationService: {
    getForTestOrder: vi.fn()
  }
}));

vi.mock("../laboratoryConfiguration/items/services/ItemService", () => ({
  ItemService: {
    getAll: vi.fn()
  }
}));

vi.mock("../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  getSections: vi.fn(),
  getMySections: vi.fn()
}));

vi.mock("./services/TestWorkflowService", () => ({
  TestWorkflowService: {
    recordQualitativeResult: vi.fn()
  }
}));

afterEach(cleanup);

describe("QualitativePanel per-container interaction", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(SpecificationService.getForTestOrder).mockResolvedValue([
      {
        id: 101,
        testCode: "DESC",
        parameterName: "Description",
        expectedResultText: "White to off-white crystalline powder",
        displayOrder: 1
      }
    ] as never);
    vi.mocked(ItemService.getAll).mockResolvedValue([
      { id: 1, name: "Sample A", assignedTests: [{ testCode: "DESC" }] }
    ] as never);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" }
    ] as never);
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([]);
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockResolvedValue({
      outcomeSummary: "Pass",
      status: "Completed"
    } as never);
  });

  it("renders per-container field and handles count blur, row updates, toggling, and a11y attributes", async () => {
    render(
      <QualitativePanel
        testOrderId={42}
        displayName="Description"
        testCode="DESC"
        itemId={1}
        current={{
          workflowType: "Qualitative",
          perContainer: true,
          testName: "DESC"
        }}
        onRecorded={vi.fn()}
      />
    );

    // Wait for specifications to load
    await waitFor(() => {
      expect(screen.getByText(/Qualitative Observations/i)).toBeTruthy();
    });

    // 1. Initial info prompt when count is empty
    expect(
      screen.getByText(/Enter the number of containers received \(1 to 200\) to record results\./i)
    ).toBeTruthy();

    const countInput = screen.getByLabelText(/Containers received/i);
    expect(countInput).toBeTruthy();

    // 2. Typing container count does not immediately create rows until blur / Enter
    fireEvent.change(countInput, { target: { value: "3" } });
    // Rows should not exist yet before commit
    expect(screen.queryByLabelText("Container 1 result")).toBeNull();

    // Commit via blur
    fireEvent.blur(countInput);

    // Now 3 container rows appear with accessible labels
    const toggle1 = screen.getByLabelText("Container 1 result");
    const toggle2 = screen.getByLabelText("Container 2 result");
    const toggle3 = screen.getByLabelText("Container 3 result");
    expect(toggle1).toBeTruthy();
    expect(toggle2).toBeTruthy();
    expect(toggle3).toBeTruthy();

    // 3. Status role & conclusion line when all comply
    const statusBox = screen.getByRole("status");
    expect(statusBox).toBeTruthy();
    expect(statusBox.textContent).toContain("All 3 containers comply");

    // 4. Toggle container 2 to "Does not comply"
    const nonComplyBtns = screen.getAllByRole("button", { name: /Does not comply/i });
    fireEvent.click(nonComplyBtns[1]); // container 2

    // Conclusion updates to single failing container wording
    expect(statusBox.textContent).toContain("Container 2 of 3 does not comply");

    // Observation for container 2 is now required
    const obsInput2 = screen.getByPlaceholderText("Describe what container 2 shows.");
    expect(obsInput2).toBeTruthy();
    fireEvent.change(obsInput2, { target: { value: "Foreign blue speck on surface" } });
    expect((obsInput2 as HTMLInputElement).value).toBe("Foreign blue speck on surface");

    // 5. Toggle container 2 back to "Complies" -> observation is cleared and conclusion resets
    const compliesBtns = screen.getAllByRole("button", { name: /Complies/i });
    fireEvent.click(compliesBtns[1]); // container 2 complies

    expect(statusBox.textContent).toContain("All 3 containers comply");

    // If container 2 is failed again, previous observation was cleared
    fireEvent.click(nonComplyBtns[1]);
    const recheckedObs = screen.getByPlaceholderText("Describe what container 2 shows.");
    expect((recheckedObs as HTMLInputElement).value).toBe("");
  });

  it("typing '12' over '10' preserves existing container states", async () => {
    render(
      <QualitativePanel
        testOrderId={42}
        displayName="Description"
        testCode="DESC"
        itemId={1}
        current={{
          workflowType: "Qualitative",
          perContainer: true,
          testName: "DESC"
        }}
        onRecorded={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText(/Qualitative Observations/i)).toBeTruthy();
    });

    const countInput = screen.getByLabelText(/Containers received/i);

    // Commit 10 containers
    fireEvent.change(countInput, { target: { value: "10" } });
    fireEvent.blur(countInput);

    expect(screen.getByLabelText("Container 10 result")).toBeTruthy();

    // Fail container 5 with custom observation
    const nonComplyBtns = screen.getAllByRole("button", { name: /Does not comply/i });
    fireEvent.click(nonComplyBtns[4]); // index 5 (0-indexed 4)
    const obs5 = screen.getByPlaceholderText("Describe what container 5 shows.");
    fireEvent.change(obs5, { target: { value: "Discolored powder" } });

    // User types "1" then "12" without wiping container 5
    fireEvent.change(countInput, { target: { value: "1" } });
    fireEvent.change(countInput, { target: { value: "12" } });
    fireEvent.blur(countInput);

    // Verify container 12 exists and container 5 is still intact with its observation
    expect(screen.getByLabelText("Container 12 result")).toBeTruthy();
    const obs5After = screen.getByPlaceholderText("Describe what container 5 shows.");
    expect((obs5After as HTMLInputElement).value).toBe("Discolored powder");
  });

  it("submits qualitative per-container payload with observation in each container", async () => {
    const onRecorded = vi.fn();
    render(
      <QualitativePanel
        testOrderId={42}
        displayName="Description"
        testCode="DESC"
        itemId={1}
        current={{
          workflowType: "Qualitative",
          perContainer: true,
          testName: "DESC"
        }}
        onRecorded={onRecorded}
      />
    );

    await waitFor(() => {
      expect(screen.getByText(/Qualitative Observations/i)).toBeTruthy();
    });

    const countInput = screen.getByLabelText(/Containers received/i);
    fireEvent.change(countInput, { target: { value: "2" } });
    fireEvent.blur(countInput);

    // Fail container 1
    const nonComplyBtns = screen.getAllByRole("button", { name: /Does not comply/i });
    fireEvent.click(nonComplyBtns[0]);
    const obs1 = screen.getByPlaceholderText("Describe what container 1 shows.");
    fireEvent.change(obs1, { target: { value: "Particle observed" } });

    // Open signature dialog
    const signBtn = screen.getByRole("button", { name: /Sign and record result/i });
    expect((signBtn as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(signBtn);

    // Confirm signature
    const confirmBtn = screen.getByRole("button", { name: "confirm-signature" });
    fireEvent.click(confirmBtn);

    await waitFor(() => {
      expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1);
    });

    expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledWith(
      42,
      expect.objectContaining({
        containerCount: 2,
        password: "secret123",
        parameters: [
          expect.objectContaining({
            specificationId: 101,
            conforms: false,
            containers: [
              { index: 1, conforms: false, observation: "Particle observed" },
              { index: 2, conforms: true, observation: null }
            ]
          })
        ]
      })
    );
  });
});
