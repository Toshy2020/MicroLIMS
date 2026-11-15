import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import { TestMasterPage } from "../TestMasterPage";
import * as laboratorySectionService from "../../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import { HplcMethodService, type HplcMethodListItem } from "../services/HplcMethodService";
import { SolutionMasterService, type SolutionMaster } from "../services/SolutionMasterService";
import { MaterialMasterService, type MaterialMasterEntry } from "../services/MaterialMasterService";
import { TEST_PAYLOAD_FIXTURES } from "./__fixtures__/testPayloads";

// Service-boundary mocks only (CLAUDE.md Rule 4.5).
vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: {
      ...actual.masterDataOptions,
      getTestDefinitions: vi.fn(),
      createTestDefinition: vi.fn(),
      createTestWithSteps: vi.fn(),
      updateTestDefinition: vi.fn(),
      validateTestDefinition: vi.fn(),
      getTestWorkflowSteps: vi.fn().mockResolvedValue([]),
      getTestDefinitionStageReplicates: vi.fn().mockResolvedValue([]),
      getOrganisms: vi.fn().mockResolvedValue([]),
      getMediaIncubationConditions: vi.fn().mockResolvedValue([]),
      getMediaProducts: vi.fn().mockResolvedValue([])
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
  HplcMethodService: { getAll: vi.fn() }
}));

vi.mock("../services/SolutionMasterService", () => ({
  SolutionMasterService: { getAll: vi.fn() }
}));

vi.mock("../services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: vi.fn() }
}));

