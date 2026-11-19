import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../../../services/laboratorySectionService";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import type { MaterialMasterEntry } from "../../masterDataSimple/services/MaterialMasterService";
import { HplcAnalytesSection } from "../../masterDataSimple/hplcMethod/HplcAnalytesSection";
import { HplcGeneralSection } from "../../masterDataSimple/hplcMethod/HplcGeneralSection";
import {
  HplcMethodService,
  type HplcMethodResponse
} from "../../masterDataSimple/services/HplcMethodService";
import { SpecificationService } from "../../specifications/services/SpecificationService";
import type { Item } from "../services/ItemService";
import { SpecificationWizard, type SpecificationWizardProps } from "./SpecificationWizard";
import type { TestDefinitionSummary } from "./specHelpers";

vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<
    typeof import("../../specifications/services/SpecificationService")
  >();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      create: vi.fn(),
      update: vi.fn(),
      getForItem: vi.fn().mockResolvedValue([]),
      validate: vi.fn()
    }
  };
});

vi.mock("../../masterDataSimple/services/HplcMethodService", () => ({
  HplcMethodService: {
    getById: vi.fn()
  }
}));

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

vi.mock("../../../../services/masterDataUsageService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataUsageService")>();
  return {
    ...actual,
    masterDataUsageService: {
      ...actual.masterDataUsageService,
      getUsage: vi.fn()
    }
  };
});

