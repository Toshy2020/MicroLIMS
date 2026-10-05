import { describe, expect, it } from "vitest";
import {
  createInitialIcpMethodFormState,
  formToSaveRequest,
  icpMethodEntityToFormState,
  normaliseSymbol
} from "./icpMethodForm";
import { validateIcpMethodForm } from "./icpMethodValidation";
import type { IcpMethodResponse } from "../services/IcpMethodService";

describe("ICP Method Form & Validation (Contract Slice I1b-1)", () => {
  it("normalises element symbol casing correctly", () => {
    expect(normaliseSymbol("zn")).toBe("Zn");
    expect(normaliseSymbol("PB")).toBe("Pb");
    expect(normaliseSymbol("cd")).toBe("Cd");
    expect(normaliseSymbol("As")).toBe("As");
    expect(normaliseSymbol("fe")).toBe("Fe");
  });

  it("creates initial form state with valid defaults", () => {
    const form = createInitialIcpMethodFormState(1);
    expect(form.mode).toBe("MineralAssay");
    expect(form.sectionId).toBe(1);
    expect(form.standardLevelsMgPerL).toBe("0.1, 0.5, 1, 3, 6");
    expect(form.minCorrelation).toBe("0.995");
    expect(form.sampleVolumeMl).toBe("10");
    expect(form.dilutionFactor).toBe("1");
    expect(form.maxCalibrationAgeHours).toBe("24");
    expect(form.requireBlank).toBe(false);
    expect(form.requireIcv).toBe(false);
    expect(form.requireCcv).toBe(false);
    expect(form.elements).toHaveLength(1);
    expect(form.elements[0].symbol).toBe("Zn");
    expect(form.elements[0].conversionFactor).toBe("1");
  });

  it("nulls out optional-check fields when toggles are OFF in formToSaveRequest", () => {
    const form = createInitialIcpMethodFormState(1);
    form.name = "Mineral Assay by ICP";
    form.abbreviation = "ICP-MIN-01";
    form.effectiveDate = "2026-10-04";
    form.calibrationStandardEntryId = 101;
    // Enter dummy values while toggles are false
    form.requireBlank = false;
    form.blankMaxMgPerL = "0.05";
    form.requireIcv = false;
    form.icvStandardEntryId = 102;
    form.icvNominalMgPerL = "2.0";
    form.icvRecoveryLowPercent = "90";
    form.icvRecoveryHighPercent = "110";
    form.requireCcv = false;
    form.ccvNominalMgPerL = "1.0";
    form.ccvRecoveryLowPercent = "90";
    form.ccvRecoveryHighPercent = "110";

    const req = formToSaveRequest(form, 1);

    expect(req.requireBlank).toBe(false);
    expect(req.blankMaxMgPerL).toBeNull();

    expect(req.requireIcv).toBe(false);
    expect(req.icvStandardEntryId).toBeNull();
    expect(req.icvNominalMgPerL).toBeNull();
    expect(req.icvRecoveryLowPercent).toBeNull();
    expect(req.icvRecoveryHighPercent).toBeNull();

    expect(req.requireCcv).toBe(false);
    expect(req.ccvNominalMgPerL).toBeNull();
    expect(req.ccvRecoveryLowPercent).toBeNull();
    expect(req.ccvRecoveryHighPercent).toBeNull();

    expect(req.effectiveDate).toBe("2026-10-04T00:00:00Z");
  });

  it("preserves optional-check values when toggles are ON and keeps element ids on edit", () => {
    const form = createInitialIcpMethodFormState(1);
    form.name = "Elemental Impurities by ICP-OES";
    form.abbreviation = "ICP-IMP-01";
    form.mode = "ElementalImpurities";
    form.effectiveDate = "2026-10-05";
    form.calibrationStandardEntryId = 201;

    // Optional checks ON
    form.requireBlank = true;
    form.blankMaxMgPerL = "0.02";

    form.requireIcv = true;
    form.icvStandardEntryId = 202;
    form.icvNominalMgPerL = "1.5";
    form.icvRecoveryLowPercent = "95";
    form.icvRecoveryHighPercent = "105";

    form.requireCcv = true;
    form.ccvNominalMgPerL = "1.0";
    form.ccvRecoveryLowPercent = "90";
    form.ccvRecoveryHighPercent = "110";

    // Elements: one existing element with id 55, one newly added element without id
    form.elements = [
      {
        id: 55,
        symbol: "pb",
        wavelengthNm: "220.353",
        view: "Axial",
        conversionFactor: "1.0"
      },
      {
        symbol: "cd",
        wavelengthNm: "228.802",
        view: "Radial",
        conversionFactor: "1.25"
      }
    ];

    const req = formToSaveRequest(form, 1);

    expect(req.requireBlank).toBe(true);
    expect(req.blankMaxMgPerL).toBe(0.02);

    expect(req.requireIcv).toBe(true);
    expect(req.icvStandardEntryId).toBe(202);
    expect(req.icvNominalMgPerL).toBe(1.5);
    expect(req.icvRecoveryLowPercent).toBe(95);
    expect(req.icvRecoveryHighPercent).toBe(105);

    expect(req.requireCcv).toBe(true);
    expect(req.ccvNominalMgPerL).toBe(1.0);
    expect(req.ccvRecoveryLowPercent).toBe(90);
    expect(req.ccvRecoveryHighPercent).toBe(110);

    // Elements verification: element 0 keeps id 55, element 1 has null id
    expect(req.elements).toHaveLength(2);
    expect(req.elements[0]).toEqual({
      id: 55,
      symbol: "Pb",
      wavelengthNm: 220.353,
      view: "Axial",
      conversionFactor: 1.0
    });
    expect(req.elements[1]).toEqual({
      id: null,
      symbol: "Cd",
      wavelengthNm: 228.802,
      view: "Radial",
      conversionFactor: 1.25
    });
  });

  it("converts entity to form state preserving all element ids and fields", () => {
    const entity: IcpMethodResponse = {
      id: 10,
      version: 2,
      sectionId: 1,
      sectionName: "Physicochemical",
      name: "Minerals Test",
      abbreviation: "ICP-MIN",
      effectiveDate: "2026-10-04T00:00:00Z",
      isActive: true,
      mode: "MineralAssay",
      standardLevelsMgPerL: "0.1, 0.5, 1, 3, 6",
      calibrationStandardEntryId: 300,
      minCorrelation: 0.999,
      sampleVolumeMl: 10,
      dilutionFactor: 2.5,
      maxCalibrationAgeHours: 48,
      requireBlank: true,
      blankMaxMgPerL: 0.01,
      requireIcv: true,
      icvStandardEntryId: 301,
      icvNominalMgPerL: 2.0,
      icvRecoveryLowPercent: 92,
      icvRecoveryHighPercent: 108,
      requireCcv: false,
      ccvNominalMgPerL: null,
      ccvRecoveryLowPercent: null,
      ccvRecoveryHighPercent: null,
      elements: [
        {
          id: 701,
          displayOrder: 1,
          symbol: "Zn",
          wavelengthNm: 213.857,
          view: "Axial",
          conversionFactor: 1
        },
        {
          id: 702,
          displayOrder: 2,
          symbol: "Cu",
          wavelengthNm: 324.754,
          view: "Radial",
          conversionFactor: 1
        }
      ],
      createdAt: "2026-10-01T00:00:00Z",
      lastModifiedAt: "2026-10-02T00:00:00Z"
    };

    const form = icpMethodEntityToFormState(entity);

    expect(form.name).toBe("Minerals Test");
    expect(form.abbreviation).toBe("ICP-MIN");
    expect(form.effectiveDate).toBe("2026-10-04");
    expect(form.minCorrelation).toBe("0.999");
    expect(form.sampleVolumeMl).toBe("10");
    expect(form.dilutionFactor).toBe("2.5");
    expect(form.maxCalibrationAgeHours).toBe("48");
    expect(form.requireBlank).toBe(true);
    expect(form.blankMaxMgPerL).toBe("0.01");
    expect(form.requireIcv).toBe(true);
    expect(form.icvStandardEntryId).toBe(301);
    expect(form.elements).toHaveLength(2);
    expect(form.elements[0].id).toBe(701);
    expect(form.elements[1].id).toBe(702);
  });

  it("validates required fields and domain rules", () => {
    const invalidForm = createInitialIcpMethodFormState(1);
    invalidForm.name = "";
    invalidForm.abbreviation = "invalid!";
    invalidForm.standardLevelsMgPerL = "0.5"; // only 1 level
    invalidForm.minCorrelation = "1.5"; // > 1
    invalidForm.maxCalibrationAgeHours = "200"; // > 168
    invalidForm.sampleVolumeMl = "0"; // <= 0
    invalidForm.dilutionFactor = "0.5"; // < 1
    invalidForm.requireIcv = true;
    invalidForm.calibrationStandardEntryId = 100;
    invalidForm.icvStandardEntryId = 100; // Same as calibration (not 2nd source)
    invalidForm.elements = [
      {
        symbol: "TOOLONG", // > 3 chars
        wavelengthNm: "0",
        view: "Axial",
        conversionFactor: "-1"
      }
    ];

    const errors = validateIcpMethodForm(invalidForm);

    expect(errors.name).toBe("Method name is required.");
    expect(errors.abbreviation).toBe("Abbreviation must be 2-20 uppercase letters, digits or hyphens.");
    expect(errors.standardLevelsMgPerL).toBe("At least two standard levels are required.");
    expect(errors.minCorrelation).toBe("Minimum correlation must be greater than 0 and at most 1.");
    expect(errors.maxCalibrationAgeHours).toBe("Calibration age must be between 1 and 168 hours.");
    expect(errors.sampleVolumeMl).toBe("Sample volume must be greater than zero.");
    expect(errors.dilutionFactor).toBe("Dilution factor must be 1 or more.");
    expect(errors.icvStandardEntryId).toBe("The ICV standard must be a second source, not the calibration standard.");
    expect(errors.element_0_symbol).toBe("Element symbol must be 1-3 letters.");
    expect(errors.element_0_wavelengthNm).toContain("wavelength must be greater than zero");
    expect(errors.element_0_conversionFactor).toContain("conversion factor must be greater than zero");
  });
});
