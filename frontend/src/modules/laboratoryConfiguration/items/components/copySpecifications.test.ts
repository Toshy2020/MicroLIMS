import { describe, expect, it } from "vitest";
import type { CreateSpecificationPayload, SpecificationDto } from "../../specifications/services/SpecificationService";
import {
  assignDisplayOrders,
  isCannotCopy,
  ownLabTestCodes,
  selectCopyCandidates,
  specQualifiers,
  toCreatePayload
} from "./copySpecifications";

const SOURCE_ITEM_ID = 5;
const TARGET_ITEM_ID = 99;

// Server-owned fields every stored row carries; none may reach the create request.
const stored = (overrides: Partial<SpecificationDto> & Pick<SpecificationDto, "testCode">): SpecificationDto => ({
  id: 4711,
  version: 3,
  itemId: SOURCE_ITEM_ID,
  canEdit: true,
  sectionName: "Finished Product",
  displayOrder: 7,
  ...overrides
});

// Every field of the create request, empty. A test spreads the fields it expects
// on top, so a field that is dropped or invented makes toEqual fail.
const EMPTY = {
  parameterName: null,
  referenceStandard: null,
  lowerLimit: null,
  upperLimit: null,
  target: null,
  tolerance: null,
  toleranceMode: null,
  expectedResultText: null,
  expectedState: null,
  sampleQuantity: null,
  sampleQuantityUnit: null,
  alertLimit: null,
  actionLimit: null,
  specLimit: null,
  unit: null,
  dilutionFactor: null,
  resultBasis: null,
  labelClaim: null,
  labelClaimUnit: null,
  dosageForm: null,
  hplcMethodAnalyteId: null,
  hplcMethodImpurityId: null,
  relatedSubstanceRow: null,
  icpTestElementId: null,
  productionStageRole: null,
  grade: null
};

const expected = (
  overrides: Partial<CreateSpecificationPayload> & Pick<CreateSpecificationPayload, "testCode" | "limitType">
): CreateSpecificationPayload => ({
  ...EMPTY,
  itemId: TARGET_ITEM_ID,
  displayOrder: 2,
  ...overrides
});

const map = (spec: SpecificationDto) => toCreatePayload(spec, TARGET_ITEM_ID, 2);

