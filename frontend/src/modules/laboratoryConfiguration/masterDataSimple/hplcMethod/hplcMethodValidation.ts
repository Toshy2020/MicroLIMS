import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import { HplcMethodFormState } from "./hplcMethodForm";
import { HplcTechnique } from "../services/HplcMethodService";

// Errors keyed by a stable field path (e.g. "column.lengthMm", "analytes.2.name",
// "gradient.3.percentB", "solutions.1.solutionId"). Keys starting with "form." are rules
// that span a whole tab and are shown in the dialog top alert instead of on a field.
export type HplcMethodErrors = Record<string, string>;

export const HPLC_FORM_ERROR_PREFIX = "form.";

// Tab indexes in the HPLC dialog; a null tab means the always-visible general header.
export const HPLC_TAB_COLUMN = 0;
export const HPLC_TAB_ELUTION = 1;
export const HPLC_TAB_SOLUTIONS = 2;
export const HPLC_TAB_ANALYTES = 3;

// Tab indexes in the GC dialog
export const GC_TAB_COLUMN = 0;
export const GC_TAB_CONDITIONS = 1;
export const GC_TAB_ANALYTES = 2;

export function hplcErrorTab(key: string, technique: HplcTechnique = "Hplc"): number | null {
  if (technique === "Gc") {
    if (key.startsWith("column.")) return GC_TAB_COLUMN;
    if (
      key.startsWith("conditions.") ||
      key.startsWith("carrier.") ||
      key.startsWith("detection.") ||
      key.startsWith("elution.") ||
      key.startsWith("oven.") ||
      key.startsWith("headspace.") ||
      key.startsWith("solutions.") ||
      key === "form.ovenSteps"
    ) {
      return GC_TAB_CONDITIONS;
    }
    if (key.startsWith("analytes.") || key === "form.analytes" || key === "sampleSolutionVolumeMl") {
      return GC_TAB_ANALYTES;
    }
    return null;
  }

  // HPLC
  if (key.startsWith("column.")) return HPLC_TAB_COLUMN;
  if (
    key.startsWith("elution.") ||
    key.startsWith("detection.") ||
    key.startsWith("gradient.") ||
    key === "form.gradient"
  ) {
    return HPLC_TAB_ELUTION;
  }
  if (key.startsWith("solutions.") || key === "form.mobilePhases" || key === "form.ratioSum") {
    return HPLC_TAB_SOLUTIONS;
  }
  if (
    key.startsWith("analytes.") ||
    key === "form.analytes" ||
    key.startsWith("impurities.") ||
    key === "referenceSolutionPercent" ||
    key === "disregardLimitPercent" ||
    key === "sampleSolutionVolumeMl" ||
    key === "swabAreaCm2"
  ) {
    return HPLC_TAB_ANALYTES;
  }
  return null;
}

