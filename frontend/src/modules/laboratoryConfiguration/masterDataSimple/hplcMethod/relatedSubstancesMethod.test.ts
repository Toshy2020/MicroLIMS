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
    expect(form.impurities).toEqual([
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
    expect(Object.values(errors)).toContain("The disregard limit cannot be negative.");

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
    expect(Object.values(errors)).toContain("The correction factor must be greater than zero.");
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
});
