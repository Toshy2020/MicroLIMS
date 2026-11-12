import type { Item } from "../services/ItemService";
import {
  PRODUCTION_STAGE_ROLE_OPTIONS,
  productionStageRoleLabel,
  type CreateSpecificationPayload,
  type DosageForm,
  type ExpectedPresence,
  type LimitType,
  type RelatedSubstanceRow,
  type ResultBasis,
  type SpecificationDto,
  type ToleranceMode
} from "../../specifications/services/SpecificationService";
import { collapseGrades } from "../../specifications/services/gradeGroups";
import { PhyschemArea, areaIncludes, areaOfItemCategory } from "../../masterDataSimple/testMasterArea";
import { formatTrimmedDecimal } from "./SpecificationParameterDialog";

// Pure helpers behind "Copy from another item". Nothing here evaluates, rounds
// or converts a limit: values are passed through exactly as the source row
// stores them, and a row whose shape is not understood is refused rather than
// guessed (the server stays the real gate).

// ---------------------------------------------------------------------------
// How a specification's limits read in the table (moved here from
// ItemSpecificationsSection so the copy list shows the same text).
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// Mapping a stored row to the create request
// ---------------------------------------------------------------------------

export type CannotCopy = { cannotCopy: string };

export const isCannotCopy = (r: CreateSpecificationPayload | CannotCopy): r is CannotCopy => "cannotCopy" in r;

// Record<Union, true> makes the compiler flag a new union member that is not listed here.
const LIMIT_TYPES: Record<LimitType, true> = {
  Range: true,
  NotMoreThan: true,
  NotLessThan: true,
  TargetWithTolerance: true,
  CountTiered: true,
  Qualitative: true,
  PresenceAbsence: true,
  MultiStage: true,
  DissolutionQ: true,
  DisintegrationTime: true,
  WeightVariation: true
};
const TOLERANCE_MODES: Record<ToleranceMode, true> = { Absolute: true, Percent: true };
const EXPECTED_STATES: Record<ExpectedPresence, true> = { Presence: true, Absence: true };
const RELATED_SUBSTANCE_ROWS: Record<RelatedSubstanceRow, true> = {
  Named: true,
  UnspecifiedEach: true,
  Total: true,
  TotalOthers: true
};
const RESULT_BASES: Record<ResultBasis, true> = {
  MgPerKg: true,
  MgPerUnit: true,
  PercentLabelClaim: true,
  PercentAsIs: true,
  PercentDriedBasis: true,
  PercentAnhydrousBasis: true,
  Ppm: true
};
const DOSAGE_FORMS: Record<DosageForm, true> = { Tablet: true, HardCapsule: true, SoftCapsule: true };
const STAGE_ROLES: ReadonlySet<string> = new Set(PRODUCTION_STAGE_ROLE_OPTIONS.map((o) => o.value));

/**
 * The create request that reproduces `spec` on another item, or the reason it
 * cannot be copied safely.
 *
 * Every field the create endpoint accepts is carried over as stored (numbers
 * that arrive as text become numbers, as the specification dialog sends
 * them). Identity and versioning (id, version, source itemId, canEdit,
 * sectionName) are never copied. `displayOrder` is chosen by the caller, the
 * way the dialog does for a new parameter (see assignDisplayOrders).
 *
 * hplcMethodAnalyteId, hplcMethodImpurityId and icpTestElementId are rows of
 * the TEST's method (SpecificationParameterDialog lists them from the test
 * definition), and both items use the same test, so they carry over unchanged.
 */