export function validateHplcMethodForm(
  form: HplcMethodFormState,
  options: { isEditing: boolean; hasMultipleSections: boolean },
  t: TFunction = i18n.getFixedT("en", "masterData")
): HplcMethodErrors {
  const errors: HplcMethodErrors = {};
  const add = (key: string, message: string) => {
    if (!(key in errors)) errors[key] = message;
  };

  const isGc = form.technique === "Gc";
  const isResidual = isGc && form.resultMode === "ResidualSolvents";
  const isRelatedSubstances = !isGc && form.resultMode === "RelatedSubstances";
  const isCleaningResidue = !isGc && form.resultMode === "CleaningResidue";

  if (isGc && form.resultMode === "RelatedSubstances") {
    add("resultMode", t("hplcMethod.validation.relatedSubstancesHplcOnly"));
  }

  if (isGc && form.resultMode === "CleaningResidue") {
    add("resultMode", t("hplcMethod.validation.cleaningResidueHplcOnly"));
  }

  if (isCleaningResidue) {
    if (!form.productItemId) {
      add("productItemId", t("hplcMethod.validation.cleaningProductRequired"));
    }
    const vol = Number(form.sampleSolutionVolumeMl);
    if (!form.sampleSolutionVolumeMl || isNaN(vol) || vol <= 0) {
      add("sampleSolutionVolumeMl", t("hplcMethod.validation.cleaningExtractionPositive"));
    }
    const usesCm2 = form.analytes.some((a) => a.macoUnit === "UgPerCm2");
    const swabArea = Number(form.swabAreaCm2);
    if (usesCm2 && (!form.swabAreaCm2 || isNaN(swabArea) || swabArea <= 0)) {
      add("swabAreaCm2", t("hplcMethod.validation.cleaningSwabAreaPositive"));
    }
  }

  if (form.resultMode !== "RelatedSubstances") {
    const hasRsValues =
      (form.impurities && form.impurities.length > 0) ||
      (form.referenceSolutionPercent !== "" && form.referenceSolutionPercent != null) ||
      (form.disregardLimitPercent !== "" && form.disregardLimitPercent != null);
    if (hasRsValues) {
      add("resultMode", t("hplcMethod.validation.relatedSubstancesOnly"));
    }
  }

  if (form.internalStandardName && form.internalStandardName.trim()) {
    if (form.technique !== "Hplc" || form.resultMode !== "Assay") {
      add("internalStandardName", t("hplcMethod.validation.internalStandardAssayOnly"));
    } else if (form.internalStandardName.trim().length > 100) {
      add("internalStandardName", t("hplcMethod.validation.internalStandardMax"));
    }
  }

  const name = form.name.trim();
  if (!name) add("name", t("hplcMethod.validation.nameRequired"));

  const abbr = form.abbreviation.trim().toUpperCase();
  if (!abbr) add("abbreviation", t("hplcMethod.validation.abbreviationRequired"));
  else if (abbr.length < 2 || abbr.length > 20 || !/^[A-Z0-9-]+$/.test(abbr)) {
    add("abbreviation", t("hplcMethod.validation.abbreviationInvalid"));
  }

  if (!options.isEditing && options.hasMultipleSections && !form.sectionId) {
    add("sectionId", t("hplcMethod.validation.sectionRequired"));
  }

  if (!form.effectiveDate) add("effectiveDate", t("hplcMethod.validation.effectiveDateRequired"));

  if (!form.columnDesignation.trim()) add("column.designation", t("hplcMethod.validation.columnDesignationRequired"));

  const rawLength = form.columnLength;
  const colLength = Number(rawLength);
  if (!rawLength || isNaN(colLength) || colLength <= 0) {
    add("column.lengthMm", t("hplcMethod.validation.columnLengthPositive"));
    add("column.length", t("hplcMethod.validation.columnLengthPositive"));
  }

  const idMm = Number(form.columnInternalDiameterMm);
  if (!form.columnInternalDiameterMm || isNaN(idMm) || idMm <= 0) {
    add("column.internalDiameterMm", t("hplcMethod.validation.columnIdPositive"));
  }

  if (!isGc) {
    // HPLC Column & Operating Conditions
    const partUm = Number(form.particleSizeUm);
    if (!form.particleSizeUm || isNaN(partUm) || partUm <= 0) {
      add("column.particleSizeUm", t("hplcMethod.validation.particleSizePositive"));
    }
    const colTemp = Number(form.columnTemperatureC);
    if (!form.columnTemperatureC || isNaN(colTemp) || colTemp <= 0) {
      add("column.temperatureC", t("hplcMethod.validation.columnTempPositive"));
    }

    const flow = Number(form.flowRateMlPerMin);
    if (!form.flowRateMlPerMin || isNaN(flow) || flow <= 0) {
      add("elution.flowRateMlPerMin", t("hplcMethod.validation.flowRatePositive"));
    }
    if (form.equilibrationMin !== "") {
      const eqMin = Number(form.equilibrationMin);
      if (isNaN(eqMin) || eqMin < 0) add("elution.equilibrationMin", t("hplcMethod.validation.equilibrationNonNegative"));
    }

    if (!form.diluentSolutionId) add("solutions.diluent", t("hplcMethod.validation.diluentRequired"));

    if (form.mobilePhases.length === 0) {
      add("form.mobilePhases", t("hplcMethod.validation.mobilePhaseRequired"));
    }
    const channelsUsed = new Set<string>();
    for (let i = 0; i < form.mobilePhases.length; i++) {
      const mp = form.mobilePhases[i];
      if (!mp.channel) add(`solutions.${i}.channel`, t("hplcMethod.validation.channelMissing", { index: i + 1 }));
      else if (channelsUsed.has(mp.channel)) {
        add(`solutions.${i}.channel`, t("hplcMethod.validation.channelDuplicate", { channel: mp.channel }));
      } else {
        channelsUsed.add(mp.channel);
      }
      if (!mp.solutionMasterId) add(`solutions.${i}.solutionId`, t("hplcMethod.validation.channelSolutionRequired", { channel: mp.channel }));
    }

    if (form.elutionMode === "Isocratic") {
      const hasAnyRatio = form.mobilePhases.some((p) => p.ratioPercent !== "");
      if (hasAnyRatio) {
        let sumRatio = 0;
        let ratiosValid = true;
        form.mobilePhases.forEach((mp, i) => {
          if (mp.ratioPercent === "") {
            add(`solutions.${i}.ratioPercent`, t("hplcMethod.validation.ratiosAllRequired"));
            ratiosValid = false;
            return;
          }
          const r = Number(mp.ratioPercent);
          if (isNaN(r) || r < 0) {
            add(`solutions.${i}.ratioPercent`, t("hplcMethod.validation.ratioNonNegative"));
            ratiosValid = false;
            return;
          }
          sumRatio += r;
        });
        if (ratiosValid && Math.abs(sumRatio - 100) > 0.01) {
          add("form.ratioSum", t("hplcMethod.validation.ratioSum", { sum: sumRatio.toFixed(1) }));
        }
      }
    } else {
      // Gradient mode
      if (form.gradientSteps.length < 2) {
        add("form.gradient", t("hplcMethod.validation.gradientMinSteps"));
      }
      let prevTime = -1;
      let prevValid = true;
      for (let i = 0; i < form.gradientSteps.length; i++) {
        const step = form.gradientSteps[i];
        const gt = Number(step.timeMin);
        if (step.timeMin === "" || isNaN(gt) || gt < 0) {
          add(`gradient.${i}.timeMin`, t("hplcMethod.validation.gradientTimeNonNegative", { index: i + 1 }));
          prevValid = false;
        } else if (i === 0 && gt !== 0) {
          add(`gradient.${i}.timeMin`, t("hplcMethod.validation.gradientFirstZero"));
          prevTime = gt;
          prevValid = true;
        } else {
          if (i > 0 && prevValid && gt <= prevTime) {
            add(
              `gradient.${i}.timeMin`,
              t("hplcMethod.validation.gradientTimeOrder", {
                index: i + 1,
                time: gt,
                prevIndex: i,
                prevTime
              })
            );
          }
          prevTime = gt;
          prevValid = true;
        }

        const pa = Number(step.percentA || 0);
        const pb = Number(step.percentB || 0);
        const pc = Number(step.percentC || 0);
        const pd = Number(step.percentD || 0);
        const sum = pa + pb + pc + pd;
        if (Math.abs(sum - 100) > 0.01) {
          add(
            `gradient.${i}.total`,
            t("hplcMethod.validation.gradientTotal100", { index: i + 1, sum: sum.toFixed(1) })
          );
        }
        if (!channelsUsed.has("A") && pa > 0) add(`gradient.${i}.percentA`, t("hplcMethod.validation.gradientUnconfiguredChannel", { index: i + 1, channel: "A" }));
        if (!channelsUsed.has("B") && pb > 0) add(`gradient.${i}.percentB`, t("hplcMethod.validation.gradientUnconfiguredChannel", { index: i + 1, channel: "B" }));
        if (!channelsUsed.has("C") && pc > 0) add(`gradient.${i}.percentC`, t("hplcMethod.validation.gradientUnconfiguredChannel", { index: i + 1, channel: "C" }));
        if (!channelsUsed.has("D") && pd > 0) add(`gradient.${i}.percentD`, t("hplcMethod.validation.gradientUnconfiguredChannel", { index: i + 1, channel: "D" }));
      }
    }
  } else {
    // GC Column & Conditions
    const filmUm = Number(form.filmThicknessUm);
    if (!form.filmThicknessUm || isNaN(filmUm) || filmUm <= 0) {
      add("column.filmThicknessUm", t("hplcMethod.validation.filmThicknessPositive"));
    }

    if (!form.carrierGas) {
      add("conditions.carrierGas", t("hplcMethod.validation.carrierGasRequired"));
      add("carrier.carrierGas", t("hplcMethod.validation.carrierGasRequired"));
    }

    const flow = Number(form.flowRateMlPerMin);
    if (!form.flowRateMlPerMin || isNaN(flow) || flow <= 0) {
      add("conditions.flowRateMlPerMin", t("hplcMethod.validation.gcFlowRatePositive"));
      add("elution.flowRateMlPerMin", t("hplcMethod.validation.gcFlowRatePositive"));
    }

    const inletT = Number(form.inletTemperatureC);
    if (!form.inletTemperatureC || isNaN(inletT) || inletT <= 0) {
      add("conditions.inletTemperatureC", t("hplcMethod.validation.inletTempPositive"));
    }

    const detT = Number(form.detectorTemperatureC);
    if (!form.detectorTemperatureC || isNaN(detT) || detT <= 0) {
      add("conditions.detectorTemperatureC", t("hplcMethod.validation.detectorTempPositive"));
    }

    if (form.splitRatio !== "" && form.splitRatio != null) {
      const split = Number(form.splitRatio);
      if (isNaN(split) || split < 1) {
        add("conditions.splitRatio", t("hplcMethod.validation.splitRatioMin"));
      }
    }

    if (!form.diluentSolutionId) {
      add("solutions.diluent", t("hplcMethod.validation.diluentRequired"));
    }

    // GC Oven Steps
    if (!form.ovenSteps || form.ovenSteps.length === 0) {
      add("form.ovenSteps", t("hplcMethod.validation.ovenMinSteps"));
    } else {
      for (let i = 0; i < form.ovenSteps.length; i++) {
        const step = form.ovenSteps[i];
        const stepNum = i + 1;
        if (i === 0) {
          if (step.rateCPerMin !== "" && step.rateCPerMin != null) {
            add("oven.0.rateCPerMin", t("hplcMethod.validation.ovenFirstNoRamp"));
          }
        } else {
          const rate = Number(step.rateCPerMin);
          if (step.rateCPerMin === "" || isNaN(rate) || rate <= 0) {
            add(`oven.${i}.rateCPerMin`, t("hplcMethod.validation.ovenRampPositive", { index: stepNum }));
          }
        }

        const temp = Number(step.temperatureC);
        if (step.temperatureC === "" || isNaN(temp) || temp <= 0) {
          add(`oven.${i}.temperatureC`, t("hplcMethod.validation.ovenTempPositive", { index: stepNum }));
        }

        const hold = Number(step.holdMin);
        if (step.holdMin === "" || isNaN(hold) || hold < 0) {
          add(`oven.${i}.holdMin`, t("hplcMethod.validation.ovenHoldNonNegative", { index: stepNum }));
        }
      }
    }

    // Headspace
    if (form.headspaceEnabled) {
      const hsEqT = Number(form.headspaceEquilibrationTemperatureC);
      if (!form.headspaceEquilibrationTemperatureC || isNaN(hsEqT) || hsEqT <= 0) {
        add("headspace.equilibrationTemperatureC", t("hplcMethod.validation.headspaceEqTempPositive"));
      }
      const hsEqMin = Number(form.headspaceEquilibrationMin);
      if (!form.headspaceEquilibrationMin || isNaN(hsEqMin) || hsEqMin <= 0) {
        add("headspace.equilibrationMin", t("hplcMethod.validation.headspaceEqTimePositive"));
      }
      const hsLineT = Number(form.headspaceTransferLineTemperatureC);
      if (!form.headspaceTransferLineTemperatureC || isNaN(hsLineT) || hsLineT <= 0) {
        add("headspace.transferLineTemperatureC", t("hplcMethod.validation.headspaceTransferTempPositive"));
      }
    }

    // Residual Solvents method-level volume
    if (isResidual) {
      const vol = Number(form.sampleSolutionVolumeMl);
      if (!form.sampleSolutionVolumeMl || isNaN(vol) || vol <= 0) {
        add("sampleSolutionVolumeMl", t("hplcMethod.validation.residualSampleVolRequired"));
      }
    }
  }

  // Related Substances fields
  if (isRelatedSubstances) {
    if (form.referenceSolutionPercent === "" || form.referenceSolutionPercent == null) {
      add("referenceSolutionPercent", t("hplcMethod.validation.refSolutionPercentRange"));
    } else {
      const rsp = Number(form.referenceSolutionPercent);
      if (isNaN(rsp) || rsp <= 0 || rsp > 100) {
        add("referenceSolutionPercent", t("hplcMethod.validation.refSolutionPercentRange"));
      }
    }

    if (form.disregardLimitPercent !== "" && form.disregardLimitPercent != null) {
      const dlp = Number(form.disregardLimitPercent);
      if (isNaN(dlp) || dlp < 0 || dlp > 100) {
        add("disregardLimitPercent", t("hplcMethod.validation.disregardLimitRange"));
      }
    }

    const impurityNames = new Set<string>();
    for (let i = 0; i < (form.impurities || []).length; i++) {
      const imp = form.impurities[i];
      const impName = (imp.name || "").trim();
      const key = (field: string) => `impurities.${i}.${field}`;
      if (!impName) {
        add(key("name"), t("hplcMethod.validation.impurityNameUnique"));
      } else if (impName.length > 100) {
        add(key("name"), t("hplcMethod.validation.impurityNameMax"));
      } else {
        const lower = impName.toLowerCase();
        if (impurityNames.has(lower)) {
          add(key("name"), t("hplcMethod.validation.impurityNameUnique"));
        }
        impurityNames.add(lower);
      }

      if (imp.relativeRetention !== "" && imp.relativeRetention != null) {
        const rrt = Number(imp.relativeRetention);
        if (isNaN(rrt) || rrt <= 0 || rrt > 100) {
          add(key("relativeRetention"), t("hplcMethod.validation.impurityRrtRange"));
        }
      }

      if (imp.correctionFactor === "" || imp.correctionFactor == null) {
        add(key("correctionFactor"), t("hplcMethod.validation.impurityCfRange"));
      } else {
        const cf = Number(imp.correctionFactor);
        if (isNaN(cf) || cf <= 0 || cf > 100) {
          add(key("correctionFactor"), t("hplcMethod.validation.impurityCfRange"));
        }
      }
    }
  }

  // Common Detection fields
  const injVol = Number(form.injectionVolumeUl);
  if (!form.injectionVolumeUl || isNaN(injVol) || injVol <= 0) {
    add("detection.injectionVolumeUl", t("hplcMethod.validation.injectionVolPositive"));
  }
  const runMin = Number(form.runTimeMin);
  if (!form.runTimeMin || isNaN(runMin) || runMin <= 0) {
    add("detection.runTimeMin", t("hplcMethod.validation.runTimePositive"));
  }

  // Analytes
  if (isRelatedSubstances) {
    if (form.analytes.length !== 1) {
      add("form.analytes", t("hplcMethod.validation.relatedOneAnalyte"));
    }
  } else {
    if (form.analytes.length === 0) add("form.analytes", t("hplcMethod.validation.analyteRequired"));
  }

  const analyteNames = new Set<string>();
  for (let i = 0; i < form.analytes.length; i++) {
    const a = form.analytes[i];
    const aName = a.name.trim();
    const key = (field: string) => `analytes.${i}.${field}`;
    if (!aName) add(key("name"), t("hplcMethod.validation.analyteNameRequired", { index: i + 1 }));
    else {
      const lowerName = aName.toLowerCase();
      if (analyteNames.has(lowerName)) add(key("name"), t("hplcMethod.validation.analyteNameDuplicate", { name: aName }));
      analyteNames.add(lowerName);
    }

    if (!isGc) {
      const wl = Number(a.wavelengthNm);
      if (!a.wavelengthNm || isNaN(wl) || wl < 190 || wl > 900) {
        add(key("wavelengthNm"), t("hplcMethod.validation.analyteWavelengthRange", { name: aName }));
      }
    }

    if (!a.standardEntryId) add(key("standardEntryId"), t("hplcMethod.validation.analyteStdRequired", { name: aName }));

    if (isResidual) {
      const stdConc = Number(a.standardConcentrationUgPerMl);
      if (!a.standardConcentrationUgPerMl || isNaN(stdConc) || stdConc <= 0) {
        add(key("standardConcentrationUgPerMl"), t("hplcMethod.validation.analyteStdConcPositive", { name: aName }));
      }
    } else if (isRelatedSubstances) {
      const twStd = Number(a.theoreticalWeightStdMg);
      const twTest = Number(a.theoreticalWeightTestMg);
      if (
        (a.theoreticalWeightStdMg !== "" && !isNaN(twStd) && twStd > 0) ||
        (a.theoreticalWeightTestMg !== "" && !isNaN(twTest) && twTest > 0)
      ) {
        add(key("theoreticalWeightStdMg"), t("hplcMethod.validation.analyteThWtUnused"));
        add(key("theoreticalWeightTestMg"), t("hplcMethod.validation.analyteThWtUnused"));
      }
    } else if (isCleaningResidue) {
      const stdConc = Number(a.standardConcentrationUgPerMl);
      if (!a.standardConcentrationUgPerMl || isNaN(stdConc) || stdConc <= 0) {
        add(key("standardConcentrationUgPerMl"), t("hplcMethod.validation.analyteStdConcPositive", { name: aName }));
      }
      const maco = Number(a.macoLimit);
      if (!a.macoLimit || isNaN(maco) || maco <= 0) {
        add(key("macoLimit"), t("hplcMethod.validation.analyteMacoPositive", { name: aName }));
      }
      if (!a.macoUnit) {
        add(key("macoUnit"), t("hplcMethod.validation.analyteMacoUnitRequired", { name: aName }));
      }
    } else {
      const twStd = Number(a.theoreticalWeightStdMg);
      if (!a.theoreticalWeightStdMg || isNaN(twStd) || twStd <= 0) {
        add(key("theoreticalWeightStdMg"), t("hplcMethod.validation.analyteThWtStdPositive", { name: aName }));
      }
      const twTest = Number(a.theoreticalWeightTestMg);
      if (!a.theoreticalWeightTestMg || isNaN(twTest) || twTest <= 0) {
        add(key("theoreticalWeightTestMg"), t("hplcMethod.validation.analyteThWtTestPositive", { name: aName }));
      }
      if (a.standardDilution !== "" && a.standardDilution != null) {
        const dil = Number(a.standardDilution);
        if (isNaN(dil) || dil <= 0) {
          add(key("standardDilution"), t("hplcMethod.validation.analyteStdDilutionPositive", { name: aName }));
        }
      }
    }

    const stdInj = Number(a.standardInjections);
    if (!a.standardInjections || isNaN(stdInj) || stdInj < 1 || !Number.isInteger(stdInj)) {
      add(key("standardInjections"), t("hplcMethod.validation.analyteStdInjectionsInt", { name: aName }));
    }

    // SST criteria (optional, but if given must be > 0)
    const checkPositive = (field: string, val: string | number, label: string) => {
      if (val !== "") {
        const n = Number(val);
        if (isNaN(n) || n <= 0) add(key(field), t("hplcMethod.validation.analyteSstPositive", { name: aName, label }));
      }
    };
    checkPositive("sstMaxRsdPercent", a.sstMaxRsdPercent, t("hplcMethod.validation.sstLabels.maxRsd"));
    checkPositive("sstMinResolution", a.sstMinResolution, t("hplcMethod.validation.sstLabels.minResolution"));
    checkPositive("sstMaxTailingFactor", a.sstMaxTailingFactor, t("hplcMethod.validation.sstLabels.maxTailing"));
    checkPositive("sstMinTheoreticalPlates", a.sstMinTheoreticalPlates, t("hplcMethod.validation.sstLabels.minPlates"));
    checkPositive("sstMinRetentionFactor", a.sstMinRetentionFactor, t("hplcMethod.validation.sstLabels.minRetention"));
    checkPositive("sstMinSignalToNoise", a.sstMinSignalToNoise, t("hplcMethod.validation.sstLabels.minSn"));
    checkPositive("sstMinPeakToValley", a.sstMinPeakToValley, t("hplcMethod.validation.sstLabels.minPv"));
  }

  return errors;
}
