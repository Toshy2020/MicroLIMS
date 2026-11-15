import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import {
  SpecificationParameterDialog,
  type TestDefinitionSummary
} from "../components/SpecificationParameterDialog";
import type { Item } from "../services/ItemService";
import {
  SpecificationService,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../../../services/laboratorySectionService";
import {
  HplcMethodService,
  type HplcMethodResponse
} from "../../masterDataSimple/services/HplcMethodService";
import { SPEC_PAYLOAD_FIXTURES } from "./__fixtures__/specPayloads";

// Service-boundary mocks only (CLAUDE.md Rule 4.5).
vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn(),
      getForItem: vi.fn()
    }
  };
});

vi.mock("../../../../services/laboratorySectionService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/laboratorySectionService")>();
  return {
    ...actual,
    getSections: vi.fn(),
    getMySections: vi.fn(),
    laboratorySectionService: {
      ...actual.laboratorySectionService,
      getSections: vi.fn(),
      getMySections: vi.fn()
    }
  };
});

vi.mock("../../masterDataSimple/services/HplcMethodService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../masterDataSimple/services/HplcMethodService")>();
  return {
    ...actual,
    HplcMethodService: {
      ...actual.HplcMethodService,
      getById: vi.fn()
    }
  };
});

const section: LaboratorySection = {
  sectionId: 1,
  sectionCode: "FP",
  sectionName: "Finished Product",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const assignedTests = [
  { testCode: "PH", displayName: "pH" },
  { testCode: "LOD", displayName: "Loss on Drying" },
  { testCode: "POT", displayName: "Potency" },
  { testCode: "DENS", displayName: "Density" },
  { testCode: "TAMC", displayName: "Total Aerobic Microbial Count" },
  { testCode: "APP", displayName: "Appearance" },
  { testCode: "ECOLI", displayName: "E. coli" },
  { testCode: "UNIF", displayName: "Content Uniformity" },
  { testCode: "DISS", displayName: "Dissolution" },
  { testCode: "DISINT", displayName: "Disintegration" },
  { testCode: "WV", displayName: "Weight Variation" },
  { testCode: "HPLC_A", displayName: "HPLC Assay" },
  { testCode: "HPLC_RS", displayName: "Related Substances" },
  { testCode: "GC_RS", displayName: "Residual Solvents" },
  { testCode: "ICP_A", displayName: "ICP Assay" },
  { testCode: "ICP_IMP", displayName: "Elemental Impurities" },
  { testCode: "TITR", displayName: "Titration Assay" }
];

const item: Item = {
  id: 7,
  version: 2,
  name: "Finished Tablets 400 mg",
  code: "FP-TAB-400",
  category: "FinishedProduct",
  sopNumber: "SOP-FP-01",
  isActive: true,
  assignedTests,
  specifications: []
};

const workflowTypeByCode: Record<string, string> = {
  PH: "Measurement",
  LOD: "Measurement",
  POT: "Measurement",
  DENS: "Measurement",
  TAMC: "CountTest",
  APP: "Measurement",
  ECOLI: "Observation",
  UNIF: "Measurement",
  DISS: "Dissolution",
  DISINT: "Disintegration",
  WV: "WeightVariation",
  HPLC_A: "HplcMethodAssay",
  HPLC_RS: "HplcMethodAssay",
  GC_RS: "HplcMethodAssay",
  ICP_A: "IcpMethodAssay",
  ICP_IMP: "IcpMethodAssay",
  TITR: "Titration"
};

const testDefinitionByCode: Record<string, TestDefinitionSummary> = {
  PH: { id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 },
  LOD: { id: 2, code: "LOD", displayName: "Loss on Drying", workflowType: "Measurement", sectionId: 1 },
  POT: { id: 3, code: "POT", displayName: "Potency", workflowType: "Measurement", sectionId: 1 },
  DENS: { id: 4, code: "DENS", displayName: "Density", workflowType: "Measurement", sectionId: 1 },
  TAMC: { id: 5, code: "TAMC", displayName: "Total Aerobic Microbial Count", workflowType: "CountTest", sectionId: 1 },
  APP: { id: 6, code: "APP", displayName: "Appearance", workflowType: "Measurement", sectionId: 1 },
  ECOLI: { id: 7, code: "ECOLI", displayName: "E. coli", workflowType: "Observation", sectionId: 1 },
  UNIF: { id: 8, code: "UNIF", displayName: "Content Uniformity", workflowType: "Measurement", sectionId: 1 },
  DISS: { id: 9, code: "DISS", displayName: "Dissolution", workflowType: "Dissolution", sectionId: 1 },
  DISINT: { id: 10, code: "DISINT", displayName: "Disintegration", workflowType: "Disintegration", sectionId: 1 },
  WV: { id: 11, code: "WV", displayName: "Weight Variation", workflowType: "WeightVariation", sectionId: 1 },
  HPLC_A: { id: 12, code: "HPLC_A", displayName: "HPLC Assay", workflowType: "HplcMethodAssay", equationType: "HplcMethodAssay", hplcMethodId: 20, sectionId: 1 },
  HPLC_RS: { id: 13, code: "HPLC_RS", displayName: "Related Substances", workflowType: "HplcMethodAssay", equationType: "HplcMethodAssay", hplcMethodId: 10, sectionId: 1 },
  GC_RS: { id: 14, code: "GC_RS", displayName: "Residual Solvents", workflowType: "HplcMethodAssay", equationType: "HplcMethodAssay", hplcMethodId: 30, sectionId: 1 },
  ICP_A: {
    id: 15,
    code: "ICP_A",
    displayName: "ICP Assay",
    workflowType: "IcpMethodAssay",
    equationType: "IcpMethodAssay",
    icpMode: "Assay",
    icpElements: [{ id: 11, displayOrder: 1, symbol: "Fe", targetPpm: 50 }],
    sectionId: 1
  },
  ICP_IMP: {
    id: 16,
    code: "ICP_IMP",
    displayName: "Elemental Impurities",
    workflowType: "IcpMethodAssay",
    equationType: "IcpMethodAssay",
    icpMode: "Impurity",
    icpElements: [{ id: 21, displayOrder: 1, symbol: "Pb" }],
    sectionId: 1
  },
  TITR: { id: 17, code: "TITR", displayName: "Titration Assay", workflowType: "Titration", sectionId: 1 }
};

const hplcAssayMethod: HplcMethodResponse = {
  id: 20,
  sectionId: 1,
  name: "Ibuprofen Assay",
  abbreviation: "IBU-A",
  effectiveDate: "2026-10-27T00:00:00Z",
  isActive: true,
  columnDesignation: "L1",
  columnLengthMm: 150,
  columnInternalDiameterMm: 4.6,
  particleSizeUm: 5,
  columnTemperatureC: 30,
  elutionMode: "Isocratic",
  flowRateMlPerMin: 1.0,
  detectorType: "UV",
  injectionVolumeUl: 20,
  runTimeMin: 15,
  diluentSolutionId: 2,
  mobilePhases: [],
  gradientSteps: [],
  technique: "Hplc",
  resultMode: "Assay",
  analytes: [
    {
      id: 601,
      displayOrder: 1,
      name: "Ibuprofen",
      wavelengthNm: 254,
      standardEntryId: 5,
      standardEntryCode: "STD-IBU",
      theoreticalWeightStdMg: 50,
      theoreticalWeightTestMg: 50,
      standardInjections: 5
    }
  ]
};

const hplcRsMethod: HplcMethodResponse = {
  ...hplcAssayMethod,
  id: 10,
  name: "Related Substances Method",
  abbreviation: "RS-M",
  resultMode: "RelatedSubstances",
  impurities: [
    { id: 101, name: "Impurity A", relativeRetention: 1.25, correctionFactor: 1.0 }
  ],
  analytes: [
    {
      id: 501,
      displayOrder: 1,
      name: "Principal Peak",
      wavelengthNm: 254,
      standardEntryId: 5,
      standardEntryCode: "STD-PP",
      theoreticalWeightStdMg: 0,
      theoreticalWeightTestMg: 0,
      standardInjections: 5
    }
  ]
};

const gcRsMethod: HplcMethodResponse = {
  ...hplcAssayMethod,
  id: 30,
  name: "Residual Solvents Method",
  abbreviation: "GC-RS",
  technique: "Gc",
  resultMode: "ResidualSolvents",
  analytes: [
    {
      id: 701,
      displayOrder: 1,
      name: "Methanol",
      standardEntryId: 6,
      standardEntryCode: "STD-MEOH",
      theoreticalWeightStdMg: 10,
      theoreticalWeightTestMg: 10,
      standardInjections: 5
    }
  ]
};

function renderDialog(opts: { preselectedTestCode?: string; editingSpec?: SpecificationDto | null }) {
  const onClose = vi.fn();
  const onSuccess = vi.fn();
  const user = userEvent.setup();
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <SpecificationParameterDialog
          open
          item={item}
          editingSpec={opts.editingSpec ?? null}
          preselectedTestCode={opts.preselectedTestCode ?? null}
          workflowTypeByCode={workflowTypeByCode}
          testDefinitionByCode={testDefinitionByCode}
          existingSpecs={[]}
          onClose={onClose}
          onSuccess={onSuccess}
        />
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { user, onClose, onSuccess };
}

async function selectOption(user: ReturnType<typeof userEvent.setup>, label: RegExp | string, optionText: RegExp | string) {
  const trigger = await screen.findByLabelText(label);
  await user.click(trigger);
  const option = await screen.findByRole("option", { name: optionText });
  await user.click(option);
}

describe("SpecificationParameterDialog payload characterisation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([section]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([section]);
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 900, testCode: "PH" });
    vi.mocked(SpecificationService.update).mockResolvedValue({ id: 42, testCode: "PH" });
    vi.mocked(HplcMethodService.getById).mockImplementation(async (id: number) => {
      if (id === 20) return hplcAssayMethod;
      if (id === 10) return hplcRsMethod;
      return gcRsMethod;
    });
  });

  afterEach(cleanup);

  it("characterises Range create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "PH" });
    await user.type(await screen.findByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "5");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "7");
    await user.click(screen.getByRole("checkbox", { name: /Upper inclusive/i }));
    await user.type(screen.getByRole("textbox", { name: "Unit" }), "pH units");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <791>");
    await selectOption(user, /Production Stage/i, "Finished Product");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.range);
  });

  it("characterises NotMoreThan create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "LOD" });
    await selectOption(user, /Limit Type \*/i, "NMT (Not More Than)");
    await user.type(await screen.findByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "0.5");
    await user.type(screen.getByRole("textbox", { name: "Unit" }), "%");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <731>");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.notMoreThan);
  });

  it("characterises NotLessThan create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "POT" });
    await selectOption(user, /Limit Type \*/i, "NLT (Not Less Than)");
    await user.type(await screen.findByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "95");
    await user.type(screen.getByRole("textbox", { name: "Unit" }), "%");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.notLessThan);
  });

  it("characterises TargetWithTolerance create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "DENS" });
    await selectOption(user, /Limit Type \*/i, /Target ± Tolerance/i);
    await user.type(await screen.findByRole("spinbutton", { name: "Target *" }), "1.02");
    await user.type(screen.getByRole("spinbutton", { name: "Tolerance *" }), "5");
    await selectOption(user, /Mode \*/i, /Percent/i);
    await user.type(screen.getByRole("textbox", { name: "Unit" }), "g/mL");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.targetWithTolerance);
  });

  it("characterises CountTiered create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "TAMC" });
    await user.type(await screen.findByRole("textbox", { name: "Alert Limit" }), "100");
    await user.type(screen.getByRole("textbox", { name: "Action Limit" }), "500");
    await user.type(screen.getByRole("textbox", { name: "Specification Limit *" }), "1000");
    await user.type(screen.getByRole("textbox", { name: "Unit" }), "CFU/g");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <61>");
    await user.type(screen.getByRole("spinbutton", { name: "Dilution Factor" }), "10");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.countTiered);
  });

  it("characterises Qualitative create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "APP" });
    await selectOption(user, /Limit Type \*/i, /Qualitative/i);
    await user.type(await screen.findByRole("textbox", { name: "Expected Result Text *" }), "White round tablets");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.qualitative);
  });

  it("characterises PresenceAbsence create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "ECOLI" });
    await selectOption(user, /Expected State \*/i, /Absence \(Absent\)/i);
    await user.type(await screen.findByRole("spinbutton", { name: "Sample Quantity" }), "10");
    await user.type(screen.getByRole("textbox", { name: "Quantity Unit" }), "g");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <62>");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.presenceAbsence);
  });

  it("characterises MultiStage create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "UNIF" });
    await selectOption(user, /Limit Type \*/i, "Multi-Stage Criteria");
    await user.click(await screen.findByRole("button", { name: "Remove stage 3" }));
    const criteriaInputs = screen.getAllByRole("textbox", { name: "Acceptance Criteria Text *" });
    await user.type(criteriaInputs[0], "Each unit >= 85%");
    await user.type(criteriaInputs[1], "Average >= 75%");
    await user.type(screen.getByRole("textbox", { name: "Unit" }), "%");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.multiStage);
  });

  it("characterises DissolutionQ create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "DISS" });
    await user.type(await screen.findByRole("spinbutton", { name: "Q (% dissolved) *" }), "75");
    await user.type(screen.getByRole("spinbutton", { name: "Label Claim *" }), "500");
    await user.type(screen.getByPlaceholderText("e.g. % w/w, CFU/g, pH units"), "%");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <711>");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.dissolutionQ);
  });

  it("characterises DisintegrationTime create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "DISINT" });
    await user.type(await screen.findByRole("spinbutton", { name: "Time limit (min) *" }), "15");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <701>");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.disintegrationTime);
  });

  it("characterises WeightVariation create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "WV" });
    await selectOption(user, /Dosage Form \*/i, "Tablet");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <2091>");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.weightVariation);
  });

  it("characterises HPLC Assay parameter create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "HPLC_A" });
    await waitFor(() => expect(HplcMethodService.getById).toHaveBeenCalledWith(20));
    await selectOption(user, /Analyte \*/i, /Ibuprofen/i);
    await selectOption(user, /Basis \*/i, "Amount per unit");
    await user.type(await screen.findByRole("spinbutton", { name: "Label Claim *" }), "400");
    await user.type(screen.getByRole("textbox", { name: "Label Claim Unit *" }), "mg");
    await user.type(screen.getByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "380");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "420");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.hplcAssay);
  });

  it("characterises HPLC Related Substances parameter create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "HPLC_RS" });
    await waitFor(() => expect(HplcMethodService.getById).toHaveBeenCalledWith(10));
    await selectOption(user, /Row \*/i, "Named impurity");
    await selectOption(user, /Impurity \*/i, /Impurity A/i);
    await user.type(screen.getByRole("spinbutton", { name: "NMT (%) *" }), "0.15");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.hplcRelatedSubstances);
  });

  it("characterises GC Residual Solvents parameter create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "GC_RS" });
    await waitFor(() => expect(HplcMethodService.getById).toHaveBeenCalledWith(30));
    await selectOption(user, /Solvent \*/i, "Methanol");
    await user.type(screen.getByRole("spinbutton", { name: "NMT (ppm) *" }), "3000");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.hplcResidualSolvents);
  });

  it("characterises ICP Assay parameter create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "ICP_A" });
    await selectOption(user, /Element \*/i, /Fe/i);
    await user.type(screen.getByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "90");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "110");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.icpAssay);
  });

  it("characterises ICP Impurity parameter create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "ICP_IMP" });
    await selectOption(user, /Element \*/i, "Pb");
    await user.type(await screen.findByRole("spinbutton", { name: "NMT (ppm) *" }), "0.5");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.icpImpurity);
  });

  it("characterises Titration basis create payload", async () => {
    const { user } = renderDialog({ preselectedTestCode: "TITR" });
    await selectOption(user, /Result Basis \*/i, "% of label claim");
    await user.type(await screen.findByRole("spinbutton", { name: "Label Claim (mg per unit) *" }), "500");
    await user.type(screen.getByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "98");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "102");
    await user.type(screen.getByRole("textbox", { name: "Unit" }), "%");

    await user.click(screen.getByRole("button", { name: "Add Parameter" }));
    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(SPEC_PAYLOAD_FIXTURES.titration);
  });

  it("characterises Range EDIT update payload", async () => {
    const editingSpec: SpecificationDto = {
      id: 42,
      version: 3,
      itemId: 7,
      testCode: "PH",
      parameterName: "pH",
      displayOrder: 2,
      limitType: "Range",
      referenceStandard: "USP <791>",
      unit: "pH units",
      lowerLimit: 5,
      upperLimit: 7,
      lowerInclusive: true,
      upperInclusive: true,
      productionStageRole: "Bulk"
    };
    const { user } = renderDialog({ editingSpec });
    const upperInput = await screen.findByRole("spinbutton", { name: "Upper Limit (NMT) *" });
    await user.clear(upperInput);
    await user.type(upperInput, "7.5");

    await user.click(screen.getByRole("button", { name: "Save Changes" }));
    await waitFor(() => expect(SpecificationService.update).toHaveBeenCalledTimes(1));
    expect(SpecificationService.update).toHaveBeenCalledWith(42, SPEC_PAYLOAD_FIXTURES.rangeEdit, 3);
  });
});