export function toCreatePayload(
  spec: SpecificationDto,
  targetItemId: number,
  displayOrder: number
): CreateSpecificationPayload | CannotCopy {
  const problems: string[] = [];

  const num = (v: number | string | null | undefined, field: string): number | null => {
    if (v == null || v === "") return null;
    const n = typeof v === "number" ? v : Number(v);
    if (!Number.isFinite(n)) {
      problems.push(`${field} "${String(v)}" is not a number`);
      return null;
    }
    return n;
  };
  const oneOf = <T extends string>(
    v: string | null | undefined,
    allowed: Record<T, true>,
    field: string
  ): T | null => {
    if (v == null || v === "") return null;
    if (Object.prototype.hasOwnProperty.call(allowed, v)) return v as T;
    problems.push(`${field} "${v}" is not recognised`);
    return null;
  };

  if (!spec.testCode) return { cannotCopy: "The row has no test code." };
  if (!spec.limitType) return { cannotCopy: "The row has no limit type." };
  const limitType = oneOf<LimitType>(spec.limitType, LIMIT_TYPES, "Limit type");
  if (limitType == null) return { cannotCopy: `Limit type "${spec.limitType}" is not recognised.` };

  const lowerLimit = num(spec.lowerLimit, "Lower limit");
  const upperLimit = num(spec.upperLimit, "Upper limit");
  const target = num(spec.target, "Target");
  const tolerance = num(spec.tolerance, "Tolerance");
  const sampleQuantity = num(spec.sampleQuantity, "Sample quantity");
  const labelClaim = num(spec.labelClaim, "Label claim");
  const dilutionFactor = num(spec.dilutionFactor, "Dilution factor");
  const toleranceMode = oneOf<ToleranceMode>(spec.toleranceMode, TOLERANCE_MODES, "Tolerance mode");
  const expectedState = oneOf<ExpectedPresence>(spec.expectedState, EXPECTED_STATES, "Expected state");
  const relatedSubstanceRow = oneOf<RelatedSubstanceRow>(
    spec.relatedSubstanceRow,
    RELATED_SUBSTANCE_ROWS,
    "Related-substance row"
  );
  const resultBasis = oneOf<ResultBasis>(spec.resultBasis, RESULT_BASES, "Result basis");
  const dosageForm = oneOf<DosageForm>(spec.dosageForm, DOSAGE_FORMS, "Dosage form");
  let productionStageRole: CreateSpecificationPayload["productionStageRole"] = null;
  if (spec.productionStageRole) {
    if (STAGE_ROLES.has(spec.productionStageRole)) {
      productionStageRole = spec.productionStageRole as CreateSpecificationPayload["productionStageRole"];
    } else {
      problems.push(`Production stage "${spec.productionStageRole}" is not recognised`);
    }
  }

  const stageRows = spec.stages ?? [];
  const stages = stageRows.map((s) => {
    if (!Number.isFinite(s.stageNumber)) problems.push("A stage has no number");
    return {
      stageNumber: s.stageNumber,
      stageLabel: s.stageLabel,
      acceptanceCriteriaText: s.acceptanceCriteriaText
    };
  });

  // The same preconditions the specification dialog enforces before it saves.
  if (limitType === "DissolutionQ" && (lowerLimit == null || labelClaim == null)) {
    problems.push("A dissolution row needs Q (lower limit) and a label claim");
  }
  if (limitType === "DisintegrationTime" && upperLimit == null) {
    problems.push("A disintegration row needs a time limit");
  }
  if (limitType === "WeightVariation" && dosageForm == null) {
    problems.push("A weight variation row needs a dosage form");
  }
  if (limitType === "MultiStage" && stages.length === 0) {
    problems.push("A multi-stage row has no stages");
  }
  if (limitType !== "MultiStage" && stages.length > 0) {
    problems.push("Stages are stored on a row that is not multi-stage");
  }

  if (problems.length > 0) return { cannotCopy: `${problems.join("; ")}.` };

  const payload: CreateSpecificationPayload = {
    itemId: targetItemId,
    testCode: spec.testCode,
    parameterName: spec.parameterName ?? null,
    displayOrder,
    limitType,
    referenceStandard: spec.referenceStandard ?? null,
    lowerLimit,
    upperLimit,
    target,
    tolerance,
    toleranceMode,
    expectedResultText: spec.expectedResultText ?? null,
    expectedState,
    sampleQuantity,
    sampleQuantityUnit: spec.sampleQuantityUnit ?? null,
    alertLimit: spec.alertLimit ?? null,
    actionLimit: spec.actionLimit ?? null,
    specLimit: spec.specLimit ?? null,
    unit: spec.unit ?? null,
    dilutionFactor,
    resultBasis,
    labelClaim,
    labelClaimUnit: spec.labelClaimUnit ?? null,
    dosageForm,
    hplcMethodAnalyteId: spec.hplcMethodAnalyteId ?? null,
    hplcMethodImpurityId: spec.hplcMethodImpurityId ?? null,
    relatedSubstanceRow,
    icpTestElementId: spec.icpTestElementId ?? null,
    productionStageRole,
    grade: spec.grade ?? null
  };
  // Absent stays absent (the server then applies its own default), never a guessed value.
  if (typeof spec.lowerInclusive === "boolean") payload.lowerInclusive = spec.lowerInclusive;
  if (typeof spec.upperInclusive === "boolean") payload.upperInclusive = spec.upperInclusive;
  if (limitType === "MultiStage") payload.stages = stages;
  return payload;
}

