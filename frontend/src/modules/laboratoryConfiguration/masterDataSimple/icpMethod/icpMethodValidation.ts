import {
  IcpMethodFormState,
  normaliseSymbol
} from "./icpMethodForm";

export type IcpMethodErrors = Record<string, string>;

export const ICP_FORM_ERROR_PREFIX = "icp_form_";

export interface ValidateIcpMethodOptions {
  isEditing?: boolean;
  hasMultipleSections?: boolean;
}

export function validateIcpMethodForm(
  form: IcpMethodFormState,
  options: ValidateIcpMethodOptions = {}
): IcpMethodErrors {
  const errors: IcpMethodErrors = {};
  const add = (field: string, msg: string) => {
    errors[field] = msg;
  };

  // 1. General
  if (!form.name || !form.name.trim()) {
    add("name", "Method name is required.");
  } else if (form.name.trim().length > 200) {
    add("name", "Method name cannot exceed 200 characters.");
  }

  if (!form.abbreviation || !form.abbreviation.trim()) {
    add("abbreviation", "Abbreviation is required.");
  } else {
    const abbr = form.abbreviation.trim().toUpperCase();
    if (abbr.length < 2 || abbr.length > 20 || !/^[A-Z0-9-]+$/.test(abbr)) {
      add("abbreviation", "Abbreviation must be 2-20 uppercase letters, digits or hyphens.");
    }
  }

  if (!form.effectiveDate) {
    add("effectiveDate", "Effective Date is required.");
  }

  if (options.hasMultipleSections && !form.sectionId) {
    add("sectionId", "Laboratory section is required.");
  }

  if (!form.mode) {
    add("mode", "Method mode is required.");
  }

  // 2. Calibration
  if (!form.standardLevelsMgPerL || !form.standardLevelsMgPerL.trim()) {
    add("standardLevelsMgPerL", "Standard levels cannot be empty.");
  } else {
    const parts = form.standardLevelsMgPerL
      .split(",")
      .map((p) => p.trim())
      .filter((p) => p.length > 0);

    if (parts.length < 2) {
      add("standardLevelsMgPerL", "At least two standard levels are required.");
    } else {
      const seen = new Set<number>();
      let hasError = false;
      for (const part of parts) {
        const val = Number(part);
        if (isNaN(val) || part === "") {
          add("standardLevelsMgPerL", `"${part}" is not a valid standard level.`);
          hasError = true;
          break;
        }
        if (val <= 0) {
          add("standardLevelsMgPerL", `Standard level ${part} must be greater than zero.`);
          hasError = true;
          break;
        }
        if (seen.has(val)) {
          add("standardLevelsMgPerL", "Standard levels must be distinct.");
          hasError = true;
          break;
        }
        seen.add(val);
      }
      if (!hasError && parts.length < 2) {
        add("standardLevelsMgPerL", "At least two standard levels are required.");
      }
    }
  }

  if (!form.calibrationStandardEntryId) {
    add("calibrationStandardEntryId", "Calibration standard is required.");
  }

  if (form.minCorrelation === "" || form.minCorrelation == null) {
    add("minCorrelation", "Minimum correlation is required.");
  } else {
    const val = Number(form.minCorrelation);
    if (isNaN(val) || val <= 0 || val > 1) {
      add("minCorrelation", "Minimum correlation must be greater than 0 and at most 1.");
    }
  }

  if (form.maxCalibrationAgeHours === "" || form.maxCalibrationAgeHours == null) {
    add("maxCalibrationAgeHours", "Calibration age is required.");
  } else {
    const val = Number(form.maxCalibrationAgeHours);
    if (isNaN(val) || val < 1 || val > 168 || !Number.isInteger(val)) {
      add("maxCalibrationAgeHours", "Calibration age must be between 1 and 168 hours.");
    }
  }

  // 3. Optional checks
  if (form.requireBlank) {
    if (form.blankMaxMgPerL === "" || form.blankMaxMgPerL == null || Number(form.blankMaxMgPerL) <= 0) {
      add("blankMaxMgPerL", "Blank limit (mg/L) is required when the blank check is on.");
    }
  }

  if (form.requireIcv) {
    const hasStd = Boolean(form.icvStandardEntryId);
    const nom = Number(form.icvNominalMgPerL);
    const low = Number(form.icvRecoveryLowPercent);
    const high = Number(form.icvRecoveryHighPercent);

    if (
      !hasStd ||
      isNaN(nom) ||
      nom <= 0 ||
      isNaN(low) ||
      low <= 0 ||
      isNaN(high) ||
      high <= 0 ||
      low >= high
    ) {
      add("icvCheck", "ICV standard, nominal (mg/L) and recovery limits are required when the ICV check is on.");
      if (!hasStd) add("icvStandardEntryId", "ICV reference standard is required.");
      if (isNaN(nom) || nom <= 0) add("icvNominalMgPerL", "Nominal concentration must be greater than zero.");
      if (isNaN(low) || low <= 0) add("icvRecoveryLowPercent", "Recovery low limit must be greater than zero.");
      if (isNaN(high) || high <= 0) add("icvRecoveryHighPercent", "Recovery high limit must be greater than zero.");
      if (!isNaN(low) && !isNaN(high) && low >= high) {
        add("icvRecoveryHighPercent", "Recovery high % must be strictly greater than low %.");
      }
    }

    if (hasStd && form.calibrationStandardEntryId && form.icvStandardEntryId === form.calibrationStandardEntryId) {
      add("icvStandardEntryId", "The ICV standard must be a second source, not the calibration standard.");
    }
  }

  if (form.requireCcv) {
    const nom = Number(form.ccvNominalMgPerL);
    const low = Number(form.ccvRecoveryLowPercent);
    const high = Number(form.ccvRecoveryHighPercent);

    if (
      isNaN(nom) ||
      nom <= 0 ||
      isNaN(low) ||
      low <= 0 ||
      isNaN(high) ||
      high <= 0 ||
      low >= high
    ) {
      add("ccvCheck", "CCV nominal (mg/L) and recovery limits are required when the CCV check is on.");
      if (isNaN(nom) || nom <= 0) add("ccvNominalMgPerL", "Nominal concentration must be greater than zero.");
      if (isNaN(low) || low <= 0) add("ccvRecoveryLowPercent", "Recovery low limit must be greater than zero.");
      if (isNaN(high) || high <= 0) add("ccvRecoveryHighPercent", "Recovery high limit must be greater than zero.");
      if (!isNaN(low) && !isNaN(high) && low >= high) {
        add("ccvRecoveryHighPercent", "Recovery high % must be strictly greater than low %.");
      }
    }
  }

  // 4. Sample prep defaults
  if (form.sampleVolumeMl === "" || form.sampleVolumeMl == null) {
    add("sampleVolumeMl", "Sample volume is required.");
  } else {
    const val = Number(form.sampleVolumeMl);
    if (isNaN(val) || val <= 0) {
      add("sampleVolumeMl", "Sample volume must be greater than zero.");
    }
  }

  if (form.dilutionFactor === "" || form.dilutionFactor == null) {
    add("dilutionFactor", "Dilution factor is required.");
  } else {
    const val = Number(form.dilutionFactor);
    if (isNaN(val) || val < 1) {
      add("dilutionFactor", "Dilution factor must be 1 or more.");
    }
  }

  // 5. Elements
  if (!form.elements || form.elements.length === 0) {
    add(`${ICP_FORM_ERROR_PREFIX}elements`, "At least one element is required.");
  } else {
    const seenSymbols = new Set<string>();
    form.elements.forEach((e, idx) => {
      const raw = e.symbol ? e.symbol.trim() : "";
      if (!raw || !/^[A-Za-z]{1,3}$/.test(raw)) {
        add(`element_${idx}_symbol`, "Element symbol must be 1-3 letters.");
      } else {
        const norm = normaliseSymbol(raw);
        if (seenSymbols.has(norm.toUpperCase())) {
          add(`element_${idx}_symbol`, `Element "${norm}" is listed more than once.`);
        } else {
          seenSymbols.add(norm.toUpperCase());
        }
      }

      const wl = Number(e.wavelengthNm);
      if (isNaN(wl) || wl <= 0) {
        const label = raw ? normaliseSymbol(raw) : `Element #${idx + 1}`;
        add(`element_${idx}_wavelengthNm`, `${label}: wavelength must be greater than zero.`);
      }

      const cf = Number(e.conversionFactor);
      if (isNaN(cf) || cf <= 0) {
        const label = raw ? normaliseSymbol(raw) : `Element #${idx + 1}`;
        add(`element_${idx}_conversionFactor`, `${label}: conversion factor must be greater than zero.`);
      }
    });
  }

  return errors;
}

