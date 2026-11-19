import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { MemoryRouter } from "react-router-dom";
import type { ReactNode } from "react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import * as laboratorySectionService from "../../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import { HplcMethodService } from "../services/HplcMethodService";
import { MaterialMasterService } from "../services/MaterialMasterService";
import { SolutionMasterService } from "../services/SolutionMasterService";
import type { WorkflowStepItem } from "../testMaster/WorkflowStepsSection";
import { TestMasterWizard, type TestMasterWizardProps } from "./TestMasterWizard";
import { toast } from "sonner";

vi.mock("sonner", () => ({ toast: { error: vi.fn(), success: vi.fn(), info: vi.fn(), warning: vi.fn() } }));

vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: {
      ...actual.masterDataOptions,
      getTestDefinitions: vi.fn().mockResolvedValue([]),
      validateTestDefinition: vi.fn().mockResolvedValue([]),
      createTestWithSteps: vi.fn(),
      createTestWorkflowStep: vi.fn(),
      updateTestDefinition: vi.fn(),
      getTestWorkflowSteps: vi.fn().mockResolvedValue([]),
      getTestDefinitionStageReplicates: vi.fn().mockResolvedValue([]),
      getMediaProducts: vi.fn().mockResolvedValue([]),
      getMediaIncubationConditions: vi.fn().mockResolvedValue([]),
      getOrganisms: vi.fn().mockResolvedValue([])
    }
  };
});

vi.mock("../../../../services/masterDataUsageService", () => ({
  masterDataUsageService: {
    getUsage: vi.fn()
  }
}));

vi.mock("../../../../services/laboratorySectionService", () => ({
  getSections: vi.fn(),
  getMySections: vi.fn()
}));

vi.mock("../services/HplcMethodService", () => ({
  HplcMethodService: { getAll: vi.fn().mockResolvedValue([]) }
}));

vi.mock("../services/SolutionMasterService", () => ({
  SolutionMasterService: { getAll: vi.fn().mockResolvedValue([]) }
}));

vi.mock("../services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: vi.fn().mockResolvedValue([]) }
}));

