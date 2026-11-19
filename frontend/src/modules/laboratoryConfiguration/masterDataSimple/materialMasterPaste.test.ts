import { describe, expect, it } from "vitest";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import { CATEGORY_OPTIONS } from "./MaterialMasterPage";
import {
  MAX_MATERIAL_MASTER_BATCH_ROWS,
  buildMaterialMasterPastePlan,
  parseOptionalDecimal,
  resolveCategory,
  resolveMaterialCategory,
  resolveMaterialUnit,
  resolveOwnerSection,
  toPendingMaterialMasterEntries,
  toSaveMaterialMasterEntryPayload
} from "./materialMasterPaste";
import type { MaterialCategory } from "./services/MaterialCategoryService";
import type { MaterialMasterEntry } from "./services/MaterialMasterService";

const SECTIONS: LaboratorySection[] = [
  { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product", departmentId: 1, departmentCode: "QC", departmentName: "QC" },
  { sectionId: 2, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" },
  { sectionId: 3, sectionCode: "DUP-A", sectionName: "Shared Lab", departmentId: 1, departmentCode: "QC", departmentName: "QC" },
  { sectionId: 4, sectionCode: "DUP-B", sectionName: "Shared Lab", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
];

const MATERIAL_CATEGORIES: MaterialCategory[] = [
  {
    id: 10,
    name: "Glassware",
    codePrefix: "GLS",
    defaultUnit: "Piece",
    coaRequired: false,
    expiryRequired: false,
    quantityTracked: true,
    approvalBeforeUse: false,
    labelRequired: false,
    isActive: true,
    sectionIds: []
  },
  {
    id: 11,
    name: "Filters",
    codePrefix: "FLT",
    defaultUnit: "Pack",
    coaRequired: true,
    expiryRequired: true,
    quantityTracked: true,
    approvalBeforeUse: true,
    labelRequired: true,
    isActive: true,
    sectionIds: [1]
  },
  {
    id: 12,
    name: "Inactive Cat",
    codePrefix: "INA",
    defaultUnit: "Piece",
    coaRequired: false,
    expiryRequired: false,
    quantityTracked: true,
    approvalBeforeUse: false,
    labelRequired: false,
    isActive: false,
    sectionIds: []
  },
  {
    id: 13,
    name: "Common Supplies",
    codePrefix: "SUP1",
    defaultUnit: "Pack",
    coaRequired: false,
    expiryRequired: false,
    quantityTracked: true,
    approvalBeforeUse: false,
    labelRequired: false,
    isActive: true,
    sectionIds: []
  },
  {
    id: 14,
    name: "Common Supplies",
    codePrefix: "SUP2",
    defaultUnit: "Piece",
    coaRequired: false,
    expiryRequired: false,
    quantityTracked: true,
    approvalBeforeUse: false,
    labelRequired: false,
    isActive: true,
    sectionIds: []
  }
] as MaterialCategory[];

const EXISTING_ENTRIES: MaterialMasterEntry[] = [
  {
    id: 1,
    sectionId: 1,
    code: "RG-001",
    name: "Sodium Hydroxide",
    category: "Reagent",
    baseUnit: "Gram",
    isActive: true
  }
];

describe("materialMasterPaste resolvers", () => {
  it("resolves category by value or label within offered options and flags unknown", () => {
    const reagentOptions = CATEGORY_OPTIONS.filter((c) => c.value === "Reagent" || c.value === "Indicator");
    expect(resolveCategory("reagent", reagentOptions).option?.value).toBe("Reagent");
    expect(resolveCategory("Indicator", reagentOptions).option?.value).toBe("Indicator");
    expect(resolveCategory("Reference Standard").option?.value).toBe("ReferenceStandard");
    expect(resolveCategory("Other material").option?.value).toBe("Other");
    expect(resolveCategory("ReferenceStandard", reagentOptions).error).toMatch(/unknown category/i);
    expect(resolveCategory("   ", reagentOptions).error).toMatch(/category is required/i);
  });

  it("resolves material category by prefix (with or without hyphen) or name, respects lab scope and active status, and flags ambiguous", () => {
    expect(resolveMaterialCategory("GLS", MATERIAL_CATEGORIES, 1, true).category?.id).toBe(10);
    expect(resolveMaterialCategory("gls-", MATERIAL_CATEGORIES, 1, true).category?.id).toBe(10);
    expect(resolveMaterialCategory("Glassware", MATERIAL_CATEGORIES, 2, true).category?.id).toBe(10);
    expect(resolveMaterialCategory("Filters", MATERIAL_CATEGORIES, 1, true).category?.id).toBe(11);
    expect(resolveMaterialCategory("Filters", MATERIAL_CATEGORIES, 2, true).error).toMatch(/unknown material category/i);
    expect(resolveMaterialCategory("Inactive Cat", MATERIAL_CATEGORIES, 1, true).error).toMatch(/unknown material category/i);
    expect(resolveMaterialCategory("Common Supplies", MATERIAL_CATEGORIES, 1, true).error).toMatch(/ambiguous material category/i);
    expect(resolveMaterialCategory("", MATERIAL_CATEGORIES, 1, false)).toEqual({ category: null, error: null });
    expect(resolveMaterialCategory("", MATERIAL_CATEGORIES, 1, true).error).toMatch(/material category is required/i);
  });

  it("resolves base unit case-insensitively and flags unknown", () => {
    expect(resolveMaterialUnit("milliliter").unit).toBe("Milliliter");
    expect(resolveMaterialUnit("GRAM").unit).toBe("Gram");
    expect(resolveMaterialUnit("").error).toMatch(/base unit is required/i);
    expect(resolveMaterialUnit("Gallon").error).toMatch(/unknown base unit/i);
  });

  it("resolves owner lab by code or name and flags unknown or ambiguous", () => {
    expect(resolveOwnerSection("fp", SECTIONS).section?.sectionId).toBe(1);
    expect(resolveOwnerSection("Microbiology", SECTIONS).section?.sectionId).toBe(2);
    expect(resolveOwnerSection("DUP-A", SECTIONS).section?.sectionId).toBe(3);
    expect(resolveOwnerSection("Shared Lab", SECTIONS).error).toMatch(/ambiguous owner lab/i);
    expect(resolveOwnerSection("NOPE", SECTIONS).error).toMatch(/unknown owner lab/i);
    expect(resolveOwnerSection("", SECTIONS).error).toMatch(/owner lab is required/i);
  });

  it("parses optional decimal numbers and rejects invalid text", () => {
    expect(parseOptionalDecimal("")).toEqual({ value: null, error: null });
    expect(parseOptionalDecimal(" 8.2 ")).toEqual({ value: 8.2, error: null });
    expect(parseOptionalDecimal("-1.5")).toEqual({ value: -1.5, error: null });
    expect(parseOptionalDecimal("0")).toEqual({ value: 0, error: null });
    for (const bad of ["abc", "1.2.3", "1e2", "< 5"]) {
      expect(parseOptionalDecimal(bad).error).not.toBeNull();
    }
  });
});

describe("buildMaterialMasterPastePlan", () => {
  it("builds exact form-order columns per tab and section count", () => {
    const singleLabReagents = buildMaterialMasterPastePlan({
      entries: EXISTING_ENTRIES,
      categoryOptions: CATEGORY_OPTIONS.filter((c) => c.value === "Reagent" || c.value === "Indicator"),
      categories: ["Reagent", "Indicator"],
      materialCategories: MATERIAL_CATEGORIES,
      mySections: [SECTIONS[0]]
    });
    expect(singleLabReagents.maxRows).toBe(MAX_MATERIAL_MASTER_BATCH_ROWS);
    expect(singleLabReagents.columns.map((c) => c.label)).toEqual([
      "Code",
      "Category",
      "Name",
      "Base Unit",
      "Grade",
      "Source / Pharmacopoeia",
      "Working Concentration",
      "Solvent",
      "Transition Range (From)",
      "Transition Range (To)",
      "Colour Change",
      "Indicator Use"
    ]);

    const multiLabStandards = buildMaterialMasterPastePlan({
      entries: EXISTING_ENTRIES,
      categoryOptions: CATEGORY_OPTIONS.filter(
        (c) => c.value === "ReferenceStandard" || c.value === "PrimaryStandard"
      ),
      categories: ["ReferenceStandard", "PrimaryStandard"],
      materialCategories: MATERIAL_CATEGORIES,
      mySections: [SECTIONS[0], SECTIONS[1]]
    });
    expect(multiLabStandards.columns.map((c) => c.label)).toEqual([
      "Code",
      "Category",
      "Name",
      "Base Unit",
      "Grade",
      "Source / Pharmacopoeia",
      "Owner Lab"
    ]);

    const multiLabOther = buildMaterialMasterPastePlan({
      entries: EXISTING_ENTRIES,
      categoryOptions: CATEGORY_OPTIONS.filter((c) => c.value === "Other"),
      categories: ["Other"],
      materialCategories: MATERIAL_CATEGORIES,
      mySections: [SECTIONS[0], SECTIONS[1]]
    });
    expect(multiLabOther.columns.map((c) => c.label)).toEqual([
      "Code",
      "Category",
      "Material category",
      "Name",
      "Base Unit",
      "Grade",
      "Source / Pharmacopoeia",
      "Owner Lab"
    ]);
  });

  it("flags existing codes (case-insensitive) and duplicate codes inside the paste on every matching row", () => {
    const plan = buildMaterialMasterPastePlan({
      entries: EXISTING_ENTRIES,
      categoryOptions: CATEGORY_OPTIONS.filter((c) => c.value === "Reagent" || c.value === "Indicator"),
      categories: ["Reagent", "Indicator"],
      materialCategories: MATERIAL_CATEGORIES,
      mySections: [SECTIONS[0]]
    });

    const codeCol = plan.columns[0];
    expect(codeCol.check(" rg-001 ")).toMatch(/already exists/i);
    expect(codeCol.check("RG-002")).toBeNull();

    const matched = plan.match!([
      ["RG-010", "Reagent", "First", "Gram"],
      ["RG-011", "Reagent", "Unique", "Gram"],
      [" rg-010 ", "Reagent", "Second", "Gram"]
    ]);
    expect(matched.rows[0].problem).toMatch(/duplicate code in paste/i);
    expect(matched.rows[1].problem).toBeUndefined();
    expect(matched.rows[2].problem).toMatch(/duplicate code in paste/i);
  });

  it("requires Material category only for Other rows in standalone mode and maps accepted rows to exact DTOs", () => {
    const plan = buildMaterialMasterPastePlan({
      entries: [],
      categoryOptions: CATEGORY_OPTIONS,
      materialCategories: MATERIAL_CATEGORIES,
      mySections: [SECTIONS[0], SECTIONS[1]]
    });

    const matched = plan.match!([
      ["RG-020", "Reagent", "", "HCl", "Milliliter", "AR", "Merck", "FP"],
      ["GLS-001", "Other", "", "Beaker 250mL", "Piece", "", "", "MICRO"],
      ["FLT-001", "Other", "Filters", "0.22um Filter", "Pack", "", "", "FP"]
    ]);
    expect(matched.rows[0].problem).toBeUndefined();
    expect(matched.rows[1].problem).toMatch(/material category is required/i);
    expect(matched.rows[2].problem).toBeUndefined();

    const pending = toPendingMaterialMasterEntries(
      [
        {
          target: null,
          values: {
            code: "ind-010",
            category: "Indicator",
            materialCategory: "",
            name: "Phenolphthalein",
            baseUnit: "Milliliter",
            grade: "AR",
            source: "USP",
            section: "Finished Product",
            workingConcentration: "1% w/v",
            solvent: "Ethanol",
            transitionRangeFrom: "8.2",
            transitionRangeTo: "10.0",
            colourChange: "Colourless to Pink",
            indicatorUse: "Acid-Base"
          }
        },
        {
          target: null,
          values: {
            code: "flt-001",
            category: "Other material",
            materialCategory: "FLT",
            name: "0.22um Filter",
            baseUnit: "Pack",
            grade: "",
            source: "",
            section: "FP",
            workingConcentration: "",
            solvent: "",
            transitionRangeFrom: "",
            transitionRangeTo: "",
            colourChange: "",
            indicatorUse: ""
          }
        }
      ],
      {
        categoryOptions: CATEGORY_OPTIONS,
        materialCategories: MATERIAL_CATEGORIES,
        mySections: [SECTIONS[0], SECTIONS[1]]
      }
    );

    expect(toSaveMaterialMasterEntryPayload(pending[0])).toEqual({
      code: "IND-010",
      name: "Phenolphthalein",
      category: "Indicator",
      materialCategoryId: null,
      grade: "AR",
      source: "USP",
      baseUnit: "Milliliter",
      sectionId: 1,
      workingConcentration: "1% w/v",
      solvent: "Ethanol",
      transitionRangeFrom: 8.2,
      transitionRangeTo: 10,
      colourChange: "Colourless to Pink",
      indicatorUse: "Acid-Base"
    });

    expect(toSaveMaterialMasterEntryPayload(pending[1])).toEqual({
      code: "FLT-001",
      name: "0.22um Filter",
      category: "Other",
      materialCategoryId: 11,
      grade: null,
      source: null,
      baseUnit: "Pack",
      sectionId: 1,
      workingConcentration: null,
      solvent: null,
      transitionRangeFrom: null,
      transitionRangeTo: null,
      colourChange: null,
      indicatorUse: null
    });
  });
});
