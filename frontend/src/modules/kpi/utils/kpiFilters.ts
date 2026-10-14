import { toDateInputValue } from "../../reports/utils/dateRange";

export interface KpiFiltersValue {
  from: string; // yyyy-MM-dd, inclusive
  to: string;   // yyyy-MM-dd, inclusive
  category: string | null;
  location: string | null;
  testCode: string | null;
}

// The last 30 days including today.
export function defaultFilters(today: Date = new Date()): KpiFiltersValue {
  const from = new Date(today);
  from.setDate(today.getDate() - 29);
  return { from: toDateInputValue(from), to: toDateInputValue(today), category: null, location: null, testCode: null };
}

// <input type="date"> reports "" while a date is half typed, and ISO dates compare correctly as strings.
export function filtersError(f: { from: string; to: string }): string | null {
  if (!f.from || !f.to) return "Choose both dates.";
  if (f.from > f.to) return "The start date must not be after the end date.";
  return null;
}

// Unset filters become undefined so axios leaves them out of the query string.
export function toQueryParams(f: KpiFiltersValue) {
  return {
    from: f.from,
    to: f.to,
    category: f.category ?? undefined,
    location: f.location ?? undefined,
    testCode: f.testCode ?? undefined
  };
}
