import type { TFunction } from "i18next";
import { parseDecimal } from "../../../components/bench/NumericEntryField";
import type { PasteAcceptedRow, PasteColumn, PasteMatchRow, PastePlan } from "../../../components/entry/PastePreviewDialog";
import { i18n } from "../../../i18n";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import type { MaterialUnit } from "../../inventory/materials/types/materialTypes";
import { CATEGORY_OPTIONS, MATERIAL_UNITS } from "./MaterialMasterPage";
import type { MaterialCategory } from "./services/MaterialCategoryService";
import type {
  MaterialMasterCategory,
  MaterialMasterEntry,
  SaveMaterialMasterEntryRequest
} from "./services/MaterialMasterService";

export const MAX_MATERIAL_MASTER_BATCH_ROWS = 200;

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");
const fold = (value: string) => value.trim().toLowerCase();

export interface CategoryOption {
  value: MaterialMasterCategory;
  label: string;
}

export interface PendingMaterialMasterEntry {
  code: string;
  name: string;
  category: MaterialMasterCategory;
  categoryLabel: string;
  materialCategoryId: number | null;
  materialCategoryName: string;
  baseUnit: MaterialUnit;
  grade: string;
  source: string;
  sectionId: number | null;
  sectionName: string;
  workingConcentration: string;
  solvent: string;
  transitionRangeFrom: number | null;
  transitionRangeTo: number | null;
  colourChange: string;
  indicatorUse: string;
  errors?: Record<string, string>;
}

export interface MaterialMasterPasteOptions {
  entries: MaterialMasterEntry[];
  categoryOptions: CategoryOption[];
  categories?: MaterialMasterCategory[];
  materialCategories: MaterialCategory[];
  mySections: LaboratorySection[];
  t?: TFunction;
}

/**
 * Resolves a pasted category against the category select options available on the current form.
 */
export function resolveCategory(
  raw: string,
  categoryOptions: CategoryOption[] = CATEGORY_OPTIONS,
  t: TFunction = defaultT()
): { option: CategoryOption | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { option: null, error: t("materialMasterPaste.errors.categoryRequired") };
  }
  const key = fold(trimmed);
  const matched = categoryOptions.find((opt) => fold(opt.value) === key || fold(opt.label) === key);
  if (!matched) {
    return { option: null, error: t("materialMasterPaste.errors.categoryUnknown", { value: trimmed }) };
  }
  return { option: matched, error: null };
}

/**
 * Resolves a pasted material category by code prefix (with or without trailing hyphen) or name
 * against active categories offered to the resolved owner lab.
 */
export function resolveMaterialCategory(
  raw: string,
  materialCategories: MaterialCategory[],
  ownerSectionId: number | null,
  required: boolean,
  t: TFunction = defaultT()
): { category: MaterialCategory | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return required
      ? { category: null, error: t("materialMasterPaste.errors.materialCategoryRequired") }
      : { category: null, error: null };
  }

  const pickable = materialCategories.filter(
    (c) =>
      c.isActive &&
      (ownerSectionId == null || c.sectionIds.length === 0 || c.sectionIds.includes(ownerSectionId))
  );

  const key = fold(trimmed);
  const prefixKey = key.endsWith("-") ? key.slice(0, -1) : key;

  const byPrefix = pickable.filter(
    (c) => fold(c.codePrefix) === key || (prefixKey !== "" && fold(c.codePrefix) === prefixKey)
  );
  if (byPrefix.length === 1) {
    return { category: byPrefix[0], error: null };
  }
  if (byPrefix.length > 1) {
    return { category: null, error: t("materialMasterPaste.errors.materialCategoryAmbiguous", { value: trimmed }) };
  }

  const byName = pickable.filter((c) => fold(c.name) === key);
  if (byName.length === 1) {
    return { category: byName[0], error: null };
  }
  if (byName.length > 1) {
    return { category: null, error: t("materialMasterPaste.errors.materialCategoryAmbiguous", { value: trimmed }) };
  }

  return { category: null, error: t("materialMasterPaste.errors.materialCategoryUnknown", { value: trimmed }) };
}

