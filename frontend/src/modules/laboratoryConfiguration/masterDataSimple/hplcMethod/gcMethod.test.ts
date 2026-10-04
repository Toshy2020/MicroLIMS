import { describe, expect, it } from "vitest";
import {
  createInitialHplcMethodFormState,
  formToSaveRequest,
  hplcMethodEntityToFormState
} from "./hplcMethodForm";
import { validateHplcMethodForm, hplcErrorTab } from "./hplcMethodValidation";
import { HplcMethodResponse } from "../services/HplcMethodService";

describe("GC Method Form & Validation (Contract Slice G2a)", () => {
  it("creates initial state correctly for HPLC and GC", () => {
    const hplc = createInitialHplcMethodFormState(1, "Hplc");
    expect(hplc.technique).toBe("Hplc");
    expect(hplc.resultMode).toBe("Assay");
    expect(hplc.columnLength).toBe("150");
    expect(hplc.particleSizeUm).toBe("5");
    expect(hplc.columnTemperatureC).toBe("25");
    expect(hplc.mobilePhases.length).toBeGreaterThan(0);

    const gc = createInitialHplcMethodFormState(1, "Gc");
    expect(gc.technique).toBe("Gc");
    expect(gc.resultMode).toBe("Assay");
    expect(gc.columnLength).toBe("30"); // in metres
    expect(gc.filmThicknessUm).toBe("1.8");
    expect(gc.particleSizeUm).toBe("");
    expect(gc.columnTemperatureC).toBe("");
    expect(gc.mobilePhases.length).toBe(0);
    expect(gc.gradientSteps.length).toBe(0);
    expect(gc.carrierGas).toBe("Nitrogen");
    expect(gc.inletTemperatureC).toBe("200");
    expect(gc.detectorTemperatureC).toBe("250");
    expect(gc.ovenSteps.length).toBe(1);
    expect(gc.ovenSteps[0].rateCPerMin).toBe(""); // step 1 has no rate
  });

  it("converts GC form to save request with column length in mm (x1000) and no mobile phases", () => {
    const gcForm = createInitialHplcMethodFormState(1, "Gc");
    gcForm.name = "Residual Solvents by GC";
    gcForm.abbreviation = "RS-GC-01";
    gcForm.columnDesignation = "G43";
    gcForm.columnLength = "30"; // 30 metres
    gcForm.columnInternalDiameterMm = "0.32";
    gcForm.filmThicknessUm = "1.8";
    gcForm.carrierGas = "Nitrogen";
    gcForm.inletTemperatureC = "200";
    gcForm.detectorTemperatureC = "250";
    gcForm.detectorType = "Fid";
    gcForm.flowRateMlPerMin = "2.2";
    gcForm.injectionVolumeUl = "1";
    gcForm.runTimeMin = "20";
    gcForm.diluentSolutionId = 5;
    gcForm.resultMode = "ResidualSolvents";
    gcForm.sampleSolutionVolumeMl = "5.0";
    gcForm.ovenSteps = [
      { rateCPerMin: "", temperatureC: "40", holdMin: "5" },
      { rateCPerMin: "10", temperatureC: "240", holdMin: "3" }
    ];
    gcForm.analytes = [
      {
        name: "Methanol",
        wavelengthNm: "",
        standardEntryId: 10,
        theoreticalWeightStdMg: "0",
        theoreticalWeightTestMg: "0",
        standardDilution: "",
        standardInjections: "6",
        sstMaxRsdPercent: "2.0",
        sstMinResolution: "",
        sstMaxTailingFactor: "2.0",
        sstMinTheoreticalPlates: "2000",
        sstMinRetentionFactor: "",
        sstMinSignalToNoise: "",
        sstMinPeakToValley: "",
        standardConcentrationUgPerMl: "50"
      }
    ];

    const req = formToSaveRequest(gcForm, 1);
    expect(req.technique).toBe("Gc");
    expect(req.resultMode).toBe("ResidualSolvents");
    expect(req.columnLengthMm).toBe(30000); // 30 m * 1000 = 30000 mm
    expect(req.particleSizeUm).toBeNull();
    expect(req.columnTemperatureC).toBeNull();
    expect(req.equilibrationMin).toBeNull();
    expect(req.mobilePhases).toEqual([]);
    expect(req.gradientSteps).toEqual([]);
    expect(req.filmThicknessUm).toBe(1.8);
    expect(req.carrierGas).toBe("Nitrogen");
    expect(req.inletTemperatureC).toBe(200);
    expect(req.detectorTemperatureC).toBe(250);
    expect(req.sampleSolutionVolumeMl).toBe(5.0);
    expect(req.ovenSteps).toHaveLength(2);
    expect(req.ovenSteps![0].rateCPerMin).toBeNull();
    expect(req.ovenSteps![1].rateCPerMin).toBe(10);
    expect(req.analytes[0].wavelengthNm).toBeNull();
    expect(req.analytes[0].theoreticalWeightStdMg).toBe(0);
    expect(req.analytes[0].theoreticalWeightTestMg).toBe(0);
    expect(req.analytes[0].standardDilution).toBeNull();
    expect(req.analytes[0].standardConcentrationUgPerMl).toBe(50);
  });

  it("converts GC entity back to form state with column length in metres (/1000)", () => {
    const entity: HplcMethodResponse = {
      id: 99,
      sectionId: 1,
      name: "Residual Solvents GC",
      abbreviation: "RS-GC",
      effectiveDate: "2026-10-04T00:00:00Z",
      isActive: true,
      columnDesignation: "G43",
      columnLengthMm: 30000,
      columnInternalDiameterMm: 0.32,
      particleSizeUm: null,
      columnTemperatureC: null,
      elutionMode: "Isocratic",
      flowRateMlPerMin: 2.0,
      detectorType: "Fid",
      injectionVolumeUl: 1,
      runTimeMin: 20,
      diluentSolutionId: 5,
      mobilePhases: [],
      gradientSteps: [],
      technique: "Gc",
      resultMode: "ResidualSolvents",
      filmThicknessUm: 1.8,
      carrierGas: "Nitrogen",
      splitRatio: null,
      inletTemperatureC: 200,
      detectorTemperatureC: 250,
      headspaceEnabled: true,
      headspaceEquilibrationTemperatureC: 80,
      headspaceEquilibrationMin: 45,
      headspaceTransferLineTemperatureC: 90,
      sampleSolutionVolumeMl: 5.0,
      ovenSteps: [
        { id: 1, stepNo: 1, rateCPerMin: null, temperatureC: 40, holdMin: 5 },
        { id: 2, stepNo: 2, rateCPerMin: 15, temperatureC: 240, holdMin: 2 }
      ],
      analytes: [
        {
          id: 101,
          displayOrder: 1,
          name: "Methanol",
          wavelengthNm: null,
          standardEntryId: 22,
          standardEntryCode: "RS-MEOH",
          theoreticalWeightStdMg: 0,
          theoreticalWeightTestMg: 0,
          standardInjections: 6,
          sstMaxRsdPercent: 2.0,
          sstMinResolution: null,
          sstMaxTailingFactor: null,
          sstMinTheoreticalPlates: null,
          sstMinRetentionFactor: null,
          sstMinSignalToNoise: null,
          sstMinPeakToValley: null,
          standardConcentrationUgPerMl: 50
        }
      ]
    };

    const form = hplcMethodEntityToFormState(entity);
    expect(form.technique).toBe("Gc");
    expect(form.resultMode).toBe("ResidualSolvents");
    expect(form.columnLength).toBe("30"); // 30000 mm / 1000 = 30 m
    expect(form.filmThicknessUm).toBe("1.8");
    expect(form.particleSizeUm).toBe("");
    expect(form.headspaceEnabled).toBe(true);
    expect(form.headspaceEquilibrationTemperatureC).toBe("80");
    expect(form.ovenSteps).toHaveLength(2);
    expect(form.ovenSteps[0].rateCPerMin).toBe("");
    expect(form.ovenSteps[1].rateCPerMin).toBe("15");
    expect(form.analytes[0].standardConcentrationUgPerMl).toBe("50");
  });

  it("validates GC required fields and oven step rules", () => {
    const gcForm = createInitialHplcMethodFormState(1, "Gc");
    gcForm.name = "";
    gcForm.abbreviation = "";
    gcForm.columnDesignation = "";
    gcForm.filmThicknessUm = "";
    gcForm.carrierGas = "";
    gcForm.inletTemperatureC = "";
    gcForm.detectorTemperatureC = "";
    gcForm.ovenSteps = [];

    const errors = validateHplcMethodForm(gcForm, { isEditing: false, hasMultipleSections: false });
    expect(errors["name"]).toBeDefined();
    expect(errors["column.designation"]).toBeDefined();
    expect(errors["column.filmThicknessUm"]).toBeDefined();
    expect(errors["conditions.carrierGas"]).toBeDefined();
    expect(errors["conditions.inletTemperatureC"]).toBeDefined();
    expect(errors["conditions.detectorTemperatureC"]).toBeDefined();
    expect(errors["form.ovenSteps"]).toBeDefined();
  });

  it("validates GC step 1 has no rate and step 2+ requires rate > 0", () => {
    const gcForm = createInitialHplcMethodFormState(1, "Gc");
    gcForm.name = "GC Test";
    gcForm.abbreviation = "GC-TEST";
    gcForm.columnDesignation = "G43";
    gcForm.columnLength = "30";
    gcForm.columnInternalDiameterMm = "0.32";
    gcForm.filmThicknessUm = "1.8";
    gcForm.carrierGas = "Helium";
    gcForm.inletTemperatureC = "200";
    gcForm.detectorTemperatureC = "250";
    gcForm.diluentSolutionId = 1;
    gcForm.ovenSteps = [
      { rateCPerMin: "10", temperatureC: "40", holdMin: "5" }, // Step 1 shouldn't have rate
      { rateCPerMin: "", temperatureC: "200", holdMin: "2" } // Step 2 needs rate
    ];

    const errors = validateHplcMethodForm(gcForm, { isEditing: false, hasMultipleSections: false });
    expect(errors["oven.0.rateCPerMin"]).toMatch(/initial temperature and has no ramp rate/);
    expect(errors["oven.1.rateCPerMin"]).toMatch(/ramp rate must be greater than zero/);
  });

  it("validates GC Headspace required values when enabled", () => {
    const gcForm = createInitialHplcMethodFormState(1, "Gc");
    gcForm.name = "GC Test";
    gcForm.abbreviation = "GC-TEST";
    gcForm.columnDesignation = "G43";
    gcForm.filmThicknessUm = "1.8";
    gcForm.carrierGas = "Helium";
    gcForm.inletTemperatureC = "200";
    gcForm.detectorTemperatureC = "250";
    gcForm.diluentSolutionId = 1;
    gcForm.headspaceEnabled = true;
    gcForm.headspaceEquilibrationTemperatureC = "";
    gcForm.headspaceEquilibrationMin = "";
    gcForm.headspaceTransferLineTemperatureC = "";

    const errors = validateHplcMethodForm(gcForm, { isEditing: false, hasMultipleSections: false });
    expect(errors["headspace.equilibrationTemperatureC"]).toBeDefined();
    expect(errors["headspace.equilibrationMin"]).toBeDefined();
    expect(errors["headspace.transferLineTemperatureC"]).toBeDefined();
  });

  it("validates GC Residual Solvents volume and analyte concentration", () => {
    const gcForm = createInitialHplcMethodFormState(1, "Gc");
    gcForm.name = "GC RS Test";
    gcForm.abbreviation = "GC-RS";
    gcForm.columnDesignation = "G43";
    gcForm.filmThicknessUm = "1.8";
    gcForm.carrierGas = "Nitrogen";
    gcForm.inletTemperatureC = "200";
    gcForm.detectorTemperatureC = "250";
    gcForm.diluentSolutionId = 1;
    gcForm.resultMode = "ResidualSolvents";
    gcForm.sampleSolutionVolumeMl = "";
    gcForm.analytes[0].standardEntryId = 1;
    gcForm.analytes[0].name = "Acetone";
    gcForm.analytes[0].standardConcentrationUgPerMl = "";

    const errors = validateHplcMethodForm(gcForm, { isEditing: false, hasMultipleSections: false });
    expect(errors["sampleSolutionVolumeMl"]).toBeDefined();
    expect(errors["analytes.0.standardConcentrationUgPerMl"]).toBeDefined();
  });

  it("routes error keys to correct tab index for GC vs HPLC", () => {
    // GC Tabs: 0 Column, 1 Conditions & Oven, 2 Analytes
    expect(hplcErrorTab("column.filmThicknessUm", "Gc")).toBe(0);
    expect(hplcErrorTab("conditions.carrierGas", "Gc")).toBe(1);
    expect(hplcErrorTab("oven.0.rateCPerMin", "Gc")).toBe(1);
    expect(hplcErrorTab("headspace.equilibrationMin", "Gc")).toBe(1);
    expect(hplcErrorTab("solutions.diluent", "Gc")).toBe(1);
    expect(hplcErrorTab("sampleSolutionVolumeMl", "Gc")).toBe(2);
    expect(hplcErrorTab("analytes.0.name", "Gc")).toBe(2);

    // HPLC Tabs: 0 Column, 1 Elution, 2 Solutions, 3 Analytes
    expect(hplcErrorTab("column.particleSizeUm", "Hplc")).toBe(0);
    expect(hplcErrorTab("elution.flowRateMlPerMin", "Hplc")).toBe(1);
    expect(hplcErrorTab("solutions.diluent", "Hplc")).toBe(2);
    expect(hplcErrorTab("analytes.0.wavelengthNm", "Hplc")).toBe(3);
  });
});
