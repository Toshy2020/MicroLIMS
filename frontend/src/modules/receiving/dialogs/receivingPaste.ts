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

const DATE_HELP = "Use a date as DD/MM/YYYY or YYYY-MM-DD.";
const DATE_TIME_HELP = "Use a date and time as DD/MM/YYYY HH:mm or YYYY-MM-DD HH:mm.";

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

const text = (key: PasteableKey, label: string): PasteColumn => ({ key, label, check: () => null });

export const PASTEABLE_COLUMNS: Record<PasteableKey, PasteColumn> = {
  previousProductBatchNumber: text("previousProductBatchNumber", "Previous batch number"),
  sampleQuantity: text("sampleQuantity", "Quantity"),
  sampledBy: text("sampledBy", "Sampled by"),
  batchNumber: text("batchNumber", "Batch number"),
  sampledAt: {
    key: "sampledAt",
    label: "Sampled at",
    check: (v) => (v === "" || toLocalDateTime(v) !== null ? null : DATE_TIME_HELP),
    normalize: (v) => toLocalDateTime(v) ?? v
  },
  controlNumber: text("controlNumber", "Control number"),
  mfgDate: {
    key: "mfgDate",
    label: "Mfg date",
    check: (v) => (v === "" || toIsoDate(v) !== null ? null : DATE_HELP),
    normalize: (v) => toIsoDate(v) ?? v
  },
  expDate: {
    key: "expDate",
    label: "Exp date",
    check: (v) => (v === "" || toIsoDate(v) !== null ? null : DATE_HELP),
    normalize: (v) => toIsoDate(v) ?? v
  }
};

export function isPasteableKey(key: string): key is PasteableKey {
  return Object.prototype.hasOwnProperty.call(PASTEABLE_COLUMNS, key);
}

// Names shown in the "not pasted" list for the choice columns.
export const CHOICE_COLUMN_LABELS: Record<string, string> = {
  itemId: "Item",
  targetSectionIds: "Laboratories",
  grade: "Grade",
  coaFile: "Supplier CoA",
  previousProduct: "Previous product",
  techniques: "Techniques",
  productionStage: "Production stage",
  departmentId: "Department",
  machineId: "Machine",
  causeOfTestingId: "Cause of testing"
};
