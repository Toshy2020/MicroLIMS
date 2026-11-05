import { describe, expect, it } from "vitest";
import {
  createInitialSolutionFormState,
  createEmptyComponentRow,
  formToSaveRequest,
  getAvailableSourceSolutions,
  validateSolutionForm,
  type SolutionFormState
} from "./solutionForm";
import type { SolutionMaster } from "../services/SolutionMasterService";

const baseForm = (over: Partial<SolutionFormState> = {}): SolutionFormState => ({
  ...createInitialSolutionFormState(7),
  name: "Phosphate buffer pH 7.2",
  type: "Diluent",
  storageCondition: "2-8 C",
  instructions: "Mix and autoclave.",
  ...over
});

const recipe = (over: Partial<SolutionMaster>): SolutionMaster => ({
  id: 1, sectionId: 7, name: "Stock", type: "Diluent", shelfLifeValue: 1, shelfLifeUnit: "Days",
  storageCondition: "x", finalVolumeMl: 100, instructions: "x", isActive: true, blankRequired: false,
  components: [], ...over
});

describe("formToSaveRequest", () => {
  it("sends the flags and all three component kinds for a general type", () => {
    const form = baseForm({
      sterilizationRequired: true,
      releaseRequired: true,
      components: [
        { ...createEmptyComponentRow("Material"), materialMasterEntryId: 11, quantity: "6.8", unit: "Gram" },
        { ...createEmptyComponentRow("PreparedSolution"), sourceSolutionMasterId: 4, quantity: "10", unit: "Milliliter" },
        { ...createEmptyComponentRow("Text"), text: "  Purified water to 1000 mL " }
      ]
    });
    const req = formToSaveRequest(form, 7);
    expect(req.sterilizationRequired).toBe(true);
    expect(req.releaseRequired).toBe(true);
    expect(req.components).toEqual([
      { kind: "Material", materialMasterEntryId: 11, quantity: 6.8, unit: "Gram" },
      { kind: "PreparedSolution", sourceSolutionMasterId: 4, quantity: 10, unit: "Milliliter" },
      { kind: "Text", text: "Purified water to 1000 mL" }
    ]);
  });

  it("sends false for both flags on Mobile Phase and Titrant", () => {
    for (const type of ["MobilePhase", "Titrant"] as const) {
      const req = formToSaveRequest(baseForm({ type, sterilizationRequired: true, releaseRequired: true }), 7);
      expect(req.sterilizationRequired).toBe(false);
      expect(req.releaseRequired).toBe(false);
    }
  });

  it("keeps Reagent and Neutralizer as general types", () => {
    for (const type of ["Reagent", "Neutralizer"] as const) {
      const req = formToSaveRequest(baseForm({ type, sterilizationRequired: true, releaseRequired: false }), 7);
      expect(req.type).toBe(type);
      expect(req.sterilizationRequired).toBe(true);
      expect(req.releaseRequired).toBe(false);
    }
  });
});

describe("validateSolutionForm components", () => {
  const opts = { isEditing: false, hasMultipleSections: false };

  it("accepts a text line with no quantity", () => {
    const form = baseForm({ components: [{ ...createEmptyComponentRow("Text"), text: "Purified water to 1000 mL" }] });
    expect(validateSolutionForm(form, opts)).toBeNull();
  });

  it("asks for a source recipe on a prepared-solution line", () => {
    const form = baseForm({ components: [{ ...createEmptyComponentRow("PreparedSolution"), quantity: "5" }] });
    expect(validateSolutionForm(form, opts)?.message).toMatch(/prepared solution recipe/);
  });
});

describe("getAvailableSourceSolutions", () => {
  it("offers active general recipes of the same lab, never the recipe itself", () => {
    const editing = recipe({ id: 1, name: "Self" });
    const list = [
      editing,
      recipe({ id: 2, name: "Active diluent" }),
      recipe({ id: 3, name: "Inactive", isActive: false }),
      recipe({ id: 4, name: "Other lab", sectionId: 9 }),
      recipe({ id: 5, name: "HPLC phase", type: "MobilePhase" }),
      recipe({ id: 6, name: "Neutralizer", type: "Neutralizer" })
    ];
    expect(getAvailableSourceSolutions(7, list, editing).map((s) => s.id)).toEqual([2, 6]);
  });
});
