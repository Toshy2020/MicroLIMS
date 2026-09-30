import { HplcMethodFormState } from "./hplcMethodForm";

export function validateHplcMethodForm(
  form: HplcMethodFormState,
  options: { isEditing: boolean; hasMultipleSections: boolean }
): string | null {
  const name = form.name.trim();
  if (!name) return "Method Name is required.";

  const abbr = form.abbreviation.trim().toUpperCase();
  if (!abbr) return "Method Abbreviation is required.";
  if (abbr.length < 2 || abbr.length > 20 || !/^[A-Z0-9-]+$/.test(abbr)) {
    return "Abbreviation must be 2–20 uppercase alphanumeric characters or hyphens.";
  }

  if (!options.isEditing && options.hasMultipleSections && !form.sectionId) {
    return "Laboratory Section is required.";
  }

  if (!form.effectiveDate) return "Effective Date is required.";

  if (!form.columnDesignation.trim()) return "Column Designation is required.";

  const lengthMm = Number(form.columnLengthMm);
  if (!form.columnLengthMm || isNaN(lengthMm) || lengthMm <= 0) {
    return "Column Length must be greater than zero.";
  }
  const idMm = Number(form.columnInternalDiameterMm);
  if (!form.columnInternalDiameterMm || isNaN(idMm) || idMm <= 0) {
    return "Column Internal Diameter must be greater than zero.";
  }
  const partUm = Number(form.particleSizeUm);
  if (!form.particleSizeUm || isNaN(partUm) || partUm <= 0) {
    return "Particle Size must be greater than zero.";
  }
  const colTemp = Number(form.columnTemperatureC);
  if (!form.columnTemperatureC || isNaN(colTemp) || colTemp <= 0) {
    return "Column Temperature must be greater than zero.";
  }

  const flow = Number(form.flowRateMlPerMin);
  if (!form.flowRateMlPerMin || isNaN(flow) || flow <= 0) {
    return "Flow Rate must be greater than zero.";
  }
  if (form.equilibrationMin !== "") {
    const eqMin = Number(form.equilibrationMin);
    if (isNaN(eqMin) || eqMin < 0) return "Equilibration Time cannot be negative.";
  }

  const injVol = Number(form.injectionVolumeUl);
  if (!form.injectionVolumeUl || isNaN(injVol) || injVol <= 0) {
    return "Injection Volume must be greater than zero.";
  }
  const runMin = Number(form.runTimeMin);
  if (!form.runTimeMin || isNaN(runMin) || runMin <= 0) {
    return "Run Time must be greater than zero.";
  }

  if (!form.diluentSolutionId) return "Diluent Solution is required.";

  if (form.mobilePhases.length === 0) return "At least one Mobile Phase channel is required.";
  const channelsUsed = new Set<string>();
  for (let i = 0; i < form.mobilePhases.length; i++) {
    const mp = form.mobilePhases[i];
    if (!mp.channel) return `Channel label missing on mobile phase #${i + 1}.`;
    if (channelsUsed.has(mp.channel)) return `Channel ${mp.channel} is duplicated in mobile phases.`;
    channelsUsed.add(mp.channel);
    if (!mp.solutionMasterId) return `Select a solution master for Channel ${mp.channel}.`;
  }

  if (form.elutionMode === "Isocratic") {
    const hasAnyRatio = form.mobilePhases.some((p) => p.ratioPercent !== "");
    if (hasAnyRatio) {
      let sumRatio = 0;
      for (const mp of form.mobilePhases) {
        if (mp.ratioPercent === "") return "All mobile phase ratios must be specified if any ratio is entered.";
        const r = Number(mp.ratioPercent);
        if (isNaN(r) || r < 0) return "Mobile phase ratio must be 0 or greater.";
        sumRatio += r;
      }
      if (Math.abs(sumRatio - 100) > 0.01) {
        return `Mobile phase ratios must sum to 100% (currently ${sumRatio.toFixed(1)}%).`;
      }
    }
  } else {
    // Gradient mode
    if (form.gradientSteps.length < 2) {
      return "Gradient elution requires at least two gradient steps.";
    }
    let prevTime = -1;
    for (let i = 0; i < form.gradientSteps.length; i++) {
      const step = form.gradientSteps[i];
      const t = Number(step.timeMin);
      if (step.timeMin === "" || isNaN(t) || t < 0) {
        return `Step #${i + 1} time must be 0 or greater.`;
      }
      if (i === 0 && t !== 0) {
        return "The first gradient step must start at time 0.0 min.";
      }
      if (i > 0 && t <= prevTime) {
        return `Step #${i + 1} time (${t} min) must be strictly greater than step #${i} (${prevTime} min).`;
      }
      prevTime = t;

      const pa = Number(step.percentA || 0);
      const pb = Number(step.percentB || 0);
      const pc = Number(step.percentC || 0);
      const pd = Number(step.percentD || 0);
      const sum = pa + pb + pc + pd;
      if (Math.abs(sum - 100) > 0.01) {
        return `Step #${i + 1} solvent percentages (A+B+C+D) must equal 100% (currently ${sum.toFixed(1)}%).`;
      }
      // Channels not defined in mobile phases must be 0%
      if (!channelsUsed.has("A") && pa > 0) return `Step #${i + 1} specifies %A, but Channel A is not configured.`;
      if (!channelsUsed.has("B") && pb > 0) return `Step #${i + 1} specifies %B, but Channel B is not configured.`;
      if (!channelsUsed.has("C") && pc > 0) return `Step #${i + 1} specifies %C, but Channel C is not configured.`;
      if (!channelsUsed.has("D") && pd > 0) return `Step #${i + 1} specifies %D, but Channel D is not configured.`;
    }
  }

  if (form.analytes.length === 0) return "At least one Analyte is required.";
  const analyteNames = new Set<string>();
  for (let i = 0; i < form.analytes.length; i++) {
    const a = form.analytes[i];
    const aName = a.name.trim();
    if (!aName) return `Analyte #${i + 1} name is required.`;
    const lowerName = aName.toLowerCase();
    if (analyteNames.has(lowerName)) return `Analyte name "${aName}" is duplicated.`;
    analyteNames.add(lowerName);

    const wl = Number(a.wavelengthNm);
    if (!a.wavelengthNm || isNaN(wl) || wl < 190 || wl > 900) {
      return `Analyte "${aName}" wavelength must be between 190 and 900 nm.`;
    }

    if (!a.standardEntryId) return `Analyte "${aName}" must select a Reference Standard.`;

    const twStd = Number(a.theoreticalWeightStdMg);
    if (!a.theoreticalWeightStdMg || isNaN(twStd) || twStd <= 0) {
      return `Analyte "${aName}" Theoretical Weight Std must be greater than zero.`;
    }
    const twTest = Number(a.theoreticalWeightTestMg);
    if (!a.theoreticalWeightTestMg || isNaN(twTest) || twTest <= 0) {
      return `Analyte "${aName}" Theoretical Weight Test must be greater than zero.`;
    }

    const stdInj = Number(a.standardInjections);
    if (!a.standardInjections || isNaN(stdInj) || stdInj < 1 || !Number.isInteger(stdInj)) {
      return `Analyte "${aName}" Standard Injections must be an integer of at least 1.`;
    }

    // SST criteria (optional, but if given must be > 0)
    const checkPositive = (val: string | number, label: string) => {
      if (val !== "") {
        const n = Number(val);
        if (isNaN(n) || n <= 0) return `Analyte "${aName}" ${label} must be greater than zero.`;
      }
      return null;
    };
    const sstErr =
      checkPositive(a.sstMaxRsdPercent, "Max RSD %") ||
      checkPositive(a.sstMinResolution, "Min Resolution") ||
      checkPositive(a.sstMaxTailingFactor, "Max Tailing Factor") ||
      checkPositive(a.sstMinTheoreticalPlates, "Min Theoretical Plates") ||
      checkPositive(a.sstMinRetentionFactor, "Min Retention Factor") ||
      checkPositive(a.sstMinSignalToNoise, "Min Signal-to-Noise") ||
      checkPositive(a.sstMinPeakToValley, "Min Peak-to-Valley");
    if (sstErr) return sstErr;
  }

  return null;
}
