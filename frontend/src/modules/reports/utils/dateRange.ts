// Shared by the quick-period selector (Reports.tsx) and the Record
// Search filter panel's editable date range - kept separate from any
// one component since Trending & Analysis will read the same
// page-level period state later.

export type QuickPeriod = "7d" | "30d" | "3m" | "6m" | "12m" | "custom";

export const QUICK_PERIOD_OPTIONS: { value: QuickPeriod; label: string }[] = [
  { value: "7d", label: "Last 7 Days" },
  { value: "30d", label: "Last 30 Days" },
  { value: "3m", label: "Last 3 Months" },
  { value: "6m", label: "Last 6 Months" },
  { value: "12m", label: "Last 12 Months" },
  { value: "custom", label: "Custom" }
];

export function toDateInputValue(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export function startOfDayIso(dateOnly: string): string {
  return new Date(`${dateOnly}T00:00:00`).toISOString();
}

export function endOfDayIso(dateOnly: string): string {
  return new Date(`${dateOnly}T23:59:59.999`).toISOString();
}

// Non-custom periods only - "custom" has no fixed range to compute, the
// caller reads the user's two picked dates instead.
export function computeQuickPeriodRange(period: Exclude<QuickPeriod, "custom">): { fromDate: string; toDate: string } {
  const now = new Date();
  const from = new Date(now);

  switch (period) {
    case "7d": from.setDate(now.getDate() - 7); break;
    case "30d": from.setDate(now.getDate() - 30); break;
    case "3m": from.setMonth(now.getMonth() - 3); break;
    case "6m": from.setMonth(now.getMonth() - 6); break;
    case "12m": from.setMonth(now.getMonth() - 12); break;
  }

  return { fromDate: startOfDayIso(toDateInputValue(from)), toDate: endOfDayIso(toDateInputValue(now)) };
}
