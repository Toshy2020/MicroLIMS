import { parseDecimal } from "../../../components/bench/NumericEntryField";
import type { PasteAcceptedRow, PasteColumn, PasteMatchRow, PastePlan } from "../../../components/entry/PastePreviewDialog";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import { EQUIPMENT_TYPES, MICRO_EQUIPMENT_TYPES } from "./EquipmentPage";
import type { ConfiguredEquipmentSummary, CreateEquipmentRequest } from "./services/EquipmentConfigurationService";

export const MAX_EQUIPMENT_BATCH_ROWS = 200;

const DATE_HELP = "Use a date as YYYY-MM-DD, for example 2027-06-15.";
const TEMPERATURE_HELP = "Enter a number using digits, at most one decimal point, and an optional leading minus sign.";
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
  raw: string
): { option: EquipmentTypeOption | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { option: null, error: "Equipment type is required." };
  }
  const key = fold(trimmed);
  const microMatch = MICRO_EQUIPMENT_TYPES.find(
    (opt) => fold(opt.value) === key || fold(opt.label) === key
  );
  if (microMatch) {
    return { option: microMatch, error: null };
  }
  const fpMatch = EQUIPMENT_TYPES.find(
    (opt) => fold(opt.value) === key || fold(opt.label) === key
  );
  if (fpMatch) {
    if (TYPES_REQUIRING_CDS.has(fpMatch.value)) {
      return {
        option: null,
        error: `Equipment type '${fpMatch.label}' is not supported by paste (requires CDS Software).`
      };
    }
    return {
      option: null,
      error: `Equipment type '${fpMatch.label}' is not supported by paste on this page.`
    };
  }
  return { option: null, error: `Unknown equipment type '${trimmed}'.` };
}

/**
 * Resolves a pasted laboratory section by code, name, or "Name (Department)" label against `mySections`.
 * Blank is allowed when the user has at most one section (the add-equipment form hides the selector then).
 */
export function resolveLaboratorySection(
  raw: string,
  mySections: LaboratorySection[]
): { section: LaboratorySection | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    if (mySections.length > 1) {
      return { section: null, error: "Laboratory section is required." };
    }
    return { section: null, error: null };
  }
  const key = fold(trimmed);
  const byCode = mySections.filter((s) => fold(s.sectionCode) === key);
  if (byCode.length === 1) {
    return { section: byCode[0], error: null };
  }
  if (byCode.length > 1) {
    return { section: null, error: `Ambiguous laboratory section '${trimmed}'.` };
  }
  const byName = mySections.filter((s) => fold(s.sectionName) === key);
  if (byName.length === 1) {
    return { section: byName[0], error: null };
  }
  if (byName.length > 1) {
    return { section: null, error: `Ambiguous laboratory section '${trimmed}'.` };
  }
  const byFullLabel = mySections.filter(
    (s) => fold(`${s.sectionName} (${s.departmentName})`) === key
  );
  if (byFullLabel.length === 1) {
    return { section: byFullLabel[0], error: null };
  }
  if (byFullLabel.length > 1) {
    return { section: null, error: `Ambiguous laboratory section '${trimmed}'.` };
  }
  return { section: null, error: `Unknown laboratory section '${trimmed}'.` };
}

/**
 * Resolves an optional pasted asset register entry by asset code, "CODE — Instrument" label, or instrument type.
 */
export function resolveInventoryAsset(
  raw: string,
  inventoryList: EquipmentItem[]
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
    return { asset: null, error: `Ambiguous asset register entry '${trimmed}'.` };
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
    return { asset: null, error: `Ambiguous asset register entry '${trimmed}'.` };
  }
  const byInstrumentType = inventoryList.filter((a) => fold(a.instrumentType) === key);
  if (byInstrumentType.length === 1) {
    return { asset: byInstrumentType[0], error: null };
  }
  if (byInstrumentType.length > 1) {
    return { asset: null, error: `Ambiguous asset register entry '${trimmed}'.` };
  }
  return { asset: null, error: `Unknown asset register entry '${trimmed}'.` };
}

