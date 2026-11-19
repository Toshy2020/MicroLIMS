import axios from "axios";
import type { FieldError } from "../services/apiTypes";
import { getErrorMessage } from "./errorMessage";

function isFieldError(value: unknown): value is FieldError {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Record<string, unknown>;
  if (typeof candidate.field !== "string" || typeof candidate.message !== "string") {
    return false;
  }
  if (
    candidate.row !== undefined &&
    candidate.row !== null &&
    typeof candidate.row !== "number"
  ) {
    return false;
  }
  return true;
}

/**
 * Extracts structured field-level errors from an Axios error whose response
 * body matches the `ApiResponse` `fieldErrors` shape. Returns `[]` for network
 * errors, non-Axios errors, and legacy responses without `fieldErrors`.
 */
export function getFieldErrors(err: unknown): FieldError[] {
  if (!axios.isAxiosError(err)) return [];
  const data = err.response?.data;
  if (typeof data !== "object" || data === null) return [];
  const raw = (data as { fieldErrors?: unknown }).fieldErrors;
  if (!Array.isArray(raw)) return [];
  return raw.filter(isFieldError);
}

/**
 * Maps top-level field errors (where `row` is null/undefined) to `{ field: message }`,
 * keeping the first message per field. Row-scoped errors are ignored.
 */
export function errorsByField(err: unknown): Record<string, string> {
  const result: Record<string, string> = {};
  for (const item of getFieldErrors(err)) {
    if (item.row !== undefined && item.row !== null) continue;
    if (!(item.field in result)) {
      result[item.field] = item.message;
    }
  }
  return result;
}

/**
 * Groups row-scoped field errors into `{ [row]: { [field]: message } }`,
 * keeping the first message per (row, field) pair.
 */
export function errorsByRow(err: unknown): Record<number, Record<string, string>> {
  const result: Record<number, Record<string, string>> = {};
  for (const item of getFieldErrors(err)) {
    if (typeof item.row !== "number") continue;
    const rowErrors = result[item.row] ?? (result[item.row] = {});
    if (!(item.field in rowErrors)) {
      rowErrors[item.field] = item.message;
    }
  }
  return result;
}

const pluralCount = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

/**
 * Summarizes an all-or-nothing batch save error into per-row field errors (if present)
 * and a top-level alert message.
 */
export function extractBatchSaveErrors(
  err: unknown,
  fallback: string
): { summary: string; rowErrors: Record<number, Record<string, string>> | null } {
  const rowErrs = errorsByRow(err);
  const topErrs = errorsByField(err);
  const failingRowIndices = Object.keys(rowErrs);
  if (failingRowIndices.length === 0) {
    return { summary: getErrorMessage(err, fallback), rowErrors: null };
  }
  const totalProblems = failingRowIndices.reduce(
    (sum, key) => sum + Object.keys(rowErrs[Number(key)]).length,
    0
  );
  const topMsgs = Object.values(topErrs);
  const summary = `${pluralCount(totalProblems, "problem")} in ${pluralCount(failingRowIndices.length, "row")}. Nothing was created.`;
  return {
    summary: topMsgs.length > 0 ? `${summary} ${topMsgs.join(" ")}` : summary,
    rowErrors: rowErrs
  };
}
