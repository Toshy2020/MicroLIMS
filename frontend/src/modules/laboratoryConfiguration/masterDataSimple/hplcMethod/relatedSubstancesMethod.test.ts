import { renderHook, act } from "@testing-library/react";
import { useHplcMethodDialogState } from "./useHplcMethodDialogState";
import { describe, expect, it } from "vitest";
import {
  createInitialHplcMethodFormState,
  formToSaveRequest,
  hplcMethodEntityToFormState
} from "./hplcMethodForm";
import { validateHplcMethodForm } from "./hplcMethodValidation";
import type { HplcMethodResponse } from "../services/HplcMethodService";

describe("Related Substances & Internal Standard Method Form & Validation (Task 6)", () => {
  it("converts an RS form to a save request with impurities and reference/disregard values", () => {
    const form = createInitialHplcMethodFormState(1, "Hplc");
    form.name = "Sildenafil Related Substances by HPLC";
    form.abbreviation = "SIL-RS-01";
    form.columnDesignation = "L1";
    form.columnLength = "150";
    form.columnInternalDiameterMm = "4.6";
    form.particleSizeUm = "5";
    form.columnTemperatureC = "30";
    form.flowRateMlPerMin = "1.0";
    form.injectionVolumeUl = "20";
    form.runTimeMin = "35";
    form.diluentSolutionId = 2;
    form.resultMode = "RelatedSubstances";
    form.referenceSolutionPercent = "0.1";
    form.disregardLimitPercent = "0.05";
    form.impurities = [
      { name: "Impurity D", relativeRetention: "0.15", correctionFactor: "0.7" },
      { name: "Impurity A", relativeRetention: "1.25", correctionFactor: "1.0" }
    ];
    form.analytes = [
      {
        name: "Sildenafil Principal Peak",
        wavelengthNm: "230",
        standardEntryId: 5,
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
        standardConcentrationUgPerMl: ""
      }
    ];

    const req = formToSaveRequest(form, 1);
    expect(req.technique).toBe("Hplc");
    expect(req.resultMode).toBe("RelatedSubstances");
    expect(req.referenceSolutionPercent).toBe(0.1);
    expect(req.disregardLimitPercent).toBe(0.05);
    expect(req.internalStandardName).toBeNull();
    expect(req.impurities).toEqual([
      { id: null, name: "Impurity D", relativeRetention: 0.15, correctionFactor: 0.7 },
      { id: null, name: "Impurity A", relativeRetention: 1.25, correctionFactor: 1.0 }
    ]);
    expect(req.analytes).toHaveLength(1);
    expect(req.analytes[0].name).toBe("Sildenafil Principal Peak");
    expect(req.analytes[0].wavelengthNm).toBe(230);
    expect(req.analytes[0].theoreticalWeightStdMg).toBe(0);
    expect(req.analytes[0].theoreticalWeightTestMg).toBe(0);
    expect(req.analytes[0].standardDilution).toBeNull();
    expect(req.analytes[0].standardInjections).toBe(5);
  });

  it("converts an Assay form with internalStandardName to save request", () => {
    const form = createInitialHplcMethodFormState(1, "Hplc");
    form.name = "Pyridoxine HCl Assay by HPLC";
    form.abbreviation = "PYR-ASSAY";
    form.columnDesignation = "L1";
    form.columnLength = "250";
    form.columnInternalDiameterMm = "4.6";
    form.particleSizeUm = "5";
    form.columnTemperatureC = "25";
    form.flowRateMlPerMin = "1.5";
    form.injectionVolumeUl = "10";
    form.runTimeMin = "15";
    form.diluentSolutionId = 2;
    form.resultMode = "Assay";
    form.internalStandardName = "p-hydroxybenzoic acid";
    form.analytes = [
      {
        name: "Pyridoxine HCl",
        wavelengthNm: "280",
        standardEntryId: 8,
        theoreticalWeightStdMg: "50",
        theoreticalWeightTestMg: "50",
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "1.5",
        sstMinResolution: "",
        sstMaxTailingFactor: "2.0",
        sstMinTheoreticalPlates: "2000",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: ""
      }
    ];

    const req = formToSaveRequest(form, 1);
    expect(req.resultMode).toBe("Assay");
    expect(req.internalStandardName).toBe("p-hydroxybenzoic acid");
    expect(req.referenceSolutionPercent).toBeNull();
    expect(req.disregardLimitPercent).toBeNull();
    expect(req.impurities).toEqual([]);
    expect(req.analytes[0].theoreticalWeightStdMg).toBe(50);
  });

  it("converts entity back to form state for RelatedSubstances and internalStandardName", () => {
    const entity: HplcMethodResponse = {
      id: 50,
      sectionId: 1,
      name: "Sildenafil RS",
      abbreviation: "SIL-RS",
      effectiveDate: "2026-10-27T00:00:00Z",
      isActive: true,
      columnDesignation: "L1",
      columnLengthMm: 150,
      columnInternalDiameterMm: 4.6,
      particleSizeUm: 5,
      columnTemperatureC: 30,
      elutionMode: "Isocratic",
      flowRateMlPerMin: 1.0,
      detectorType: "UV",
      injectionVolumeUl: 20,
      runTimeMin: 35,
      diluentSolutionId: 2,
      mobilePhases: [],
      gradientSteps: [],
      technique: "Hplc",
      resultMode: "RelatedSubstances",
      referenceSolutionPercent: 0.1,
      disregardLimitPercent: 0.05,
      impurities: [
        { id: 101, displayOrder: 1, name: "Impurity D", relativeRetention: 0.15, correctionFactor: 0.7 },
        { id: 102, displayOrder: 2, name: "Impurity A", relativeRetention: 1.25, correctionFactor: 1.0 }
      ],
      analytes: [
        {
          id: 201,
          displayOrder: 1,
          name: "Sildenafil Peak",
          wavelengthNm: 230,
          standardEntryId: 5,
          standardEntryCode: "STD-SIL",
          theoreticalWeightStdMg: 0,
          theoreticalWeightTestMg: 0,
          standardInjections: 5
        }
      ]
    };

    const form = hplcMethodEntityToFormState(entity);
    expect(form.resultMode).toBe("RelatedSubstances");
    expect(form.referenceSolutionPercent).toBe("0.1");
    expect(form.disregardLimitPercent).toBe("0.05");
    expect(form.impurities).toMatchObject([
      { id: 101, name: "Impurity D", relativeRetention: "0.15", correctionFactor: "0.7" },
      { id: 102, name: "Impurity A", relativeRetention: "1.25", correctionFactor: "1" }
    ]);
  });

  it("validates RS rules: exactly one analyte, no theoretical weights, reference percent, disregard limit, impurities", () => {
    const form = createInitialHplcMethodFormState(1, "Hplc");
    form.name = "RS Method";
    form.abbreviation = "RS-01";
    form.columnDesignation = "L1";
    form.columnLength = "150";
    form.columnInternalDiameterMm = "4.6";
    form.particleSizeUm = "5";
    form.columnTemperatureC = "25";
    form.flowRateMlPerMin = "1.0";
    form.injectionVolumeUl = "10";
    form.runTimeMin = "20";
    form.diluentSolutionId = 1;
    form.resultMode = "RelatedSubstances";

    // 1. Two analytes
    form.analytes = [
      {
        name: "Peak 1",
        wavelengthNm: "230",
        standardEntryId: 1,
        theoreticalWeightStdMg: "0",
        theoreticalWeightTestMg: "0",
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "",
        sstMinResolution: "",
        sstMaxTailingFactor: "",
        sstMinTheoreticalPlates: "",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: ""
      },
      {
        name: "Peak 2",
        wavelengthNm: "230",
        standardEntryId: 1,
        theoreticalWeightStdMg: "0",
        theoreticalWeightTestMg: "0",
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "",
        sstMinResolution: "",
        sstMaxTailingFactor: "",
        sstMinTheoreticalPlates: "",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: ""
      }
    ];
    form.referenceSolutionPercent = "0.1";
    form.disregardLimitPercent = "0.05";
    let errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain(
      "A related-substances method has exactly one analyte: the principal peak."
    );

    // 2. Single analyte with theoretical weights given
    form.analytes = [
      {
        name: "Peak 1",
        wavelengthNm: "230",
        standardEntryId: 1,
        theoreticalWeightStdMg: "50",
        theoreticalWeightTestMg: "50",
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "",
        sstMinResolution: "",
        sstMaxTailingFactor: "",
        sstMinTheoreticalPlates: "",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: ""
      }
    ];
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain(
      "Theoretical weights are not used in a related-substances method."
    );

    // 3. Reference solution percent missing, <= 0 or > 100
    form.analytes[0].theoreticalWeightStdMg = "0";
    form.analytes[0].theoreticalWeightTestMg = "0";
    form.referenceSolutionPercent = "";
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain(
      "Enter the reference solution concentration as % of the test solution (above 0, up to 100)."
    );

    form.referenceSolutionPercent = "105";
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain(
      "Enter the reference solution concentration as % of the test solution (above 0, up to 100)."
    );

    // 4. Disregard limit negative
    form.referenceSolutionPercent = "0.1";
    form.disregardLimitPercent = "-0.01";
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain("The disregard limit must be between 0 and 100 %.");

    // 5. Impurity blank or duplicate name, or CF <= 0
    form.disregardLimitPercent = "0.05";
    form.impurities = [
      { name: "Impurity A", relativeRetention: "0.5", correctionFactor: "1.0" },
      { name: "impurity a", relativeRetention: "0.8", correctionFactor: "0.5" }
    ];
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain("Each impurity needs a unique name.");

    form.impurities = [
      { name: "Impurity A", relativeRetention: "0.5", correctionFactor: "0" }
    ];
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain("The correction factor must be greater than zero and at most 100.");
  });

  it("validates that Related Substances is refused for GC, and RS values refused for Assay", () => {
    const gcForm = createInitialHplcMethodFormState(1, "Gc");
    gcForm.name = "GC RS";
    gcForm.abbreviation = "GC-RS";
    gcForm.columnDesignation = "G43";
    gcForm.columnLength = "30";
    gcForm.columnInternalDiameterMm = "0.32";
    gcForm.filmThicknessUm = "1.8";
    gcForm.carrierGas = "Nitrogen";
    gcForm.inletTemperatureC = "200";
    gcForm.detectorTemperatureC = "250";
    gcForm.diluentSolutionId = 1;
    gcForm.resultMode = "RelatedSubstances";

    let errors = validateHplcMethodForm(gcForm, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain("Related substances is available for HPLC methods only.");

    // Assay method with impurities or referenceSolutionPercent
    const hplcAssay = createInitialHplcMethodFormState(1, "Hplc");
    hplcAssay.name = "HPLC Assay";
    hplcAssay.abbreviation = "HPLC-ASSAY";
    hplcAssay.columnDesignation = "L1";
    hplcAssay.diluentSolutionId = 1;
    hplcAssay.resultMode = "Assay";
    hplcAssay.referenceSolutionPercent = "0.1";
    errors = validateHplcMethodForm(hplcAssay, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain(
      "Impurities and reference-solution values belong to related-substances methods only."
    );
  });

  it("validates that internalStandardName is allowed only for HPLC Assay methods", () => {
    const gcForm = createInitialHplcMethodFormState(1, "Gc");
    gcForm.name = "GC Assay";
    gcForm.abbreviation = "GC-ASSAY";
    gcForm.columnDesignation = "G43";
    gcForm.diluentSolutionId = 1;
    gcForm.internalStandardName = "Internal Std";
    let errors = validateHplcMethodForm(gcForm, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain(
      "An internal standard applies to HPLC assay methods only."
    );

    const rsForm = createInitialHplcMethodFormState(1, "Hplc");
    rsForm.name = "HPLC RS";
    rsForm.abbreviation = "HPLC-RS";
    rsForm.columnDesignation = "L1";
    rsForm.diluentSolutionId = 1;
    rsForm.resultMode = "RelatedSubstances";
    rsForm.referenceSolutionPercent = "0.1";
    rsForm.analytes = [
      {
        name: "Peak 1",
        wavelengthNm: "230",
        standardEntryId: 1,
        theoreticalWeightStdMg: "0",
        theoreticalWeightTestMg: "0",
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "",
        sstMinResolution: "",
        sstMaxTailingFactor: "",
        sstMinTheoreticalPlates: "",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: ""
      }
    ];
    rsForm.internalStandardName = "Internal Std";
    errors = validateHplcMethodForm(rsForm, { isEditing: false, hasMultipleSections: false });
    expect(Object.values(errors)).toContain(
      "An internal standard applies to HPLC assay methods only."
    );
  });
  it("clears RS and IS fields when technique is switched in useHplcMethodDialogState", () => {
    const { result } = renderHook(() =>
      useHplcMethodDialogState({
        open: true,
        editingId: null,
        solutions: [],
        materialMasters: [],
        mySections: [],
        onSuccess: () => {},
        onClose: () => {}
      })
    );

    act(() => {
      result.current.updateField("resultMode", "RelatedSubstances");
      result.current.updateField("referenceSolutionPercent", "0.5");
      result.current.updateField("disregardLimitPercent", "0.05");
      result.current.updateField("impurities", [
        { name: "Impurity A", relativeRetention: "0.5", correctionFactor: "1.0" }
      ]);
      result.current.updateField("internalStandardName", "IS-1");
    });

    expect(result.current.form.referenceSolutionPercent).toBe("0.5");
    expect(result.current.form.disregardLimitPercent).toBe("0.05");
    expect(result.current.form.impurities).toHaveLength(1);
    expect(result.current.form.internalStandardName).toBe("IS-1");

    // Switch technique to Gc
    act(() => {
      result.current.handleTechniqueChange("Gc");
    });

    expect(result.current.form.technique).toBe("Gc");
    expect(result.current.form.referenceSolutionPercent).toBe("");
    expect(result.current.form.disregardLimitPercent).toBe("");
    expect(result.current.form.impurities).toEqual([]);
    expect(result.current.form.internalStandardName).toBe("");

    // Switch technique back to Hplc
    act(() => {
      result.current.handleTechniqueChange("Hplc");
    });

    expect(result.current.form.technique).toBe("Hplc");
    expect(result.current.form.referenceSolutionPercent).toBe("");
    expect(result.current.form.disregardLimitPercent).toBe("");
    expect(result.current.form.impurities).toEqual([]);
    expect(result.current.form.internalStandardName).toBe("");
  });

  it("validates server-matching upper-bound and format messages", () => {
    const form = createInitialHplcMethodFormState(1, "Hplc");
    form.name = "RS Method";
    form.abbreviation = "RS-01";
    form.columnDesignation = "L1";
    form.diluentSolutionId = 1;
    form.resultMode = "RelatedSubstances";
    form.referenceSolutionPercent = "0.1";
    form.analytes = [
      {
        name: "Principal Peak",
        wavelengthNm: "230",
        standardEntryId: 1,
        theoreticalWeightStdMg: "0",
        theoreticalWeightTestMg: "0",
        standardDilution: "",
        standardInjections: "5",
        sstMaxRsdPercent: "",
        sstMinResolution: "",
        sstMaxTailingFactor: "",
        sstMinTheoreticalPlates: "",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: ""
      }
    ];

    // Disregard limit > 100
    form.disregardLimitPercent = "105";
    let errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(errors["disregardLimitPercent"]).toBe("The disregard limit must be between 0 and 100 %.");

    // Disregard limit < 0
    form.disregardLimitPercent = "-1";
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(errors["disregardLimitPercent"]).toBe("The disregard limit must be between 0 and 100 %.");

    form.disregardLimitPercent = "0.05";

    // Correction factor > 100
    form.impurities = [
      { name: "Impurity A", relativeRetention: "0.5", correctionFactor: "105" }
    ];
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(errors["impurities.0.correctionFactor"]).toBe(
      "The correction factor must be greater than zero and at most 100."
    );

    // Relative retention > 100
    form.impurities = [
      { name: "Impurity A", relativeRetention: "105", correctionFactor: "1.0" }
    ];
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(errors["impurities.0.relativeRetention"]).toBe(
      "The relative retention must be greater than zero and at most 100."
    );

    // Impurity name > 100 chars
    form.impurities = [
      { name: "A".repeat(101), relativeRetention: "0.5", correctionFactor: "1.0" }
    ];
    errors = validateHplcMethodForm(form, { isEditing: false, hasMultipleSections: false });
    expect(errors["impurities.0.name"]).toBe("Impurity names are at most 100 characters.");

    // Internal standard name > 100 chars
    const assayForm = createInitialHplcMethodFormState(1, "Hplc");
    assayForm.name = "Assay Method";
    assayForm.abbreviation = "AS-01";
    assayForm.columnDesignation = "L1";
    assayForm.diluentSolutionId = 1;
    assayForm.resultMode = "Assay";
    assayForm.internalStandardName = "B".repeat(101);
    errors = validateHplcMethodForm(assayForm, { isEditing: false, hasMultipleSections: false });
    expect(errors["internalStandardName"]).toBe("Internal standard name cannot exceed 100 characters.");
  });

  it("preserves impurity id on entity-to-form conversion and maps it to save request", () => {
    const entity: HplcMethodResponse = {
      id: 50,
      version: 1,
      isActive: true,
      technique: "Hplc",
      resultMode: "RelatedSubstances",
      name: "RS Method with Existing Impurities",
      abbreviation: "RS-EXIST",
      effectiveDate: "2026-01-01",
      columnDesignation: "L1",
      columnLengthMm: 150,
      columnInternalDiameterMm: 4.6,
      particleSizeUm: 5,
      columnBrand: null,
      columnPartNumber: null,
      columnTemperatureC: 25,
      elutionMode: "Isocratic",
      flowRateMlPerMin: 1.0,
      equilibrationMin: null,
      detectorType: "UV",
      injectionVolumeUl: 10,
      runTimeMin: 20,
      diluentSolutionId: 1,
      sectionId: 1,
      sectionName: "QC",
      mobilePhases: [],
      gradientSteps: [],
      analytes: [
        {
          id: 10,
          displayOrder: 0,
          name: "Principal Peak",
          wavelengthNm: 230,
          standardEntryId: 1,
          standardEntryCode: "RS-1",
          theoreticalWeightStdMg: 0,
          theoreticalWeightTestMg: 0,
          standardDilution: null,
          standardInjections: 5,
          sstMaxRsdPercent: null,
          sstMinResolution: null,
          sstMaxTailingFactor: null,
          sstMinTheoreticalPlates: null,
          sstMinRetentionFactor: null,
          sstMinSignalToNoise: null,
          sstMinPeakToValley: null,
          standardConcentrationUgPerMl: null
        }
      ],
      referenceSolutionPercent: 0.2,
      disregardLimitPercent: 0.05,
      impurities: [
        { id: 401, name: "Impurity A", relativeRetention: 0.45, correctionFactor: 1.1 },
        { id: 402, name: "Impurity B", relativeRetention: 1.35, correctionFactor: 0.9 }
      ]
    };

    const form = hplcMethodEntityToFormState(entity);
    expect(form.impurities).toHaveLength(2);
    expect(form.impurities[0].id).toBe(401);
    expect(form.impurities[0].name).toBe("Impurity A");
    expect(form.impurities[1].id).toBe(402);
    expect(form.impurities[1].name).toBe("Impurity B");

    const req = formToSaveRequest(form, 1);
    expect(req.impurities).toEqual([
      { id: 401, name: "Impurity A", relativeRetention: 0.45, correctionFactor: 1.1 },
      { id: 402, name: "Impurity B", relativeRetention: 1.35, correctionFactor: 0.9 }
    ]);
  });
});
