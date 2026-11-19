import type { TFunction } from "i18next";
import { parseDecimal } from "../../../components/bench/NumericEntryField";
import type { PasteAcceptedRow, PasteColumn, PasteMatchRow, PastePlan } from "../../../components/entry/PastePreviewDialog";
import { i18n } from "../../../i18n";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import { EQUIPMENT_TYPES, MICRO_EQUIPMENT_TYPES } from "./EquipmentPage";
import type { ConfiguredEquipmentSummary, CreateEquipmentRequest, EquipmentType } from "./services/EquipmentConfigurationService";

export const MAX_EQUIPMENT_BATCH_ROWS = 200;

const defaultT = (): TFunction => i18n.getFixedT("en", "masterData");
const TYPES_REQUIRING_CDS = new Set(["Hplc", "IcpOes"]);

const fold = (value: string) => value.trim().toLowerCase();

export interface EquipmentTypeOption {
  value: string;
  label: string;
}

export interface PendingEquipment {
  name: string;
  code: string;
  type: string;
  typeLabel: string;
  vendor: string;
  sectionId: number | null;
  sectionName: string;
  location: string;
  equipmentInventoryId: number | null;
  inventoryLabel: string;
  connectionSettings: string;
  setPointTemperature: number | null;
  calibrationDueDate: string;
  errors?: Record<string, string>;
}

/**
 * Resolves a pasted equipment type by enum code or display label against `MICRO_EQUIPMENT_TYPES`.
 * Types requiring CDS Software or belonging to the FP Instruments page are refused as not supported by paste.
 */
export function resolveEquipmentType(
  raw: string,
  t: TFunction = defaultT()
): { option: EquipmentTypeOption | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { option: null, error: t("equipmentPaste.errors.typeRequired") };
  }
  const key = fold(trimmed);
  const microMatch = MICRO_EQUIPMENT_TYPES.find(
    (opt) => fold(opt.value) === key || fold(opt.label) === key
  );
  if (microMatch) {
    return {
      option: { value: microMatch.value, label: t(`equipmentTypes.${microMatch.value}`, { defaultValue: microMatch.label }) },
      error: null
    };
  }
  const fpMatch = EQUIPMENT_TYPES.find(
    (opt) => fold(opt.value) === key || fold(opt.label) === key
  );
  if (fpMatch) {
    const label = t(`equipmentTypes.${fpMatch.value}`, { defaultValue: fpMatch.label });
    if (TYPES_REQUIRING_CDS.has(fpMatch.value)) {
      return {
        option: null,
        error: t("equipmentPaste.errors.typeNotSupportedCds", { label })
      };
    }
    return {
      option: null,
      error: t("equipmentPaste.errors.typeNotSupportedPage", { label })
    };
  }
  return { option: null, error: t("equipmentPaste.errors.typeUnknown", { value: trimmed }) };
}

/**
 * Resolves a pasted laboratory section by code, name, or "Name (Department)" label against `mySections`.
 * Blank is allowed when the user has at most one section (the add-equipment form hides the selector then).
 */
export function resolveLaboratorySection(
  raw: string,
  mySections: LaboratorySection[],
  t: TFunction = defaultT()
): { section: LaboratorySection | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    if (mySections.length > 1) {
      return { section: null, error: t("equipmentPaste.errors.sectionRequired") };
    }
    return { section: null, error: null };
  }
  const key = fold(trimmed);
  const byCode = mySections.filter((s) => fold(s.sectionCode) === key);
  if (byCode.length === 1) {
    return { section: byCode[0], error: null };
  }
  if (byCode.length > 1) {
    return { section: null, error: t("equipmentPaste.errors.sectionAmbiguous", { value: trimmed }) };
  }
  const byName = mySections.filter((s) => fold(s.sectionName) === key);
  if (byName.length === 1) {
    return { section: byName[0], error: null };
  }
  if (byName.length > 1) {
    return { section: null, error: t("equipmentPaste.errors.sectionAmbiguous", { value: trimmed }) };
  }
  const byFullLabel = mySections.filter(
    (s) => fold(`${s.sectionName} (${s.departmentName})`) === key
  );
  if (byFullLabel.length === 1) {
    return { section: byFullLabel[0], error: null };
  }
  if (byFullLabel.length > 1) {
    return { section: null, error: t("equipmentPaste.errors.sectionAmbiguous", { value: trimmed }) };
  }
  return { section: null, error: t("equipmentPaste.errors.sectionUnknown", { value: trimmed }) };
}

/**
 * Resolves an optional pasted asset register entry by asset code, "CODE — Instrument" label, or instrument type.
 */