describe("toCreatePayload", () => {
  it("copies a Range row including the inclusivity flags, converting text numbers", () => {
    const result = map(
      stored({
        testCode: "PH",
        parameterName: "pH",
        limitType: "Range",
        lowerLimit: "5.50",
        upperLimit: 7.5,
        lowerInclusive: true,
        upperInclusive: false,
        unit: "pH",
        referenceStandard: "BP 2024",
        resultBasis: "PercentAsIs"
      })
    );
    expect(result).toEqual(
      expected({
        testCode: "PH",
        limitType: "Range",
        parameterName: "pH",
        lowerLimit: 5.5,
        upperLimit: 7.5,
        lowerInclusive: true,
        upperInclusive: false,
        unit: "pH",
        referenceStandard: "BP 2024",
        resultBasis: "PercentAsIs"
      })
    );
  });

  it("never carries identity, version or ownership fields", () => {
    const result = map(stored({ testCode: "PH", limitType: "Range", lowerLimit: 1, upperLimit: 2, lowerInclusive: true, upperInclusive: true }));
    expect(isCannotCopy(result)).toBe(false);
    const keys = Object.keys(result);
    for (const forbidden of ["id", "version", "canEdit", "sectionName"]) expect(keys).not.toContain(forbidden);
    expect((result as CreateSpecificationPayload).itemId).toBe(TARGET_ITEM_ID);
    expect((result as CreateSpecificationPayload).displayOrder).toBe(2);
  });

  it("copies a NotMoreThan row", () => {
    expect(map(stored({ testCode: "RES", limitType: "NotMoreThan", upperLimit: 0.2, unit: "%", parameterName: "Residue" }))).toEqual(
      expected({ testCode: "RES", limitType: "NotMoreThan", upperLimit: 0.2, unit: "%", parameterName: "Residue" })
    );
  });

  it("copies a NotLessThan row", () => {
    expect(map(stored({ testCode: "ASSAY", limitType: "NotLessThan", lowerLimit: 98, unit: "%" }))).toEqual(
      expected({ testCode: "ASSAY", limitType: "NotLessThan", lowerLimit: 98, unit: "%" })
    );
  });

  it("copies a TargetWithTolerance row", () => {
    expect(
      map(stored({ testCode: "FILL", limitType: "TargetWithTolerance", target: "100", tolerance: 5, toleranceMode: "Percent", unit: "mL" }))
    ).toEqual(
      expected({ testCode: "FILL", limitType: "TargetWithTolerance", target: 100, tolerance: 5, toleranceMode: "Percent", unit: "mL" })
    );
  });

  it("copies a CountTiered row with its dilution factor", () => {
    expect(
      map(
        stored({
          testCode: "TAMC",
          limitType: "CountTiered",
          alertLimit: "50",
          actionLimit: "80",
          specLimit: "100",
          dilutionFactor: 10,
          unit: "CFU/g"
        })
      )
    ).toEqual(
      expected({
        testCode: "TAMC",
        limitType: "CountTiered",
        alertLimit: "50",
        actionLimit: "80",
        specLimit: "100",
        dilutionFactor: 10,
        unit: "CFU/g"
      })
    );
  });

  it("copies a Qualitative row", () => {
    expect(map(stored({ testCode: "APP", limitType: "Qualitative", expectedResultText: "White crystalline powder" }))).toEqual(
      expected({ testCode: "APP", limitType: "Qualitative", expectedResultText: "White crystalline powder" })
    );
  });

  it("copies a PresenceAbsence row", () => {
    expect(
      map(
        stored({
          testCode: "SALM",
          limitType: "PresenceAbsence",
          expectedState: "Absence",
          sampleQuantity: "25",
          sampleQuantityUnit: "g"
        })
      )
    ).toEqual(
      expected({
        testCode: "SALM",
        limitType: "PresenceAbsence",
        expectedState: "Absence",
        sampleQuantity: 25,
        sampleQuantityUnit: "g"
      })
    );
  });

  it("copies a MultiStage row with its stages and without the stage ids", () => {
    expect(
      map(
        stored({
          testCode: "UDU",
          limitType: "MultiStage",
          stages: [
            { id: 1, stageNumber: 1, stageLabel: "Stage 1 (S1, n=6)", acceptanceCriteriaText: "Each unit 85-115 %" },
            { id: 2, stageNumber: 2, stageLabel: "Stage 2 (S1+S2, n=12)", acceptanceCriteriaText: "Mean 90-110 %" }
          ]
        })
      )
    ).toEqual(
      expected({
        testCode: "UDU",
        limitType: "MultiStage",
        stages: [
          { stageNumber: 1, stageLabel: "Stage 1 (S1, n=6)", acceptanceCriteriaText: "Each unit 85-115 %" },
          { stageNumber: 2, stageLabel: "Stage 2 (S1+S2, n=12)", acceptanceCriteriaText: "Mean 90-110 %" }
        ]
      })
    );
  });

  it("copies a DissolutionQ row with its label claim", () => {
    expect(
      map(
        stored({
          testCode: "DISS",
          limitType: "DissolutionQ",
          lowerLimit: 80,
          labelClaim: 500,
          labelClaimUnit: "mg",
          specLimit: "Q = 80 %"
        })
      )
    ).toEqual(
      expected({
        testCode: "DISS",
        limitType: "DissolutionQ",
        lowerLimit: 80,
        labelClaim: 500,
        labelClaimUnit: "mg",
        specLimit: "Q = 80 %"
      })
    );
  });

  it("copies a DisintegrationTime row", () => {
    expect(map(stored({ testCode: "DISI", limitType: "DisintegrationTime", upperLimit: 15, unit: "min", specLimit: "NMT 15 min" }))).toEqual(
      expected({ testCode: "DISI", limitType: "DisintegrationTime", upperLimit: 15, unit: "min", specLimit: "NMT 15 min" })
    );
  });

  it("copies a WeightVariation row with its dosage form", () => {
    expect(
      map(
        stored({
          testCode: "WV",
          limitType: "WeightVariation",
          dosageForm: "Tablet",
          unit: "mg",
          specLimit: "USP <2091>: tablets, limit by average weight"
        })
      )
    ).toEqual(
      expected({
        testCode: "WV",
        limitType: "WeightVariation",
        dosageForm: "Tablet",
        unit: "mg",
        specLimit: "USP <2091>: tablets, limit by average weight"
      })
    );
  });

  it("copies an HPLC analyte row with its basis and label claim", () => {
    expect(
      map(
        stored({
          testCode: "HPLC-ASSAY",
          parameterName: "Paracetamol (amount per unit)",
          limitType: "Range",
          lowerLimit: 450,
          upperLimit: 550,
          lowerInclusive: true,
          upperInclusive: true,
          unit: "mg",
          resultBasis: "MgPerUnit",
          labelClaim: 500,
          labelClaimUnit: "mg",
          hplcMethodAnalyteId: 31
        })
      )
    ).toEqual(
      expected({
        testCode: "HPLC-ASSAY",
        limitType: "Range",
        parameterName: "Paracetamol (amount per unit)",
        lowerLimit: 450,
        upperLimit: 550,
        lowerInclusive: true,
        upperInclusive: true,
        unit: "mg",
        resultBasis: "MgPerUnit",
        labelClaim: 500,
        labelClaimUnit: "mg",
        hplcMethodAnalyteId: 31
      })
    );
  });

  it("copies a named related-substance row with its analyte and impurity", () => {
    expect(
      map(
        stored({
          testCode: "RS",
          parameterName: "Impurity A",
          limitType: "NotMoreThan",
          upperLimit: 0.15,
          unit: "%",
          resultBasis: "PercentAsIs",
          hplcMethodAnalyteId: 31,
          hplcMethodImpurityId: 77,
          relatedSubstanceRow: "Named"
        })
      )
    ).toEqual(
      expected({
        testCode: "RS",
        limitType: "NotMoreThan",
        parameterName: "Impurity A",
        upperLimit: 0.15,
        unit: "%",
        resultBasis: "PercentAsIs",
        hplcMethodAnalyteId: 31,
        hplcMethodImpurityId: 77,
        relatedSubstanceRow: "Named"
      })
    );
  });

  it("copies a residual-solvent row", () => {
    expect(
      map(
        stored({
          testCode: "RSOL",
          parameterName: "Methanol",
          limitType: "NotMoreThan",
          upperLimit: 3000,
          unit: "ppm",
          resultBasis: "Ppm",
          hplcMethodAnalyteId: 12
        })
      )
    ).toEqual(
      expected({
        testCode: "RSOL",
        limitType: "NotMoreThan",
        parameterName: "Methanol",
        upperLimit: 3000,
        unit: "ppm",
        resultBasis: "Ppm",
        hplcMethodAnalyteId: 12
      })
    );
  });

  it("copies an ICP element row", () => {
    expect(
      map(
        stored({
          testCode: "ICP-EI",
          parameterName: "Lead",
          limitType: "NotMoreThan",
          upperLimit: 5,
          unit: "ppm",
          icpTestElementId: 9
        })
      )
    ).toEqual(
      expected({
        testCode: "ICP-EI",
        limitType: "NotMoreThan",
        parameterName: "Lead",
        upperLimit: 5,
        unit: "ppm",
        icpTestElementId: 9
      })
    );
  });

  it("copies the grade and the production stage of a row", () => {
    expect(
      map(
        stored({
          testCode: "PH",
          limitType: "Range",
          lowerLimit: 5,
          upperLimit: 7,
          lowerInclusive: true,
          upperInclusive: true,
          grade: "BP",
          productionStageRole: "Bulk"
        })
      )
    ).toEqual(
      expected({
        testCode: "PH",
        limitType: "Range",
        lowerLimit: 5,
        upperLimit: 7,
        lowerInclusive: true,
        upperInclusive: true,
        grade: "BP",
        productionStageRole: "Bulk"
      })
    );
  });

  it("leaves inclusivity out when the row does not state it, rather than inventing a value", () => {
    const result = map(stored({ testCode: "PH", limitType: "Range", lowerLimit: 5, upperLimit: 7, lowerInclusive: null, upperInclusive: null }));
    expect(result).toEqual(expected({ testCode: "PH", limitType: "Range", lowerLimit: 5, upperLimit: 7 }));
    expect(Object.keys(result)).not.toContain("lowerInclusive");
    expect(Object.keys(result)).not.toContain("upperInclusive");
  });
});