const fpSection = {
  sectionId: 1,
  sectionCode: "FP",
  sectionName: "Finished Product",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const microSection = {
  sectionId: 2,
  sectionCode: "MICRO",
  sectionName: "Microbiology",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

function wrap(node: ReactNode) {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>{node}</MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

function renderWizard(overrides: Partial<TestMasterWizardProps> = {}) {
  const onCreateWithSteps = vi.fn().mockResolvedValue({
    id: 901,
    code: "CREATED",
    displayName: "Created",
    isActive: true,
    workflowType: "CountTest"
  });
  const onUpdateTest = vi.fn().mockResolvedValue({
    id: 71,
    code: "PH",
    displayName: "pH Updated",
    isActive: true,
    workflowType: "Measurement"
  });
  const onReloadDefinitions = vi.fn().mockResolvedValue(undefined);
  const onClose = vi.fn();
  const onSaved = vi.fn();

  const utils = wrap(
    <TestMasterWizard
      open
      lab="micro"
      defaultSectionId={2}
      sections={[microSection]}
      mySections={[microSection]}
      hplcMethods={[]}
      onCreateWithSteps={onCreateWithSteps}
      onUpdateTest={onUpdateTest}
      onReloadDefinitions={onReloadDefinitions}
      onClose={onClose}
      onSaved={onSaved}
      {...overrides}
    />
  );

  return {
    ...utils,
    onCreateWithSteps,
    onUpdateTest,
    onReloadDefinitions,
    onClose,
    onSaved
  };
}

describe("TestMasterWizard", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([fpSection, microSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([fpSection, microSection]);
    vi.mocked(masterDataOptions.validateTestDefinition).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getTestWorkflowSteps).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getTestDefinitionStageReplicates).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMediaProducts).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMediaIncubationConditions).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getOrganisms).mockResolvedValue([]);
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "TestDefinition",
      id: 71,
      openWorkCount: 0,
      historicalCount: 0,
      lines: []
    });
    vi.mocked(HplcMethodService.getAll).mockResolvedValue([]);
    vi.mocked(SolutionMasterService.getAll).mockResolvedValue([]);
    vi.mocked(MaterialMasterService.getAll).mockResolvedValue([]);
  });

  afterEach(cleanup);

  it("'New from existing' pre-fills everything except code and display name, and creating leaves the source unchanged", async () => {
    const sourceSteps: WorkflowStepItem[] = [
      {
        id: 55,
        version: 1,
        stepOrder: 1,
        stepName: "Biochemical Screen",
        stepType: "BiochemicalTest",
        phenotypicTestType: null,
        phenotypicTestTypes: ["Oxidase"],
        isFinalStep: true,
        targetOrganismId: null,
        stepMedia: []
      }
    ];
    vi.mocked(masterDataOptions.getTestWorkflowSteps).mockResolvedValue(sourceSteps);

    const sourceTest: TestDefinitionOption = {
      id: 21,
      code: "ECOLI-SRC",
      displayName: "Escherichia coli Source",
      coaDisplayOrder: 4,
      sectionId: 2,
      workflowType: "Observation",
      equationType: "None",
      isActive: true
    };

    const user = userEvent.setup();
    const { onCreateWithSteps, onUpdateTest } = renderWizard({
      lab: "micro",
      duplicateFrom: sourceTest
    });

    const dialog = await screen.findByRole("dialog");
    const codeInput = (await within(dialog).findByLabelText(/^Code/)) as HTMLInputElement;
    const nameInput = within(dialog).getByLabelText(/^Display Name/) as HTMLInputElement;
    const coaInput = within(dialog).getByLabelText(/^CoA order/) as HTMLInputElement;

    expect(codeInput.value).toBe("");
    expect(nameInput.value).toBe("");
    expect(coaInput.value).toBe("");
    expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain("Microbiology");
    expect(within(dialog).getByLabelText("Workflow Type").textContent).toContain("Observation");

    await user.type(codeInput, "ECOLI-COPY");
    await user.type(nameInput, "Escherichia coli Copy");

    // Step 2: Steps step should have the source's workflow step pre-filled.
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    expect(await within(dialog).findByText("Biochemical Screen")).toBeTruthy();

    // Step 3: Review shows AnalystPreview and saves via createTestWithSteps.
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    expect(await within(dialog).findByText("Preview — configuration only")).toBeTruthy();

    const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
    await waitFor(() => expect(saveBtn.disabled).toBe(false));
    await user.click(saveBtn);

    await waitFor(() => expect(onCreateWithSteps).toHaveBeenCalledTimes(1));
    expect(onUpdateTest).not.toHaveBeenCalled();
    expect(masterDataOptions.updateTestDefinition).not.toHaveBeenCalled();
    expect(masterDataOptions.createTestWorkflowStep).not.toHaveBeenCalled();

    const sent = onCreateWithSteps.mock.calls[0][0];
    expect(sent.test).toMatchObject({
      code: "ECOLI-COPY",
      displayName: "Escherichia coli Copy",
      sectionId: 2,
      workflowType: "Observation"
    });
    expect(sent.steps).toHaveLength(1);
    expect(sent.steps[0]).toMatchObject({
      stepName: "Biochemical Screen",
      stepType: "BiochemicalTest",
      isFinalStep: true,
      phenotypicTestTypes: ["Oxidase"]
    });
  });

  it("'New from existing' closes with an error instead of copying without steps when they cannot be loaded", async () => {
    vi.mocked(masterDataOptions.getTestWorkflowSteps).mockRejectedValue(new Error("Network down"));
    const sourceTest: TestDefinitionOption = {
      id: 22,
      code: "ECOLI-SRC2",
      displayName: "Escherichia coli Source 2",
      sectionId: 2,
      workflowType: "Observation",
      equationType: "None",
      isActive: true
    };

    const { onClose, onCreateWithSteps } = renderWizard({ lab: "micro", duplicateFrom: sourceTest });

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
    expect(toast.error).toHaveBeenCalledTimes(1);
    expect(onCreateWithSteps).not.toHaveBeenCalled();
  });

  it("asks for a change reason when editing a test with open test orders", async () => {
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "TestDefinition",
      id: 71,
      openWorkCount: 3,
      historicalCount: 1,
      lines: [
        {
          key: "test-orders",
          label: "Open test orders",
          count: 3,
          isOpenWork: true,
          listRoute: null
        }
      ]
    });

    const editingTest: TestDefinitionOption = {
      id: 71,
      version: 4,
      code: "PH",
      displayName: "pH",
      sectionId: 1,
      workflowType: "Measurement",
      equationType: "Measurement",
      replicateCount: 2,
      evaluationBasis: "Mean",
      physchemArea: "FinishedProduct",
      isActive: true
    };

    const user = userEvent.setup();
    const { onUpdateTest } = renderWizard({
      lab: "fp",
      area: "fp",
      defaultSectionId: 1,
      sections: [fpSection],
      mySections: [fpSection],
      editingTest
    });

    const dialog = await screen.findByRole("dialog");
    const nameInput = await within(dialog).findByLabelText(/^Display Name/);
    await user.clear(nameInput);
    await user.type(nameInput, "pH Updated");

    // Advance through Basics -> Type Settings -> Stages -> Review.
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));

    const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
    await waitFor(() => expect(saveBtn.disabled).toBe(false));
    await user.click(saveBtn);

    // Reason dialog opens; update is not sent yet.
    expect(onUpdateTest).not.toHaveBeenCalled();
    const reasonField = await screen.findByLabelText(/Reason for change/i);
    await user.type(reasonField, "Revised SOP-09 display name.");
    await user.click(screen.getByRole("button", { name: /Confirm/i }));

    await waitFor(() => expect(onUpdateTest).toHaveBeenCalledTimes(1));
    expect(onUpdateTest).toHaveBeenCalledWith(
      71,
      expect.objectContaining({ displayName: "pH Updated" }),
      "Revised SOP-09 display name.",
      4
    );
  });

  it("create sends one with-steps call containing test, stages, and steps", async () => {
    const user = userEvent.setup();
    const { onCreateWithSteps } = renderWizard({
      lab: "micro",
      defaultSectionId: 2,
      sections: [microSection],
      mySections: [microSection]
    });

    const dialog = await screen.findByRole("dialog");
    await user.type(await within(dialog).findByLabelText(/^Code/), "MY-COUNT");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "My Count Test");
    await user.click(within(dialog).getByLabelText("Workflow Type"));
    await user.click(await screen.findByRole("option", { name: "Count Test" }));

    // Step 2: Steps - add a BiochemicalTest step without needing external media catalogs.
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    await user.type(await within(dialog).findByLabelText("Step Name"), "Oxidase Screen");
    await user.click(within(dialog).getByLabelText("Step type"));
    await user.click(await screen.findByRole("option", { name: "BiochemicalTest" }));
    await user.click(within(dialog).getByLabelText("Phenotypic Test Type"));
    await user.click(await screen.findByRole("option", { name: "Oxidase" }));
    await user.click(within(dialog).getByRole("button", { name: /^Add$/i }));
    await user.click(within(dialog).getByRole("checkbox", { name: /Final Step/i }));
    await user.click(within(dialog).getByRole("button", { name: /^Add Step$/i }));

    // Step 3: Review - verify AnalystPreview and click Save.
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    const previewRegion = within(dialog).getByLabelText("Analyst preview");
    expect(within(previewRegion).getByText("Preview — configuration only")).toBeTruthy();
    expect(within(previewRegion).getByText(/1\. Oxidase Screen/)).toBeTruthy();

    const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
    await waitFor(() => expect(saveBtn.disabled).toBe(false));
    await user.click(saveBtn);

    await waitFor(() => expect(onCreateWithSteps).toHaveBeenCalledTimes(1));
    expect(onCreateWithSteps.mock.calls[0][0]).toMatchObject({
      test: expect.objectContaining({
        code: "MY-COUNT",
        displayName: "My Count Test",
        workflowType: "CountTest"
      }),
      stages: [],
      steps: [
        expect.objectContaining({
          stepName: "Oxidase Screen",
          stepType: "BiochemicalTest",
          isFinalStep: true,
          phenotypicTestTypes: ["Oxidase"]
        })
      ]
    });
  });

  it("after 409 conflict, reloads version 8 and sends version 8 on retry", async () => {
    const editingTest: TestDefinitionOption = {
      id: 71,
      version: 2,
      code: "PH",
      displayName: "pH",
      sectionId: 1,
      workflowType: "Measurement",
      equationType: "Measurement",
      replicateCount: 2,
      evaluationBasis: "Mean",
      physchemArea: "FinishedProduct",
      isActive: true
    };
    const conflictErr = new AxiosError("Conflict", "ERR_BAD_REQUEST", undefined, undefined, {
      status: 409,
      statusText: "Conflict",
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { message: "Modified by another user" },
    });
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([
      { ...editingTest, version: 8, displayName: "pH Server" }
    ]);
    const onUpdateTest = vi
      .fn()
      .mockRejectedValueOnce(conflictErr)
      .mockResolvedValueOnce({ ...editingTest, version: 9, displayName: "pH Updated" });

    const user = userEvent.setup();
    const { onSaved } = renderWizard({
      lab: "fp",
      area: "fp",
      defaultSectionId: 1,
      sections: [fpSection],
      mySections: [fpSection],
      editingTest,
      onUpdateTest
    });

    const dialog = await screen.findByRole("dialog");
    const nameInput = await within(dialog).findByLabelText(/^Display Name/);
    await user.clear(nameInput);
    await user.type(nameInput, "pH Updated");

    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));

    const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
    await waitFor(() => expect(saveBtn.disabled).toBe(false));
    await user.click(saveBtn);

    await screen.findByRole("alert");
    const reviewBtn = screen.getByRole("button", { name: "Review and save again" }) as HTMLButtonElement;
    await waitFor(() => expect(reviewBtn.disabled).toBe(false));
    await user.click(reviewBtn);
    await user.click(await within(dialog).findByRole("button", { name: /^Save$/i }));

    await waitFor(() => expect(onUpdateTest).toHaveBeenCalledTimes(2));
    expect(onUpdateTest.mock.calls[0][3]).toBe(2);
    expect(onUpdateTest.mock.calls[1][3]).toBe(8);
    expect(onSaved).toHaveBeenCalledTimes(1);
  });
});