vi.mock("../services/TestItemsService", () => ({
  TestItemsService: { getItems: vi.fn().mockResolvedValue({ items: [], page: 0, pageSize: 10, totalCount: 0 }) }
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

const hplcMethods: HplcMethodListItem[] = [
  {
    id: 10,
    name: "Ibuprofen Assay",
    abbreviation: "IBU-A",
    isActive: true,
    analyteCount: 1,
    sectionName: "Finished Product",
    lastModifiedAt: "2026-10-27T00:00:00Z",
    technique: "Hplc",
    resultMode: "Assay"
  }
];

const titrants: SolutionMaster[] = [
  {
    id: 101,
    sectionId: 1,
    name: "0.1 N NaOH",
    type: "Titrant",
    shelfLifeValue: 30,
    shelfLifeUnit: "Days",
    storageCondition: "RT",
    finalVolumeMl: 1000,
    instructions: "",
    isActive: true,
    components: [],
    nominalStrength: 0.1,
    strengthUnit: "Normal",
    blankRequired: false
  },
  {
    id: 102,
    sectionId: 1,
    name: "KF Reagent",
    type: "Titrant",
    shelfLifeValue: 30,
    shelfLifeUnit: "Days",
    storageCondition: "RT",
    finalVolumeMl: 1000,
    instructions: "",
    isActive: true,
    components: [],
    nominalStrength: 5,
    strengthUnit: "MgWaterPerMl",
    blankRequired: false
  }
];

const standards: MaterialMasterEntry[] = [
  {
    id: 301,
    sectionId: 1,
    code: "RS-IBU",
    name: "Ibuprofen RS",
    category: "ReferenceStandard",
    baseUnit: "Gram",
    isActive: true
  }
];

const indicators: MaterialMasterEntry[] = [
  {
    id: 201,
    sectionId: 1,
    code: "IND-PP",
    name: "Phenolphthalein",
    category: "Indicator",
    baseUnit: "Gram",
    isActive: true
  }
];

function renderPage(opts: { lab: "micro" | "fp"; area?: "fp" | "rmpm" }) {
  const user = userEvent.setup();
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <TestMasterPage lab={opts.lab} area={opts.area} />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { user };
}

async function openAddDialog(
  user: ReturnType<typeof userEvent.setup>,
  lab: "micro" | "fp",
  expectedSectionName: string
) {
  renderPage(lab === "fp" ? { lab: "fp", area: "fp" } : { lab: "micro" });
  await user.click(await screen.findByRole("button", { name: /Add Test/i }));
  const dialog = await screen.findByRole("dialog");
  await waitFor(() => {
    expect(within(dialog).getByRole("combobox", { name: "Section" }).textContent).toContain(expectedSectionName);
  });
  return dialog;
}

async function selectInDialog(
  user: ReturnType<typeof userEvent.setup>,
  dialog: HTMLElement,
  label: RegExp | string,
  optionName: RegExp | string
) {
  await user.click(within(dialog).getByLabelText(label));
  await user.click(await screen.findByRole("option", { name: optionName }));
}

async function clickNextStep(user: ReturnType<typeof userEvent.setup>, dialog: HTMLElement) {
  await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
}

async function submitWizard(user: ReturnType<typeof userEvent.setup>, dialog: HTMLElement) {
  while (within(dialog).queryByRole("button", { name: /^Next$/i })) {
    await user.click(within(dialog).getByRole("button", { name: /^Next$/i }));
  }
  const saveBtn = (await within(dialog).findByRole("button", { name: /^Save$/i })) as HTMLButtonElement;
  await waitFor(() => expect(saveBtn.disabled).toBe(false));
  await user.click(saveBtn);
}

describe("TestMasterPage payload characterisation", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([fpSection, microSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([fpSection]);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([]);
    vi.mocked(masterDataOptions.validateTestDefinition).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getTestDefinitionStageReplicates).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getTestWorkflowSteps).mockResolvedValue([]);
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "TestDefinition",
      id: 71,
      openWorkCount: 0,
      historicalCount: 0,
      lines: []
    });
    vi.mocked(masterDataOptions.createTestDefinition).mockImplementation(async (payload) => {
      const p = typeof payload === "string" ? { code: payload, displayName: payload } : payload;
      return { id: 900, code: p.code, displayName: p.displayName, isActive: true, workflowType: "Observation" };
    });
    vi.mocked(masterDataOptions.createTestWithSteps).mockImplementation(async ({ test }) => ({
      id: 900,
      code: test.code,
      displayName: test.displayName,
      isActive: true,
      workflowType: test.workflowType ?? "Observation"
    }));
    vi.mocked(masterDataOptions.updateTestDefinition).mockResolvedValue({
      id: 71,
      code: "PH",
      displayName: "pH (25 °C)",
      isActive: true,
      workflowType: "Measurement"
    });
    vi.mocked(HplcMethodService.getAll).mockResolvedValue(hplcMethods);
    vi.mocked(SolutionMasterService.getAll).mockResolvedValue(titrants);
    vi.mocked(MaterialMasterService.getAll).mockImplementation(async (category) => {
      if (category === "ReferenceStandard") return standards;
      if (category === "Indicator") return indicators;
      return [];
    });
  });

  afterEach(cleanup);

  it("characterises CountTest create payload (micro)", async () => {
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([microSection]);
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "micro", "Microbiology");

    await selectInDialog(user, dialog, "Workflow Type", "Count Test");
    await user.type(within(dialog).getByLabelText(/^Code/), "TAMC");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Total Aerobic Microbial Count");
    await user.type(within(dialog).getByLabelText(/^CoA order/), "1");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.countTest
    );
  });

  it("characterises Observation create payload (micro pathogen)", async () => {
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([microSection]);
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "micro", "Microbiology");

    await selectInDialog(user, dialog, "Workflow Type", "Observation");
    await user.type(within(dialog).getByLabelText(/^Code/), "ECOLI");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Escherichia coli");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.observation
    );
  });

  it("characterises Qualitative create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Qualitative/);
    await user.type(within(dialog).getByLabelText(/^Code/), "APP");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Appearance");
    await user.type(within(dialog).getByLabelText(/^CoA order/), "2");
    await clickNextStep(user, dialog);

    await user.click(within(dialog).getByRole("switch", { name: /Recorded per container/i }));
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.qualitative
    );
  });

  it("characterises Measurement create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Measurement/);
    await user.type(within(dialog).getByLabelText(/^Code/), "PH");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "pH");
    await user.type(within(dialog).getByLabelText(/^CoA order/), "3");
    await clickNextStep(user, dialog);

    const repInput = within(dialog).getByLabelText(/^Replicate Count/);
    await user.clear(repInput);
    await user.type(repInput, "3");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.measurement
    );
  });

  it("characterises GravimetricLoss create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Gravimetric/);
    await user.type(within(dialog).getByLabelText(/^Code/), "LOD");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Loss on Drying");
    await clickNextStep(user, dialog);

    const repInput = within(dialog).getByLabelText(/^Replicate Count/);
    await user.clear(repInput);
    await user.type(repInput, "2");
    await user.click(within(dialog).getByRole("switch", { name: /Uses tare/i }));
    await user.type(within(dialog).getByLabelText(/^Condition fields/), "Temperature (°C),Time (h)");
    await selectInDialog(user, dialog, /^Moisture role/, "Loss on drying");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.gravimetricLoss
    );
  });

  it("characterises GravimetricResidue create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Gravimetric/);
    await user.type(within(dialog).getByLabelText(/^Code/), "ASH");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Sulfated Ash");
    await clickNextStep(user, dialog);

    await selectInDialog(user, dialog, "Equation Type", /Ash \/ Gravimetric residue/i);
    const repInput = within(dialog).getByLabelText(/^Replicate Count/);
    await user.clear(repInput);
    await user.type(repInput, "2");
    await user.type(within(dialog).getByLabelText(/^Condition fields/), "Temperature (°C)");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.gravimetricResidue
    );
  });

  it("characterises Dissolution create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Dissolution/);
    await user.type(within(dialog).getByLabelText(/^Code/), "DISS");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Dissolution");
    await clickNextStep(user, dialog);

    await selectInDialog(user, dialog, "HPLC Method", /Ibuprofen Assay \(IBU-A\)/);
    await user.type(within(dialog).getByLabelText(/^Condition fields/), "Apparatus,RPM,Medium");
    await user.type(within(dialog).getByPlaceholderText("e.g. VIT-C"), "DISS-UV");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.dissolution
    );
  });

  it("characterises Disintegration create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Disintegration/);
    await user.type(within(dialog).getByLabelText(/^Code/), "DISINT");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Disintegration");
    await clickNextStep(user, dialog);

    await user.type(within(dialog).getByLabelText(/^Condition fields/), "Medium,Temperature (°C)");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.disintegration
    );
  });

  it("characterises WeightVariation create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Weight Variation/);
    await user.type(within(dialog).getByLabelText(/^Code/), "WV");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Weight Variation");
    await clickNextStep(user, dialog);

    await user.type(within(dialog).getByLabelText(/^Condition fields/), "Balance ID");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.weightVariation
    );
  });

  it("characterises Titration (AcidBase) create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Titration/);
    await user.type(within(dialog).getByLabelText(/^Code/), "TITR-AB");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Ascorbic Acid Titration");
    await clickNextStep(user, dialog);

    await selectInDialog(user, dialog, /^Titration type/i, "Acid-base");
    await selectInDialog(user, dialog, /^Titrant/i, /0\.1 N NaOH/);
    await user.type(within(dialog).getByLabelText(/^Equivalency factor F/i), "88.06");
    await user.type(within(dialog).getByLabelText(/^Maximum RSD %/i), "1.5");
    await selectInDialog(user, dialog, /^Indicator/i, /Phenolphthalein/);
    await user.click(within(dialog).getByRole("switch", { name: /Blank determination required/i }));
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.titration
    );
  });

  it("characterises Titration (KarlFischer) create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^Titration/);
    await user.type(within(dialog).getByLabelText(/^Code/), "KF-H2O");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Water Determination KF");
    await clickNextStep(user, dialog);

    await selectInDialog(user, dialog, /^Titration type/i, /Karl Fischer/i);
    await selectInDialog(user, dialog, /^Moisture role/i, "Water");
    await selectInDialog(user, dialog, /^KF reagent/i, /KF Reagent/);
    await selectInDialog(user, dialog, /^Endpoint/i, "Potentiometric");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.titrationKarlFischer
    );
  });

  it("characterises HplcMethodAssay create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await user.type(within(dialog).getByLabelText(/^Code/), "HPLC-IBU");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Ibuprofen Assay");
    await clickNextStep(user, dialog);

    await selectInDialog(user, dialog, "HPLC Method", /Ibuprofen Assay \(IBU-A\)/);
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.hplcAssay
    );
  });

  it("characterises IcpMethodAssay (Assay) create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^ICP method assay/);
    await user.type(within(dialog).getByLabelText(/^Code/), "ICP-FE");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Iron Assay by ICP");
    await clickNextStep(user, dialog);

    await user.type(within(dialog).getByLabelText(/^Theoretical weight \(g\)/i), "0.5");
    await user.type(within(dialog).getByLabelText("Element 1 symbol"), "Fe");
    await user.type(within(dialog).getByLabelText("Element 1 target ppm"), "50");
    await user.type(within(dialog).getByLabelText("Element 1 cleaning limit ppm"), "10");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.icpAssay
    );
  });

  it("characterises IcpMethodAssay (Impurity) create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^ICP method assay/);
    await user.type(within(dialog).getByLabelText(/^Code/), "ICP-PB");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Lead Impurity by ICP");
    await clickNextStep(user, dialog);

    await user.click(within(dialog).getByRole("button", { name: /Elemental impurity \(ppm\)/i }));
    await user.type(within(dialog).getByLabelText("Element 1 symbol"), "Pb");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.icpImpurity
    );
  });

  it("characterises UvSpectrophotometry (StandardComparison) create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^UV Spectrophotometry/);
    await user.type(within(dialog).getByLabelText(/^Code/), "UV-STD");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "UV Assay against Standard");
    await clickNextStep(user, dialog);

    await selectInDialog(user, dialog, /^Calculation mode/i, /Assay against Standard/);
    await selectInDialog(user, dialog, /^Standard material/i, /Ibuprofen RS/);
    await user.type(within(dialog).getByLabelText(/^Total volume \(mL\)/i), "100");
    await user.type(within(dialog).getByLabelText(/^Standard volume \(mL\)/i), "50");
    const repInput = within(dialog).getByLabelText(/^Replicate count/i);
    await user.clear(repInput);
    await user.type(repInput, "3");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.uvStandardComparison
    );
  });

  it("characterises UvSpectrophotometry (SpecificAbsorbanceAssay) create payload", async () => {
    const user = userEvent.setup();
    const dialog = await openAddDialog(user, "fp", "Finished Product");

    await selectInDialog(user, dialog, "Workflow Type", /^UV Spectrophotometry/);
    await user.type(within(dialog).getByLabelText(/^Code/), "UV-A1");
    await user.type(within(dialog).getByLabelText(/^Display Name/), "Riboflavin A1% Assay");
    await clickNextStep(user, dialog);

    await selectInDialog(user, dialog, /^Calculation mode/i, /Specific Absorbance Assay/i);
    await user.type(within(dialog).getByLabelText(/^Specific absorbance \(A1%\)/i), "328");
    await user.type(within(dialog).getByLabelText(/^Total volume \(mL\)/i), "5000");
    const repInput = within(dialog).getByLabelText(/^Replicate count/i);
    await user.clear(repInput);
    await user.type(repInput, "2");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.createTestWithSteps).toHaveBeenCalledTimes(1));
    expect(vi.mocked(masterDataOptions.createTestWithSteps).mock.calls[0][0].test).toStrictEqual(
      TEST_PAYLOAD_FIXTURES.uvSpecificAbsorbanceAssay
    );
  });

  it("characterises Measurement EDIT update payload", async () => {
    const existing: TestDefinitionOption = {
      id: 71,
      version: 4,
      code: "PH",
      displayName: "pH",
      coaDisplayOrder: 4,
      sectionId: 1,
      workflowType: "Measurement",
      equationType: "Measurement",
      replicateCount: 2,
      evaluationBasis: "Mean",
      physchemArea: "FinishedProduct",
      isActive: true
    };
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([existing]);
    const { user } = renderPage({ lab: "fp", area: "fp" });

    await user.click((await screen.findAllByRole("button", { name: /Row actions/i }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog");

    const nameInput = await within(dialog).findByLabelText(/^Display Name/);
    await user.clear(nameInput);
    await user.type(nameInput, "pH (25 °C)");
    await user.clear(within(dialog).getByLabelText(/^CoA order/));
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.updateTestDefinition).toHaveBeenCalledTimes(1));
    expect(masterDataOptions.updateTestDefinition).toHaveBeenCalledWith(
      71,
      TEST_PAYLOAD_FIXTURES.measurementEdit,
      undefined,
      undefined,
      4
    );
  });

  it("characterises Titration EDIT update payload with changeReason", async () => {
    const existing: TestDefinitionOption = {
      id: 72,
      version: 2,
      code: "TITR-AB",
      displayName: "Ascorbic Acid Titration",
      sectionId: 1,
      workflowType: "Titration",
      equationType: "Titration",
      replicateCount: 3,
      titrationType: "AcidBase",
      titrationNonAqueous: false,
      titrationMode: "Direct",
      titrationCalculation: "UspFactor",
      titrantSolutionMasterId: 101,
      titrationEquivalencyFactor: 88.06,
      titrationBlankRequired: true,
      titrationMaxRsdPercent: 1.5,
      titrationEndpoint: "Visual",
      titrationIndicatorEntryId: 201,
      physchemArea: "FinishedProduct",
      isActive: true
    };
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([existing]);
    const { user } = renderPage({ lab: "fp", area: "fp" });

    await user.click((await screen.findAllByRole("button", { name: /Row actions/i }))[0]);
    await user.click(await screen.findByRole("menuitem", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog");

    await within(dialog).findByLabelText(/^Display Name/);
    await clickNextStep(user, dialog);

    const eqFactorInput = await within(dialog).findByLabelText(/^Equivalency factor F/i);
    await user.clear(eqFactorInput);
    await user.type(eqFactorInput, "88.1");

    const reasonInput = await within(dialog).findByLabelText(/Reason for Change/i);
    await user.type(reasonInput, "SOP revision 4 updated equivalency factor.");
    await submitWizard(user, dialog);

    await waitFor(() => expect(masterDataOptions.updateTestDefinition).toHaveBeenCalledTimes(1));
    expect(masterDataOptions.updateTestDefinition).toHaveBeenCalledWith(
      72,
      TEST_PAYLOAD_FIXTURES.titrationEdit,
      undefined,
      undefined,
      2
    );
  });
});
