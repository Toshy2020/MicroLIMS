import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, within, fireEvent, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { WorkflowStepsSection } from "./WorkflowStepsSection";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

const api = vi.hoisted(() => ({
  getOrganisms: vi.fn(),
  getMediaProducts: vi.fn(),
  getMediaIncubationConditions: vi.fn(),
  getTestWorkflowSteps: vi.fn(),
  createTestWorkflowStep: vi.fn(),
  updateTestWorkflowStep: vi.fn()
}));
vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return { ...actual, masterDataOptions: api };
});

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const product = (id: number, name: string, isActive = true) => ({
  id, name, code: `MP${id}`, isActive, hasStock: true, configurationCount: 0, batchCount: 0, incubationConditionCount: 1
});
const condition = (id: number, mediaProductId: number, hours: number) => ({
  id, mediaProductId, incubationMinHours: hours, incubationMaxHours: hours + 24, temperatureMin: 30, temperatureMax: 35,
  configurationCount: 0, stepMediaCount: 0
});

const test = { id: 9, workflowType: "CountTest" } as unknown as TestDefinitionOption;

function setup(steps: unknown[] = []) {
  api.getOrganisms.mockResolvedValue([]);
  api.getMediaProducts.mockResolvedValue([product(1, "Tryptic soy agar"), product(2, "Retired agar", false)]);
  api.getMediaIncubationConditions.mockResolvedValue([condition(10, 1, 48), condition(20, 2, 72)]);
  api.getTestWorkflowSteps.mockResolvedValue(steps);
  api.createTestWorkflowStep.mockResolvedValue({});
  api.updateTestWorkflowStep.mockResolvedValue({});
  return render(
    <ThemeProvider theme={lightTheme}>
      <WorkflowStepsSection test={test} workflowTypes={["CountTest"]} onWorkflowTypeChanged={() => {}} />
    </ThemeProvider>
  );
}

describe("WorkflowStepsSection step media", () => {
  it("saves a new step medium as a media product with one of that product's conditions", async () => {
    setup();
    fireEvent.change(await screen.findByLabelText("Step Name"), { target: { value: "TAMC" } });
    fireEvent.click(screen.getByRole("button", { name: "Add Medium" }));

    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Culture media product/ }));
    const products = within(await screen.findByRole("listbox"));
    // Inactive products can't be newly configured.
    expect(products.queryByText(/Retired agar/)).toBeNull();
    fireEvent.click(products.getByText(/Tryptic soy agar/));

    fireEvent.mouseDown(screen.getByRole("combobox", { name: "Incubation condition" }));
    const conditions = within(await screen.findByRole("listbox"));
    // Only the picked product's condition is offered.
    expect(conditions.getAllByRole("option")).toHaveLength(2);
    fireEvent.click(conditions.getAllByRole("option")[1]);

    fireEvent.click(screen.getByRole("button", { name: "Add Step" }));

    await waitFor(() => expect(api.createTestWorkflowStep).toHaveBeenCalledTimes(1));
    const [testId, payload] = api.createTestWorkflowStep.mock.calls[0];
    expect(testId).toBe(9);
    expect(payload.stepMedia).toEqual([
      { mediaProductId: 1, mediaIncubationConditionId: 10, isRequired: false, displayOrder: 0 }
    ]);
    expect(payload.stepMedia[0]).not.toHaveProperty("materialId");
  });

  it("keeps an inactive product that is already on the step when the step is edited", async () => {
    setup([{
      id: 5, stepName: "TAMC", stepOrder: 1, isFinalStep: true, stepType: "PlateCount", version: 3,
      stepMedia: [{ mediaProductId: 2, mediaProductName: "Retired agar", mediaIncubationConditionId: 20, isRequired: true, displayOrder: 0 }]
    }]);
    fireEvent.click(await screen.findByRole("button", { name: "Edit step" }));

    expect(await screen.findByDisplayValue(/MP2: Retired agar/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(api.updateTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.updateTestWorkflowStep.mock.calls[0][1].stepMedia).toEqual([
      { mediaProductId: 2, mediaIncubationConditionId: 20, isRequired: true, displayOrder: 0 }
    ]);
  });
});