export function resolveInventoryAsset(
  raw: string,
  inventoryList: EquipmentItem[],
  t: TFunction = defaultT()
): { asset: EquipmentItem | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { asset: null, error: null };
  }
  const key = fold(trimmed);
  const byCode = inventoryList.filter((a) => fold(a.code) === key);
  if (byCode.length === 1) {
    return { asset: byCode[0], error: null };
  }
  if (byCode.length > 1) {
    return { asset: null, error: t("equipmentPaste.errors.assetAmbiguous", { value: trimmed }) };
  }
  const byLabel = inventoryList.filter(
    (a) =>
      fold(`${a.code} — ${a.instrumentType}`) === key ||
      fold(`${a.code} - ${a.instrumentType}`) === key
  );
  if (byLabel.length === 1) {
    return { asset: byLabel[0], error: null };
  }
  if (byLabel.length > 1) {
    return { asset: null, error: t("equipmentPaste.errors.assetAmbiguous", { value: trimmed }) };
  }
  const byInstrumentType = inventoryList.filter((a) => fold(a.instrumentType) === key);
  if (byInstrumentType.length === 1) {
    return { asset: byInstrumentType[0], error: null };
  }
  if (byInstrumentType.length > 1) {
    return { asset: null, error: t("equipmentPaste.errors.assetAmbiguous", { value: trimmed }) };
  }
  return { asset: null, error: t("equipmentPaste.errors.assetUnknown", { value: trimmed }) };
}

/**
 * Parses an optional set-point temperature cell using the shared `parseDecimal` parser.
 */
export function parseOptionalTemperature(raw: string, t: TFunction = defaultT()): { value: number | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { value: null, error: null };
  }
  const parsed = parseDecimal(trimmed);
  if (parsed === null) {
    return { value: null, error: t("equipmentPaste.errors.temperatureHelp") };
  }
  return { value: parsed === 0 ? 0 : parsed, error: null };
}