export function icpErrorTab(errorKey: string): number | null {
  if (
    errorKey === "name" ||
    errorKey === "abbreviation" ||
    errorKey === "effectiveDate" ||
    errorKey === "sectionId" ||
    errorKey === "mode"
  ) {
    return 0; // General
  }

  if (
    errorKey === "standardLevelsMgPerL" ||
    errorKey === "calibrationStandardEntryId" ||
    errorKey === "minCorrelation" ||
    errorKey === "maxCalibrationAgeHours"
  ) {
    return 1; // Calibration
  }

  if (
    errorKey === "requireBlank" ||
    errorKey === "blankMaxMgPerL" ||
    errorKey === "requireIcv" ||
    errorKey === "icvCheck" ||
    errorKey === "icvStandardEntryId" ||
    errorKey === "icvNominalMgPerL" ||
    errorKey === "icvRecoveryLowPercent" ||
    errorKey === "icvRecoveryHighPercent" ||
    errorKey === "requireCcv" ||
    errorKey === "ccvCheck" ||
    errorKey === "ccvNominalMgPerL" ||
    errorKey === "ccvRecoveryLowPercent" ||
    errorKey === "ccvRecoveryHighPercent"
  ) {
    return 2; // Optional Checks
  }

  if (errorKey === "sampleVolumeMl" || errorKey === "dilutionFactor") {
    return 3; // Sample Prep
  }

  if (errorKey.startsWith("element_") || errorKey.includes("elements")) {
    return 4; // Elements
  }

  return null;
}
