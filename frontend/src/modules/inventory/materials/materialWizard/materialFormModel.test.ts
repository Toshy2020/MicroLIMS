import { describe, expect, it } from "vitest";
import {
  MATERIAL_CATEGORY_MESSAGE,
  MATERIAL_MASTER_MESSAGE,
  MATERIAL_MEDIA_MESSAGE,
  MATERIAL_MOISTURE_MESSAGE,
  MATERIAL_PURITY_RANGE_MESSAGE,
  MATERIAL_QUANTITY_MESSAGE,
  MATERIAL_REQUIRED_MESSAGE,
  MATERIAL_SECTION_MESSAGE,
  MATERIAL_STRAIN_MESSAGE,
  createEmptyMaterialFormModel,
  firstMaterialError,
  formatCalendarDate,
  fromMaterial,
  newLotTypesFor,
  toMaterialPayload,
  validateStep,
  withMaterialType,
  type MaterialFormModel,
  type MaterialModelContext
} from "./materialFormModel";
import { DIALOG_MATERIAL_FIXTURES, EDITED_LOTS, RECEIVED_ON } from "./__fixtures__/materialPayloads";

const single: MaterialModelContext = { isEditing: false, sectionCount: 1, category: null };
const complete = (over: Partial<MaterialFormModel>) =>
  createEmptyMaterialFormModel(RECEIVED_ON, {
    materialType: "Chemical",
    materialName: "Acetone AR",
    batchNumber: "L1",
    location: "Microbiology Lab",
    quantityReceived: "5",
    materialMasterEntryId: 21,
    ...over
  });

describe("toMaterialPayload", () => {
  it.each(Object.entries(DIALOG_MATERIAL_FIXTURES))("equals the captured payload for %s", (_name, fixture) => {
    expect(toMaterialPayload(fixture.model, fixture.ctx)).toStrictEqual(fixture.payload);
  });

  it("sends the media product only for dehydrated media, so switching kind leaves no stale value", () => {
    const media = complete({ materialType: "DehydratedMedia", mediaProductId: 4, materialMasterEntryId: null });
    const switched = withMaterialType(media, "Chemical", null, false);
    expect(switched.mediaProductId).toBeNull();
    expect(toMaterialPayload({ ...switched, materialName: "A", batchNumber: "B", quantityReceived: "1", location: "L" }, single).mediaProductId).toBeNull();
    // Even a value left in a model of another kind is never sent.
    expect(toMaterialPayload({ ...complete({}), mediaProductId: 4 }, single).mediaProductId).toBeNull();
  });

  it("sends purity and moisture only for the kinds that have them", () => {
    const chem = { ...complete({}), purity: "99", moisturePercent: "1" };
    const payload = toMaterialPayload(chem, single);
    expect(payload.purity).toBeNull();
    expect(payload.moisturePercent).toBeNull();
    expect(toMaterialPayload({ ...chem, materialType: "PrimaryStandard" }, single)).toMatchObject({ purity: 99, moisturePercent: null });
  });

  it("sends the section only when creating with several sections to choose from", () => {
    const model = complete({ sectionId: 3 });
    expect("sectionId" in toMaterialPayload(model, { ...single, sectionCount: 2 })).toBe(true);
    expect("sectionId" in toMaterialPayload(model, single)).toBe(false);
    expect("sectionId" in toMaterialPayload(model, { isEditing: true, sectionCount: 2, category: null })).toBe(false);
  });

  it("sends a custom type only when an old Other lot is edited", () => {
    const model = complete({ materialType: "Other", customType: " Culture Media " });
    expect(toMaterialPayload(model, single).customType).toBeNull();
    expect(toMaterialPayload(model, { ...single, isEditing: true }).customType).toBe("Culture Media");
  });
});

describe("withMaterialType", () => {
  it("keeps the master entry when the new kind uses the same master category, and clears name and code when it does not", () => {
    const chem = complete({ code: "CHEM-001" });
    expect(withMaterialType(chem, "Chemical", null, false).materialMasterEntryId).toBe(21);
    const indicator = withMaterialType(chem, "Indicator", "Milliliter", false);
    expect(indicator).toMatchObject({ materialMasterEntryId: null, materialName: "", code: "", unit: "Milliliter", storageCondition: "", categoryId: "" });
  });

  it("keeps the name of a lot being edited when the entry changes", () => {
    expect(withMaterialType(complete({}), "Indicator", null, true).materialName).toBe("Acetone AR");
  });
});

describe("fromMaterial", () => {
  it("copies what the dialog loaded, with dates cut to the day and empty fields as empty strings", () => {
    const model = fromMaterial({ ...EDITED_LOTS.referenceStandardLinked, receivingDate: "2026-01-01T00:00:00Z", expiryDate: "2027-02-03T00:00:00Z" });
    expect(model).toMatchObject({
      receivingDate: "2026-01-01",
      expiryDate: "2027-02-03",
      purity: 99.8,
      moisturePercent: 0.5,
      materialMasterEntryId: 23,
      minimumStockLevel: "",
      storageCondition: "",
      categoryId: "",
      sectionId: ""
    });
  });
});

