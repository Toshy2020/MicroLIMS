import { describe, expect, it } from "vitest";
import { selectionPayload, withLot } from "./preparationMaterial";

describe("preparationMaterial", () => {
  it.each([
    ["MediaProduct", { diluentMediaProductId: 3, diluentSolutionMasterId: null, diluentMaterialMasterEntryId: null }],
    ["SolutionRecipe", { diluentMediaProductId: null, diluentSolutionMasterId: 3, diluentMaterialMasterEntryId: null }],
    ["MaterialEntry", { diluentMediaProductId: null, diluentSolutionMasterId: null, diluentMaterialMasterEntryId: 3 }]
  ] as const)("sends exactly the id that matches %s", (source, ids) => {
    expect(selectionPayload("diluent", { source, masterId: 3 })).toEqual({ diluentSource: source, ...ids });
  });

  it("sends no id for a neutralizer contained in the medium", () => {
    expect(selectionPayload("neutralizer", { source: "ContainedInMedium", masterId: null })).toEqual({
      neutralizerSource: "ContainedInMedium",
      neutralizerMediaProductId: null, neutralizerSolutionMasterId: null, neutralizerMaterialMasterEntryId: null
    });
  });

  it("appends the lot only when there is one", () => {
    expect(withLot("Saline", "SP-1")).toBe("Saline — lot SP-1");
    expect(withLot("Saline", null)).toBe("Saline");
  });
});