function toIsoDate(text: string): string | null {
  const iso = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
  let y: number;
  let m: number;
  let d: number;
  if (iso) {
    [y, m, d] = [Number(iso[1]), Number(iso[2]), Number(iso[3])];
  } else {
    return null;
  }
  const date = new Date(Date.UTC(y, m - 1, d));
  if (date.getUTCFullYear() !== y || date.getUTCMonth() !== m - 1 || date.getUTCDate() !== d) {
    return null;
  }
  return `${String(y).padStart(4, "0")}-${String(m).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
}

/**
 * Parses an optional calibration due date in `YYYY-MM-DD` format (unambiguous) into `YYYY-MM-DD`.
 */
export function parseOptionalDate(raw: string, t: TFunction = defaultT()): { value: string | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { value: null, error: null };
  }
  const iso = toIsoDate(trimmed);
  if (iso === null) {
    return { value: null, error: t("equipmentPaste.errors.dateHelp") };
  }
  return { value: iso, error: null };
}

/**
 * Builds the `PastePlan` for equipment master entries in the exact field order of the Add Equipment form.
 * Checks required fields, numeric temperature, valid date, known choices, existing codes (exact trimmed match),
 * and duplicate codes inside the pasted block. Server owns all other business rules.
 */
export function buildEquipmentPastePlan({
  existingEquipment,
  mySections,
  inventoryList,
  t = defaultT()
}: {
  existingEquipment: Pick<ConfiguredEquipmentSummary, "code">[];
  mySections: LaboratorySection[];
  inventoryList: EquipmentItem[];
  t?: TFunction;
}): PastePlan {
  const existingCodes = new Set(existingEquipment.map((e) => e.code.trim()).filter(Boolean));

  const columns: PasteColumn[] = [
    {
      key: "name",
      label: t("equipmentPaste.columns.name"),
      check: (v) => (v.trim() ? null : t("equipmentPaste.errors.nameRequired")),
      normalize: (v) => v.trim()
    },
    {
      key: "code",
      label: t("equipmentPaste.columns.code"),
      check: (v) => {
        const trimmed = v.trim();
        if (!trimmed) return t("equipmentPaste.errors.codeRequired");
        if (existingCodes.has(trimmed)) return t("equipmentPaste.errors.codeExists");
        return null;
      },
      normalize: (v) => v.trim()
    },
    {
      key: "type",
      label: t("equipmentPaste.columns.type"),
      check: (v) => resolveEquipmentType(v, t).error,
      normalize: (v) => resolveEquipmentType(v, t).option?.label ?? v.trim()
    },
    {
      key: "vendor",
      label: t("equipmentPaste.columns.vendor"),
      check: () => null,
      normalize: (v) => v.trim()
    },
    {
      key: "section",
      label: t("equipmentPaste.columns.section"),
      check: (v) => resolveLaboratorySection(v, mySections, t).error,
      normalize: (v) => resolveLaboratorySection(v, mySections, t).section?.sectionName ?? v.trim()
    },
    {
      key: "location",
      label: t("equipmentPaste.columns.location"),
      check: () => null,
      normalize: (v) => v.trim()
    },
    {
      key: "equipmentInventory",
      label: t("equipmentPaste.columns.inventory"),
      check: (v) => resolveInventoryAsset(v, inventoryList, t).error,
      normalize: (v) => {
        const asset = resolveInventoryAsset(v, inventoryList, t).asset;
        return asset ? `${asset.code} — ${asset.instrumentType}` : v.trim();
      }
    },
    {
      key: "connectionSettings",
      label: t("equipmentPaste.columns.connection"),
      check: () => null,
      normalize: (v) => v.trim()
    },
    {
      key: "setPointTemperature",
      label: t("equipmentPaste.columns.setPoint"),
      check: (v) => parseOptionalTemperature(v, t).error,
      normalize: (v) => v.trim()
    },
    {
      key: "calibrationDueDate",
      label: t("equipmentPaste.columns.calibrationDue"),
      check: (v) => parseOptionalDate(v, t).error,
      normalize: (v) => parseOptionalDate(v, t).value ?? v.trim()
    }
  ];

  return {
    title: t("equipmentPaste.previewTitle"),
    maxRows: MAX_EQUIPMENT_BATCH_ROWS,
    columns,
    match: (cells) => {
      const codeCounts = new Map<string, number>();
      for (const r of cells) {
        const key = fold(r[1] ?? "");
        if (key) {
          codeCounts.set(key, (codeCounts.get(key) ?? 0) + 1);
        }
      }

      const rows: PasteMatchRow[] = cells.map((r) => {
        const code = (r[1] ?? "").trim();
        const key = fold(code);
        const duplicateInPaste = key !== "" && (codeCounts.get(key) ?? 0) > 1;
        return {
          target: null,
          values: Object.fromEntries(columns.map((c, j) => [c.key, (r[j] ?? "").trim()])),
          problem: duplicateInPaste ? t("equipmentPaste.errors.duplicateCode") : undefined
        };
      });

      return { rows, unmatchedTargets: [] };
    }
  };
}

/**
 * Converts rows accepted from `PastePreviewDialog` into typed pending equipment rows.
 */
export function toPendingEquipmentRows(
  accepted: PasteAcceptedRow[],
  mySections: LaboratorySection[],
  inventoryList: EquipmentItem[],
  t: TFunction = defaultT()
): PendingEquipment[] {
  return accepted.map((row) => {
    const typeRes = resolveEquipmentType(row.values.type ?? "", t);
    const sectionRes = resolveLaboratorySection(row.values.section ?? "", mySections, t);
    const assetRes = resolveInventoryAsset(row.values.equipmentInventory ?? "", inventoryList, t);
    const tempRes = parseOptionalTemperature(row.values.setPointTemperature ?? "", t);
    const dateRes = parseOptionalDate(row.values.calibrationDueDate ?? "", t);

    return {
      name: (row.values.name ?? "").trim(),
      code: (row.values.code ?? "").trim(),
      type: typeRes.option?.value ?? "Other",
      typeLabel: typeRes.option?.label ?? (row.values.type ?? "").trim(),
      vendor: (row.values.vendor ?? "").trim(),
      sectionId: sectionRes.section?.sectionId ?? null,
      sectionName: sectionRes.section?.sectionName ?? (mySections.length === 1 ? mySections[0].sectionName : ""),
      location: (row.values.location ?? "").trim(),
      equipmentInventoryId: assetRes.asset?.id ?? null,
      inventoryLabel: assetRes.asset ? `${assetRes.asset.code} — ${assetRes.asset.instrumentType}` : "",
      connectionSettings: (row.values.connectionSettings ?? "").trim(),
      setPointTemperature: tempRes.value,
      calibrationDueDate: dateRes.value ?? ""
    };
  });
}

/**
 * Maps a pending equipment row into the `CreateEquipmentRequest` DTO for `POST /masterdata/equipment/batch`.
 */
export function toCreateEquipmentPayload(row: PendingEquipment): CreateEquipmentRequest {
  const payload: CreateEquipmentRequest = {
    name: row.name,
    code: row.code,
    type: row.type as EquipmentType,
    location: row.location === "" ? null : row.location,
    setPointTemperature: row.setPointTemperature,
    calibrationDueDate: row.calibrationDueDate === "" ? null : row.calibrationDueDate,
    vendor: row.vendor === "" ? null : row.vendor,
    connectionSettings: row.connectionSettings === "" ? null : row.connectionSettings,
    equipmentInventoryId: row.equipmentInventoryId
  };
  if (row.sectionId !== null) {
    payload.sectionId = row.sectionId;
  }
  return payload;
}
