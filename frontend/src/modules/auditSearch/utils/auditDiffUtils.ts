import { FIELD_DISPLAY_NAMES } from "../types/auditTypes";
import { lookupEnumLabel } from "./auditEnumLabels";

export interface FieldChange {
  key: string;
  label: string;
  oldVal: any;
  newVal: any;
  oldDisplay: string;
  newDisplay: string;
}

function humanizeKey(key: string): string {
  if (FIELD_DISPLAY_NAMES[key]) return FIELD_DISPLAY_NAMES[key];
  // Insert spaces before capital letters: "SetPointTemperature" -> "Set Point Temperature"
  return key
    .replace(/([A-Z]+)/g, " $1")
    .replace(/([A-Z][a-z])/g, " $1")
    .trim();
}

function formatAuditValue(val: any, entityName?: string, fieldKey?: string): string {
  if (val === null || val === undefined) return "—";
  if (typeof val === "boolean") return val ? "Yes" : "No";
  if (typeof val === "number") {
    // Whole numbers on a field known to store an enum are a stored
    // ordinal (e.g. Sample.Status = 4), not a quantity - resolve it to
    // its label rather than showing raw digits to a GMP auditor.
    if (entityName && fieldKey && Number.isInteger(val)) {
      const label = lookupEnumLabel(entityName, fieldKey, val);
      if (label) return label;
    }
    return val.toString();
  }
  if (typeof val === "string") {
    if (!val.trim()) return "—";
    // Check if it's an ISO date string
    if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(val)) {
      try {
        const d = new Date(val);
        return d.toLocaleDateString("en-GB", {
          day: "2-digit",
          month: "short",
          year: "numeric"
        });
      } catch {
        return val;
      }
    }
    return val;
  }
  if (Array.isArray(val)) {
    if (val.length === 0) return "[]";
    return `[${val.map((v) => formatAuditValue(v)).join(", ")}]`;
  }
  if (typeof val === "object") {
    try {
      return JSON.stringify(val);
    } catch {
      return "{...}";
    }
  }
  return String(val);
}

export function computeAuditDiff(
  action: string,
  previousJson: string | null,
  newJson: string | null,
  entityName?: string
): FieldChange[] {
  let prevObj: Record<string, any> = {};
  let newObj: Record<string, any> = {};

  try {
    if (previousJson) prevObj = JSON.parse(previousJson);
  } catch {
    prevObj = {};
  }

  try {
    if (newJson) newObj = JSON.parse(newJson);
  } catch {
    newObj = {};
  }

  const changes: FieldChange[] = [];

  if (action === "Update") {
    const allKeys = Array.from(new Set([...Object.keys(prevObj), ...Object.keys(newObj)]));

    for (const key of allKeys) {
      // Skip internal bookkeeping fields unless they carry business meaning
      if (key === "LastModifiedAt" || key === "LastModifiedByUserId") continue;

      const pVal = prevObj[key];
      const nVal = newObj[key];

      const pStr = JSON.stringify(pVal);
      const nStr = JSON.stringify(nVal);

      if (pStr !== nStr) {
        changes.push({
          key,
          label: humanizeKey(key),
          oldVal: pVal,
          newVal: nVal,
          oldDisplay: formatAuditValue(pVal, entityName, key),
          newDisplay: formatAuditValue(nVal, entityName, key)
        });
      }
    }
  } else if (action === "Create") {
    for (const [key, nVal] of Object.entries(newObj)) {
      if (nVal !== null && nVal !== undefined && nVal !== "") {
        changes.push({
          key,
          label: humanizeKey(key),
          oldVal: null,
          newVal: nVal,
          oldDisplay: "—",
          newDisplay: formatAuditValue(nVal, entityName, key)
        });
      }
    }
  } else if (action === "Delete") {
    for (const [key, pVal] of Object.entries(prevObj)) {
      if (pVal !== null && pVal !== undefined && pVal !== "") {
        changes.push({
          key,
          label: humanizeKey(key),
          oldVal: pVal,
          newVal: null,
          oldDisplay: formatAuditValue(pVal, entityName, key),
          newDisplay: "—"
        });
      }
    }
  }

  return changes;
}
