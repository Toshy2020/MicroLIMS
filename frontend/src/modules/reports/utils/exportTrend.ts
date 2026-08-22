import { NumericTrendPoint, QualitativeTrendPoint } from "../types/reportingTypes";

export interface TrendExportContext {
  testName: string;
  subjectName: string;
  unit: string | null;
  isNumeric: boolean;
  numericPoints: NumericTrendPoint[];
  qualitativePoints: QualitativeTrendPoint[];
}

function escapeHtml(str: string | null | undefined): string {
  if (str === null || str === undefined) return "—";
  return String(str).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

// Same column set TrendingDataDialog's own "Export CSV" button already
// produces - built independently here (not by editing that dialog) so
// the Trend Analysis page's export menu can offer CSV as a one-click
// option without routing through the "View Data Table" dialog first.
export function buildTrendCsv(ctx: TrendExportContext): string {
  if (ctx.isNumeric) {
    const header = "Period,Reference,Reported Value,Numeric Value,Result Level,Mean,Upper Limit,Alert Level,Action Level";
    const rows = ctx.numericPoints.map((p) =>
      `"${p.label}","${p.referenceNumber}","${p.reportedValue}","${p.value ?? ""}","${p.resultLevel}","${p.mean ?? ""}","${p.upperLimit ?? ""}","${p.alertLevel ?? ""}","${p.actionLevel ?? ""}"`
    );
    return [header, ...rows].join("\n");
  }
  const header = "Period,Detected Count,Absent Count,Total Samples";
  const rows = ctx.qualitativePoints.map((p) => `"${p.label}","${p.detectedCount}","${p.absentCount}","${p.totalCount}"`);
  return [header, ...rows].join("\n");
}

export function downloadCsv(csv: string, filename: string): void {
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

function buildTableRowsHtml(ctx: TrendExportContext): string {
  if (ctx.isNumeric) {
    return ctx.numericPoints.map((p, idx) => `
      <tr class="${idx % 2 === 0 ? "even" : "odd"}">
        <td>${escapeHtml(p.label)}</td>
        <td><strong>${escapeHtml(p.referenceNumber)}</strong></td>
        <td>${escapeHtml(p.reportedValue)} ${escapeHtml(ctx.unit)}</td>
        <td>${escapeHtml(p.resultLevel)}</td>
        <td>${p.mean != null ? p.mean.toFixed(1) : "—"}</td>
        <td>${p.upperLimit ?? "—"}</td>
        <td>${p.alertLevel ?? "—"}</td>
        <td>${p.actionLevel ?? "—"}</td>
      </tr>`).join("");
  }
  return ctx.qualitativePoints.map((p, idx) => `
    <tr class="${idx % 2 === 0 ? "even" : "odd"}">
      <td>${escapeHtml(p.label)}</td>
      <td>${p.detectedCount}</td>
      <td>${p.absentCount}</td>
      <td>${p.totalCount}</td>
    </tr>`).join("");
}

function tableHeadHtml(isNumeric: boolean): string {
  return isNumeric
    ? "<tr><th>Period</th><th>Reference</th><th>Reported Result</th><th>Result Level</th><th>Mean</th><th>Upper Limit</th><th>Alert Level</th><th>Action Level</th></tr>"
    : "<tr><th>Period</th><th>Detected Count</th><th>Absent Count</th><th>Total Evaluated</th></tr>";
}

// Shared print-window document - same "build an HTML string, open a
// blank tab, write it, let the browser's own print-to-PDF do the work"
// pattern as exportResultsPdf() in exportPdf.ts (Record Search's export),
// reused here rather than rebuilt, with the trend-specific header/table
// and an optional chart image slotted in above the table.
function buildPrintDocument(ctx: TrendExportContext, chartImageDataUrl: string | null): string {
  const generatedAt = new Date().toLocaleString("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Trend Analysis Export — MicroLIMS</title>
<style>
  @page { size: A4 landscape; margin: 12mm; @bottom-right { content: "Page " counter(page); font-size: 8pt; color: #666; } }
  body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif; font-size: 9pt; color: #1f2937; margin: 0; padding: 10px; }
  .header-container { border-bottom: 2px solid #5c1477; padding-bottom: 8px; margin-bottom: 12px; display: flex; justify-content: space-between; align-items: flex-start; }
  .brand-title { font-size: 14pt; font-weight: 800; color: #5c1477; margin: 0 0 2px 0; }
  .doc-title { font-size: 12pt; font-weight: 700; color: #111827; margin: 0; }
  .meta-box { background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 4px; padding: 8px 12px; margin-bottom: 12px; display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 8px; font-size: 8.5pt; }
  .meta-item strong { color: #4b5563; }
  .chart-container { margin-bottom: 14px; text-align: center; }
  .chart-container img { max-width: 100%; border: 1px solid #e5e7eb; border-radius: 4px; }
  table { width: 100%; border-collapse: collapse; font-size: 8pt; margin-bottom: 15px; }
  th { background-color: #5c1477; color: #ffffff; font-weight: 700; text-align: left; padding: 6px 8px; border: 1px solid #4a0f61; white-space: nowrap; }
  td { padding: 5px 8px; border: 1px solid #e5e7eb; vertical-align: top; }
  tr.odd { background-color: #ffffff; } tr.even { background-color: #f9fafb; }
  .footer { border-top: 1px solid #e5e7eb; padding-top: 8px; margin-top: 12px; font-size: 7.5pt; color: #6b7280; display: flex; justify-content: space-between; }
</style>
</head>
<body>
  <div class="header-container">
    <div>
      <div class="brand-title">MicroLIMS</div>
      <div class="doc-title">Trend Analysis Export</div>
    </div>
    <div style="text-align: right; font-size: 8pt; color: #6b7280;">
      <div>Laboratory Information Management System</div>
      <div>Confidential &amp; Proprietary Data</div>
    </div>
  </div>
  <div class="meta-box">
    <div class="meta-item"><strong>Test:</strong> ${escapeHtml(ctx.testName)}</div>
    <div class="meta-item"><strong>Subject:</strong> ${escapeHtml(ctx.subjectName)}</div>
    <div class="meta-item"><strong>Generated:</strong> ${escapeHtml(generatedAt)}</div>
  </div>
  ${chartImageDataUrl ? `<div class="chart-container"><img src="${chartImageDataUrl}" alt="Trend chart" /></div>` : ""}
  <table>
    <thead>${tableHeadHtml(ctx.isNumeric)}</thead>
    <tbody>${buildTableRowsHtml(ctx) || `<tr><td colspan="8" style="text-align:center;padding:20px;color:#6b7280;">No matching records found.</td></tr>`}</tbody>
  </table>
  <div class="footer">
    <div>Trend Analysis Export — Generated for analytical review. Not a formal Certificate of Analysis.</div>
    <div>MicroLIMS Quality Assurance</div>
  </div>
  <script>window.onload = function() { setTimeout(function() { window.print(); }, 250); };</script>
</body>
</html>`;
}

function openPrintWindow(html: string): void {
  const printWindow = window.open("", "_blank");
  if (printWindow) {
    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();
  }
}

export function exportTrendPdfTable(ctx: TrendExportContext): void {
  openPrintWindow(buildPrintDocument(ctx, null));
}

export function exportTrendPdfWithChart(ctx: TrendExportContext, chartImageDataUrl: string): void {
  openPrintWindow(buildPrintDocument(ctx, chartImageDataUrl));
}
