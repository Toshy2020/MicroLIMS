import { HplcAnalytesSection } from "../../masterDataSimple/hplcMethod/HplcAnalytesSection";
import { HplcGeneralSection } from "../../masterDataSimple/hplcMethod/HplcGeneralSection";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { SpecificationParameterDialog } from "./SpecificationParameterDialog";
import { SpecificationService } from "../../specifications/services/SpecificationService";
import { HplcMethodService } from "../../masterDataSimple/services/HplcMethodService";
import type { Item } from "../services/ItemService";
import type { HplcMethodResponse } from "../../masterDataSimple/services/HplcMethodService";
import type { TestDefinitionSummary } from "./SpecificationParameterDialog";

// Stable references: the dialog memoizes on these, so a mock returning a fresh
// array per call re-fires its form-reset effect every render (endless loop).
const { myLabs, labSections } = vi.hoisted(() => ({
  myLabs: { codes: ["QC"], labs: [{ sectionId: 1, sectionCode: "QC" }] },
  labSections: { sections: [{ sectionId: 1, sectionCode: "QC", sectionName: "Quality Control" }] }
}));

vi.mock("../../../../hooks/useMyLabs", () => ({ useMyLabs: () => myLabs }));

vi.mock("../../../../hooks/useLaboratorySections", () => ({ useLaboratorySections: () => labSections }));

vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: {
      create: vi.fn(),
      update: vi.fn(),
      getForItem: vi.fn().mockResolvedValue([])
    }
  };
});

vi.mock("../../masterDataSimple/services/HplcMethodService", () => ({
  HplcMethodService: {
    getById: vi.fn()
  }
}));

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

