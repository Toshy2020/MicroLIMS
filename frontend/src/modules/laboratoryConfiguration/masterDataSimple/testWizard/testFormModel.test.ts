import { describe, expect, it } from "vitest";
import type { TestDefinitionOption } from "../../../../hooks/useTestDefinitions";
import { TEST_PAYLOAD_FIXTURES } from "./__fixtures__/testPayloads";
import {
  TEST_WIZARD_STEP_ORDER,
  TestFormModel,
  createEmptyTestFormModel,
  fromTestDefinition,
  toTestPayload,
  validateStep,
  withEquationType,
  withWorkflowType
} from "./testFormModel";

function buildMeasurementModel(): TestFormModel {
  return createEmptyTestFormModel({
    lab: "fp",
    area: "fp",
    physchemArea: "FinishedProduct",
    code: "PH",
    displayName: "pH",
    coaDisplayOrder: "3",
    sectionId: 1,
    workflowType: "Measurement",
    equationType: "Measurement",
    replicateCount: "3",
    evaluationBasis: "Mean"
  });
}

describe("testFormModel", () => {
  it("exposes the four wizard steps in order", () => {
    expect(TEST_WIZARD_STEP_ORDER).toEqual(["basics", "typeSettings", "stages", "steps"]);
  });

  describe("toTestPayload matches every captured characterisation fixture", () => {
    it("CountTest", () => {
      const model = createEmptyTestFormModel({
        lab: "micro",
        code: "TAMC",
        displayName: "Total Aerobic Microbial Count",
        coaDisplayOrder: "1",
        sectionId: 2,
        workflowType: "CountTest",
        equationType: "None"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.countTest);
    });

    it("Observation", () => {
      const model = createEmptyTestFormModel({
        lab: "micro",
        code: "ECOLI",
        displayName: "Escherichia coli",
        sectionId: 2,
        workflowType: "Observation",
        equationType: "None"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.observation);
    });

    it("Qualitative", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "APP",
        displayName: "Appearance",
        coaDisplayOrder: "2",
        sectionId: 1,
        workflowType: "Qualitative",
        equationType: "Qualitative",
        perContainer: true
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.qualitative);
    });

    it("Measurement", () => {
      expect(toTestPayload(buildMeasurementModel())).toStrictEqual(TEST_PAYLOAD_FIXTURES.measurement);
    });

    it("GravimetricLoss", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "LOD",
        displayName: "Loss on Drying",
        sectionId: 1,
        workflowType: "Gravimetric",
        equationType: "GravimetricLoss",
        replicateCount: "2",
        usesTare: true,
        conditionFields: "Temperature (°C),Time (h)",
        moistureRole: "LossOnDrying"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.gravimetricLoss);
    });

    it("GravimetricResidue", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "ASH",
        displayName: "Sulfated Ash",
        sectionId: 1,
        workflowType: "Gravimetric",
        equationType: "GravimetricResidue",
        replicateCount: "2",
        usesTare: false,
        conditionFields: "Temperature (°C)",
        moistureRole: "None"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.gravimetricResidue);
    });

    it("Dissolution", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "DISS",
        displayName: "Dissolution",
        sectionId: 1,
        workflowType: "Dissolution",
        equationType: "Dissolution",
        requiresSystemSuitability: true,
        hplcMethodId: 10,
        methodAbbreviation: "DISS-UV",
        conditionFields: "Apparatus,RPM,Medium"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.dissolution);
    });

    it("Disintegration", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "DISINT",
        displayName: "Disintegration",
        sectionId: 1,
        workflowType: "Disintegration",
        equationType: "Disintegration",
        conditionFields: "Medium,Temperature (°C)"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.disintegration);
    });

    it("WeightVariation", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "WV",
        displayName: "Weight Variation",
        sectionId: 1,
        workflowType: "WeightVariation",
        equationType: "WeightVariation",
        conditionFields: "Balance ID"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.weightVariation);
    });

    it("Titration (AcidBase)", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "TITR-AB",
        displayName: "Ascorbic Acid Titration",
        sectionId: 1,
        workflowType: "Titration",
        equationType: "Titration",
        titration: {
          type: "AcidBase",
          nonAqueous: false,
          mode: "Direct",
          calculation: "UspFactor",
          titrantSolutionMasterId: 101,
          equivalencyFactor: "88.06",
          blankRequired: true,
          excessSolutionMasterId: "",
          excessVolumeMl: "",
          replicateCount: "3",
          maxRsdPercent: "1.5",
          endpoint: "Visual",
          indicatorEntryId: 201,
          tempCorrection: false,
          expansionCoefficient: "0.001",
          standardEntryId: ""
        }
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.titration);
    });

    it("Titration (KarlFischer)", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "KF-H2O",
        displayName: "Water Determination KF",
        sectionId: 1,
        workflowType: "Titration",
        equationType: "Titration",
        moistureRole: "Water",
        titration: {
          type: "KarlFischer",
          nonAqueous: false,
          mode: "Direct",
          calculation: "UspFactor",
          titrantSolutionMasterId: 102,
          equivalencyFactor: "",
          blankRequired: false,
          excessSolutionMasterId: "",
          excessVolumeMl: "",
          replicateCount: "3",
          maxRsdPercent: "",
          endpoint: "Potentiometric",
          indicatorEntryId: "",
          tempCorrection: false,
          expansionCoefficient: "0.001",
          standardEntryId: ""
        }
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.titrationKarlFischer);
    });

    it("HplcMethodAssay", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "HPLC-IBU",
        displayName: "Ibuprofen Assay",
        sectionId: 1,
        workflowType: "HplcMethodAssay",
        equationType: "HplcMethodAssay",
        requiresSystemSuitability: true,
        hplcMethodId: 10,
        methodAbbreviation: "IBU-A"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.hplcAssay);
    });

    it("IcpMethodAssay (Assay)", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "ICP-FE",
        displayName: "Iron Assay by ICP",
        sectionId: 1,
        workflowType: "IcpMethodAssay",
        equationType: "IcpMethodAssay",
        icp: {
          mode: "Assay",
          sampleForm: "Solid",
          theoreticalAmount: "0.5",
          specificGravity: "",
          elements: [{ symbol: "Fe", targetPpm: "50", cleaningLimitPpm: "10" }]
        }
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.icpAssay);
    });

    it("IcpMethodAssay (Impurity)", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "ICP-PB",
        displayName: "Lead Impurity by ICP",
        sectionId: 1,
        workflowType: "IcpMethodAssay",
        equationType: "IcpMethodAssay",
        icp: {
          mode: "Impurity",
          sampleForm: "Solid",
          theoreticalAmount: "",
          specificGravity: "",
          elements: [{ symbol: "Pb", targetPpm: "", cleaningLimitPpm: "" }]
        }
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.icpImpurity);
    });

    it("UvSpectrophotometry (StandardComparison)", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "UV-STD",
        displayName: "UV Assay against Standard",
        sectionId: 1,
        workflowType: "UvSpectrophotometry",
        equationType: "UvSpectrophotometry",
        replicateCount: "3",
        uvCalculationMode: "StandardComparison",
        uvTotalVolumeMl: "100",
        uvStandardVolumeMl: "50",
        uvStandardEntryId: 301
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.uvStandardComparison);
    });

    it("UvSpectrophotometry (SpecificAbsorbanceAssay)", () => {
      const model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "UV-A1",
        displayName: "Riboflavin A1% Assay",
        sectionId: 1,
        workflowType: "UvSpectrophotometry",
        equationType: "UvSpectrophotometry",
        replicateCount: "2",
        uvCalculationMode: "SpecificAbsorbanceAssay",
        uvSpecificAbsorbance: "328",
        uvTotalVolumeMl: "5000"
      });
      expect(toTestPayload(model)).toStrictEqual(TEST_PAYLOAD_FIXTURES.uvSpecificAbsorbanceAssay);
    });

    it("Measurement Edit via fromTestDefinition", () => {
      const existing: TestDefinitionOption = {
        id: 501,
        code: "PH",
        displayName: "pH",
        isActive: true,
        workflowType: "Measurement",
        equationType: "Measurement",
        replicateCount: 3,
        evaluationBasis: "Mean",
        sectionId: 1,
        physchemArea: "FinishedProduct",
        coaDisplayOrder: 3
      };
      const baseModel = fromTestDefinition(existing, { lab: "fp", area: "fp" });
      const editedModel: TestFormModel = {
        ...baseModel,
        displayName: "pH (25 °C)",
        coaDisplayOrder: "",
        replicateCount: "2"
      };
      expect(toTestPayload(editedModel)).toStrictEqual(TEST_PAYLOAD_FIXTURES.measurementEdit);
    });

    it("Titration Edit via fromTestDefinition with changeReason", () => {
      const existing: TestDefinitionOption = {
        id: 502,
        code: "TITR-AB",
        displayName: "Ascorbic Acid Titration",
        isActive: true,
        workflowType: "Titration",
        equationType: "Titration",
        replicateCount: 3,
        sectionId: 1,
        physchemArea: "FinishedProduct",
        titrationType: "AcidBase",
        titrationNonAqueous: false,
        titrationMode: "Direct",
        titrationCalculation: "UspFactor",
        titrantSolutionMasterId: 101,
        titrationEquivalencyFactor: 88.06,
        titrationBlankRequired: true,
        titrationMaxRsdPercent: 1.5,
        titrationEndpoint: "Visual",
        titrationIndicatorEntryId: 201
      };
      const baseModel = fromTestDefinition(existing, { lab: "fp", area: "fp" });
      const editedModel: TestFormModel = {
        ...baseModel,
        titration: { ...baseModel.titration, equivalencyFactor: "88.1" },
        changeReason: "SOP revision 4 updated equivalency factor."
      };
      expect(toTestPayload(editedModel)).toStrictEqual(TEST_PAYLOAD_FIXTURES.titrationEdit);
    });
  });

  describe("switching workflowType/equationType then back sends no stale fields", () => {
    it("drops non-Measurement fields across switches and preserves clean Measurement payload when switched back", () => {
      let model = buildMeasurementModel();
      model = { ...model, perContainer: true, moistureRole: "LossOnDrying", conditionFields: "Temp" };

      // Switch to Dissolution and populate dissolution fields.
      model = withWorkflowType(model, "Dissolution");
      expect(model.perContainer).toBe(false);
      expect(model.moistureRole).toBe("None");
      expect(model.requiresSystemSuitability).toBe(true);
      model = { ...model, hplcMethodId: 10, methodAbbreviation: "DISS-UV", dissolutionS1Offset: "9" };

      // Switch through Disintegration -> WeightVariation -> UV -> ICP -> Titration -> Qualitative.
      model = withWorkflowType(model, "Disintegration");
      expect(model.requiresSystemSuitability).toBe(false);

      model = withWorkflowType(model, "WeightVariation");
      model = withWorkflowType(model, "UvSpectrophotometry");
      expect(model.replicateCount).toBe("1");

      model = {
        ...model,
        uvCalculationMode: "SpecificAbsorbanceAssay",
        uvSpecificAbsorbance: "328",
        uvTotalVolumeMl: "5000",
        uvStandardVolumeMl: "50",
        uvStandardEntryId: 301
      };

      model = withWorkflowType(model, "IcpMethodAssay");
      model = withWorkflowType(model, "Titration");
      model = {
        ...model,
        titration: { ...model.titration, type: "KarlFischer", titrantSolutionMasterId: 102 },
        moistureRole: "Water"
      };

      // Switching away from Titration clears KarlFischer Water moistureRole.
      model = withWorkflowType(model, "Qualitative");
      expect(model.moistureRole).toBe("None");
      model = { ...model, perContainer: true };

      // Switch back to Measurement and restore replicateCount ("3", since UV reset it to "1").
      model = withWorkflowType(model, "Measurement");
      expect(model.perContainer).toBe(false);
      expect(model.requiresSystemSuitability).toBe(false);
      expect(model.moistureRole).toBe("None");
      model = { ...model, replicateCount: "3" };

      const payload = toTestPayload(model);
      expect(payload).toStrictEqual(TEST_PAYLOAD_FIXTURES.measurement);
      expect("icpMode" in payload).toBe(false);
      expect("icpElements" in payload).toBe(false);
    });

    it("clears Qualitative perContainer and GravimetricLoss moistureRole on equation/workflow transitions", () => {
      let model = createEmptyTestFormModel({
        lab: "fp",
        area: "fp",
        physchemArea: "FinishedProduct",
        code: "LOD",
        displayName: "Loss on Drying",
        sectionId: 1,
        workflowType: "Gravimetric",
        equationType: "GravimetricLoss",
        replicateCount: "2",
        usesTare: false,
        conditionFields: "Temperature (°C)",
        moistureRole: "LossOnDrying"
      });

      // Switching GravimetricLoss -> GravimetricResidue clears LossOnDrying moistureRole.
      model = withEquationType(model, "GravimetricResidue");
      expect(model.moistureRole).toBe("None");

      const ashPayload = toTestPayload({ ...model, code: "ASH", displayName: "Sulfated Ash" });
      expect(ashPayload).toStrictEqual(TEST_PAYLOAD_FIXTURES.gravimetricResidue);

      // Switching equationType to Dissolution syncs workflowType and requiresSystemSuitability.
      const dissFromEq = withEquationType(model, "Dissolution");
      expect(dissFromEq.workflowType).toBe("Dissolution");
      expect(dissFromEq.requiresSystemSuitability).toBe(true);
      expect(dissFromEq.perContainer).toBe(false);

      // Micro lab CountTest <-> Observation round-trip has no FP stale fields.
      let micro = createEmptyTestFormModel({
        lab: "micro",
        code: "TAMC",
        displayName: "Total Aerobic Microbial Count",
        coaDisplayOrder: "1",
        sectionId: 2,
        workflowType: "CountTest",
        equationType: "None"
      });
      micro = withWorkflowType(micro, "Observation");
      micro = withWorkflowType(micro, "CountTest");
      expect(toTestPayload(micro)).toStrictEqual(TEST_PAYLOAD_FIXTURES.countTest);
    });
  });

  describe("validateStep", () => {
    it("basics: validates code, displayName, sectionId, and coaDisplayOrder", () => {
      const empty = createEmptyTestFormModel({ lab: "fp", sectionId: "" });
      expect(validateStep("basics", empty)).toEqual({
        code: "Both Code and Display Name are required.",
        displayName: "Both Code and Display Name are required.",
        sectionId: "Laboratory section is required."
      });

      expect(
        validateStep("basics", { ...empty, code: "T1", displayName: "Test 1", sectionId: 1, coaDisplayOrder: "0" })
      ).toEqual({
        coaDisplayOrder: "CoA order must be a whole number from 1 to 9999, or empty."
      });

      expect(
        validateStep("basics", { ...empty, code: "T1", displayName: "Test 1", sectionId: 1, coaDisplayOrder: "10000" })
      ).toEqual({
        coaDisplayOrder: "CoA order must be a whole number from 1 to 9999, or empty."
      });

      expect(validateStep("basics", buildMeasurementModel())).toEqual({});
    });

    it("typeSettings: validates Dissolution exact messages", () => {
      const base = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "Dissolution",
        equationType: "Dissolution",
        methodAbbreviation: "DISS-UV"
      });

      expect(validateStep("typeSettings", { ...base, methodAbbreviation: "" })).toEqual({
        methodAbbreviation: "Method abbreviation is required when system suitability is enabled."
      });
      expect(validateStep("typeSettings", { ...base, methodAbbreviation: "BAD ABBR!" })).toEqual({
        methodAbbreviation: "Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens."
      });
      expect(validateStep("typeSettings", { ...base, dissolutionS1Offset: "" })).toEqual({
        dissolutionS1Offset: 'Dissolution: "S1: each unit ≥ Q +" is required.'
      });
      expect(validateStep("typeSettings", { ...base, dissolutionS2MinOffset: "abc" })).toEqual({
        dissolutionS2MinOffset: 'Dissolution: "S2: no unit < Q −" must be a number.'
      });
      expect(validateStep("typeSettings", { ...base, dissolutionS3MinOffset: "-1" })).toEqual({
        dissolutionS3MinOffset: "Dissolution stage offsets must be greater than or equal to zero."
      });
      expect(validateStep("typeSettings", { ...base, conditionFields: "a".repeat(501) })).toEqual({
        conditionFields: "Condition fields cannot exceed 500 characters."
      });
      expect(validateStep("typeSettings", base)).toEqual({});
    });

    it("typeSettings: validates Disintegration exact messages", () => {
      const base = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "Disintegration",
        equationType: "Disintegration"
      });

      expect(validateStep("typeSettings", { ...base, disintegrationStage1Units: "" })).toEqual({
        disintegrationStage1Units: 'Disintegration: "Stage 1 Units" is required.'
      });
      expect(validateStep("typeSettings", { ...base, disintegrationStage2Units: "0" })).toEqual({
        disintegrationStage2Units: "Disintegration stage units must be greater than or equal to 1."
      });
      expect(validateStep("typeSettings", { ...base, disintegrationMaxStage1Failures: "6" })).toEqual({
        disintegrationMaxStage1Failures: "Disintegration maximum Stage 1 failures must be between 0 and 5."
      });
      expect(validateStep("typeSettings", { ...base, disintegrationMinPassTotal: "19" })).toEqual({
        disintegrationMinPassTotal: "Disintegration minimum pass total must be between 1 and 18."
      });
      expect(validateStep("typeSettings", { ...base, conditionFields: "x".repeat(501) })).toEqual({
        conditionFields: "Condition fields cannot exceed 500 characters."
      });
      expect(validateStep("typeSettings", base)).toEqual({});
    });

    it("typeSettings: validates WeightVariation exact messages", () => {
      const base = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "WeightVariation",
        equationType: "WeightVariation"
      });

      expect(validateStep("typeSettings", { ...base, wvUnitCount: "" })).toEqual({
        wvUnitCount: 'Weight variation: "Unit Count (Stage 1)" is required.'
      });
      expect(validateStep("typeSettings", { ...base, wvUnitCount: "0" })).toEqual({
        wvUnitCount: "Weight variation unit count and extra units must be greater than or equal to 1."
      });
      expect(validateStep("typeSettings", { ...base, wvTabletMaxOutside: "-1" })).toEqual({
        wvTabletMaxOutside: "Weight variation maximum outside counts must be greater than or equal to 0."
      });
      expect(validateStep("typeSettings", { ...base, wvTabletBand1MaxMg: "0" })).toEqual({
        wvTabletBand1MaxMg: "Weight variation tablet band weight limits must be greater than zero."
      });
      expect(validateStep("typeSettings", { ...base, wvTabletBand3Percent: "0" })).toEqual({
        wvTabletBand3Percent: "Weight variation percentages must be greater than zero."
      });
      expect(validateStep("typeSettings", { ...base, wvTabletBand1MaxMg: "324" })).toEqual({
        wvTabletBand1MaxMg: "Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg."
      });
      expect(validateStep("typeSettings", { ...base, wvCapsuleInnerPercent: "25" })).toEqual({
        wvCapsuleInnerPercent: "Weight variation capsule inner percentage must be less than outer percentage."
      });
      expect(validateStep("typeSettings", { ...base, wvCapsuleS1MaxOutside: "6" })).toEqual({
        wvCapsuleS1MaxOutside:
          "Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count."
      });
      expect(validateStep("typeSettings", { ...base, wvCapsuleS2MaxOutside: "60" })).toEqual({
        wvCapsuleS2MaxOutside: "Weight variation capsule Stage 2 max outside must be less than total units (60)."
      });
      expect(validateStep("typeSettings", base)).toEqual({});
    });

    it("typeSettings: validates UV exact messages", () => {
      const base = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "UvSpectrophotometry",
        equationType: "UvSpectrophotometry",
        replicateCount: "2"
      });

      expect(validateStep("typeSettings", base)).toEqual({
        uvCalculationMode: "A UV test needs a calculation mode."
      });
      expect(
        validateStep("typeSettings", {
          ...base,
          uvCalculationMode: "AbsorbanceLimit",
          replicateCount: "31"
        })
      ).toEqual({
        replicateCount: "Replicate count must be between 1 and 30."
      });
      expect(
        validateStep("typeSettings", {
          ...base,
          uvCalculationMode: "SpecificAbsorbanceAssay",
          uvSpecificAbsorbance: "0",
          uvTotalVolumeMl: "0"
        })
      ).toEqual({
        uvSpecificAbsorbance: "This UV mode needs a specific absorbance above 0.",
        uvTotalVolumeMl: "This UV mode needs a total volume above 0."
      });
      expect(
        validateStep("typeSettings", {
          ...base,
          uvCalculationMode: "StandardComparison",
          uvTotalVolumeMl: "",
          uvStandardVolumeMl: "",
          uvStandardEntryId: ""
        })
      ).toEqual({
        uvTotalVolumeMl: "This UV mode needs a total volume above 0.",
        uvStandardVolumeMl: "A UV test against a standard needs a standard volume above 0.",
        uvStandardEntryId: "The standard material is required for an assay against standard."
      });
    });

    it("typeSettings: validates Titration, Edit changeReason, HPLC, ICP, Measurement, and Gravimetric exact messages", () => {
      const titrationModel = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "Titration",
        equationType: "Titration"
      });
      expect(validateStep("typeSettings", titrationModel)).toEqual({
        titration: "Titration type is required."
      });
      expect(
        validateStep("typeSettings", {
          ...titrationModel,
          titration: {
            ...titrationModel.titration,
            type: "AcidBase",
            mode: "Direct",
            calculation: "UspFactor",
            endpoint: "Visual"
          }
        })
      ).toEqual({
        titration: "Titrant is required."
      });

      const editedTitration = fromTestDefinition(
        {
          id: 502,
          code: "TITR-AB",
          displayName: "Ascorbic Acid Titration",
          isActive: true,
          workflowType: "Titration",
          equationType: "Titration",
          replicateCount: 3,
          sectionId: 1,
          physchemArea: "FinishedProduct",
          titrationType: "AcidBase",
          titrationNonAqueous: false,
          titrationMode: "Direct",
          titrationCalculation: "UspFactor",
          titrantSolutionMasterId: 101,
          titrationEquivalencyFactor: 88.06,
          titrationBlankRequired: true,
          titrationMaxRsdPercent: 1.5,
          titrationEndpoint: "Visual",
          titrationIndicatorEntryId: 201
        },
        { lab: "fp", area: "fp" }
      );
      expect(
        validateStep("typeSettings", {
          ...editedTitration,
          titration: { ...editedTitration.titration, equivalencyFactor: "88.1" },
          changeReason: ""
        })
      ).toEqual({
        changeReason: "A reason for change is required when titration or moisture settings change (5 to 500 characters)."
      });

      const hplcModel = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "HplcMethodAssay",
        equationType: "HplcMethodAssay"
      });
      expect(validateStep("typeSettings", hplcModel)).toEqual({
        hplcMethodId: "HPLC method is required for HPLC method assay tests."
      });

      const icpModel = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "IcpMethodAssay",
        equationType: "IcpMethodAssay"
      });
      expect(validateStep("typeSettings", icpModel)).toEqual({
        icp: "Add at least one element."
      });

      const measurementInvalid = buildMeasurementModel();
      expect(validateStep("typeSettings", { ...measurementInvalid, replicateCount: "0" })).toEqual({
        replicateCount: "Replicate count must be between 1 and 30 for Measurement tests."
      });

      const gravimetricInvalid = createEmptyTestFormModel({
        lab: "fp",
        workflowType: "Gravimetric",
        equationType: "Measurement",
        replicateCount: "31",
        conditionFields: "z".repeat(501)
      });
      expect(validateStep("typeSettings", gravimetricInvalid)).toEqual({
        replicateCount: "Replicate count must be between 1 and 30 for Gravimetric tests.",
        equationType: "Equation type must be GravimetricLoss or GravimetricResidue for Gravimetric tests.",
        conditionFields: "Condition fields cannot exceed 500 characters."
      });
    });

    it("stages and steps return empty errors", () => {
      const m = buildMeasurementModel();
      expect(validateStep("stages", m)).toEqual({});
      expect(validateStep("steps", m)).toEqual({});
    });
  });
});
