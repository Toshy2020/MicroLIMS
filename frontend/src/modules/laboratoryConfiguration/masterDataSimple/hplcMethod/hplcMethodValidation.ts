import { HplcMethodFormState } from "./hplcMethodForm";

// Errors keyed by a stable field path (e.g. "column.lengthMm", "analytes.2.name",
// "gradient.3.percentB", "solutions.1.solutionId"). Keys starting with "form." are rules
// that span a whole tab and are shown in the dialog top alert instead of on a field.
export type HplcMethodErrors = Record<string, string>;

export const HPLC_FORM_ERROR_PREFIX = "form.";

// Tab indexes in the dialog; a null tab means the always-visible general header.
export const HPLC_TAB_COLUMN = 0;
export const HPLC_TAB_ELUTION = 1;
export const HPLC_TAB_SOLUTIONS = 2;
export const HPLC_TAB_ANALYTES = 3;

export function hplcErrorTab(key: string): number | null {
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

  const lengthMm = Number(form.columnLengthMm);
  if (!form.columnLengthMm || isNaN(lengthMm) || lengthMm <= 0) {
    add("column.lengthMm", "Column Length must be greater than zero.");
  }
  const idMm = Number(form.columnInternalDiameterMm);
  if (!form.columnInternalDiameterMm || isNaN(idMm) || idMm <= 0) {
    add("column.internalDiameterMm", "Column Internal Diameter must be greater than zero.");
  }
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

  const injVol = Number(form.injectionVolumeUl);
  if (!form.injectionVolumeUl || isNaN(injVol) || injVol <= 0) {
    add("detection.injectionVolumeUl", "Injection Volume must be greater than zero.");
  }
  const runMin = Number(form.runTimeMin);
  if (!form.runTimeMin || isNaN(runMin) || runMin <= 0) {
    add("detection.runTimeMin", "Run Time must be greater than zero.");
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
      // Channels not defined in mobile phases must be 0%
      if (!channelsUsed.has("A") && pa > 0) add(`gradient.${i}.percentA`, `Step #${i + 1} specifies %A, but Channel A is not configured.`);
      if (!channelsUsed.has("B") && pb > 0) add(`gradient.${i}.percentB`, `Step #${i + 1} specifies %B, but Channel B is not configured.`);
      if (!channelsUsed.has("C") && pc > 0) add(`gradient.${i}.percentC`, `Step #${i + 1} specifies %C, but Channel C is not configured.`);
      if (!channelsUsed.has("D") && pd > 0) add(`gradient.${i}.percentD`, `Step #${i + 1} specifies %D, but Channel D is not configured.`);
    }
  }

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

    const wl = Number(a.wavelengthNm);
    if (!a.wavelengthNm || isNaN(wl) || wl < 190 || wl > 900) {
      add(key("wavelengthNm"), `Analyte "${aName}" wavelength must be between 190 and 900 nm.`);
    }

    if (!a.standardEntryId) add(key("standardEntryId"), `Analyte "${aName}" must select a Reference Standard.`);

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