describe("SpecificationParameterDialog - Related Substances & Raw Material Assay (Task 6)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("shows Row select, fixed NMT limit type, % unit, and % as is basis for an RS test", async () => {
    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockRsMethod);

    render(
      <PinnedLightTheme>
        <SpecificationParameterDialog
          open={true}
          item={mockItemRawMaterial}
          preselectedTestCode="TEST-RS"
          workflowTypeByCode={{ "TEST-RS": "HplcMethodAssay" }}
          testDefinitionByCode={mockTestDefs}
          existingSpecs={[]}
          onClose={vi.fn()}
          onSuccess={vi.fn()}
        />
      </PinnedLightTheme>
    );

    // Wait for method to load
    await waitFor(() => {
      expect(HplcMethodService.getById).toHaveBeenCalledWith(10);
    });

    // Row select is rendered
    expect(await screen.findByLabelText(/Row \*/i)).toBeTruthy();

    // Limit type is fixed to NMT (read-only)
    expect(screen.getByDisplayValue("NMT (Not More Than)")).toBeTruthy();

    // Basis is fixed to % as is (read-only)
    expect(screen.getByDisplayValue("% as is")).toBeTruthy();

    // Unit is fixed to %
    expect(screen.getByDisplayValue("%")).toBeTruthy();

    // Impurity picker is NOT shown before selecting Named impurity
    expect(screen.queryByLabelText(/Impurity \*/i)).toBeNull();
  });

  it("shows impurity picker only for Named impurity and submits payload with relatedSubstanceRow and hplcMethodImpurityId", async () => {
    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockRsMethod);
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 999, testCode: "TEST-RS" });

    const onSuccess = vi.fn();
    render(
      <PinnedLightTheme>
        <SpecificationParameterDialog
          open={true}
          item={mockItemRawMaterial}
          preselectedTestCode="TEST-RS"
          workflowTypeByCode={{ "TEST-RS": "HplcMethodAssay" }}
          testDefinitionByCode={mockTestDefs}
          existingSpecs={[]}
          onClose={vi.fn()}
          onSuccess={onSuccess}
        />
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(HplcMethodService.getById).toHaveBeenCalledWith(10);
    });

    const rowSelect = await screen.findByLabelText(/Row \*/i);

    // Pick "Any unspecified impurity" first
    fireEvent.mouseDown(rowSelect);
    const unspecOption = await screen.findByRole("option", { name: /Any unspecified impurity/i });
    fireEvent.click(unspecOption);

    // Impurity picker should still NOT appear
    expect(screen.queryByLabelText(/Impurity \*/i)).toBeNull();

    // Now switch to "Named impurity"
    fireEvent.mouseDown(rowSelect);
    const namedOption = await screen.findByRole("option", { name: /Named impurity/i });
    fireEvent.click(namedOption);

    // Impurity picker should now appear
    const impuritySelect = await screen.findByLabelText(/Impurity \*/i);
    expect(impuritySelect).toBeTruthy();

    // Choose "Impurity D"
    fireEvent.mouseDown(impuritySelect);
    const impDOption = await screen.findByRole("option", { name: /Impurity D/i });
    fireEvent.click(impDOption);

    // Fill upper limit: 0.15
    const limitInput = screen.getByLabelText(/NMT \(%\) \*/i);
    fireEvent.change(limitInput, { target: { value: "0.15" } });

    // Click Add Parameter
    const saveButton = screen.getByRole("button", { name: /Add Parameter/i });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(SpecificationService.create).toHaveBeenCalledTimes(1);
    });

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

    render(
      <PinnedLightTheme>
        <SpecificationParameterDialog
          open={true}
          item={mockItemRawMaterial}
          preselectedTestCode="TEST-ASSAY"
          workflowTypeByCode={{ "TEST-ASSAY": "HplcMethodAssay" }}
          testDefinitionByCode={mockTestDefs}
          existingSpecs={[]}
          onClose={vi.fn()}
          onSuccess={vi.fn()}
        />
      </PinnedLightTheme>
    );

    await waitFor(() => {
      expect(HplcMethodService.getById).toHaveBeenCalledWith(20);
    });

    // Select Analyte
    const analyteSelect = await screen.findByLabelText(/Analyte \*/i);
    fireEvent.mouseDown(analyteSelect);
    const analyteOption = await screen.findByRole("option", { name: /Sildenafil Citrate/i });
    fireEvent.click(analyteOption);

    // Open Basis Select
    const basisSelect = screen.getByLabelText(/Basis \*/i);
    fireEvent.mouseDown(basisSelect);

    // Verify raw material options exist
    expect(await screen.findByRole("option", { name: /Assay %/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /Amount per unit/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /% as is/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /% dried basis/i })).toBeTruthy();
    expect(await screen.findByRole("option", { name: /% anhydrous basis/i })).toBeTruthy();

    // Select "% dried basis"
    const driedOption = screen.getByRole("option", { name: /% dried basis/i });
    fireEvent.click(driedOption);

    // Set lower & upper limits (Range)
    const lowerInput = screen.getByLabelText(/Lower Limit \(NLT\) \*/i);
    fireEvent.change(lowerInput, { target: { value: "98.0" } });
    const upperInput = screen.getByLabelText(/Upper Limit \(NMT\) \*/i);
    fireEvent.change(upperInput, { target: { value: "102.0" } });

    // Click Add Parameter
    const saveButton = screen.getByRole("button", { name: /Add Parameter/i });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(SpecificationService.create).toHaveBeenCalledTimes(1);
    });

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
        availableStandards={[{ id: 1, code: "RS-01", name: "Std 1", isActive: true } as any]}
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
        mySections={[{ sectionId: 1, sectionName: "QC Section" } as any]}
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
      assignedTests: [
        { testCode: "TEST-ASSAY", displayName: "HPLC Assay" }
      ],
      specifications: []
    };

    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockAssayMethod);

    render(
      <SpecificationParameterDialog
        open={true}
        item={mockFpItem}
        existingSpecs={[]}
        workflowTypeByCode={{ "TEST-ASSAY": "HplcMethodAssay" }}
        testDefinitionByCode={mockTestDefs}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await screen.findByDisplayValue("TEST-ASSAY");
    await waitFor(() => {
      expect(HplcMethodService.getById).toHaveBeenCalledWith(20);
    });

    const analyteSelect = await screen.findByLabelText(/Analyte \*/i);
    fireEvent.mouseDown(analyteSelect);
    fireEvent.click(await screen.findByRole("option", { name: /Sildenafil Citrate/i }));

    const basisSelect = await screen.findByLabelText(/Basis \*/i);
    fireEvent.mouseDown(basisSelect);

    expect(await screen.findByRole("option", { name: /Assay %/i })).toBeTruthy();
    expect(screen.getByRole("option", { name: /Amount per unit/i })).toBeTruthy();

    expect(screen.queryByRole("option", { name: /% as is/i })).toBeNull();
    expect(screen.queryByRole("option", { name: /% dried basis/i })).toBeNull();
    expect(screen.queryByRole("option", { name: /% anhydrous basis/i })).toBeNull();
  });

  it("submits a Total impurities row with hplcMethodImpurityId null", async () => {
    vi.mocked(HplcMethodService.getById).mockResolvedValue(mockRsMethod);
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 1001, testCode: "TEST-RS" });

    render(
      <SpecificationParameterDialog
        open={true}
        item={mockItemRawMaterial}
        existingSpecs={[]}
        workflowTypeByCode={{ "TEST-RS": "HplcMethodAssay" }}
        testDefinitionByCode={mockTestDefs}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    await screen.findByDisplayValue("TEST-RS");
    const rowSelect = await screen.findByLabelText(/Row \*/i);

    // Pick "Total impurities"
    fireEvent.mouseDown(rowSelect);
    const totalOption = await screen.findByRole("option", { name: /Total impurities/i });
    fireEvent.click(totalOption);

    // Impurity picker should NOT appear
    expect(screen.queryByLabelText(/Impurity \*/i)).toBeNull();

    // Fill upper limit: 1.00
    const limitInput = screen.getByLabelText(/NMT \(%\) \*/i);
    fireEvent.change(limitInput, { target: { value: "1.00" } });

    // Click Add Parameter
    const saveButton = screen.getByRole("button", { name: /Add Parameter/i });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(SpecificationService.create).toHaveBeenCalledTimes(1);
    });

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
});