/**
 * Parses an optional set-point temperature cell using the shared `parseDecimal` parser.
 */
export function parseOptionalTemperature(raw: string): { value: number | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { value: null, error: null };
  }
  const parsed = parseDecimal(trimmed);
  if (parsed === null) {
    return { value: null, error: TEMPERATURE_HELP };
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
export function parseOptionalDate(raw: string): { value: string | null; error: string | null } {
  const trimmed = raw.trim();
  if (!trimmed) {
    return { value: null, error: null };
  }
  const iso = toIsoDate(trimmed);
  if (iso === null) {
    return { value: null, error: DATE_HELP };
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
  inventoryList
}: {
  existingEquipment: Pick<ConfiguredEquipmentSummary, "code">[];
  mySections: LaboratorySection[];
  inventoryList: EquipmentItem[];
}): PastePlan {
  const existingCodes = new Set(existingEquipment.map((e) => e.code.trim()).filter(Boolean));

  const columns: PasteColumn[] = [
    {
      key: "name",
      label: "Equipment name",
      check: (v) => (v.trim() ? null : "Equipment name is required."),
      normalize: (v) => v.trim()
    },
    {
      key: "code",
      label: "Equipment code",
      check: (v) => {
        const trimmed = v.trim();
        if (!trimmed) return "Equipment code is required.";
        if (existingCodes.has(trimmed)) return "Already exists.";
        return null;
      },
      normalize: (v) => v.trim()
    },
    {
      key: "type",
      label: "Equipment type",
      check: (v) => resolveEquipmentType(v).error,
      normalize: (v) => resolveEquipmentType(v).option?.label ?? v.trim()
    },
    {
      key: "vendor",
      label: "Vendor / Manufacturer",
      check: () => null,
      normalize: (v) => v.trim()
    },
    {
      key: "section",
      label: "Laboratory section",
      check: (v) => resolveLaboratorySection(v, mySections).error,
      normalize: (v) => resolveLaboratorySection(v, mySections).section?.sectionName ?? v.trim()
    },
    {
      key: "location",
      label: "Location",
      check: () => null,
      normalize: (v) => v.trim()
    },
    {
      key: "equipmentInventory",
      label: "Asset register entry",
      check: (v) => resolveInventoryAsset(v, inventoryList).error,
      normalize: (v) => {
        const asset = resolveInventoryAsset(v, inventoryList).asset;
        return asset ? `${asset.code} — ${asset.instrumentType}` : v.trim();
      }
    },
    {
      key: "connectionSettings",
      label: "Connection settings",
      check: () => null,
      normalize: (v) => v.trim()
    },
    {
      key: "setPointTemperature",
      label: "Set point temp (°C)",
      check: (v) => parseOptionalTemperature(v).error,
      normalize: (v) => v.trim()
    },
    {
      key: "calibrationDueDate",
      label: "Calibration due date",
      check: (v) => parseOptionalDate(v).error,
      normalize: (v) => parseOptionalDate(v).value ?? v.trim()
    }
  ];

  return {
    title: "Paste preview — Equipment",
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
          problem: duplicateInPaste ? "Duplicate equipment code in paste." : undefined
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
  inventoryList: EquipmentItem[]
): PendingEquipment[] {
  return accepted.map((row) => {
    const typeRes = resolveEquipmentType(row.values.type ?? "");
    const sectionRes = resolveLaboratorySection(row.values.section ?? "", mySections);
    const assetRes = resolveInventoryAsset(row.values.equipmentInventory ?? "", inventoryList);
    const tempRes = parseOptionalTemperature(row.values.setPointTemperature ?? "");
    const dateRes = parseOptionalDate(row.values.calibrationDueDate ?? "");

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
    type: row.type,
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