/**
 * Resolves a pasted base unit against the form's `MATERIAL_UNITS` list.
 */
export function resolveMaterialUnit(raw: string, t: TFunction = defaultT()): { unit: MaterialUnit | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { unit: null, error: t("materialMasterPaste.errors.unitRequired") };
  }
  const key = fold(trimmed);
  const matched = MATERIAL_UNITS.find((u) => fold(u) === key);
  if (!matched) {
    return { unit: null, error: t("materialMasterPaste.errors.unitUnknown", { value: trimmed }) };
  }
  return { unit: matched, error: null };
}

/**
 * Resolves a pasted owner lab by section code or section name against the user's assignable sections.
 */
export function resolveOwnerSection(
  raw: string,
  mySections: LaboratorySection[],
  t: TFunction = defaultT()
): { section: LaboratorySection | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { section: null, error: t("materialMasterPaste.errors.ownerLabRequired") };
  }
  const key = fold(trimmed);
  const byCode = mySections.filter((s) => fold(s.sectionCode) === key);
  if (byCode.length === 1) {
    return { section: byCode[0], error: null };
  }
  if (byCode.length > 1) {
    return { section: null, error: t("materialMasterPaste.errors.ownerLabAmbiguous", { value: trimmed }) };
  }
  const byName = mySections.filter((s) => fold(s.sectionName) === key);
  if (byName.length === 1) {
    return { section: byName[0], error: null };
  }
  if (byName.length > 1) {
    return { section: null, error: t("materialMasterPaste.errors.ownerLabAmbiguous", { value: trimmed }) };
  }
  return { section: null, error: t("materialMasterPaste.errors.ownerLabUnknown", { value: trimmed }) };
}

/**
 * Parses an optional numeric cell using the shared decimal parser.
 */
export function parseOptionalDecimal(raw: string, t: TFunction = defaultT()): { value: number | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { value: null, error: null };
  }
  const parsed = parseDecimal(trimmed);
  if (parsed === null) {
    return {
      value: null,
      error: t("materialMasterPaste.errors.decimalHelp")
    };
  }
  return { value: parsed === 0 ? 0 : parsed, error: null };
}

export function includeMaterialCategoryColumn(categories?: MaterialMasterCategory[]): boolean {
  return !categories || categories.includes("Other");
}

export function includeIndicatorColumns(categories?: MaterialMasterCategory[]): boolean {
  return !categories || categories.includes("Indicator");
}

function uniqueMaterialCategoryDisplay(
  cat: MaterialCategory,
  materialCategories: MaterialCategory[],
  ownerSectionId: number | null
): string {
  const pickable = materialCategories.filter(
    (c) =>
      c.isActive &&
      (ownerSectionId == null || c.sectionIds.length === 0 || c.sectionIds.includes(ownerSectionId))
  );
  const sameName = pickable.filter((c) => fold(c.name) === fold(cat.name));
  return sameName.length === 1 ? cat.name : cat.codePrefix;
}

function uniqueSectionDisplay(section: LaboratorySection, mySections: LaboratorySection[]): string {
  const sameName = mySections.filter((s) => fold(s.sectionName) === fold(section.sectionName));
  return sameName.length === 1 ? section.sectionName : section.sectionCode;
}

/**
 * Builds the `PastePlan` for Material Master entries in the exact field order of the Add dialog.
 */
