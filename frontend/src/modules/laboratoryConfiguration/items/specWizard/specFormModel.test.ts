import { describe, expect, it } from "vitest";
import {
  createEmptySpecFormModel,
  fromSpecification,
  toPayload,
  validateStep,
  type SpecFormModel
} from "./specFormModel";
import { SPEC_PAYLOAD_FIXTURES } from "./__fixtures__/specPayloads";
import type { SpecificationDto } from "../../specifications/services/SpecificationService";

const fixtureCases: Array<{ name: keyof typeof SPEC_PAYLOAD_FIXTURES; model: SpecFormModel }> = [
  {
    name: "range",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Measurement",
      testCode: "PH",
      parameterName: "pH",
      limitType: "Range",
      lowerLimit: "5",
      upperLimit: "7",
      lowerInclusive: true,
      upperInclusive: false,
      unit: "pH units",
      referenceStandard: "USP <791>",
      productionStageRole: "Finished"
    })
  },
  {
    name: "notMoreThan",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Measurement",
      testCode: "LOD",
      parameterName: "Loss on Drying",
      limitType: "NotMoreThan",
      upperLimit: "0.5",
      unit: "%",
      referenceStandard: "USP <731>"
    })
  },
  {
    name: "notLessThan",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Measurement",
      testCode: "POT",
      parameterName: "Potency",
      limitType: "NotLessThan",
      lowerLimit: "95",
      unit: "%"
    })
  },
  {
    name: "targetWithTolerance",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Measurement",
      testCode: "DENS",
      parameterName: "Density",
      limitType: "TargetWithTolerance",
      target: "1.02",
      tolerance: "5",
      toleranceMode: "Percent",
      unit: "g/mL"
    })
  },
  {
    name: "countTiered",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "CountTest",
      testCode: "TAMC",
      parameterName: "Total Aerobic Microbial Count",
      limitType: "CountTiered",
      alertLimit: "100",
      actionLimit: "500",
      specLimit: "1000",
      dilutionFactor: "10",
      unit: "CFU/g",
      referenceStandard: "USP <61>"
    })
  },
  {
    name: "qualitative",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Measurement",
      testCode: "APP",
      parameterName: "Appearance",
      limitType: "Qualitative",
      expectedResultText: "White round tablets"
    })
  },
  {
    name: "presenceAbsence",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Observation",
      testCode: "ECOLI",
      parameterName: "E. coli",
      limitType: "PresenceAbsence",
      expectedState: "Absence",
      sampleQuantity: "10",
      sampleQuantityUnit: "g",
      referenceStandard: "USP <62>"
    })
  },
  {
    name: "multiStage",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Measurement",
      testCode: "UNIF",
      parameterName: "Content Uniformity",
      limitType: "MultiStage",
      unit: "%",
      stages: [
        {
          stageNumber: 1,
          stageLabel: "Stage 1 (S1, n=6)",
          acceptanceCriteriaText: "Each unit >= 85%"
        },
        {
          stageNumber: 2,
          stageLabel: "Stage 2 (S1+S2, n=12)",
          acceptanceCriteriaText: "Average >= 75%"
        }
      ]
    })
  },
  {
    name: "dissolutionQ",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Dissolution",
      testCode: "DISS",
      parameterName: "Dissolution",
      limitType: "DissolutionQ",
      lowerLimit: "75",
      labelClaim: "500",
      unit: "%",
      referenceStandard: "USP <711>"
    })
  },
  {
    name: "disintegrationTime",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Disintegration",
      testCode: "DISINT",
      parameterName: "Disintegration",
      limitType: "DisintegrationTime",
      upperLimit: "15",
      unit: "min",
      referenceStandard: "USP <701>"
    })
  },
  {
    name: "weightVariation",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "WeightVariation",
      testCode: "WV",
      parameterName: "Weight Variation",
      limitType: "WeightVariation",
      dosageForm: "Tablet",
      unit: "mg",
      referenceStandard: "USP <2091>"
    })
  },
  {
    name: "hplcAssay",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "HplcMethodAssay",
      equationType: "HplcMethodAssay",
      hplcMethodResultMode: "Assay",
      testCode: "HPLC_A",
      parameterName: "Ibuprofen (amount per unit)",
      hplcMethodAnalyteId: 601,
      resultBasis: "MgPerUnit",
      labelClaim: "400",
      labelClaimUnit: "mg",
      limitType: "Range",
      lowerLimit: "380",
      upperLimit: "420",
      unit: "mg"
    })
  },
  {
    name: "hplcRelatedSubstances",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "HplcMethodAssay",
      equationType: "HplcMethodAssay",
      hplcMethodResultMode: "RelatedSubstances",
      defaultHplcAnalyteId: 501,
      testCode: "HPLC_RS",
      parameterName: "Impurity A",
      hplcMethodAnalyteId: 501,
      relatedSubstanceRow: "Named",
      hplcMethodImpurityId: 101,
      resultBasis: "PercentAsIs",
      limitType: "NotMoreThan",
      upperLimit: "0.15",
      unit: "%"
    })
  },
  {
    name: "hplcResidualSolvents",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "HplcMethodAssay",
      equationType: "HplcMethodAssay",
      hplcMethodResultMode: "ResidualSolvents",
      testCode: "GC_RS",
      parameterName: "Methanol",
      hplcMethodAnalyteId: 701,
      resultBasis: "Ppm",
      limitType: "NotMoreThan",
      upperLimit: "3000",
      unit: "ppm"
    })
  },
  {
    name: "icpAssay",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "IcpMethodAssay",
      equationType: "IcpMethodAssay",
      icpMode: "Assay",
      testCode: "ICP_A",
      parameterName: "Fe",
      icpTestElementId: 11,
      resultBasis: "",
      limitType: "Range",
      lowerLimit: "90",
      upperLimit: "110",
      unit: "%"
    })
  },
  {
    name: "icpImpurity",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "IcpMethodAssay",
      equationType: "IcpMethodAssay",
      icpMode: "Impurity",
      testCode: "ICP_IMP",
      parameterName: "Pb",
      icpTestElementId: 21,
      resultBasis: "",
      limitType: "NotMoreThan",
      upperLimit: "0.5",
      unit: "ppm"
    })
  },
  {
    name: "titration",
    model: createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Titration",
      testCode: "TITR",
      parameterName: "Titration Assay",
      resultBasis: "PercentLabelClaim",
      labelClaim: "500",
      limitType: "Range",
      lowerLimit: "98",
      upperLimit: "102",
      unit: "%"
    })
  },
  {
    name: "rangeEdit",
    model: {
      ...fromSpecification(
        {
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
        } satisfies SpecificationDto,
        { itemId: 7, itemCategory: "FinishedProduct", workflowType: "Measurement" }
      ),
      upperLimit: "7.5"
    }
  }
];

