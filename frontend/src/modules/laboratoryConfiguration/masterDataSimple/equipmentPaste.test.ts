import { describe, expect, it } from "vitest";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import {
  MAX_EQUIPMENT_BATCH_ROWS,
  buildEquipmentPastePlan,
  parseOptionalDate,
  parseOptionalTemperature,
  resolveEquipmentType,
  resolveInventoryAsset,
  resolveLaboratorySection,
  toCreateEquipmentPayload,
  toPendingEquipmentRows
} from "./equipmentPaste";

const SECTIONS: LaboratorySection[] = [
  {
    sectionId: 1,
    sectionCode: "MICRO",
    sectionName: "Microbiology",
    departmentId: 1,
    departmentCode: "QC",
    departmentName: "Quality Control"
  },
  {
    sectionId: 2,
    sectionCode: "EM",
    sectionName: "Environmental Monitoring",
    departmentId: 1,
    departmentCode: "QC",
    departmentName: "Quality Control"
  }
];

const INVENTORY: EquipmentItem[] = [
  {
    id: 42,
    version: 1,
    instrumentType: "Binder Incubator",
    manufacturerName: "Binder",
    serialNumber: "SN-42",
    firmwareVersion: null,
    code: "AST-INC-42",
    location: "Room 101",
    calibrationDueDate: null,
    status: "InService",
    isCalibrationOverdue: false,
    sectionId: 1
  },
  {
    id: 43,
    version: 1,
    instrumentType: "Autoclave",
    manufacturerName: "Tuttnauer",
    serialNumber: "SN-43",
    firmwareVersion: null,
    code: "AST-AUTO-43",
    location: "Room 102",
    calibrationDueDate: null,
    status: "InService",
    isCalibrationOverdue: false,
    sectionId: 1
  },
  {
    id: 44,
    version: 1,
    instrumentType: "Autoclave",
    manufacturerName: "Fedegari",
    serialNumber: "SN-44",
    firmwareVersion: null,
    code: "AST-AUTO-44",
    location: "Room 103",
    calibrationDueDate: null,
    status: "InService",
    isCalibrationOverdue: false,
    sectionId: 1
  }
];

describe("equipmentPaste resolvers", () => {
  it("resolves MICRO_EQUIPMENT_TYPES by value or label and marks FP-only types as not supported by paste", () => {
    expect(resolveEquipmentType("Incubator").option?.value).toBe("Incubator");
    expect(resolveEquipmentType("laf cabinet").option?.value).toBe("LafCabinet");
    expect(resolveEquipmentType("BiologicalSafetyCabinet").option?.label).toBe("Biological Safety Cabinet");
    expect(resolveEquipmentType("Water Bath").option?.value).toBe("WaterBath");
    expect(resolveEquipmentType("Other").option?.value).toBe("Other");

    expect(resolveEquipmentType("").error).toMatch(/required/i);
    expect(resolveEquipmentType("HPLC").error).toMatch(/not supported by paste/i);
    expect(resolveEquipmentType("IcpOes").error).toMatch(/not supported by paste/i);
    expect(resolveEquipmentType("pH Meter").error).toMatch(/not supported by paste/i);
    expect(resolveEquipmentType("Balance").error).toMatch(/not supported by paste/i);
    expect(resolveEquipmentType("Mass Spec").error).toMatch(/unknown equipment type/i);
  });

  it("resolves laboratory sections by code, name, or label and enforces blank only when multiple sections exist", () => {
    expect(resolveLaboratorySection("", [SECTIONS[0]])).toEqual({ section: null, error: null });
    expect(resolveLaboratorySection("", SECTIONS).error).toMatch(/required/i);
    expect(resolveLaboratorySection("micro", SECTIONS).section?.sectionId).toBe(1);
    expect(resolveLaboratorySection("Environmental Monitoring", SECTIONS).section?.sectionId).toBe(2);
    expect(resolveLaboratorySection("Microbiology (Quality Control)", SECTIONS).section?.sectionId).toBe(1);
    expect(resolveLaboratorySection("FP", SECTIONS).error).toMatch(/unknown laboratory section/i);

    const ambiguousSections: LaboratorySection[] = [
      { ...SECTIONS[0], sectionId: 1, sectionCode: "DUP-A", sectionName: "Shared Lab" },
      { ...SECTIONS[1], sectionId: 2, sectionCode: "DUP-B", sectionName: "Shared Lab" }
    ];
    expect(resolveLaboratorySection("Shared Lab", ambiguousSections).error).toMatch(/ambiguous laboratory section/i);
  });

  it("resolves optional inventory assets by code, label, or unique instrument type", () => {
    expect(resolveInventoryAsset("", INVENTORY)).toEqual({ asset: null, error: null });
    expect(resolveInventoryAsset("ast-inc-42", INVENTORY).asset?.id).toBe(42);
    expect(resolveInventoryAsset("AST-AUTO-43 — Autoclave", INVENTORY).asset?.id).toBe(43);
    expect(resolveInventoryAsset("Binder Incubator", INVENTORY).asset?.id).toBe(42);
    expect(resolveInventoryAsset("Autoclave", INVENTORY).error).toMatch(/ambiguous asset register entry/i);
    expect(resolveInventoryAsset("AST-NOPE", INVENTORY).error).toMatch(/unknown asset register entry/i);
  });

  it("parses optional temperature decimals and calibration dates", () => {
    expect(parseOptionalTemperature("")).toEqual({ value: null, error: null });
    expect(parseOptionalTemperature("  32.5  ")).toEqual({ value: 32.5, error: null });
    expect(parseOptionalTemperature("-20")).toEqual({ value: -20, error: null });
    expect(parseOptionalTemperature("0")).toEqual({ value: 0, error: null });
    for (const bad of ["abc", "1.2.3", "1e2", "32.5 C"]) {
      expect(parseOptionalTemperature(bad).error).not.toBeNull();
    }

    expect(parseOptionalDate("")).toEqual({ value: null, error: null });
    expect(parseOptionalDate("2027-06-15")).toEqual({ value: "2027-06-15", error: null });
    expect(parseOptionalDate("15/06/2027").error).not.toBeNull();
    for (const badDate of ["2026-02-30", "03/04/2027", "next-tuesday"]) {
      expect(parseOptionalDate(badDate).error).not.toBeNull();
    }
  });
});