/**
 * displayOrder for each row of `specs` (parallel array), the way the
 * specification dialog gives a new parameter its order: one more than the
 * highest order the test already has on the item. Rows that differ only by
 * grade are saved together by the dialog and share one order, so they do here.
 */
export function assignDisplayOrders(specs: SpecificationDto[], existingTargetSpecs: SpecificationDto[]): number[] {
  const highest = new Map<string, number>();
  for (const s of existingTargetSpecs) {
    highest.set(s.testCode, Math.max(highest.get(s.testCode) ?? -1, s.displayOrder ?? 0));
  }
  const leaderOf = new Map<SpecificationDto, SpecificationDto>();
  for (const group of collapseGrades(specs)) {
    for (const sibling of group.gradeSiblings) leaderOf.set(sibling, group.gradeSiblings[0]);
  }
  const orderOfLeader = new Map<SpecificationDto, number>();
  return specs.map((s) => {
    const leader = leaderOf.get(s) ?? s;
    const known = orderOfLeader.get(leader);
    if (known != null) return known;
    const order = (highest.get(s.testCode) ?? -1) + 1;
    highest.set(s.testCode, order);
    orderOfLeader.set(leader, order);
    return order;
  });
}

// ---------------------------------------------------------------------------
// Which source rows may be offered
// ---------------------------------------------------------------------------

export interface TestDefinitionForLab {
  physchemArea?: PhyschemArea | null;
  sectionId?: number | null;
}

/**
 * The target item's assigned tests that belong to the caller's own laboratory.
 * This is the rule SpecificationParameterDialog uses for `ownLabAssignedTests`:
 * a test whose section is unknown, or while the caller's lab list is empty, is
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

export interface CopyCandidate {
  // Stable per source row; used for selection and progress.
  key: string;
  spec: SpecificationDto;
  // Why the row is listed but cannot be selected; null when it can be copied.
  cannotCopy: string | null;
}

export interface CopySkipCounts {
  notAssigned: number;
  otherLab: number;
  alreadyHasLimits: number;
}

export interface CopySelection {
  candidates: CopyCandidate[];
  skipped: CopySkipCounts;
  // Grades and production stages are category-specific: nothing is offered across categories.
  categoryMismatch: boolean;
}

/**
 * Sorts the source item's rows into what can be offered and what is skipped
 * (each skipped row is counted once: not assigned, then other laboratory, then
 * already has limits). A test that already has any specification on the
 * target is never offered, so existing limits are never added to or replaced.
 */
export function selectCopyCandidates(input: {
  sourceItem: Pick<Item, "category">;
  targetItem: Pick<Item, "id" | "category" | "assignedTests">;
  sourceSpecs: SpecificationDto[];
  targetSpecs: SpecificationDto[];
  ownLabTestCodes: ReadonlySet<string>;
}): CopySelection {
  const skipped: CopySkipCounts = { notAssigned: 0, otherLab: 0, alreadyHasLimits: 0 };
  if (input.sourceItem.category !== input.targetItem.category) {
    return { candidates: [], skipped, categoryMismatch: true };
  }
  const assigned = new Set(input.targetItem.assignedTests.map((t) => t.testCode));
  const hasLimits = new Set(input.targetSpecs.map((s) => s.testCode));
  const candidates: CopyCandidate[] = [];
  input.sourceSpecs.forEach((spec, index) => {
    if (!assigned.has(spec.testCode)) {
      skipped.notAssigned += 1;
    } else if (!input.ownLabTestCodes.has(spec.testCode)) {
      skipped.otherLab += 1;
    } else if (hasLimits.has(spec.testCode)) {
      skipped.alreadyHasLimits += 1;
    } else {
      // The order is a placeholder here: it only checks that the row can be mapped.
      const mapped = toCreatePayload(spec, input.targetItem.id, 0);
      candidates.push({
        key: String(spec.id ?? `row-${index}`),
        spec,
        cannotCopy: isCannotCopy(mapped) ? mapped.cannotCopy : null
      });
    }
  });
  return { candidates, skipped, categoryMismatch: false };
}