describe("specFormModel", () => {
  it.each(fixtureCases)("toPayload($name) deep-equals captured fixture", ({ name, model }) => {
    expect(toPayload(model)).toStrictEqual(SPEC_PAYLOAD_FIXTURES[name]);
  });

  it("switching limit type then back sends no stale fields (Review Focus 1)", () => {
    // User starts with Range
    const initialRange = createEmptySpecFormModel({
      itemId: 7,
      itemCategory: "FinishedProduct",
      workflowType: "Measurement",
      testCode: "PH",
      parameterName: "pH",
      limitType: "Range",
      lowerLimit: "5",
      upperLimit: "7",
      lowerInclusive: true,
      upperInclusive: false,
      unit: "pH units",
      referenceStandard: "USP <791>",
      productionStageRole: "Finished"
    });

    // Switches to CountTiered and fills CountTiered fields
    const asCountTiered: SpecFormModel = {
      ...initialRange,
      limitType: "CountTiered",
      alertLimit: "100",
      actionLimit: "500",
      specLimit: "1000",
      dilutionFactor: "10"
    };
    const countPayload = toPayload(asCountTiered);
    expect(countPayload.limitType).toBe("CountTiered");
    expect(countPayload.lowerLimit).toBeNull();
    expect(countPayload.upperLimit).toBeNull();
    expect(countPayload.lowerInclusive).toBeUndefined();
    expect(countPayload.upperInclusive).toBeUndefined();

    // Switches to PresenceAbsence and fills PresenceAbsence fields
    const asPresence: SpecFormModel = {
      ...asCountTiered,
      limitType: "PresenceAbsence",
      expectedState: "Presence",
      sampleQuantity: "25",
      sampleQuantityUnit: "g"
    };
    const presencePayload = toPayload(asPresence);
    expect(presencePayload.alertLimit).toBeNull();
    expect(presencePayload.actionLimit).toBeNull();
    expect(presencePayload.specLimit).toBeNull();
    expect(presencePayload.dilutionFactor).toBeNull();

    // Switches back to Range while stale fields from previous limit types remain on model
    const backToRange: SpecFormModel = {
      ...asPresence,
      limitType: "Range"
    };
    expect(toPayload(backToRange)).toStrictEqual(SPEC_PAYLOAD_FIXTURES.range);

    // Switching from DissolutionQ / WeightVariation to Range also drops labelClaim, dosageForm, specLimit
    const fromDissolutionToRange: SpecFormModel = {
      ...createEmptySpecFormModel({
        itemId: 7,
        itemCategory: "FinishedProduct",
        workflowType: "Measurement",
        testCode: "PH",
        parameterName: "pH",
        limitType: "DissolutionQ",
        lowerLimit: "5",
        upperLimit: "7",
        labelClaim: "500",
        dosageForm: "Tablet",
        specLimit: "Q = 75 %",
        expectedResultText: "Clear",
        target: "6",
        tolerance: "1",
        stages: [{ stageNumber: 1, stageLabel: "S1", acceptanceCriteriaText: "Pass" }]
      }),
      limitType: "Range"
    };
    const cleanRange = toPayload(fromDissolutionToRange);
    expect(cleanRange.labelClaim).toBeNull();
    expect(cleanRange.labelClaimUnit).toBeNull();
    expect(cleanRange.dosageForm).toBeNull();
    expect(cleanRange.specLimit).toBeNull();
    expect(cleanRange.expectedResultText).toBeNull();
    expect(cleanRange.target).toBeNull();
    expect(cleanRange.tolerance).toBeNull();
    expect(cleanRange.toleranceMode).toBeNull();
    expect(cleanRange.stages).toBeUndefined();
  });

  describe("validateStep returns the dialog messages for each missing/invalid field", () => {
    it("validates test step", () => {
      const model = createEmptySpecFormModel({ testCode: "" });
      expect(validateStep("test", model)).toEqual({
        testCode: "Assigned Test is required."
      });
      expect(validateStep("test", { ...model, testCode: "PH" })).toEqual({});
    });

    it("validates method step for HPLC Assay (FP & RM)", () => {
      const fpBase = createEmptySpecFormModel({
        workflowType: "HplcMethodAssay",
        equationType: "HplcMethodAssay",
        hplcMethodResultMode: "Assay",
        itemCategory: "FinishedProduct",
        testCode: "HPLC_A"
      });
      expect(validateStep("method", { ...fpBase, hplcMethodAnalyteId: "", resultBasis: "PercentLabelClaim" })).toEqual({
        hplcMethodAnalyteId: "Please select an analyte from the HPLC method."
      });
      expect(validateStep("method", { ...fpBase, hplcMethodAnalyteId: 601, resultBasis: "PercentAsIs" })).toEqual({
        resultBasis: "Please select a basis (Assay % or Amount per unit)."
      });
      expect(
        validateStep("method", {
          ...fpBase,
          itemCategory: "RawMaterial",
          hplcMethodAnalyteId: 601,
          resultBasis: ""
        })
      ).toEqual({
        resultBasis:
          "Please select a basis (Assay %, Amount per unit, % as is, % dried basis, or % anhydrous basis)."
      });
      expect(
        validateStep("method", {
          ...fpBase,
          hplcMethodAnalyteId: 601,
          resultBasis: "MgPerUnit",
          labelClaim: "0",
          labelClaimUnit: ""
        })
      ).toEqual({
        labelClaim: "Label claim must be greater than 0 for Amount per unit.",
        labelClaimUnit: "Label claim unit is required for Amount per unit."
      });
    });

    it("validates method step for HPLC Related Substances and GC Residual Solvents", () => {
      const rsBase = createEmptySpecFormModel({
        workflowType: "HplcMethodAssay",
        equationType: "HplcMethodAssay",
        hplcMethodResultMode: "RelatedSubstances",
        testCode: "HPLC_RS"
      });
      expect(validateStep("method", { ...rsBase, relatedSubstanceRow: "" })).toEqual({
        relatedSubstanceRow:
          "Choose the related-substance row: named impurity, any unspecified impurity, total, or total of others."
      });
      expect(
        validateStep("method", { ...rsBase, relatedSubstanceRow: "Named", hplcMethodImpurityId: "" })
      ).toEqual({
        hplcMethodImpurityId: "Choose the impurity for this row."
      });

      const gcBase = createEmptySpecFormModel({
        workflowType: "HplcMethodAssay",
        equationType: "HplcMethodAssay",
        hplcMethodResultMode: "ResidualSolvents",
        testCode: "GC_RS"
      });
      expect(validateStep("method", { ...gcBase, hplcMethodAnalyteId: "", resultBasis: "PercentAsIs" })).toEqual({
        hplcMethodAnalyteId: "Please select a solvent from the GC method.",
        resultBasis: "Basis must be ppm for residual solvents."
      });
    });

    it("validates method step for ICP and Titration", () => {
      const icpBase = createEmptySpecFormModel({
        workflowType: "IcpMethodAssay",
        equationType: "IcpMethodAssay",
        icpMode: "Assay",
        testCode: "ICP_A",
        icpTestElementId: ""
      });
      expect(validateStep("method", icpBase)).toEqual({
        icpTestElementId: "Please select an element of the test."
      });

      const titrBase = createEmptySpecFormModel({
        workflowType: "Titration",
        testCode: "TITR",
        resultBasis: ""
      });
      expect(validateStep("method", titrBase)).toEqual({
        resultBasis: "Please select a result basis."
      });
      expect(
        validateStep("method", { ...titrBase, resultBasis: "PercentLabelClaim", labelClaim: "0" })
      ).toEqual({
        labelClaim: "Label claim (mg per unit) must be greater than 0 for this basis."
      });
    });

    it("validates limit step for DissolutionQ, DisintegrationTime, and WeightVariation", () => {
      const dissBase = createEmptySpecFormModel({
        testCode: "DISS",
        limitType: "DissolutionQ",
        lowerLimit: "0",
        labelClaim: ""
      });
      expect(validateStep("limit", dissBase)).toEqual({
        lowerLimit: "Lower limit Q (%) must be between 0 and 100 (exclusive of 0, inclusive of 100).",
        labelClaim: "Label claim must be greater than zero for Dissolution Q specifications."
      });
      expect(
        validateStep("limit", {
          ...dissBase,
          lowerLimit: "75",
          labelClaim: "500",
          existingSpecs: [{ id: 9, testCode: "DISS", limitType: "DissolutionQ", displayOrder: 0 }]
        })
      ).toEqual({
        limitType: "Only one DissolutionQ specification is allowed per test."
      });
      // Editing the existing row itself is allowed
      expect(
        validateStep("limit", {
          ...dissBase,
          editingId: 9,
          lowerLimit: "75",
          labelClaim: "500",
          existingSpecs: [{ id: 9, testCode: "DISS", limitType: "DissolutionQ", displayOrder: 0 }]
        })
      ).toEqual({});

      const disintBase = createEmptySpecFormModel({
        testCode: "DISINT",
        limitType: "DisintegrationTime",
        upperLimit: "0"
      });
      expect(validateStep("limit", disintBase)).toEqual({
        upperLimit: "Time limit (min) must be greater than zero for Disintegration Time specifications."
      });
      expect(
        validateStep("limit", {
          ...disintBase,
          upperLimit: "15",
          existingSpecs: [{ id: 10, testCode: "DISINT", limitType: "DisintegrationTime", displayOrder: 0 }]
        })
      ).toEqual({
        limitType: "Only one DisintegrationTime specification is allowed per test."
      });

      const wvBase = createEmptySpecFormModel({
        testCode: "WV",
        limitType: "WeightVariation",
        dosageForm: ""
      });
      expect(validateStep("limit", wvBase)).toEqual({
        dosageForm: "Dosage form is required for Weight Variation specifications."
      });
      expect(
        validateStep("limit", {
          ...wvBase,
          dosageForm: "Tablet",
          existingSpecs: [{ id: 11, testCode: "WV", limitType: "WeightVariation", displayOrder: 0 }]
        })
      ).toEqual({
        limitType: "Only one WeightVariation specification is allowed per test."
      });
    });

    it("returns no errors on unit step", () => {
      expect(validateStep("unit", createEmptySpecFormModel({ testCode: "PH" }))).toEqual({});
    });
  });
});
