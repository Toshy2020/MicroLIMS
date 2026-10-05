import { describe, expect, it } from "vitest";
import { findMissingSstFields } from "./sstValidation";
import type { HplcSstAnalyteDto, SaveSstAnalyteInput } from "../types";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

describe("findMissingSstFields", () => {
  const dummyAnalyte: HplcSstAnalyteDto = {
    id: 1,
    hplcMethodAnalyteId: 10,
    analyteName: "Ethanol",
    standardEntryId: 10,
    standardWeightMg: 10,
    standardMaterialId: 5,
    reportedRsdPercent: null,
    resolution: null,
    tailingFactor: null,
    theoreticalPlates: null,
    retentionFactor: null,
    signalToNoise: null,
    peakToValley: null,
    passed: false,
    failureReasons: null,
    injections: []
  };

  const dummyMethod: HplcMethodResponse = {
    id: 1,
    name: "Residual Solvents by GC",
    abbreviation: "RS-GC",
    version: 1,
    sectionId: 1,
    effectiveDate: "2026-01-01",
    isActive: true,
    columnDesignation: "G43",
    columnLengthMm: 30000,
    columnInternalDiameterMm: 0.32,
    filmThicknessUm: 1.8,
    flowRateMlPerMin: 1.0,
    carrierGas: "Nitrogen",
    elutionMode: "Isocratic",
    detectorType: "Fid",
    injectionVolumeUl: 1.0,
    runTimeMin: 30,
    diluentSolutionId: 1,
    technique: "Gc",
    resultMode: "ResidualSolvents",
    analytes: [
      {
        id: 10,
        displayOrder: 1,
        name: "Ethanol",
        standardEntryId: 10,
        standardEntryCode: "RS-ETH",
        standardInjections: 3,
        theoreticalWeightStdMg: 0,
        theoreticalWeightTestMg: 0
      }
    ],
    mobilePhases: [],
    gradientSteps: [],
    ovenSteps: []
  };

  it("requires actual standard weight when isResidualSolvents is false", () => {
    const input: Record<number, SaveSstAnalyteInput> = {
      1: {
        hplcSstAnalyteId: 1,
        standardMaterialId: 5,
        standardWeightMg: 0, // 0 standard weight
        responses: [1000, 1005, 1002]
      }
    };

    const missing = findMissingSstFields([dummyAnalyte], input, dummyMethod, false);
    expect(missing.length).toBe(1);
    expect(missing[0]).toContain("actual standard weight");
  });

  it("does NOT require actual standard weight when isResidualSolvents is true", () => {
    const input: Record<number, SaveSstAnalyteInput> = {
      1: {
        hplcSstAnalyteId: 1,
        standardMaterialId: 5,
        standardWeightMg: 0, // 0 standard weight is allowed in Residual Solvents
        responses: [1000, 1005, 1002]
      }
    };

    const missing = findMissingSstFields([dummyAnalyte], input, dummyMethod, true);
    expect(missing).toEqual([]);
  });

  it("flags missing reference standard lot and missing injection responses", () => {
    const input: Record<number, SaveSstAnalyteInput> = {
      1: {
        hplcSstAnalyteId: 1,
        standardMaterialId: 0, // missing lot
        standardWeightMg: 0,
        responses: [1000, 0, 1002] // injection #2 missing
      }
    };

    const missing = findMissingSstFields([dummyAnalyte], input, dummyMethod, true);
    expect(missing.length).toBe(1);
    expect(missing[0]).toContain("reference standard lot");
    expect(missing[0]).toContain("injection #2");
  });
});
