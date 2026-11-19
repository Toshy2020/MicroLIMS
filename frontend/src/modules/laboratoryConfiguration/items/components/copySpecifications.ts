import type { TFunction } from "i18next";
import { i18n } from "../../../../i18n";
import {
  productionStageRoleLabel,
  type LimitType,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import { PhyschemArea, areaIncludes, areaOfItemCategory } from "../../masterDataSimple/testMasterArea";
import { formatTrimmedDecimal } from "../specWizard/specFormModel";

// Display and lab-ownership helpers shared between ItemSpecificationsSection,
// the specification wizard steps, and CopySpecificationsDialog.

export const formatLimitCell = (
  spec: SpecificationDto,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): string => {
  const type = (spec.limitType as LimitType) || "CountTiered";
  switch (type) {
    case "Range": {
      const lower = formatTrimmedDecimal(spec.lowerLimit);
      const upper = formatTrimmedDecimal(spec.upperLimit);
      if (lower && upper) return t("items.specFormatting.rangeBoth", { lower, upper });
      if (upper) return t("items.specFormatting.nmt", { upper });
      if (lower) return t("items.specFormatting.nlt", { lower });
      return spec.specLimit || "—";
    }
    case "NotMoreThan": {
      const upper = formatTrimmedDecimal(spec.upperLimit);
      return upper ? t("items.specFormatting.nmt", { upper }) : spec.specLimit || "—";
    }
    case "NotLessThan": {
      const lower = formatTrimmedDecimal(spec.lowerLimit);
      return lower ? t("items.specFormatting.nlt", { lower }) : spec.specLimit || "—";
    }
    case "TargetWithTolerance": {
      const target = formatTrimmedDecimal(spec.target);
      const tol = formatTrimmedDecimal(spec.tolerance);
      const suffix = spec.toleranceMode === "Percent" ? "%" : "";
      if (target && tol) return `${target} ± ${tol}${suffix}`;
      return spec.specLimit || "—";
    }
    case "CountTiered": {
      const alert = spec.alertLimit ? t("items.specFormatting.countAlert", { value: spec.alertLimit }) : "";
      const action = spec.actionLimit ? t("items.specFormatting.countAction", { value: spec.actionLimit }) : "";
      const sp = spec.specLimit ? t("items.specFormatting.countSpec", { value: spec.specLimit }) : "";
      const parts = [alert, action, sp].filter(Boolean);
      return parts.length > 0 ? parts.join(" · ") : spec.specLimit || "—";
    }
    case "Qualitative": {
      return spec.expectedResultText || spec.specLimit || "—";
    }
    case "PresenceAbsence": {
      const state = spec.expectedState === "Presence" ? t("items.specFormatting.present") : t("items.specFormatting.absent");
      const qty = formatTrimmedDecimal(spec.sampleQuantity);
      if (qty) {
        return spec.sampleQuantityUnit
          ? t("items.specFormatting.presenceInQtyUnit", { state, qty, unit: spec.sampleQuantityUnit })
          : t("items.specFormatting.presenceInQty", { state, qty });
      }
      return spec.specLimit || state;
    }
    case "MultiStage": {
      return "—";
    }
    case "DissolutionQ": {
      const q = formatTrimmedDecimal(spec.lowerLimit);
      return q ? t("items.specFormatting.dissolutionQ", { q }) : spec.specLimit || "—";
    }
    case "DisintegrationTime": {
      const u = formatTrimmedDecimal(spec.upperLimit);
      return u ? t("items.specFormatting.disintegrationTime", { t: u }) : spec.specLimit || "—";
    }
    case "WeightVariation": {
      if (spec.dosageForm === "Tablet") {
        return t("items.specFormatting.weightVariationTablet");
      }
      if (spec.dosageForm === "HardCapsule" || spec.dosageForm === "SoftCapsule") {
        return t("items.specFormatting.weightVariationCapsule");
      }
      return spec.specLimit || "—";
    }
    default:
      return spec.specLimit || "—";
  }
};

export const formatResultBasis = (
  basis?: string | null,
  isIcp?: boolean,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
) => {
  if (!basis) return null;
  if (isIcp && basis === "MgPerKg") return t("items.specFormatting.basis.Ppm");
  switch (basis) {
    case "MgPerKg":
      return t("items.specFormatting.basis.MgPerKg");
    case "MgPerUnit":
      return t("items.specFormatting.basis.MgPerUnit");
    case "PercentLabelClaim":
      return t("items.specFormatting.basis.PercentLabelClaim");
    case "PercentAsIs":
      return t("items.specFormatting.basis.PercentAsIs");
    case "PercentDriedBasis":
      return t("items.specFormatting.basis.PercentDriedBasis");
    case "PercentAnhydrousBasis":
      return t("items.specFormatting.basis.PercentAnhydrousBasis");
    case "Ppm":
      return t("items.specFormatting.basis.Ppm");
    default:
      return basis;
  }
};

// One line a reviewer can read: the limit as the table shows it, the unit and
// the multi-stage criteria (which the table lists on their own rows).
export function limitSummary(
  spec: SpecificationDto,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): string {
  const stages = spec.limitType === "MultiStage" ? (spec.stages ?? []) : [];
  const limit =
    stages.length > 0
      ? stages.map((s) => `${s.stageLabel}: ${s.acceptanceCriteriaText}`).join(" · ")
      : formatLimitCell(spec, t);
  return spec.unit ? `${limit} ${spec.unit}` : limit;
}

// The qualifiers the table shows as chips next to a row.
export function specQualifiers(
  spec: SpecificationDto,
  t: TFunction<"labConfig"> = i18n.getFixedT("en", "labConfig")
): string[] {
  const out: string[] = [];
  if (spec.grade) out.push(t("items.specifications.badges.grade", { grades: spec.grade }));
  if (spec.productionStageRole) out.push(t("items.specifications.badges.stage", { stage: productionStageRoleLabel(spec.productionStageRole) }));
  if (spec.resultBasis) out.push(t("items.specifications.badges.basis", { basis: formatResultBasis(spec.resultBasis as string, false, t) }));
  if (spec.limitType === "CountTiered" && spec.dilutionFactor != null) {
    out.push(t("items.specifications.badges.dilutionFactor", { factor: spec.dilutionFactor }));
  }
  if (spec.limitType === "DissolutionQ" && spec.labelClaim != null) {
    out.push(t("items.specifications.badges.labelClaim", { claim: spec.labelClaim, unit: spec.labelClaimUnit || "mg" }));
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
