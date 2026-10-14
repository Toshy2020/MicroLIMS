import type { KpiDashboard, KpiEntry } from "../types/kpiTypes";
import type { AnalystWorkload } from "../types/workloadTypes";
import type { KpiFiltersValue } from "./kpiFilters";
import {
  FLAG_LABELS, entryView, formatExtras, formatKpiValue, formatPrior, formatRatio, formatUnits, sortByName
} from "./kpiFormat";

function esc(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

const row = (cells: (string | number | null | undefined)[], tag: "td" | "th" = "td") =>
  `<tr>${cells.map((c) => `<${tag}>${esc(c)}</${tag}>`).join("")}</tr>`;

function valueText(e: KpiEntry): string {
  const v = entryView(e);
  if (v.kind === "error") return "Could not be calculated";
  if (v.kind === "noData") return "No data";
  return v.text;
}

function notesText(e: KpiEntry): string {
  const v = entryView(e);
  if (v.kind === "error") return v.message;
  const parts: string[] = [];
  if (v.kind === "value" && v.muted) parts.push("too few records to judge");
  for (const x of formatExtras(e)) parts.push(`${x.label} ${x.text}`);
  for (const g of v.result.gaps) parts.push(g.message);
  for (const f of v.result.flags) parts.push(FLAG_LABELS[f] ?? f);
  return parts.join("; ");
}

function headlineTable(entries: KpiEntry[]): string {
  const body = entries.map((e) => {
    const r = e.result;
    return row([e.name, valueText(e), r ? r.n : "—", r?.prior ? formatPrior(e.unit, r.prior) : "", notesText(e)]);
  });
  return `<table><thead>${row(["Measure", "Value", "n", "Prior period", "Notes"], "th")}</thead><tbody>${body.join("")}</tbody></table>`;
}

function seriesTable(e: KpiEntry): string {
  const points = e.result?.series ?? [];
  if (points.length === 0) return "";
  const stage = e.key === "stage-tat";
  const hasSecondary = points.some((p) => p.secondary != null);
  const head = ["Item", stage ? "Median" : "Value", ...(hasSecondary ? [stage ? "P90" : "Secondary"] : []), "n"];
  const body = points.map((p) =>
    row([
      p.group ? `${p.group} / ${p.label}` : p.label,
      formatKpiValue(e.unit, p.value),
      ...(hasSecondary ? [formatKpiValue(e.unit, p.secondary)] : []),
      p.n
    ])
  );
  return `<div class="section-title">${esc(e.name)}: ${esc(valueText(e))}</div><table><thead>${row(head, "th")}</thead><tbody>${body.join("")}</tbody></table>`;
}

function workloadBlock(w: AnalystWorkload): string {
  const notes: string[] = [];
  if (w.onlyOwnRow) notes.push("Showing the viewer's own numbers only.");
  if (w.capacityUsesDefaults) notes.push("Capacity assumes full availability and the default hours.");
  if (w.unassignedOpenTests > 0) notes.push(`${w.unassignedOpenTests} open tests have no analyst.`);
  for (const g of w.gaps) notes.push(g.message);
  const body = sortByName(w.rows).map((r) =>
    row([
      r.fullName, formatUnits(r.capacityUnits), formatUnits(r.deliveredUnits),
      r.utilisationCheckProfile ? `${formatRatio(r.utilisation)} (check profile)` : formatRatio(r.utilisation),
      formatUnits(r.openUnits),
      r.forwardLoadOverCapacity ? `${formatRatio(r.forwardLoad)} (over capacity)` : formatRatio(r.forwardLoad),
      formatRatio(r.weightedOnTime), formatRatio(r.weightedRightFirstTime)
    ])
  );
  const head = ["Analyst", "Capacity (units)", "Delivered units", "Utilisation", "Open units", "Forward load", "Weighted on time", "Weighted right first time"];
  return `<div class="section-title">Workload</div>
    ${notes.map((n) => `<div class="note">${esc(n)}</div>`).join("")}
    <table><thead>${row(head, "th")}</thead><tbody>${body.join("")}</tbody></table>
    <div class="note">Listed alphabetically. Units are a planning estimate, not a performance score (1 unit = 1 standard analyst-hour).</div>`;
}

export function buildKpiReportHtml(
  dashboard: KpiDashboard,
  workload: AnalystWorkload | null,
  filters: KpiFiltersValue,
  generatedBy: string,
  workloadError?: string | null
): string {
  const generatedAt = new Date(dashboard.generatedAt).toLocaleString("en-GB", {
    day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit"
  });
  const scope = [
    `Period: ${filters.from} to ${filters.to}`,
    filters.category ? `Category: ${filters.category}` : null,
    filters.location ? `Location: ${filters.location}` : null,
    filters.testCode ? `Test: ${filters.testCode}` : null
  ].filter(Boolean).join(" | ");

  const ordered = [...dashboard.kpis].sort((a, b) => a.order - b.order);
  const headline = ordered.filter((e) => e.group === "Headline");
  const supporting = ordered.filter((e) => e.group !== "Headline");

  return `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>KPI &amp; Performance Report</title>
  <style>
    @page { size: A4 landscape; margin: 12mm; }
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif; font-size: 10pt; color: #1e293b; margin: 0; padding: 12px; }
    .header { border-bottom: 2px solid #0f766e; padding-bottom: 8px; margin-bottom: 12px; }
    .title { font-size: 16pt; font-weight: bold; color: #0f766e; }
    .meta { font-size: 8.5pt; color: #64748b; margin-top: 4px; }
    .section-title { font-size: 11pt; font-weight: bold; color: #0f766e; margin: 12px 0 6px 0; }
    table { width: 100%; border-collapse: collapse; font-size: 8.5pt; margin-bottom: 14px; }
    th { background: #0f766e; color: #ffffff; padding: 6px 8px; text-align: left; font-weight: 600; }
    td { padding: 5px 8px; border-bottom: 1px solid #e2e8f0; }
    .note { font-size: 8.5pt; color: #64748b; margin: 2px 0 6px 0; }
    .footer { margin-top: 16px; border-top: 1px solid #cbd5e1; padding-top: 8px; font-size: 7.5pt; color: #64748b; }
  </style>
</head>
<body>
  <div class="header">
    <div class="title">MicroLIMS KPI &amp; Performance Report</div>
    <div class="meta"><strong>Scope:</strong> ${esc(scope)} &nbsp;|&nbsp; <strong>Generated by:</strong> ${esc(generatedBy)} &nbsp;|&nbsp; <strong>Calculated:</strong> ${esc(generatedAt)}</div>
  </div>

  <div class="section-title">Headline measures</div>
  ${headlineTable(headline)}

  ${supporting.map(seriesTable).join("")}

  ${workload
    ? workloadBlock(workload)
    : workloadError
      ? `<div class="section-title">Workload</div><div class="note">${esc(`Workload could not be loaded: ${workloadError}`)}</div>`
      : ""}

  <div class="footer">Figures are as shown on screen for the same period and filters. GMP audit trail and data-integrity standard. MicroLIMS verified report.</div>

  <script>
    window.addEventListener("load", () => { window.print(); });
  </script>
</body>
</html>`;
}

export function exportKpiPdf(
  dashboard: KpiDashboard,
  workload: AnalystWorkload | null,
  filters: KpiFiltersValue,
  generatedBy: string,
  workloadError?: string | null
): void {
  const html = buildKpiReportHtml(dashboard, workload, filters, generatedBy, workloadError);
  const printWindow = window.open("", "_blank");
  if (printWindow) {
    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();
  }
}