describe("buildEquipmentPastePlan", () => {
  it("uses the Add Equipment form columns in exact order", () => {
    const plan = buildEquipmentPastePlan({
      existingEquipment: [{ code: "INC-01" }],
      mySections: SECTIONS,
      inventoryList: INVENTORY
    });
    expect(plan.maxRows).toBe(MAX_EQUIPMENT_BATCH_ROWS);
    expect(plan.columns.map((c) => c.label)).toEqual([
      "Equipment name",
      "Equipment code",
      "Equipment type",
      "Vendor / Manufacturer",
      "Laboratory section",
      "Location",
      "Asset register entry",
      "Connection settings",
      "Set point temp (°C)",
      "Calibration due date"
    ]);
  });

  it("flags existing codes (exact trimmed match) and flags duplicate codes inside the paste on both rows", () => {
    const plan = buildEquipmentPastePlan({
      existingEquipment: [{ code: "INC-01" }],
      mySections: [SECTIONS[0]],
      inventoryList: INVENTORY
    });

    const codeCol = plan.columns[1];
    expect(codeCol.check("  INC-01  ")).toBe("Already exists.");
    expect(codeCol.check("inc-01")).toBeNull();
    expect(codeCol.check("INC-02")).toBeNull();

    const matched = plan.match!([
      ["Inc A", "EQ-DUP", "Incubator"],
      ["Inc B", "EQ-OK", "Incubator"],
      ["Inc C", "eq-dup", "Autoclave"]
    ]);
    expect(matched.rows[0].problem).toMatch(/duplicate equipment code/i);
    expect(matched.rows[1].problem).toBeUndefined();
    expect(matched.rows[2].problem).toMatch(/duplicate equipment code/i);
  });

  it("maps accepted rows into CreateEquipmentRequest payloads without cdsSoftware", () => {
    const pending = toPendingEquipmentRows(
      [
        {
          target: null,
          values: {
            name: "  Incubator 2  ",
            code: "  INC-02 ",
            type: "Incubator",
            vendor: " Memmert ",
            section: "MICRO",
            location: " Room 102 ",
            equipmentInventory: "AST-INC-42 — Binder Incubator",
            connectionSettings: " COM3 ",
            setPointTemperature: "37.0",
            calibrationDueDate: "2027-03-15"
          }
        },
        {
          target: null,
          values: {
            name: "Water Bath 1",
            code: "WB-01",
            type: "Water Bath",
            vendor: "",
            section: "",
            location: "",
            equipmentInventory: "",
            connectionSettings: "",
            setPointTemperature: "",
            calibrationDueDate: ""
          }
        }
      ],
      [SECTIONS[0]],
      INVENTORY
    );

    const firstPayload = toCreateEquipmentPayload(pending[0]);
    expect(firstPayload).toEqual({
      name: "Incubator 2",
      code: "INC-02",
      type: "Incubator",
      vendor: "Memmert",
      sectionId: 1,
      location: "Room 102",
      equipmentInventoryId: 42,
      connectionSettings: "COM3",
      setPointTemperature: 37,
      calibrationDueDate: "2027-03-15"
    });
    expect("cdsSoftware" in firstPayload).toBe(false);

    const secondPayload = toCreateEquipmentPayload(pending[1]);
    expect(secondPayload).toEqual({
      name: "Water Bath 1",
      code: "WB-01",
      type: "WaterBath",
      vendor: null,
      location: null,
      equipmentInventoryId: null,
      connectionSettings: null,
      setPointTemperature: null,
      calibrationDueDate: null
    });
    expect("sectionId" in secondPayload).toBe(false);
    expect("cdsSoftware" in secondPayload).toBe(false);
  });
});
