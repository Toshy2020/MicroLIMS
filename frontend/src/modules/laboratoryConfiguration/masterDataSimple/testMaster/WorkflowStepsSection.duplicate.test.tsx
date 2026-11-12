import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { WorkflowStepsSection, type WorkflowStepItem } from "./WorkflowStepsSection";
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

const product = (id: number, name: string) => ({
  id, name, code: `MP${id}`, isActive: true, hasStock: true, configurationCount: 0, batchCount: 0, incubationConditionCount: 1
});
const condition = (id: number, mediaProductId: number, hours: number) => ({
  id, mediaProductId, incubationMinHours: hours, incubationMaxHours: hours + 24, temperatureMin: 30, temperatureMax: 35,
  configurationCount: 0, stepMediaCount: 0
});

const test = { id: 9, workflowType: "CountTest" } as unknown as TestDefinitionOption;

const plating: WorkflowStepItem = {
  id: 5, stepName: "Plating", stepOrder: 1, isFinalStep: true, stepType: "SelectivePlating", version: 3,
  targetOrganismId: 7, targetOrganism: { name: "Organism A" }, requiresIncubationTransfer: false,
  stepMedia: [{ mediaProductId: 1, mediaProductName: "Tryptic soy agar", mediaIncubationConditionId: 10, isRequired: true, displayOrder: 0 }],
  incubationStages: [], phenotypicTestType: null, phenotypicTestTypes: []
};
const count: WorkflowStepItem = {
  id: 6, stepName: "Count", stepOrder: 2, isFinalStep: false, stepType: "PlateCount", version: 1,
  requiresIncubationTransfer: false,
  stepMedia: [{ mediaProductId: 2, mediaProductName: "Other agar", mediaIncubationConditionId: 20, isRequired: true, displayOrder: 0 }]
};

const refusal = { response: { data: { message: "A step with this name already exists." } } };

function setup(steps: WorkflowStepItem[] = [plating, count]) {
  api.getOrganisms.mockResolvedValue([{ id: 7, scientificName: "Organism A" }, { id: 8, scientificName: "Organism B" }]);
  api.getMediaProducts.mockResolvedValue([product(1, "Tryptic soy agar"), product(2, "Other agar")]);
  api.getMediaIncubationConditions.mockResolvedValue([condition(10, 1, 48), condition(20, 2, 72)]);
  api.getTestWorkflowSteps.mockResolvedValue(steps);
  api.createTestWorkflowStep.mockResolvedValue({});
  api.updateTestWorkflowStep.mockResolvedValue({});
  const user = userEvent.setup();
  render(
    <PinnedLightTheme>
      <WorkflowStepsSection test={test} workflowTypes={["CountTest"]} onWorkflowTypeChanged={() => {}} />
    </PinnedLightTheme>
  );
  return user;
}

const expectedPayload = {
  stepName: "Plating (copy)",
  phenotypicTestType: null,
  phenotypicTestTypes: [],
  incubationMinHours: 0, incubationMaxHours: 0, temperatureMin: 0, temperatureMax: 0,
  isFinalStep: false,
  stepType: "SelectivePlating",
  targetOrganismId: 7,
  stepMedia: [{ mediaProductId: 1, mediaIncubationConditionId: 10, isRequired: true, displayOrder: 0 }],
  requiresIncubationTransfer: false,
  incubationStages: []
};

describe("WorkflowStepsSection duplicate step", () => {
  it("fills the form in add mode with the copy name, focus on Step Name and the original's values", async () => {
    const user = setup();
    await user.click((await screen.findAllByRole("button", { name: "Duplicate step" }))[0]);

    const name = screen.getByLabelText("Step Name") as HTMLInputElement;
    expect(name.value).toBe("Plating (copy)");
    expect(document.activeElement).toBe(name);
    expect(screen.getByRole("button", { name: "Add Step" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Save Changes" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Cancel" })).toBeNull();
    expect(screen.getByRole("combobox", { name: "Step type" }).textContent).toBe("SelectivePlating");
    expect(screen.getByRole("combobox", { name: "Target Organism (required)" }).textContent).toBe("Organism A");
    expect(await screen.findByDisplayValue(/MP1: Tryptic soy agar/)).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "Incubation condition" }).textContent).toMatch(/48/);
    expect((screen.getByRole("checkbox", { name: "Final Step" }) as HTMLInputElement).checked).toBe(false);
    expect(api.createTestWorkflowStep).not.toHaveBeenCalled();
    expect(api.updateTestWorkflowStep).not.toHaveBeenCalled();
  });

  it("Add Step creates a new step with the original's data and no final flag, never an update", async () => {
    const user = setup();
    await user.click((await screen.findAllByRole("button", { name: "Duplicate step" }))[0]);
    await user.click(screen.getByRole("button", { name: "Add Step" }));

    await waitFor(() => expect(api.createTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.createTestWorkflowStep).toHaveBeenCalledWith(9, expectedPayload);
    expect(api.updateTestWorkflowStep).not.toHaveBeenCalled();
  });

  it("sends the renamed step name", async () => {
    const user = setup();
    await user.click((await screen.findAllByRole("button", { name: "Duplicate step" }))[0]);
    const name = screen.getByLabelText("Step Name");
    await user.clear(name);
    await user.type(name, "Plating B");
    await user.click(screen.getByRole("button", { name: "Add Step" }));

    await waitFor(() => expect(api.createTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.createTestWorkflowStep).toHaveBeenCalledWith(9, { ...expectedPayload, stepName: "Plating B" });
  });

  it("leaves edit mode when duplicating while another step is being edited", async () => {
    const user = setup();
    await user.click((await screen.findAllByRole("button", { name: "Edit step" }))[1]);
    expect(screen.getByRole("button", { name: "Save Changes" })).toBeTruthy();
    expect((screen.getByLabelText("Step Name") as HTMLInputElement).value).toBe("Count");

    await user.click(screen.getAllByRole("button", { name: "Duplicate step" })[0]);
    expect(screen.queryByRole("button", { name: "Save Changes" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Cancel" })).toBeNull();
    expect((screen.getByLabelText("Step Name") as HTMLInputElement).value).toBe("Plating (copy)");

    await user.click(screen.getByRole("button", { name: "Add Step" }));
    await waitFor(() => expect(api.createTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.createTestWorkflowStep).toHaveBeenCalledWith(9, expectedPayload);
    expect(api.updateTestWorkflowStep).not.toHaveBeenCalled();
  });

  it("shows the server's refusal and keeps the copy in the form", async () => {
    const user = setup();
    await user.click((await screen.findAllByRole("button", { name: "Duplicate step" }))[0]);
    api.createTestWorkflowStep.mockRejectedValue(refusal);
    await user.click(screen.getByRole("button", { name: "Add Step" }));

    expect(await screen.findByText("A step with this name already exists.")).toBeTruthy();
    expect((screen.getByLabelText("Step Name") as HTMLInputElement).value).toBe("Plating (copy)");
    expect(screen.getByRole("button", { name: "Add Step" })).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "Target Organism (required)" }).textContent).toBe("Organism A");
  });
});
