import axios from "axios";
import type { FieldError } from "../services/apiTypes";

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
