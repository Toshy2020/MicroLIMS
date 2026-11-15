import { describe, expect, it } from "vitest";
import type { SpecificationDto } from "../../specifications/services/SpecificationService";
import { ownLabTestCodes, specQualifiers } from "./copySpecifications";

const stored = (overrides: Partial<SpecificationDto> & Pick<SpecificationDto, "testCode">): SpecificationDto => ({
  id: 4711,
  version: 3,
  itemId: 5,
  canEdit: true,
  sectionName: "Finished Product",
  displayOrder: 7,
  ...overrides
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