export function buildMaterialMasterPastePlan({
  entries,
  categoryOptions,
  categories,
  materialCategories,
  mySections,
  t = defaultT()
}: MaterialMasterPasteOptions): PastePlan {
  const existingCodes = new Set(entries.map((e) => fold(e.code)).filter(Boolean));
  const withMaterialCategory = includeMaterialCategoryColumn(categories);
  const withOwnerLab = mySections.length > 1;
  const withIndicatorFields = includeIndicatorColumns(categories);

  const columns: PasteColumn[] = [
    {
      key: "code",
      label: t("materialMasterPaste.columns.code"),
      check: (v) => {
        const trimmed = v.trim();
        if (!trimmed) return t("materialMasterPaste.errors.codeRequired");
        if (existingCodes.has(fold(trimmed))) return t("materialMasterPaste.errors.codeExists");
        return null;
      },
      normalize: (v) => v.trim().toUpperCase()
    },
    {
      key: "category",
      label: t("materialMasterPaste.columns.category"),
      check: (v) => resolveCategory(v, categoryOptions, t).error,
      normalize: (v) => resolveCategory(v, categoryOptions, t).option?.label ?? v.trim()
    }
  ];

  if (withMaterialCategory) {
    columns.push({
      key: "materialCategory",
      label: t("materialMasterPaste.columns.materialCategory"),
      // Cross-column rules (required when Category is Other, scoped to Owner Lab) run in match().
      check: () => null
    });
  }

  columns.push(
    {
      key: "name",
      label: t("materialMasterPaste.columns.name"),
      check: (v) => (v.trim() ? null : t("materialMasterPaste.errors.nameRequired")),
      normalize: (v) => v.trim()
    },
    {
      key: "baseUnit",
      label: t("materialMasterPaste.columns.baseUnit"),
      check: (v) => resolveMaterialUnit(v, t).error,
      normalize: (v) => resolveMaterialUnit(v, t).unit ?? v.trim()
    },
    {
      key: "grade",
      label: t("materialMasterPaste.columns.grade"),
      check: () => null,
      normalize: (v) => v.trim()
    },
    {
      key: "source",
      label: t("materialMasterPaste.columns.source"),
      check: () => null,
      normalize: (v) => v.trim()
    }
  );

  if (withOwnerLab) {
    columns.push({
      key: "section",
      label: t("materialMasterPaste.columns.section"),
      check: (v) => resolveOwnerSection(v, mySections, t).error,
      normalize: (v) => {
        const sec = resolveOwnerSection(v, mySections, t).section;
        return sec ? uniqueSectionDisplay(sec, mySections) : v.trim();
      }
    });
  }

  if (withIndicatorFields) {
    columns.push(
      {
        key: "workingConcentration",
        label: t("materialMasterPaste.columns.workingConcentration"),
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "solvent",
        label: t("materialMasterPaste.columns.solvent"),
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "transitionRangeFrom",
        label: t("materialMasterPaste.columns.transitionRangeFrom"),
        check: (v) => parseOptionalDecimal(v, t).error,
        normalize: (v) => v.trim()
      },
      {
        key: "transitionRangeTo",
        label: t("materialMasterPaste.columns.transitionRangeTo"),
        check: (v) => parseOptionalDecimal(v, t).error,
        normalize: (v) => v.trim()
      },
      {
        key: "colourChange",
        label: t("materialMasterPaste.columns.colourChange"),
        check: () => null,
        normalize: (v) => v.trim()
      },
      {
        key: "indicatorUse",
        label: t("materialMasterPaste.columns.indicatorUse"),
        check: () => null,
        normalize: (v) => v.trim()
      }
    );
  }

  return {
    title: t("materialMasterPaste.previewTitle"),
    maxRows: MAX_MATERIAL_MASTER_BATCH_ROWS,
    columns,
    match: (cells) => {
      const codeCounts = new Map<string, number>();
      for (const r of cells) {
        const key = fold(r[0] ?? "");
        if (key) codeCounts.set(key, (codeCounts.get(key) ?? 0) + 1);
      }

      const rows: PasteMatchRow[] = cells.map((r) => {
        const values = Object.fromEntries(columns.map((c, j) => [c.key, (r[j] ?? "").trim()]));
        const rowProblems: string[] = [];

        const codeKey = fold(values.code ?? "");
        if (codeKey !== "" && (codeCounts.get(codeKey) ?? 0) > 1) {
          rowProblems.push(t("materialMasterPaste.errors.duplicateCode"));
        }

        if (withMaterialCategory) {
          const catRes = resolveCategory(values.category ?? "", categoryOptions, t);
          const ownerSectionId = withOwnerLab
            ? (resolveOwnerSection(values.section ?? "", mySections, t).section?.sectionId ?? null)
            : mySections.length === 1
              ? mySections[0].sectionId
              : null;
          const isOther = catRes.option?.value === "Other";
          const matCatRes = resolveMaterialCategory(
            values.materialCategory ?? "",
            materialCategories,
            ownerSectionId,
            isOther,
            t
          );
          if (matCatRes.error) {
            rowProblems.push(t("materialMasterPaste.errors.materialCategoryPrefix", { error: matCatRes.error }));
          } else if (matCatRes.category) {
            values.materialCategory = uniqueMaterialCategoryDisplay(
              matCatRes.category,
              materialCategories,
              ownerSectionId
            );
          }
        }

        return {
          target: null,
          values,
          problem: rowProblems.length > 0 ? rowProblems.join(" ") : undefined
        };
      });

      return { rows, unmatchedTargets: [] };
    }
  };
}

