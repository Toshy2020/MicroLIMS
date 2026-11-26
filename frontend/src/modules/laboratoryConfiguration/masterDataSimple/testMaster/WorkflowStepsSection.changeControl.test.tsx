import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { WorkflowStepsSection } from "./WorkflowStepsSection";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";

// Network boundaries only: the master-data service and the change-control lookups.
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
const lookup = vi.hoisted(() => ({ listApproved: vi.fn(), getSectionSetting: vi.fn() }));
vi.mock("../../../../services/changeControlLookupService", () => ({ changeControlLookupService: lookup }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const step = (id: number, stepOrder: number, version: number, stepName: string) => ({
  id, stepName, stepOrder, isFinalStep: false, stepType: "PlateCount", version, stepMedia: [], incubationStages: []
});

const test = { id: 9, workflowType: "CountTest", version: 7, sectionId: 4 } as unknown as TestDefinitionOption;

function gateRefusal(code: string, message: string) {
  return { response: { status: 403, data: { success: false, message, errorDetails: [{ code, message }] } } };
}

function setup() {
  api.getOrganisms.mockResolvedValue([]);
  api.getMediaProducts.mockResolvedValue([]);
  api.getMediaIncubationConditions.mockResolvedValue([]);
  api.getTestWorkflowSteps.mockResolvedValue([step(5, 1, 3, "TAMC"), step(6, 2, 4, "Plating")]);
  api.moveTestWorkflowStep.mockResolvedValue({});
  api.deleteTestWorkflowStep.mockResolvedValue({});
  lookup.getSectionSetting.mockResolvedValue({ sectionId: 4, changeControlRequired: false, version: 1 });
  lookup.listApproved.mockResolvedValue([]);
  render(
    <ThemeProvider theme={lightTheme}>
      <WorkflowStepsSection test={test} workflowTypes={["CountTest", "Observation"]} onWorkflowTypeChanged={() => {}} />
    </ThemeProvider>
  );
}

describe("WorkflowStepsSection change-control gate", () => {
  it("a gate refusal on a step move opens the prompt; the retry repeats the move with the same version and the change control", async () => {
    const user = userEvent.setup();
    setup();
    api.moveTestWorkflowStep
      .mockRejectedValueOnce(gateRefusal("ChangeControl.GateRequired", "This laboratory requires an approved change control for this change."))
      .mockResolvedValueOnce({});
    await user.click((await screen.findAllByRole("button", { name: "Move down" }))[0]);

    const prompt = await screen.findByRole("dialog", { name: "Change control needed" });
    expect(within(prompt).getByRole("alert").textContent).toContain("requires an approved change control");
    expect(lookup.listApproved).toHaveBeenCalledWith(4, expect.any(AbortSignal));
    expect(api.moveTestWorkflowStep).toHaveBeenCalledTimes(1);

    await user.type(within(prompt).getByRole("combobox", { name: "Approved change control" }), "CC-31");
    await user.click(within(prompt).getByRole("button", { name: "Retry with this change control" }));

    await waitFor(() => expect(api.moveTestWorkflowStep).toHaveBeenCalledTimes(2));
    expect(api.moveTestWorkflowStep.mock.calls[0]).toEqual([5, "down", 3]);
    expect(api.moveTestWorkflowStep.mock.calls[1]).toEqual([5, "down", 3, "CC-31"]);
  });

  it("reuses the accepted number for the next step change of the same test", async () => {
    const user = userEvent.setup();
    setup();
    api.deleteTestWorkflowStep
      .mockRejectedValueOnce(gateRefusal("ChangeControl.GateRequired", "This laboratory requires an approved change control for this change."))
      .mockResolvedValue({});
    await user.click((await screen.findAllByRole("button", { name: "Delete step" }))[0]);
    await user.click(await screen.findByRole("button", { name: "Delete Step" }));
    const prompt = await screen.findByRole("dialog", { name: "Change control needed" });
    await user.type(within(prompt).getByRole("combobox", { name: "Approved change control" }), "CC-31");
    await user.click(within(prompt).getByRole("button", { name: "Retry with this change control" }));
    await waitFor(() => expect(api.deleteTestWorkflowStep).toHaveBeenCalledTimes(2));

    await user.click((await screen.findAllByRole("button", { name: "Move down" }))[0]);

    await waitFor(() => expect(api.moveTestWorkflowStep).toHaveBeenCalledTimes(1));
    expect(api.moveTestWorkflowStep.mock.calls[0]).toEqual([5, "down", 3, "CC-31"]);
    expect(screen.queryByRole("dialog", { name: "Change control needed" })).toBeNull();
  });

  it("shows the cancel text beside the steps when the prompt is cancelled, and saves nothing more", async () => {
    const user = userEvent.setup();
    setup();
    api.moveTestWorkflowStep.mockRejectedValueOnce(gateRefusal("ChangeControl.GateRequired", "This laboratory requires an approved change control for this change."));
    await user.click((await screen.findAllByRole("button", { name: "Move down" }))[0]);
    const prompt = await screen.findByRole("dialog", { name: "Change control needed" });

    await user.click(within(prompt).getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.getByRole("alert").textContent).toContain("Not saved: no change control was chosen."));
    expect(api.moveTestWorkflowStep).toHaveBeenCalledTimes(1);
  });
});