describe("toCreatePayload refusals", () => {
  const refusal = (spec: SpecificationDto) => {
    const result = map(spec);
    if (!isCannotCopy(result)) throw new Error("expected the row to be refused");
    return result.cannotCopy;
  };

  it("refuses an unknown limit type", () => {
    expect(refusal(stored({ testCode: "X", limitType: "Logarithmic" }))).toContain('Limit type "Logarithmic"');
  });

  it("refuses a row without a limit type", () => {
    expect(refusal(stored({ testCode: "X", limitType: null }))).toContain("no limit type");
  });

  it("refuses a limit that is not a number", () => {
    expect(refusal(stored({ testCode: "X", limitType: "NotMoreThan", upperLimit: "about 5" }))).toContain("Upper limit");
  });

  it("refuses an unknown result basis or production stage", () => {
    expect(refusal(stored({ testCode: "X", limitType: "Qualitative", resultBasis: "PerFurlong" }))).toContain("Result basis");
    expect(refusal(stored({ testCode: "X", limitType: "Qualitative", productionStageRole: "Cooling" }))).toContain("Production stage");
  });

  it("refuses a dissolution row without a label claim", () => {
    expect(refusal(stored({ testCode: "DISS", limitType: "DissolutionQ", lowerLimit: 80 }))).toContain("label claim");
  });

  it("refuses a multi-stage row without stages", () => {
    expect(refusal(stored({ testCode: "UDU", limitType: "MultiStage", stages: [] }))).toContain("no stages");
  });

  it("refuses stages on a row that is not multi-stage", () => {
    expect(
      refusal(stored({ testCode: "X", limitType: "Range", stages: [{ stageNumber: 1, stageLabel: "S1", acceptanceCriteriaText: "x" }] }))
    ).toContain("not multi-stage");
  });

  it("refuses a weight variation row without a dosage form", () => {
    expect(refusal(stored({ testCode: "WV", limitType: "WeightVariation" }))).toContain("dosage form");
  });
});