describe("validation messages (the old save handler's, split by step)", () => {
  it("material step", () => {
    const media = complete({ materialType: "DehydratedMedia", materialMasterEntryId: null });
    expect(validateStep("material", media, single).mediaProductId).toBe(MATERIAL_MEDIA_MESSAGE);
    expect(validateStep("material", complete({ materialType: "LyophilizedMicroorganism", materialMasterEntryId: null }), single).referenceOrganismId).toBe(MATERIAL_STRAIN_MESSAGE);
    expect(validateStep("material", complete({ materialType: "Other" }), single).categoryId).toBe(MATERIAL_CATEGORY_MESSAGE);
    expect(validateStep("material", complete({ materialMasterEntryId: null }), single).materialMasterEntryId).toBe(MATERIAL_MASTER_MESSAGE);
    expect(validateStep("material", complete({ materialName: " " }), single).materialName).toBe(MATERIAL_REQUIRED_MESSAGE);
    expect(validateStep("material", complete({}), { ...single, sectionCount: 2 }).section).toBe(MATERIAL_SECTION_MESSAGE);
    expect(validateStep("material", complete({}), single)).toEqual({});
  });

  it("purity and moisture", () => {
    const ps = complete({ materialType: "PrimaryStandard", materialMasterEntryId: 24 });
    expect(validateStep("material", ps, single).purity).toBe("Purity percentage is required for primary standards.");
    expect(validateStep("material", { ...ps, materialType: "ReferenceStandard" }, single).purity).toBe(
      "Purity percentage is required for reference standards."
    );
    expect(validateStep("material", { ...ps, purity: "0" }, single).purity).toBe(MATERIAL_PURITY_RANGE_MESSAGE);
    expect(validateStep("material", { ...ps, purity: "100.1" }, single).purity).toBe(MATERIAL_PURITY_RANGE_MESSAGE);
    expect(validateStep("material", { ...ps, purity: "100" }, single)).toEqual({});
    const rs = { ...ps, materialType: "ReferenceStandard" as const, purity: "99" };
    expect(validateStep("material", { ...rs, moisturePercent: "100" }, single).moisturePercent).toBe(MATERIAL_MOISTURE_MESSAGE);
    expect(validateStep("material", { ...rs, moisturePercent: "-1" }, single).moisturePercent).toBe(MATERIAL_MOISTURE_MESSAGE);
    expect(validateStep("material", { ...rs, moisturePercent: "0" }, single)).toEqual({});
    // A primary standard has no moisture field.
    expect(validateStep("material", { ...ps, purity: "99", moisturePercent: "500" }, single)).toEqual({});
  });

  it("lot step", () => {
    expect(validateStep("lot", complete({ batchNumber: "" }), single).batchNumber).toBe(MATERIAL_REQUIRED_MESSAGE);
    expect(validateStep("lot", complete({ location: " " }), single).location).toBe(MATERIAL_REQUIRED_MESSAGE);
    expect(validateStep("lot", complete({ quantityReceived: "" }), single).quantityReceived).toBe(MATERIAL_REQUIRED_MESSAGE);
    expect(validateStep("lot", complete({ quantityReceived: "0" }), single).quantityReceived).toBe(MATERIAL_QUANTITY_MESSAGE);
    expect(validateStep("lot", complete({ quantityReceived: "-2" }), single).quantityReceived).toBe(MATERIAL_QUANTITY_MESSAGE);
    expect(validateStep("lot", complete({}), single)).toEqual({});
  });

  it("dates step", () => {
    expect(validateStep("dates", complete({ receivingDate: "" }), single).receivingDate).toBe(MATERIAL_REQUIRED_MESSAGE);
    const category = { name: "Supplement", expiryRequired: true };
    expect(validateStep("dates", complete({}), { ...single, category }).expiryDate).toBe("Supplement lots need an expiry date.");
    expect(validateStep("dates", complete({ expiryDate: "2030-01-01" }), { ...single, category })).toEqual({});
    // The category rule is for new lots only.
    expect(validateStep("dates", complete({}), { ...single, isEditing: true, category })).toEqual({});
  });

  it("firstMaterialError returns the message the old dialog showed first", () => {
    const empty = createEmptyMaterialFormModel(RECEIVED_ON);
    expect(firstMaterialError(empty, single)).toBe(MATERIAL_MEDIA_MESSAGE);
    expect(firstMaterialError({ ...empty, materialType: "Chemical" }, single)).toBe(MATERIAL_MASTER_MESSAGE);
    expect(firstMaterialError({ ...empty, materialType: "Chemical", materialMasterEntryId: 21 }, single)).toBe(MATERIAL_REQUIRED_MESSAGE);
    expect(firstMaterialError(complete({ quantityReceived: "0" }), { ...single, sectionCount: 2 })).toBe(MATERIAL_QUANTITY_MESSAGE);
    expect(firstMaterialError(complete({}), { ...single, sectionCount: 2 })).toBe(MATERIAL_SECTION_MESSAGE);
    expect(firstMaterialError(complete({}), single)).toBeNull();
  });
});

describe("helpers", () => {
  it("offers what the server lists plus the Other path, never legacy types", () => {
    expect(newLotTypesFor({ builtIn: ["DehydratedMedia", "Supplement", "Chemical"] })).toEqual(["DehydratedMedia", "Chemical", "Other"]);
  });

  it("shows a calendar date as the date fields do", () => {
    expect(formatCalendarDate("2026-11-02")).toBe("02/11/2026");
    expect(formatCalendarDate("")).toBe("");
  });
});
