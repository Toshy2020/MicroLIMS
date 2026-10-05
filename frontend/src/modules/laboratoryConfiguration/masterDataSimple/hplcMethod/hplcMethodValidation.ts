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
  if (key.startsWith("analytes.") || key === "form.analytes") return HPLC_TAB_ANALYTES;
  return null;
}

export function validateHplcMethodForm(
  form: HplcMethodFormState,
  options: { isEditing: boolean; hasMultipleSections: boolean }
): HplcMethodErrors {
  const errors: HplcMethodErrors = {};
  const add = (key: string, message: string) => {
    if (!(key in errors)) errors[key] = message;
  };

  const isGc = form.technique === "Gc";
  const isResidual = isGc && form.resultMode === "ResidualSolvents";

  const name = form.name.trim();
  if (!name) add("name", "Method Name is required.");

  const abbr = form.abbreviation.trim().toUpperCase();
  if (!abbr) add("abbreviation", "Method Abbreviation is required.");
  else if (abbr.length < 2 || abbr.length > 20 || !/^[A-Z0-9-]+$/.test(abbr)) {
    add("abbreviation", "Abbreviation must be 2–20 uppercase alphanumeric characters or hyphens.");
  }

  if (!options.isEditing && options.hasMultipleSections && !form.sectionId) {
    add("sectionId", "Laboratory Section is required.");
  }

  if (!form.effectiveDate) add("effectiveDate", "Effective Date is required.");

  if (!form.columnDesignation.trim()) add("column.designation", "Column Designation is required.");

  const rawLength = form.columnLength;
  const colLength = Number(rawLength);
  if (!rawLength || isNaN(colLength) || colLength <= 0) {
    add("column.lengthMm", "Column Length must be greater than zero.");
    add("column.length", "Column Length must be greater than zero.");
  }

  const idMm = Number(form.columnInternalDiameterMm);
  if (!form.columnInternalDiameterMm || isNaN(idMm) || idMm <= 0) {
    add("column.internalDiameterMm", "Column Internal Diameter must be greater than zero.");
  }

  if (!isGc) {
    // HPLC Column & Operating Conditions
    const partUm = Number(form.particleSizeUm);
    if (!form.particleSizeUm || isNaN(partUm) || partUm <= 0) {
      add("column.particleSizeUm", "Particle Size must be greater than zero.");
    }
    const colTemp = Number(form.columnTemperatureC);
    if (!form.columnTemperatureC || isNaN(colTemp) || colTemp <= 0) {
      add("column.temperatureC", "Column Temperature must be greater than zero.");
    }

    const flow = Number(form.flowRateMlPerMin);
    if (!form.flowRateMlPerMin || isNaN(flow) || flow <= 0) {
      add("elution.flowRateMlPerMin", "Flow Rate must be greater than zero.");
    }
    if (form.equilibrationMin !== "") {
      const eqMin = Number(form.equilibrationMin);
      if (isNaN(eqMin) || eqMin < 0) add("elution.equilibrationMin", "Equilibration Time cannot be negative.");
    }

    if (!form.diluentSolutionId) add("solutions.diluent", "Diluent Solution is required.");

    if (form.mobilePhases.length === 0) {
      add("form.mobilePhases", "At least one Mobile Phase channel is required.");
    }
    const channelsUsed = new Set<string>();
    for (let i = 0; i < form.mobilePhases.length; i++) {
      const mp = form.mobilePhases[i];
      if (!mp.channel) add(`solutions.${i}.channel`, `Channel label missing on mobile phase #${i + 1}.`);
      else if (channelsUsed.has(mp.channel)) {
        add(`solutions.${i}.channel`, `Channel ${mp.channel} is duplicated in mobile phases.`);
      } else {
        channelsUsed.add(mp.channel);
      }
      if (!mp.solutionMasterId) add(`solutions.${i}.solutionId`, `Select a solution master for Channel ${mp.channel}.`);
    }

    if (form.elutionMode === "Isocratic") {
      const hasAnyRatio = form.mobilePhases.some((p) => p.ratioPercent !== "");
      if (hasAnyRatio) {
        let sumRatio = 0;
        let ratiosValid = true;
        form.mobilePhases.forEach((mp, i) => {
          if (mp.ratioPercent === "") {
            add(`solutions.${i}.ratioPercent`, "All mobile phase ratios must be specified if any ratio is entered.");
            ratiosValid = false;
            return;
          }
          const r = Number(mp.ratioPercent);
          if (isNaN(r) || r < 0) {
            add(`solutions.${i}.ratioPercent`, "Mobile phase ratio must be 0 or greater.");
            ratiosValid = false;
            return;
          }
          sumRatio += r;
        });
        if (ratiosValid && Math.abs(sumRatio - 100) > 0.01) {
          add("form.ratioSum", `Mobile phase ratios must sum to 100% (currently ${sumRatio.toFixed(1)}%).`);
        }
      }
    } else {
      // Gradient mode
      if (form.gradientSteps.length < 2) {
        add("form.gradient", "Gradient elution requires at least two gradient steps.");
      }
      let prevTime = -1;
      let prevValid = true;
      for (let i = 0; i < form.gradientSteps.length; i++) {
        const step = form.gradientSteps[i];
        const t = Number(step.timeMin);
        if (step.timeMin === "" || isNaN(t) || t < 0) {
          add(`gradient.${i}.timeMin`, `Step #${i + 1} time must be 0 or greater.`);
          prevValid = false;
        } else if (i === 0 && t !== 0) {
          add(`gradient.${i}.timeMin`, "The first gradient step must start at time 0.0 min.");
          prevTime = t;
          prevValid = true;
        } else {
          if (i > 0 && prevValid && t <= prevTime) {
            add(
              `gradient.${i}.timeMin`,
              `Step #${i + 1} time (${t} min) must be strictly greater than step #${i} (${prevTime} min).`
            );
          }
          prevTime = t;
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
            `Step #${i + 1} solvent percentages (A+B+C+D) must equal 100% (currently ${sum.toFixed(1)}%).`
          );
        }
        if (!channelsUsed.has("A") && pa > 0) add(`gradient.${i}.percentA`, `Step #${i + 1} specifies %A, but Channel A is not configured.`);
        if (!channelsUsed.has("B") && pb > 0) add(`gradient.${i}.percentB`, `Step #${i + 1} specifies %B, but Channel B is not configured.`);
        if (!channelsUsed.has("C") && pc > 0) add(`gradient.${i}.percentC`, `Step #${i + 1} specifies %C, but Channel C is not configured.`);
        if (!channelsUsed.has("D") && pd > 0) add(`gradient.${i}.percentD`, `Step #${i + 1} specifies %D, but Channel D is not configured.`);
      }
    }
  } else {
    // GC Column & Conditions
    const filmUm = Number(form.filmThicknessUm);
    if (!form.filmThicknessUm || isNaN(filmUm) || filmUm <= 0) {
      add("column.filmThicknessUm", "Film Thickness must be greater than zero.");
    }

    if (!form.carrierGas) {
      add("conditions.carrierGas", "Carrier Gas is required for a GC method.");
      add("carrier.carrierGas", "Carrier Gas is required for a GC method.");
    }

    const flow = Number(form.flowRateMlPerMin);
    if (!form.flowRateMlPerMin || isNaN(flow) || flow <= 0) {
      add("conditions.flowRateMlPerMin", "Column Flow Rate must be greater than zero.");
      add("elution.flowRateMlPerMin", "Column Flow Rate must be greater than zero.");
    }

    const inletT = Number(form.inletTemperatureC);
    if (!form.inletTemperatureC || isNaN(inletT) || inletT <= 0) {
      add("conditions.inletTemperatureC", "Inlet Temperature must be greater than zero.");
    }

    const detT = Number(form.detectorTemperatureC);
    if (!form.detectorTemperatureC || isNaN(detT) || detT <= 0) {
      add("conditions.detectorTemperatureC", "Detector Temperature must be greater than zero.");
    }

    if (form.splitRatio !== "" && form.splitRatio != null) {
      const split = Number(form.splitRatio);
      if (isNaN(split) || split < 1) {
        add("conditions.splitRatio", "Split ratio must be 1 or more (leave empty for splitless).");
      }
    }

    if (!form.diluentSolutionId) {
      add("solutions.diluent", "Diluent Solution is required.");
    }

    // GC Oven Steps
    if (!form.ovenSteps || form.ovenSteps.length === 0) {
      add("form.ovenSteps", "A GC method needs at least one oven program step.");
    } else {
      for (let i = 0; i < form.ovenSteps.length; i++) {
        const step = form.ovenSteps[i];
        const stepNum = i + 1;
        if (i === 0) {
          if (step.rateCPerMin !== "" && step.rateCPerMin != null) {
            add("oven.0.rateCPerMin", "The first oven step is the initial temperature and has no ramp rate.");
          }
        } else {
          const rate = Number(step.rateCPerMin);
          if (step.rateCPerMin === "" || isNaN(rate) || rate <= 0) {
            add(`oven.${i}.rateCPerMin`, `Oven step #${stepNum}: the ramp rate must be greater than zero.`);
          }
        }

        const temp = Number(step.temperatureC);
        if (step.temperatureC === "" || isNaN(temp) || temp <= 0) {
          add(`oven.${i}.temperatureC`, `Oven step #${stepNum}: temperature must be greater than zero.`);
        }

        const hold = Number(step.holdMin);
        if (step.holdMin === "" || isNaN(hold) || hold < 0) {
          add(`oven.${i}.holdMin`, `Oven step #${stepNum}: hold time must be zero or greater.`);
        }
      }
    }

    // Headspace
    if (form.headspaceEnabled) {
      const hsEqT = Number(form.headspaceEquilibrationTemperatureC);
      if (!form.headspaceEquilibrationTemperatureC || isNaN(hsEqT) || hsEqT <= 0) {
        add("headspace.equilibrationTemperatureC", "Headspace equilibration temperature must be greater than zero.");
      }
      const hsEqMin = Number(form.headspaceEquilibrationMin);
      if (!form.headspaceEquilibrationMin || isNaN(hsEqMin) || hsEqMin <= 0) {
        add("headspace.equilibrationMin", "Headspace equilibration time must be greater than zero.");
      }
      const hsLineT = Number(form.headspaceTransferLineTemperatureC);
      if (!form.headspaceTransferLineTemperatureC || isNaN(hsLineT) || hsLineT <= 0) {
        add("headspace.transferLineTemperatureC", "Headspace transfer line temperature must be greater than zero.");
      }
    }

    // Residual Solvents method-level volume
    if (isResidual) {
      const vol = Number(form.sampleSolutionVolumeMl);
      if (!form.sampleSolutionVolumeMl || isNaN(vol) || vol <= 0) {
        add("sampleSolutionVolumeMl", "Sample solution volume is required for residual solvents.");
      }
    }
  }

  // Common Detection fields
  const injVol = Number(form.injectionVolumeUl);
  if (!form.injectionVolumeUl || isNaN(injVol) || injVol <= 0) {
    add("detection.injectionVolumeUl", "Injection Volume must be greater than zero.");
  }
  const runMin = Number(form.runTimeMin);
  if (!form.runTimeMin || isNaN(runMin) || runMin <= 0) {
    add("detection.runTimeMin", "Run Time must be greater than zero.");
  }

  // Analytes
  if (form.analytes.length === 0) add("form.analytes", "At least one Analyte is required.");
  const analyteNames = new Set<string>();
  for (let i = 0; i < form.analytes.length; i++) {
    const a = form.analytes[i];
    const aName = a.name.trim();
    const key = (field: string) => `analytes.${i}.${field}`;
    if (!aName) add(key("name"), `Analyte #${i + 1} name is required.`);
    else {
      const lowerName = aName.toLowerCase();
      if (analyteNames.has(lowerName)) add(key("name"), `Analyte name "${aName}" is duplicated.`);
      analyteNames.add(lowerName);
    }

    if (!isGc) {
      const wl = Number(a.wavelengthNm);
      if (!a.wavelengthNm || isNaN(wl) || wl < 190 || wl > 900) {
        add(key("wavelengthNm"), `Analyte "${aName}" wavelength must be between 190 and 900 nm.`);
      }
    }

    if (!a.standardEntryId) add(key("standardEntryId"), `Analyte "${aName}" must select a Reference Standard.`);

    if (isResidual) {
      const stdConc = Number(a.standardConcentrationUgPerMl);
      if (!a.standardConcentrationUgPerMl || isNaN(stdConc) || stdConc <= 0) {
        add(key("standardConcentrationUgPerMl"), `Analyte "${aName}" Standard Concentration (µg/mL) must be greater than zero.`);
      }
    } else {
      const twStd = Number(a.theoreticalWeightStdMg);
      if (!a.theoreticalWeightStdMg || isNaN(twStd) || twStd <= 0) {
        add(key("theoreticalWeightStdMg"), `Analyte "${aName}" Theoretical Weight Std must be greater than zero.`);
      }
      const twTest = Number(a.theoreticalWeightTestMg);
      if (!a.theoreticalWeightTestMg || isNaN(twTest) || twTest <= 0) {
        add(key("theoreticalWeightTestMg"), `Analyte "${aName}" Theoretical Weight Test must be greater than zero.`);
      }
      if (a.standardDilution !== "" && a.standardDilution != null) {
        const dil = Number(a.standardDilution);
        if (isNaN(dil) || dil <= 0) {
          add(key("standardDilution"), `Analyte "${aName}" standard dilution must be greater than zero.`);
        }
      }
    }

    const stdInj = Number(a.standardInjections);
    if (!a.standardInjections || isNaN(stdInj) || stdInj < 1 || !Number.isInteger(stdInj)) {
      add(key("standardInjections"), `Analyte "${aName}" Standard Injections must be an integer of at least 1.`);
    }

    // SST criteria (optional, but if given must be > 0)
    const checkPositive = (field: string, val: string | number, label: string) => {
      if (val !== "") {
        const n = Number(val);
        if (isNaN(n) || n <= 0) add(key(field), `Analyte "${aName}" ${label} must be greater than zero.`);
      }
    };
    checkPositive("sstMaxRsdPercent", a.sstMaxRsdPercent, "Max RSD %");
    checkPositive("sstMinResolution", a.sstMinResolution, "Min Resolution");
    checkPositive("sstMaxTailingFactor", a.sstMaxTailingFactor, "Max Tailing Factor");
    checkPositive("sstMinTheoreticalPlates", a.sstMinTheoreticalPlates, "Min Theoretical Plates");
    checkPositive("sstMinRetentionFactor", a.sstMinRetentionFactor, "Min Retention Factor");
    checkPositive("sstMinSignalToNoise", a.sstMinSignalToNoise, "Min Signal-to-Noise");
    checkPositive("sstMinPeakToValley", a.sstMinPeakToValley, "Min Peak-to-Valley");
  }

  return errors;
}