describe("selectCopyCandidates", () => {
  const targetItem = {
    id: TARGET_ITEM_ID,
    category: "FinishedProduct",
    assignedTests: [
      { testCode: "PH", displayName: "pH" },
      { testCode: "ASSAY", displayName: "Assay" },
      { testCode: "DISS", displayName: "Dissolution" },
      { testCode: "RAW", displayName: "Other lab test" }
    ]
  };
  const sourceItem = { category: "FinishedProduct" };
  const own = new Set(["PH", "ASSAY", "DISS"]);
  const range = (id: number, testCode: string, extra: Partial<SpecificationDto> = {}) =>
    stored({ id, testCode, limitType: "Range", lowerLimit: 1, upperLimit: 2, lowerInclusive: true, upperInclusive: true, ...extra });

  it("offers an eligible row and counts each skip reason once", () => {
    const sourceSpecs = [
      range(1, "PH"),
      range(2, "NOT-ASSIGNED"),
      range(3, "RAW"),
      range(4, "ASSAY"),
      range(5, "NOT-ASSIGNED-2")
    ];
    const targetSpecs = [range(90, "ASSAY")];
    const result = selectCopyCandidates({ sourceItem, targetItem, sourceSpecs, targetSpecs, ownLabTestCodes: own });
    expect(result.categoryMismatch).toBe(false);
    expect(result.candidates.map((c) => c.spec.id)).toEqual([1]);
    expect(result.skipped).toEqual({ notAssigned: 2, otherLab: 1, alreadyHasLimits: 1 });
  });

  it("never offers a test that already has limits on the target, however many rows the source has", () => {
    const result = selectCopyCandidates({
      sourceItem,
      targetItem,
      sourceSpecs: [range(1, "PH", { parameterName: "pH A" }), range(2, "PH", { parameterName: "pH B" })],
      targetSpecs: [range(90, "PH")],
      ownLabTestCodes: own
    });
    expect(result.candidates).toEqual([]);
    expect(result.skipped.alreadyHasLimits).toBe(2);
  });

  it("offers every row of a test with several parameters, in source order", () => {
    const result = selectCopyCandidates({
      sourceItem,
      targetItem,
      sourceSpecs: [range(1, "PH", { parameterName: "A" }), range(2, "ASSAY"), range(3, "PH", { parameterName: "B" })],
      targetSpecs: [],
      ownLabTestCodes: own
    });
    expect(result.candidates.map((c) => c.spec.id)).toEqual([1, 2, 3]);
  });

  it("offers each grade of a raw-material test as its own row", () => {
    const rawItem = { id: TARGET_ITEM_ID, category: "RawMaterial", assignedTests: [{ testCode: "PH", displayName: "pH" }] };
    const result = selectCopyCandidates({
      sourceItem: { category: "RawMaterial" },
      targetItem: rawItem,
      sourceSpecs: [range(1, "PH", { grade: "BP" }), range(2, "PH", { grade: "USP" })],
      targetSpecs: [],
      ownLabTestCodes: new Set(["PH"])
    });
    expect(result.candidates.map((c) => c.spec.grade)).toEqual(["BP", "USP"]);
  });

  it("offers nothing when the categories differ", () => {
    const result = selectCopyCandidates({
      sourceItem: { category: "RawMaterial" },
      targetItem,
      sourceSpecs: [range(1, "PH")],
      targetSpecs: [],
      ownLabTestCodes: own
    });
    expect(result).toEqual({ candidates: [], skipped: { notAssigned: 0, otherLab: 0, alreadyHasLimits: 0 }, categoryMismatch: true });
  });

  it("lists a row that cannot be mapped as unselectable with its reason", () => {
    const result = selectCopyCandidates({
      sourceItem,
      targetItem,
      sourceSpecs: [range(1, "PH", { limitType: "Logarithmic" })],
      targetSpecs: [],
      ownLabTestCodes: own
    });
    expect(result.candidates).toHaveLength(1);
    expect(result.candidates[0].cannotCopy).toContain("Logarithmic");
  });
});

