import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { WorkflowStepsSection } from "./WorkflowStepsSection";
import { WORKFLOW_TYPE_LABELS } from "./testMasterConstants";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

const api = vi.hoisted(() => ({
  getOrganisms: vi.fn(),
  getMediaProducts: vi.fn(),
  getMediaIncubationConditions: vi.fn(),
  getTestWorkflowSteps: vi.fn(),
  createTestWorkflowStep: vi.fn(),
  updateTestWorkflowStep: vi.fn(),
  updateWorkflowType: vi.fn(),
  moveTestWorkflowStep: vi.fn(),
  deleteTestWorkflowStep: vi.fn()
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
const step = (id: number, stepOrder: number, version: number, stepName: string) => ({
  id, stepName, stepOrder, isFinalStep: false, stepType: "PlateCount", version, stepMedia: [], incubationStages: []
});

const test = { id: 9, workflowType: "CountTest", version: 7 } as unknown as TestDefinitionOption;

function setup(steps: unknown[] | unknown[][] = []) {
  api.getOrganisms.mockResolvedValue([]);
  api.getMediaProducts.mockResolvedValue([product(1, "Tryptic soy agar"), product(2, "Retired agar", false)]);
  api.getMediaIncubationConditions.mockResolvedValue([condition(10, 1, 48), condition(20, 2, 72)]);
  const batches = Array.isArray(steps[0]) ? (steps as unknown[][]) : [steps];
  api.getTestWorkflowSteps.mockImplementation(() => Promise.resolve(batches.length > 1 ? batches.shift() : batches[0]));
  api.createTestWorkflowStep.mockResolvedValue({});
  api.updateTestWorkflowStep.mockResolvedValue({});
  api.updateWorkflowType.mockResolvedValue({ id: 9, version: 8 });
  api.moveTestWorkflowStep.mockResolvedValue({});
  api.deleteTestWorkflowStep.mockResolvedValue({});
  return render(
    <ThemeProvider theme={lightTheme}>
      <WorkflowStepsSection test={test} workflowTypes={["CountTest", "Observation"]} onWorkflowTypeChanged={() => {}} />
    </ThemeProvider>
  );
}

describe("WorkflowStepsSection step media", () => {
  it("saves a new step medium as a media product with one of that product's conditions", async () => {
    const user = userEvent.setup();
    setup();
    await user.type(await screen.findByLabelText("Step Name"), "TAMC");
    await user.click(screen.getByRole("button", { name: "Add Medium" }));

    await user.click(screen.getByRole("combobox", { name: /Culture media product/ }));
    const products = within(await screen.findByRole("listbox"));
    // Inactive products can't be newly configured.
    expect(products.queryByText(/Retired agar/)).toBeNull();
    await user.click(products.getByText(/Tryptic soy agar/));

    await user.click(screen.getByRole("combobox", { name: "Incubation condition" }));
    const conditions = within(await screen.findByRole("listbox"));
    // Only the picked product's condition is offered.
    expect(conditions.getAllByRole("option")).toHaveLength(2);
    await user.click(conditions.getAllByRole("option")[1]);

    await user.click(screen.getByRole("button", { name: "Add Step" }));

    await waitFor(() => expect(api.createTestWorkflowStep).toHaveBeenCalledTimes(1));
    const [testId, payload] = api.createTestWorkflowStep.mock.calls[0];
    expect(testId).toBe(9);
    expect(payload.stepMedia).toEqual([
      { mediaProductId: 1, mediaIncubationConditionId: 10, isRequired: false, displayOrder: 0 }
    ]);
    expect(payload.stepMedia[0]).not.toHaveProperty("materialId");
  });

  it("keeps an inactive product that is already on the step when the step is edited", async () => {
    const user = userEvent.setup();
    setup([{
      id: 5, stepName: "TAMC", stepOrder: 1, isFinalStep: true, stepType: "PlateCount", version: 3,
      stepMedia: [{ mediaProductId: 2, mediaProductName: "Retired agar", mediaIncubationConditionId: 20, isRequired: true, displayOrder: 0 }]
    }]);
    await user.click(await screen.findByRole("button", { name: "Edit step" }));

    expect(await screen.findByDisplayValue(/MP2: Retired agar/)).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(api.updateTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.updateTestWorkflowStep.mock.calls[0][1].stepMedia).toEqual([
      { mediaProductId: 2, mediaIncubationConditionId: 20, isRequired: true, displayOrder: 0 }
    ]);
  });
});

describe("WorkflowStepsSection version handling", () => {
  const twoSteps = () => [step(5, 1, 3, "TAMC"), step(6, 2, 4, "Plating")];

  it("workflow type sends the loaded test version", async () => {
    const user = userEvent.setup();
    setup();
    await user.click(await screen.findByRole("combobox", { name: "Workflow type" }));
    await user.click(await screen.findByRole("option", { name: WORKFLOW_TYPE_LABELS.Observation }));

    await waitFor(() => expect(api.updateWorkflowType).toHaveBeenCalledTimes(1));
    expect(api.updateWorkflowType).toHaveBeenCalledWith(9, "Observation", 7);
  });

  it("move sends the step version", async () => {
    const user = userEvent.setup();
    setup(twoSteps());
    await user.click((await screen.findAllByRole("button", { name: "Move down" }))[0]);

    await waitFor(() => expect(api.moveTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.moveTestWorkflowStep).toHaveBeenCalledWith(5, "down", 3);
  });

  it("uses returned version for the next action", async () => {
    const user = userEvent.setup();
    // The reload after the first move shows step 5 moved to position 2 with the version the server bumped it to.
    setup([
      twoSteps(),
      [step(6, 1, 4, "Plating"), step(5, 2, 5, "TAMC")],
      [step(6, 1, 4, "Plating"), step(5, 2, 5, "TAMC")]
    ]);
    await user.click((await screen.findAllByRole("button", { name: "Move down" }))[0]);
    await waitFor(() => expect(api.moveTestWorkflowStep).toHaveBeenCalledTimes(1));

    await user.click((await screen.findAllByRole("button", { name: "Move up" }))[1]);

    await waitFor(() => expect(api.moveTestWorkflowStep).toHaveBeenCalledTimes(2));
    expect(api.moveTestWorkflowStep.mock.calls[0]).toEqual([5, "down", 3]);
    expect(api.moveTestWorkflowStep.mock.calls[1]).toEqual([5, "up", 5]);
  });

  it("delete sends the step version", async () => {
    const user = userEvent.setup();
    setup([step(5, 1, 3, "TAMC")]);
    await user.click(await screen.findByRole("button", { name: "Delete step" }));
    await user.click(await screen.findByRole("button", { name: "Delete Step" }));

    await waitFor(() => expect(api.deleteTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.deleteTestWorkflowStep).toHaveBeenCalledWith(5, 3);
  });

  it("server refusal is shown as an error", async () => {
    const user = userEvent.setup();
    setup(twoSteps());
    api.moveTestWorkflowStep.mockRejectedValue({ response: { data: { message: "This step was changed by someone else. Reload and try again." } } });
    await user.click((await screen.findAllByRole("button", { name: "Move down" }))[0]);

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("This step was changed by someone else. Reload and try again.");
  });
});
