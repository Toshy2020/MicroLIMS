import { renderHook, act } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import {
  createInitialHplcMethodFormState,
  formToSaveRequest,
  hplcMethodEntityToFormState
} from "./hplcMethodForm";
import { validateHplcMethodForm } from "./hplcMethodValidation";
import { useHplcMethodDialogState } from "./useHplcMethodDialogState";
import type { HplcMethodResponse } from "../services/HplcMethodService";

describe("Cleaning Residue HPLC Method Form & Validation", () => {
  const baseValidForm = () => {
    const form = createInitialHplcMethodFormState(1, "Hplc");
    form.name = "Paracetamol Cleaning Residue by HPLC";
    form.abbreviation = "PARA-CR-01";
    form.columnDesignation = "L1";
    form.columnLength = "150";
    form.columnInternalDiameterMm = "4.6";
    form.particleSizeUm = "5";
    form.columnTemperatureC = "30";
    form.flowRateMlPerMin = "1.0";
    form.injectionVolumeUl = "20";
    form.runTimeMin = "15";
    form.diluentSolutionId = 2;
    form.mobilePhases = [{ channel: "A", solutionMasterId: 10, ratioPercent: "" }];
    form.resultMode = "CleaningResidue";
    form.productItemId = 42;
    form.productItemName = "Paracetamol 500mg Tablets";
    form.sampleSolutionVolumeMl = "25";
    form.swabAreaCm2 = "100";
    form.analytes = [
      {
        name: "Paracetamol",
        wavelengthNm: "243",
        standardEntryId: 7,
        theoreticalWeightStdMg: "0",
        theoreticalWeightTestMg: "0",
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "2.0",
        sstMinResolution: "",
        sstMaxTailingFactor: "2.0",
        sstMinTheoreticalPlates: "2000",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: "10.0",
        macoLimit: "50",
        macoUnit: "UgPerSwab"
      }
    ];
    return form;
  };

  it("converts a Cleaning Residue form to a valid SaveHplcMethodRequest", () => {
    const form = baseValidForm();
    form.analytes[0].macoUnit = "UgPerCm2";
    form.analytes[0].macoLimit = "0.5";

    const req = formToSaveRequest(form, 1);

    expect(req.technique).toBe("Hplc");
    expect(req.resultMode).toBe("CleaningResidue");
    expect(req.productItemId).toBe(42);
    expect(req.sampleSolutionVolumeMl).toBe(25);
    expect(req.swabAreaCm2).toBe(100);
    expect(req.impurities).toEqual([]);
    expect(req.referenceSolutionPercent).toBeNull();
    expect(req.disregardLimitPercent).toBeNull();

    expect(req.analytes).toHaveLength(1);
    expect(req.analytes[0].name).toBe("Paracetamol");
    expect(req.analytes[0].wavelengthNm).toBe(243);
    expect(req.analytes[0].standardConcentrationUgPerMl).toBe(10);
    expect(req.analytes[0].macoLimit).toBe(0.5);
    expect(req.analytes[0].macoUnit).toBe("UgPerCm2");
    expect(req.analytes[0].standardInjections).toBe(5);
    expect(req.analytes[0].theoreticalWeightStdMg).toBe(0);
    expect(req.analytes[0].theoreticalWeightTestMg).toBe(0);
  });

  it("restores form state from an existing Cleaning Residue entity", () => {
    const entity: HplcMethodResponse = {
      id: 99,
      sectionId: 1,
      technique: "Hplc",
      name: "Ibuprofen Swab Cleaning Residue",
      abbreviation: "IBU-CR-01",
      effectiveDate: "2026-11-07T00:00:00Z",
      detectorType: "UV",
      columnDesignation: "L1",
      columnLengthMm: 150,
      columnInternalDiameterMm: 4.6,
      particleSizeUm: 5,
      columnTemperatureC: 35,
      flowRateMlPerMin: 1.5,
      injectionVolumeUl: 10,
      runTimeMin: 20,
      diluentSolutionId: 3,
      mobilePhases: [],
      gradientSteps: [],
      elutionMode: "Isocratic",
      analytes: [
        {
          id: 101,
          displayOrder: 0,
          name: "Ibuprofen",
          wavelengthNm: 220,
          standardEntryId: 12,
          standardEntryCode: "IBU-STD",
          theoreticalWeightStdMg: 0,
          theoreticalWeightTestMg: 0,
          standardDilution: null,
          standardInjections: 6,
          sstMaxRsdPercent: 1.5,
          sstMinResolution: null,
          sstMaxTailingFactor: null,
          sstMinTheoreticalPlates: null,
          sstMinRetentionFactor: null,
          sstMinSignalToNoise: null,
          sstMinPeakToValley: null,
          standardConcentrationUgPerMl: 15.0,
          macoLimit: 25.0,
          macoUnit: "UgPerSwab"
        }
      ],
      resultMode: "CleaningResidue",
      referenceSolutionPercent: null,
      disregardLimitPercent: null,
      internalStandardName: null,
      impurities: [],
      productItemId: 88,
      productItemName: "Ibuprofen 400mg Tablets",
      swabAreaCm2: 25,
      sampleSolutionVolumeMl: 50,
      version: 1,
      isActive: true,
      createdAt: "2026-11-07T00:00:00Z"
    };

    const state = hplcMethodEntityToFormState(entity);

    expect(state.resultMode).toBe("CleaningResidue");
    expect(state.productItemId).toBe(88);
    expect(state.productItemName).toBe("Ibuprofen 400mg Tablets");
    expect(state.sampleSolutionVolumeMl).toBe("50");
    expect(state.swabAreaCm2).toBe("25");
    expect(state.analytes).toHaveLength(1);
    expect(state.analytes[0].standardConcentrationUgPerMl).toBe("15");
    expect(state.analytes[0].macoLimit).toBe("25");
    expect(state.analytes[0].macoUnit).toBe("UgPerSwab");
  });

  describe("Validation", () => {
    const validOpts = { isEditing: false, hasMultipleSections: false };

    it("passes for a valid Cleaning Residue method with UgPerSwab", () => {
      const form = baseValidForm();
      const errors = validateHplcMethodForm(form, validOpts);
      expect(errors).toEqual({});
    });

    it("requires Finished Product item", () => {
      const form = baseValidForm();
      form.productItemId = "";
      const errors = validateHplcMethodForm(form, validOpts);
      expect(errors.productItemId).toBe("A finished product item is required for a cleaning-residue method.");
    });

    it("requires positive Extraction Volume (sampleSolutionVolumeMl)", () => {
      const form = baseValidForm();
      form.sampleSolutionVolumeMl = "";
      let errors = validateHplcMethodForm(form, validOpts);
      expect(errors.sampleSolutionVolumeMl).toBe("Extraction volume (mL) must be greater than zero.");

      form.sampleSolutionVolumeMl = "0";
      errors = validateHplcMethodForm(form, validOpts);
      expect(errors.sampleSolutionVolumeMl).toBe("Extraction volume (mL) must be greater than zero.");
    });

    it("requires MACO limit and standard concentration for each analyte", () => {
      const form = baseValidForm();
      form.analytes[0].macoLimit = "";
      form.analytes[0].standardConcentrationUgPerMl = "";
      const errors = validateHplcMethodForm(form, validOpts);
      expect(errors["analytes.0.macoLimit"]).toBe('Analyte "Paracetamol" MACO limit must be greater than zero.');
      expect(errors["analytes.0.standardConcentrationUgPerMl"]).toBe(
        'Analyte "Paracetamol" Standard Concentration (µg/mL) must be greater than zero.'
      );
    });

    it("requires MACO unit for each analyte", () => {
      const form = baseValidForm();
      form.analytes[0].macoUnit = "" as any;
      const errors = validateHplcMethodForm(form, validOpts);
      expect(errors["analytes.0.macoUnit"]).toBe('Analyte "Paracetamol" MACO unit is required.');
    });

    it("requires Swabbed Area (cm²) when any analyte uses UgPerCm2", () => {
      const form = baseValidForm();
      form.analytes[0].macoUnit = "UgPerCm2";
      form.swabAreaCm2 = "";
      let errors = validateHplcMethodForm(form, validOpts);
      expect(errors.swabAreaCm2).toBe("Swabbed area (cm²) must be greater than zero when an analyte uses µg/cm².");

      form.swabAreaCm2 = "0";
      errors = validateHplcMethodForm(form, validOpts);
      expect(errors.swabAreaCm2).toBe("Swabbed area (cm²) must be greater than zero when an analyte uses µg/cm².");

      form.swabAreaCm2 = "100";
      errors = validateHplcMethodForm(form, validOpts);
      expect(errors.swabAreaCm2).toBeUndefined();
    });

    it("does not require Swabbed Area when all analytes use UgPerSwab", () => {
      const form = baseValidForm();
      form.analytes[0].macoUnit = "UgPerSwab";
      form.swabAreaCm2 = "";
      const errors = validateHplcMethodForm(form, validOpts);
      expect(errors.swabAreaCm2).toBeUndefined();
    });
  });

  describe("Dialog state transitions", () => {
    it("resets resultMode to Assay when switching from Hplc to Gc", () => {
      const { result } = renderHook(() =>
        useHplcMethodDialogState({
          open: true,
          editingId: null,
          solutions: [],
          materialMasters: [],
          mySections: [{ sectionId: 1, sectionName: "QC" } as any],
          onClose: vi.fn(),
          onSuccess: vi.fn()
        })
      );

      act(() => {
        result.current.handleResultModeChange("CleaningResidue");
      });
      expect(result.current.form.resultMode).toBe("CleaningResidue");

      act(() => {
        result.current.handleTechniqueChange("Gc");
      });
      expect(result.current.form.technique).toBe("Gc");
      expect(result.current.form.resultMode).toBe("Assay");
    });
  });
});