describe("ownLabTestCodes", () => {
  const assigned = [{ testCode: "FP-ONLY" }, { testCode: "RMPM-ONLY" }, { testCode: "BOTH" }, { testCode: "LAB2" }, { testCode: "UNKNOWN" }];
  const defs = {
    "FP-ONLY": { physchemArea: "FinishedProduct" as const, sectionId: 1 },
    "RMPM-ONLY": { physchemArea: "RawPackaging" as const, sectionId: 1 },
    BOTH: { physchemArea: "Both" as const, sectionId: 1 },
    LAB2: { physchemArea: null, sectionId: 2 }
  };

  it("applies the physicochemical area of the item's category and the caller's sections", () => {
    expect([...ownLabTestCodes(assigned, defs, new Set([1]), "FinishedProduct")].sort()).toEqual(["BOTH", "FP-ONLY", "UNKNOWN"]);
    expect([...ownLabTestCodes(assigned, defs, new Set([1]), "RawMaterial")].sort()).toEqual(["BOTH", "RMPM-ONLY", "UNKNOWN"]);
  });

  it("keeps every area-compatible test while the caller's section list is empty", () => {
    expect([...ownLabTestCodes(assigned, defs, new Set(), "FinishedProduct")].sort()).toEqual(["BOTH", "FP-ONLY", "LAB2", "UNKNOWN"]);
  });
});

describe("assignDisplayOrders", () => {
  it("numbers the rows of a test one after another from zero", () => {
    const specs = [
      stored({ id: 1, testCode: "A", limitType: "Range" }),
      stored({ id: 2, testCode: "B", limitType: "Range" }),
      stored({ id: 3, testCode: "A", limitType: "Range", parameterName: "second" })
    ];
    expect(assignDisplayOrders(specs, [])).toEqual([0, 0, 1]);
  });

  it("continues after the highest order the test already has on the target", () => {
    const specs = [stored({ id: 1, testCode: "A", limitType: "Range" })];
    expect(assignDisplayOrders(specs, [stored({ id: 9, testCode: "A", displayOrder: 4 })])).toEqual([5]);
  });

  it("gives rows that differ only by grade one shared order, as the dialog does", () => {
    const specs = [
      stored({ id: 1, testCode: "A", limitType: "Range", lowerLimit: 1, grade: "BP" }),
      stored({ id: 2, testCode: "A", limitType: "Range", lowerLimit: 1, grade: "USP" }),
      stored({ id: 3, testCode: "A", limitType: "Range", lowerLimit: 2, grade: "BP" })
    ];
    expect(assignDisplayOrders(specs, [])).toEqual([0, 0, 1]);
  });
});

describe("specQualifiers", () => {
  it("shows the label claim on every row that carries one, not only dissolution rows", () => {
    const assay = stored({ testCode: "ASSAY", limitType: "Range", lowerLimit: 95, upperLimit: 105, resultBasis: "PercentLabelClaim", labelClaim: 500, labelClaimUnit: "mg" });
    expect(specQualifiers(assay)).toEqual(["Basis: % label claim", "Label Claim 500 mg"]);
  });

  it("does not invent a unit for a label claim stored without one", () => {
    const assay = stored({ testCode: "ASSAY", limitType: "Range", lowerLimit: 95, upperLimit: 105, labelClaim: 250 });
    expect(specQualifiers(assay)).toEqual(["Label Claim 250"]);
  });

  it("shows no label claim on a row without one", () => {
    expect(specQualifiers(stored({ testCode: "PH", limitType: "Range", lowerLimit: 5, upperLimit: 7 }))).toEqual([]);
  });
});
