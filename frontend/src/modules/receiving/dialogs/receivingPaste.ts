import type { TFunction } from "i18next";
import type { PasteColumn } from "../../../components/entry/PastePreviewDialog";

// Columns a paste can fill: the text and date cells of the receiving grid.
// Every other column is a choice (item, laboratories, grade, department, ...)
// and is left for the user to pick by hand.
export type PasteableKey =
  | "previousProductBatchNumber"
  | "sampleQuantity"
  | "sampledBy"
  | "batchNumber"
  | "sampledAt"
  | "controlNumber"
  | "mfgDate"
  | "expDate";

const PASTEABLE_KEYS: ReadonlySet<string> = new Set<PasteableKey>([
  "previousProductBatchNumber",
  "sampleQuantity",
  "sampledBy",
  "batchNumber",
  "sampledAt",
  "controlNumber",
  "mfgDate",
  "expDate"
]);

function toIsoDate(text: string): string | null {
  const iso = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
  const dmy = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(text);
  let y: number;
  let m: number;
  let d: number;
  if (iso) {
    [y, m, d] = [Number(iso[1]), Number(iso[2]), Number(iso[3])];
  } else if (dmy) {
    [d, m, y] = [Number(dmy[1]), Number(dmy[2]), Number(dmy[3])];
  } else {
    return null;
  }
  const date = new Date(Date.UTC(y, m - 1, d));
  if (date.getUTCFullYear() !== y || date.getUTCMonth() !== m - 1 || date.getUTCDate() !== d) return null;
  return `${String(y).padStart(4, "0")}-${String(m).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
}

function toLocalDateTime(text: string): string | null {
  const parts = /^(\S+)[ T](\d{1,2}):(\d{2})$/.exec(text);
  if (!parts) return null;
  const date = toIsoDate(parts[1]);
  const hour = Number(parts[2]);
  const minute = Number(parts[3]);
  if (!date || hour > 23 || minute > 59) return null;
  return `${date}T${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

export function getPasteableColumns(t: TFunction<"receiving">): Record<PasteableKey, PasteColumn> {
  const text = (key: PasteableKey, label: string): PasteColumn => ({ key, label, check: () => null });
  const dateHelp = t("paste.dateHelp");
  const dateTimeHelp = t("paste.dateTimeHelp");

  return {
    previousProductBatchNumber: text("previousProductBatchNumber", t("paste.columns.previousProductBatchNumber")),
    sampleQuantity: text("sampleQuantity", t("paste.columns.sampleQuantity")),
    sampledBy: text("sampledBy", t("paste.columns.sampledBy")),
    batchNumber: text("batchNumber", t("paste.columns.batchNumber")),
    sampledAt: {
      key: "sampledAt",
      label: t("paste.columns.sampledAt"),
      check: (v) => (v === "" || toLocalDateTime(v) !== null ? null : dateTimeHelp),
      normalize: (v) => toLocalDateTime(v) ?? v
    },
    controlNumber: text("controlNumber", t("paste.columns.controlNumber")),
    mfgDate: {
      key: "mfgDate",
      label: t("paste.columns.mfgDate"),
      check: (v) => (v === "" || toIsoDate(v) !== null ? null : dateHelp),
      normalize: (v) => toIsoDate(v) ?? v
    },
    expDate: {
      key: "expDate",
      label: t("paste.columns.expDate"),
      check: (v) => (v === "" || toIsoDate(v) !== null ? null : dateHelp),
      normalize: (v) => toIsoDate(v) ?? v
    }
  };
}

export function isPasteableKey(key: string): key is PasteableKey {
  return PASTEABLE_KEYS.has(key);
}

// Names shown in the "not pasted" list for the choice columns.
export function getChoiceColumnLabels(t: TFunction<"receiving">): Record<string, string> {
  return {
    itemId: t("paste.choiceColumns.itemId"),
    targetSectionIds: t("paste.choiceColumns.targetSectionIds"),
    grade: t("paste.choiceColumns.grade"),
    coaFile: t("paste.choiceColumns.coaFile"),
    previousProduct: t("paste.choiceColumns.previousProduct"),
    techniques: t("paste.choiceColumns.techniques"),
    productionStage: t("paste.choiceColumns.productionStage"),
    departmentId: t("paste.choiceColumns.departmentId"),
    machineId: t("paste.choiceColumns.machineId"),
    causeOfTestingId: t("paste.choiceColumns.causeOfTestingId")
  };
}