const qcSection: LaboratorySection = {
  sectionId: 1,
  sectionCode: "QC",
  sectionName: "Quality Control",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const mockRsMethod: HplcMethodResponse = {
  id: 10,
  sectionId: 1,
  name: "Sildenafil RS Method",
  abbreviation: "SIL-RS",
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
  runTimeMin: 35,
  diluentSolutionId: 2,
  mobilePhases: [],
  gradientSteps: [],
  technique: "Hplc",
  resultMode: "RelatedSubstances",
  referenceSolutionPercent: 0.1,
  disregardLimitPercent: 0.05,
  impurities: [
    { id: 101, name: "Impurity D", relativeRetention: 0.15, correctionFactor: 0.7 },
    { id: 102, name: "Impurity A", relativeRetention: 1.25, correctionFactor: 1.0 }
  ],
  analytes: [
    {
      id: 501,
      displayOrder: 1,
      name: "Sildenafil Principal Peak",
      wavelengthNm: 230,
      standardEntryId: 5,
      standardEntryCode: "STD-SIL",
      theoreticalWeightStdMg: 0,
      theoreticalWeightTestMg: 0,
      standardInjections: 5
    }
  ]
};

const mockAssayMethod: HplcMethodResponse = {
  id: 20,
  sectionId: 1,
  name: "Sildenafil Assay Method",
  abbreviation: "SIL-ASSAY",
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
  runTimeMin: 20,
  diluentSolutionId: 2,
  mobilePhases: [],
  gradientSteps: [],
  technique: "Hplc",
  resultMode: "Assay",
  analytes: [
    {
      id: 601,
      displayOrder: 1,
      name: "Sildenafil Citrate",
      wavelengthNm: 230,
      standardEntryId: 5,
      standardEntryCode: "STD-SIL",
      theoreticalWeightStdMg: 50,
      theoreticalWeightTestMg: 50,
      standardInjections: 5
    }
  ]
};

const mockItemRawMaterial: Item = {
  id: 295,
  code: "RM-SIL",
  name: "Sildenafil Citrate Raw Material",
  category: "RawMaterial",
  sopNumber: "SOP-001",
  isActive: true,
  assignedTests: [
    { testCode: "TEST-RS", displayName: "Related Substances by HPLC" },
    { testCode: "TEST-ASSAY", displayName: "HPLC Assay" }
  ],
  specifications: []
};

const mockTestDefs: Record<string, TestDefinitionSummary> = {
  "TEST-RS": {
    id: 1,
    code: "TEST-RS",
    displayName: "Related Substances by HPLC",
    workflowType: "HplcMethodAssay",
    equationType: "HplcMethodAssay",
    hplcMethodId: 10
  },
  "TEST-ASSAY": {
    id: 2,
    code: "TEST-ASSAY",
    displayName: "HPLC Assay",
    workflowType: "HplcMethodAssay",
    equationType: "HplcMethodAssay",
    hplcMethodId: 20
  }
};

function renderWizard(props: Partial<SpecificationWizardProps> & Pick<SpecificationWizardProps, "item" | "workflowTypeByCode" | "testDefinitionByCode">) {
  const user = userEvent.setup();
  const rendered = render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <SpecificationWizard
            open={true}
            existingSpecs={[]}
            onClose={vi.fn()}
            onSuccess={vi.fn()}
            {...props}
          />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { ...rendered, user };
}

async function selectOption(
  user: ReturnType<typeof userEvent.setup>,
  label: RegExp | string,
  optionText: RegExp | string
) {
  const trigger = await screen.findByLabelText(label);
  await user.click(trigger);
  const option = await screen.findByRole("option", { name: optionText });
  await user.click(option);
}

describe("SpecificationWizard - Related Substances & Raw Material Assay (Task 6)", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    localStorage.clear();
    await resetDraftDb();
    signInForTest(7);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([qcSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([qcSection]);
    vi.mocked(SpecificationService.validate).mockResolvedValue([]);
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "Specification",
      id: 999,
      openWorkCount: 0,
      historicalCount: 0,
      lines: []
    });
  });

  afterEach(cleanup);

  it("shows Row select, fixed NMT limit type, % unit, and % as is basis for an RS test", async () => {
    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockRsMethod);

    const { user } = renderWizard({
      item: mockItemRawMaterial,
      preselectedTestCode: "TEST-RS",
      workflowTypeByCode: { "TEST-RS": "HplcMethodAssay" },
      testDefinitionByCode: mockTestDefs
    });

    await user.click(await screen.findByRole("button", { name: "Next" }));

    await waitFor(() => {
      expect(HplcMethodService.getById).toHaveBeenCalledWith(10);
    });

    expect(await screen.findByLabelText(/Row \*/i)).toBeTruthy();
    expect(screen.getByDisplayValue("% as is")).toBeTruthy();
    expect(screen.queryByLabelText(/Impurity \*/i)).toBeNull();

    // Select a row to pass Method validation and inspect Limits and Unit steps
    await selectOption(user, /Row \*/i, /Total impurities/i);
    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(await screen.findByDisplayValue("NMT (Not More Than)")).toBeTruthy();
    await user.type(screen.getByLabelText(/NMT \(%\) \*/i), "1.0");
    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(await screen.findByDisplayValue("%")).toBeTruthy();
  });

  it("shows impurity picker only for Named impurity and submits payload with relatedSubstanceRow and hplcMethodImpurityId", async () => {
    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockRsMethod);
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 999, testCode: "TEST-RS" });

    const { user } = renderWizard({
      item: mockItemRawMaterial,
      preselectedTestCode: "TEST-RS",
      workflowTypeByCode: { "TEST-RS": "HplcMethodAssay" },
      testDefinitionByCode: mockTestDefs
    });

    await user.click(await screen.findByRole("button", { name: "Next" }));
    await waitFor(() => expect(HplcMethodService.getById).toHaveBeenCalledWith(10));

    await selectOption(user, /Row \*/i, /Any unspecified impurity/i);
    expect(screen.queryByLabelText(/Impurity \*/i)).toBeNull();

    await selectOption(user, /Row \*/i, /Named impurity/i);
    expect(await screen.findByLabelText(/Impurity \*/i)).toBeTruthy();

    await selectOption(user, /Impurity \*/i, /Impurity D/i);
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByLabelText(/NMT \(%\) \*/i), "0.15");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(await screen.findByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(SpecificationService.create).toHaveBeenCalledWith(
      expect.objectContaining({
        itemId: 295,
        testCode: "TEST-RS",
        relatedSubstanceRow: "Named",
        hplcMethodImpurityId: 101,
        hplcMethodAnalyteId: 501,
        limitType: "NotMoreThan",
        unit: "%",
        resultBasis: "PercentAsIs",
        upperLimit: 0.15
      })
    );
  });

  it("adds % as is, % dried basis, and % anhydrous basis to Basis select for HPLC Assay on Raw Material", async () => {
    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockAssayMethod);
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 1000, testCode: "TEST-ASSAY" });

    const { user } = renderWizard({
      item: mockItemRawMaterial,
      preselectedTestCode: "TEST-ASSAY",
      workflowTypeByCode: { "TEST-ASSAY": "HplcMethodAssay" },
      testDefinitionByCode: mockTestDefs
    });

    await user.click(await screen.findByRole("button", { name: "Next" }));
    await waitFor(() => expect(HplcMethodService.getById).toHaveBeenCalledWith(20));

    await selectOption(user, /Analyte \*/i, /Sildenafil Citrate/i);

    const basisSelect = screen.getByLabelText(/Basis \*/i);
    await user.click(basisSelect);

    expect(await screen.findByRole("option", { name: /Assay %/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /Amount per unit/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /% as is/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /% dried basis/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /% anhydrous basis/i })).toBeTruthy();

    await user.click(screen.getByRole("option", { name: /% dried basis/i }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByLabelText(/Lower Limit \(NLT\) \*/i), "98.0");
    await user.type(screen.getByLabelText(/Upper Limit \(NMT\) \*/i), "102.0");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(await screen.findByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(SpecificationService.create).toHaveBeenCalledWith(
      expect.objectContaining({
        itemId: 295,
        testCode: "TEST-ASSAY",
        hplcMethodAnalyteId: 601,
        resultBasis: "PercentDriedBasis",
        unit: "%",
        lowerLimit: 98.0,
        upperLimit: 102.0
      })
    );
  });

  it("renders RS analyte section with no theoretical weights and no Add Analyte button", () => {
    const standardEntry: MaterialMasterEntry = {
      id: 1,
      sectionId: 1,
      code: "RS-01",
      name: "Std 1",
      category: "ReferenceStandard",
      baseUnit: "Gram",
      isActive: true
    };
    render(
      <HplcAnalytesSection
        technique="Hplc"
        resultMode="RelatedSubstances"
        analytes={[
          {
            name: "Sildenafil Principal Peak",
            wavelengthNm: "230",
            standardEntryId: 1,
            theoreticalWeightStdMg: "0",
            theoreticalWeightTestMg: "0",
            standardDilution: "",
            standardInjections: "5",
            sstMaxRsdPercent: "2.0",
            sstMinResolution: "",
            sstMaxTailingFactor: "",
            sstMinTheoreticalPlates: "",
            sstMinRetentionFactor: "",
            sstMinSignalToNoise: "",
            sstMinPeakToValley: "",
            standardConcentrationUgPerMl: ""
          }
        ]}
        availableStandards={[standardEntry]}
        errors={{}}
        onAnalyteChange={vi.fn()}
        onAddAnalyte={vi.fn()}
        onRemoveAnalyte={vi.fn()}
        onMoveAnalyte={vi.fn()}
      />
    );

    expect(screen.queryByRole("button", { name: /Add Analyte/i })).toBeNull();
    expect(screen.queryByLabelText(/Th\. Wt\./i)).toBeNull();
    expect(screen.queryByLabelText(/Standard Dilution/i)).toBeNull();
    expect(screen.getByText(/Principal Peak & System Suitability Criteria/i)).toBeTruthy();
    expect(screen.getByLabelText(/Standard Injections \*/i)).toBeTruthy();
  });

  it("disables internal standard field when editing an existing method in HplcGeneralSection", () => {
    render(
      <HplcGeneralSection
        editingId={1}
        technique="Hplc"
        resultMode="Assay"
        name="Assay Method"
        abbreviation="AS-01"
        effectiveDate="2026-01-01"
        sectionId={1}
        internalStandardName="p-hydroxybenzoic acid"
        mySections={[{ ...qcSection, sectionName: "QC Section" }]}
        errors={{}}
        onTechniqueChange={vi.fn()}
        onResultModeChange={vi.fn()}
        onInternalStandardNameChange={vi.fn()}
        onNameChange={vi.fn()}
        onAbbreviationChange={vi.fn()}
        onEffectiveDateChange={vi.fn()}
        onSectionIdChange={vi.fn()}
      />
    );

    const isInput = screen.getByRole("textbox", { name: /Internal standard/i });
    expect((isInput as HTMLInputElement).disabled).toBe(true);
  });

  it("does not offer content bases (% as is, % dried basis, % anhydrous basis) for non-RM items", async () => {
    const mockFpItem: Item = {
      id: 300,
      code: "FP-TAB",
      sopNumber: "SOP-FP-01",
      name: "Sildenafil Tablets 50mg",
      category: "FinishedProduct",
      isActive: true,
      assignedTests: [{ testCode: "TEST-ASSAY", displayName: "HPLC Assay" }],
      specifications: []
    };

    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockAssayMethod);

    const { user } = renderWizard({
      item: mockFpItem,
      workflowTypeByCode: { "TEST-ASSAY": "HplcMethodAssay" },
      testDefinitionByCode: mockTestDefs
    });

    await screen.findByDisplayValue("TEST-ASSAY");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(HplcMethodService.getById).toHaveBeenCalledWith(20));

    await selectOption(user, /Analyte \*/i, /Sildenafil Citrate/i);

    const basisSelect = await screen.findByLabelText(/Basis \*/i);
    await user.click(basisSelect);

    expect(await screen.findByRole("option", { name: /Assay %/i })).toBeTruthy();
    expect(screen.getByRole("option", { name: /Amount per unit/i })).toBeTruthy();

    expect(screen.queryByRole("option", { name: /% as is/i })).toBeNull();
    expect(screen.queryByRole("option", { name: /% dried basis/i })).toBeNull();
    expect(screen.queryByRole("option", { name: /% anhydrous basis/i })).toBeNull();
  });

  it("submits a Total impurities row with hplcMethodImpurityId null", async () => {
    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockRsMethod);
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 1001, testCode: "TEST-RS" });

    const { user } = renderWizard({
      item: mockItemRawMaterial,
      workflowTypeByCode: { "TEST-RS": "HplcMethodAssay" },
      testDefinitionByCode: mockTestDefs
    });

    await screen.findByDisplayValue("TEST-RS");
    await user.click(screen.getByRole("button", { name: "Next" }));

    await selectOption(user, /Row \*/i, /Total impurities/i);
    expect(screen.queryByLabelText(/Impurity \*/i)).toBeNull();
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByLabelText(/NMT \(%\) \*/i), "1.00");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(await screen.findByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(SpecificationService.create).toHaveBeenCalledWith(
      expect.objectContaining({
        itemId: 295,
        testCode: "TEST-RS",
        relatedSubstanceRow: "Total",
        hplcMethodImpurityId: null,
        hplcMethodAnalyteId: 501,
        limitType: "NotMoreThan",
        unit: "%",
        resultBasis: "PercentAsIs",
        upperLimit: 1.0
      })
    );
  });

  it("first open of a UV AbsorbanceLimit test sends resultBasis null and Qualitative", async () => {
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 2001, testCode: "TEST-UV-LIM" });
    const mockUvItem: Item = {
      id: 300,
      code: "RM-UV",
      name: "UV Test Item",
      category: "RawMaterial",
      sopNumber: "SOP-UV",
      isActive: true,
      assignedTests: [{ testCode: "TEST-UV-LIM", displayName: "UV Inorganic Phosphates" }],
      specifications: []
    };
    const uvDefs: Record<string, TestDefinitionSummary> = {
      "TEST-UV-LIM": {
        id: 10,
        code: "TEST-UV-LIM",
        displayName: "UV Inorganic Phosphates",
        workflowType: "UvSpectrophotometry",
        uvCalculationMode: "AbsorbanceLimit"
      }
    };

    const { user } = renderWizard({
      item: mockUvItem,
      workflowTypeByCode: { "TEST-UV-LIM": "UvSpectrophotometry" },
      testDefinitionByCode: uvDefs
    });

    await user.click(await screen.findByRole("button", { name: "Next" }));
    await user.type(await screen.findByLabelText(/Expected Result Text \*/i), "Conforms");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(await screen.findByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(SpecificationService.create).toHaveBeenCalledWith(
      expect.objectContaining({
        testCode: "TEST-UV-LIM",
        limitType: "Qualitative",
        resultBasis: null
      })
    );
  });

  it("first open of a UV SpecificAbsorbanceAssay test sends resultBasis PercentAsIs", async () => {
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 2002, testCode: "TEST-UV-ASSAY" });
    const mockUvItem: Item = {
      id: 301,
      code: "RM-UV2",
      name: "UV Test Item 2",
      category: "RawMaterial",
      sopNumber: "SOP-UV2",
      isActive: true,
      assignedTests: [{ testCode: "TEST-UV-ASSAY", displayName: "UV Riboflavin Assay" }],
      specifications: []
    };
    const uvDefs: Record<string, TestDefinitionSummary> = {
      "TEST-UV-ASSAY": {
        id: 11,
        code: "TEST-UV-ASSAY",
        displayName: "UV Riboflavin Assay",
        workflowType: "UvSpectrophotometry",
        uvCalculationMode: "SpecificAbsorbanceAssay"
      }
    };

    const { user } = renderWizard({
      item: mockUvItem,
      workflowTypeByCode: { "TEST-UV-ASSAY": "UvSpectrophotometry" },
      testDefinitionByCode: uvDefs
    });

    // Test step -> Method step -> Limits step -> Unit step -> Review
    await user.click(await screen.findByRole("button", { name: "Next" }));
    await user.click(await screen.findByRole("button", { name: "Next" }));

    await user.type(await screen.findByLabelText(/Lower Limit \(NLT\) \*/i), "98.0");
    await user.type(screen.getByLabelText(/Upper Limit \(NMT\) \*/i), "102.0");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(await screen.findByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(SpecificationService.create).toHaveBeenCalledWith(
      expect.objectContaining({
        testCode: "TEST-UV-ASSAY",
        limitType: "Range",
        resultBasis: "PercentAsIs"
      })
    );
  });
});