/**
 * Converts rows accepted from `PastePreviewDialog` into typed pending Material Master entries.
 */
export function toPendingMaterialMasterEntries(
  accepted: PasteAcceptedRow[],
  {
    categoryOptions,
    materialCategories,
    mySections,
    t = defaultT()
  }: Pick<MaterialMasterPasteOptions, "categoryOptions" | "materialCategories" | "mySections" | "t">
): PendingMaterialMasterEntry[] {
  const defaultCategory = categoryOptions[0]?.value ?? "Reagent";

  return accepted.map((row) => {
    const catRes = resolveCategory(row.values.category ?? "", categoryOptions, t);
    const category = catRes.option?.value ?? defaultCategory;
    const categoryLabel = catRes.option?.label ?? (row.values.category ?? "").trim();

    const sectionRes =
      mySections.length > 1
        ? resolveOwnerSection(row.values.section ?? "", mySections, t).section
        : mySections.length === 1
          ? mySections[0]
          : null;
    const sectionId = sectionRes?.sectionId ?? null;
    const sectionName = sectionRes?.sectionName ?? "";

    const matCatRes = resolveMaterialCategory(
      row.values.materialCategory ?? "",
      materialCategories,
      sectionId,
      false,
      t
    );
    const unitRes = resolveMaterialUnit(row.values.baseUnit ?? "", t);

    return {
      code: (row.values.code ?? "").trim().toUpperCase(),
      name: (row.values.name ?? "").trim(),
      category,
      categoryLabel,
      materialCategoryId: matCatRes.category?.id ?? null,
      materialCategoryName: matCatRes.category?.name ?? "",
      baseUnit: unitRes.unit ?? "Gram",
      grade: (row.values.grade ?? "").trim(),
      source: (row.values.source ?? "").trim(),
      sectionId,
      sectionName,
      workingConcentration: (row.values.workingConcentration ?? "").trim(),
      solvent: (row.values.solvent ?? "").trim(),
      transitionRangeFrom: parseOptionalDecimal(row.values.transitionRangeFrom ?? "").value,
      transitionRangeTo: parseOptionalDecimal(row.values.transitionRangeTo ?? "").value,
      colourChange: (row.values.colourChange ?? "").trim(),
      indicatorUse: (row.values.indicatorUse ?? "").trim()
    };
  });
}

/**
 * Maps a pending Material Master entry into the `SaveMaterialMasterEntryRequest` DTO for the batch endpoint.
 */
export function toSaveMaterialMasterEntryPayload(
  entry: PendingMaterialMasterEntry
): SaveMaterialMasterEntryRequest {
  return {
    code: entry.code,
    name: entry.name,
    category: entry.category,
    materialCategoryId: entry.materialCategoryId,
    grade: entry.grade || null,
    source: entry.source || null,
    baseUnit: entry.baseUnit,
    sectionId: entry.sectionId,
    workingConcentration: entry.workingConcentration || null,
    solvent: entry.solvent || null,
    transitionRangeFrom: entry.transitionRangeFrom,
    transitionRangeTo: entry.transitionRangeTo,
    colourChange: entry.colourChange || null,
    indicatorUse: entry.indicatorUse || null
  };
}
