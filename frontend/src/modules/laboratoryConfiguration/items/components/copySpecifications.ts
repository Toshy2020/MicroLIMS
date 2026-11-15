import {
  productionStageRoleLabel,
  type LimitType,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import { PhyschemArea, areaIncludes, areaOfItemCategory } from "../../masterDataSimple/testMasterArea";
import { formatTrimmedDecimal } from "../specWizard/specFormModel";

// Display and lab-ownership helpers shared between ItemSpecificationsSection,
// the specification wizard steps, and CopySpecificationsDialog.

export const formatLimitCell = (spec: SpecificationDto): string => {
  const type = (spec.limitType as LimitType) || "CountTiered";
  switch (type) {
    case "Range": {
      const lower = formatTrimmedDecimal(spec.lowerLimit);
      const upper = formatTrimmedDecimal(spec.upperLimit);
      if (lower && upper) return `NLT ${lower} — NMT ${upper}`;
      if (upper) return `NMT ${upper}`;
      if (lower) return `NLT ${lower}`;
      return spec.specLimit || "—";
    }
    case "NotMoreThan": {
      const upper = formatTrimmedDecimal(spec.upperLimit);
      return upper ? `NMT ${upper}` : spec.specLimit || "—";
    }
    case "NotLessThan": {
      const lower = formatTrimmedDecimal(spec.lowerLimit);
      return lower ? `NLT ${lower}` : spec.specLimit || "—";
    }
    case "TargetWithTolerance": {
      const target = formatTrimmedDecimal(spec.target);
      const tol = formatTrimmedDecimal(spec.tolerance);
      const suffix = spec.toleranceMode === "Percent" ? "%" : "";
      if (target && tol) return `${target} ± ${tol}${suffix}`;
      return spec.specLimit || "—";
    }
    case "CountTiered": {
      const alert = spec.alertLimit ? `Alert ${spec.alertLimit}` : "";
      const action = spec.actionLimit ? `Action ${spec.actionLimit}` : "";
      const sp = spec.specLimit ? `Spec ${spec.specLimit}` : "";
      const parts = [alert, action, sp].filter(Boolean);
      return parts.length > 0 ? parts.join(" · ") : spec.specLimit || "—";
    }
    case "Qualitative": {
      return spec.expectedResultText || spec.specLimit || "—";
    }
    case "PresenceAbsence": {
      const state = spec.expectedState === "Presence" ? "Present" : "Absent";
      const qty = formatTrimmedDecimal(spec.sampleQuantity);
      if (qty) {
        const unitStr = spec.sampleQuantityUnit ? ` ${spec.sampleQuantityUnit}` : "";
        return `${state} in ${qty}${unitStr}`;
      }
      return spec.specLimit || state;
    }
    case "MultiStage": {
      return "—";
    }
    case "DissolutionQ": {
      const q = formatTrimmedDecimal(spec.lowerLimit);
      return q ? `Q = ${q} %` : spec.specLimit || "—";
    }
    case "DisintegrationTime": {
      const t = formatTrimmedDecimal(spec.upperLimit);
      return t ? `NMT ${t} min` : spec.specLimit || "—";
    }
    case "WeightVariation": {
      if (spec.dosageForm === "Tablet") {
        return "USP <2091>: tablets, limit by average weight";
      }
      if (spec.dosageForm === "HardCapsule" || spec.dosageForm === "SoftCapsule") {
        return "USP <2091>: net content 90-110 % of average";
      }
      return spec.specLimit || "—";
    }
    default:
      return spec.specLimit || "—";
  }
};

export const formatResultBasis = (basis?: string | null, isIcp?: boolean) => {
  if (!basis) return null;
  if (isIcp && basis === "MgPerKg") return "ppm";
  switch (basis) {
    case "MgPerKg":
      return "mg/kg per sample";
    case "MgPerUnit":
      return "mg per unit";
    case "PercentLabelClaim":
      return "% label claim";
    case "PercentAsIs":
      return "% as is";
    case "PercentDriedBasis":
      return "% dried basis";
    case "PercentAnhydrousBasis":
      return "% anhydrous basis";
    case "Ppm":
      return "ppm";
    default:
      return basis;
  }
};

// One line a reviewer can read: the limit as the table shows it, the unit and
// the multi-stage criteria (which the table lists on their own rows).
export function limitSummary(spec: SpecificationDto): string {
  const stages = spec.limitType === "MultiStage" ? (spec.stages ?? []) : [];
  const limit =
    stages.length > 0
      ? stages.map((s) => `${s.stageLabel}: ${s.acceptanceCriteriaText}`).join(" · ")
      : formatLimitCell(spec);
  return spec.unit ? `${limit} ${spec.unit}` : limit;
}

// The qualifiers the table shows as chips next to a row.
export function specQualifiers(spec: SpecificationDto): string[] {
  const out: string[] = [];
  if (spec.grade) out.push(`Grade: ${spec.grade}`);
  if (spec.productionStageRole) out.push(`Stage: ${productionStageRoleLabel(spec.productionStageRole)}`);
  if (spec.resultBasis) out.push(`Basis: ${formatResultBasis(spec.resultBasis as string)}`);
  if (spec.limitType === "CountTiered" && spec.dilutionFactor != null) out.push(`Dilution Factor ×${spec.dilutionFactor}`);
  if (spec.limitType === "DissolutionQ" && spec.labelClaim != null) {
    out.push(`Label Claim ${spec.labelClaim} ${spec.labelClaimUnit || "mg"}`);
  } else if (spec.labelClaim != null) {
    // A label claim belongs to one product strength: the reviewer must see it
    // on every row that carries one before copying it to another item.
    out.push(`Label Claim ${spec.labelClaim}${spec.labelClaimUnit ? ` ${spec.labelClaimUnit}` : ""}`);
  }
  if (spec.referenceStandard) out.push(`Reference: ${spec.referenceStandard}`);
  return out;
}

export interface TestDefinitionForLab {
  physchemArea?: PhyschemArea | null;
  sectionId?: number | null;
}

/**
 * The target item's assigned tests that belong to the caller's own laboratory.
 * A test whose section is unknown, or while the caller's lab list is empty, is
 * kept (the server is still the real gate).
 */
export function ownLabTestCodes(
  assignedTests: { testCode: string }[],
  definitionByCode: Record<string, TestDefinitionForLab | undefined>,
  myLabSectionIds: ReadonlySet<number>,
  itemCategory: string
): Set<string> {
  const codes = new Set<string>();
  for (const t of assignedTests) {
    const def = definitionByCode[t.testCode];
    const sectionId = def?.sectionId;
    if (
      areaIncludes(def?.physchemArea, areaOfItemCategory(itemCategory)) &&
      (sectionId == null || myLabSectionIds.size === 0 || myLabSectionIds.has(sectionId))
    ) {
      codes.add(t.testCode);
    }
  }
  return codes;
}
