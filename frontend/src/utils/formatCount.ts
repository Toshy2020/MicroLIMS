// Numbers always use Latin digits and "," thousands separators, in every interface language
// (the digits must not change with the locale). Dates are formatted by utils/formatDate.ts.
export function formatCount(value: number): string {
  return value.toLocaleString("en-US");
}

/** Same as formatCount, with Intl.NumberFormat options (fraction digits, grouping). */
export function formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
  return value.toLocaleString("en-US", options);
}
