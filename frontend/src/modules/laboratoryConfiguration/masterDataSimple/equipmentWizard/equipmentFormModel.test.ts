import { describe, expect, it } from "vitest";
import type { ConfiguredEquipmentSummary } from "../services/EquipmentConfigurationService";
import {
  EQUIPMENT_CDS_MESSAGE,
  EQUIPMENT_REQUIRED_MESSAGE,
  EQUIPMENT_SECTION_MESSAGE,
  createEmptyEquipmentFormModel,
  firstEquipmentError,
  fromEquipment,
  toEquipmentPayload,
  validateStep,
  withEquipmentType
} from "./equipmentFormModel";
import { DIALOG_EQUIPMENT_FIXTURES, MODEL_ONLY_EQUIPMENT_FIXTURES } from "./__fixtures__/equipmentPayloads";

const ALL_FIXTURES = { ...DIALOG_EQUIPMENT_FIXTURES, ...MODEL_ONLY_EQUIPMENT_FIXTURES };
const single = { isEditing: false, sectionCount: 1 };

describe("toEquipmentPayload", () => {
  it.each(Object.entries(ALL_FIXTURES))("equals the captured payload for %s", (_name, fixture) => {
    expect(toEquipmentPayload(fixture.model, fixture.ctx)).toStrictEqual(fixture.payload);
  });

  it("sends the CDS software only for HPLC, so switching type and back leaves no stale value", () => {
    let model = createEmptyEquipmentFormModel({ name: "X", code: "X-1", type: "Hplc", cdsSoftware: "AgilentOpenLab" });
    model = withEquipmentType(model, "Autoclave");
    expect(model.cdsSoftware).toBe("");
    model = withEquipmentType(model, "Hplc");
    expect(model.cdsSoftware).toBe("");
    expect(toEquipmentPayload(model, single).cdsSoftware).toBeNull();

    // Even a CDS value left in a non-HPLC model is never sent.
    const stale = createEmptyEquipmentFormModel({ name: "X", code: "X-1", type: "Autoclave", cdsSoftware: "AgilentOpenLab" });
    expect(toEquipmentPayload(stale, single).cdsSoftware).toBeNull();
  });

  it("keeps the CDS software when the type is chosen again as HPLC", () => {
    const model = createEmptyEquipmentFormModel({ type: "Hplc", cdsSoftware: "WatersEmpower3" });
    expect(withEquipmentType(model, "Hplc").cdsSoftware).toBe("WatersEmpower3");
  });

  it("sends the section only when creating with several sections to choose from", () => {
    const model = createEmptyEquipmentFormModel({ name: "A", code: "B", sectionId: 3 });
    expect("sectionId" in toEquipmentPayload(model, { isEditing: false, sectionCount: 2 })).toBe(true);
    expect("sectionId" in toEquipmentPayload(model, { isEditing: false, sectionCount: 1 })).toBe(false);
    expect("sectionId" in toEquipmentPayload(model, { isEditing: true, sectionCount: 2 })).toBe(false);
  });
});

describe("validateStep", () => {
  const ok = createEmptyEquipmentFormModel({ name: "A", code: "B" });

  it.each([
    ["name", { name: "  " }],
    ["code", { code: "" }],
    ["type", { type: "" }]
  ])("asks for name, code and type when %s is missing", (_field, patch) => {
    const model = { ...ok, ...patch };
    expect(validateStep("identity", model, single)).toEqual({ required: EQUIPMENT_REQUIRED_MESSAGE });
    expect(firstEquipmentError(model, single)).toBe("Name, Code, and Type are required.");
  });

  it("asks for CDS software on HPLC only", () => {
    const hplc = { ...ok, type: "Hplc" };
    expect(validateStep("typeSettings", hplc, single)).toEqual({ cdsSoftware: EQUIPMENT_CDS_MESSAGE });
    expect(firstEquipmentError(hplc, single)).toBe("CDS Software is required for HPLC equipment.");
    expect(validateStep("typeSettings", { ...hplc, cdsSoftware: "AgilentOpenLab" }, single)).toEqual({});
    expect(validateStep("typeSettings", ok, single)).toEqual({});
  });

  it("requires a section when creating with several, never when editing or with one", () => {
    expect(validateStep("identity", ok, { isEditing: false, sectionCount: 2 })).toEqual({
      section: EQUIPMENT_SECTION_MESSAGE
    });
    expect(firstEquipmentError(ok, { isEditing: false, sectionCount: 2 })).toBe("Laboratory section is required.");
    expect(validateStep("identity", ok, { isEditing: true, sectionCount: 2 })).toEqual({});
    expect(validateStep("identity", ok, single)).toEqual({});
    expect(validateStep("identity", { ...ok, sectionId: 2 }, { isEditing: false, sectionCount: 2 })).toEqual({});
  });

  it("reports the old first message: required fields, then CDS software, then section", () => {
    const ctx = { isEditing: false, sectionCount: 2 };
    const all = { ...ok, name: "", type: "Hplc" };
    expect(firstEquipmentError(all, ctx)).toBe(EQUIPMENT_REQUIRED_MESSAGE);
    expect(firstEquipmentError({ ...all, name: "A" }, ctx)).toBe(EQUIPMENT_CDS_MESSAGE);
    expect(firstEquipmentError({ ...all, name: "A", cdsSoftware: "AgilentOpenLab" }, ctx)).toBe(
      EQUIPMENT_SECTION_MESSAGE
    );
    expect(firstEquipmentError({ ...all, name: "A", cdsSoftware: "AgilentOpenLab", sectionId: 1 }, ctx)).toBeNull();
  });

  it("has no rules for the documents step", () => {
    expect(validateStep("documents", createEmptyEquipmentFormModel(), single)).toEqual({});
  });
});

describe("fromEquipment", () => {
  const summary: ConfiguredEquipmentSummary = {
    id: 5,
    name: "HPLC 1",
    code: "H-1",
    type: 6,
    location: null,
    inventoryLocation: "Lab 2",
    manufacturerName: "Waters",
    cdsSoftware: "WatersEmpower3",
    connectionSettings: null,
    setPointTemperature: 0,
    calibrationDueDate: "2027-02-01T00:00:00Z",
    equipmentInventoryId: 7,
    sectionId: 4,
    configuredProgramCount: 0
  };

  it("maps a configured-equipment row to the form values", () => {
    expect(fromEquipment(summary)).toEqual({
      name: "HPLC 1",
      code: "H-1",
      type: "Hplc",
      location: "Lab 2",
      vendor: "Waters",
      cdsSoftware: "WatersEmpower3",
      connectionSettings: "",
      setPointTemperature: "0",
      calibrationDueDate: "2027-02-01",
      equipmentInventoryId: 7,
      sectionId: 4
    });
  });
});
