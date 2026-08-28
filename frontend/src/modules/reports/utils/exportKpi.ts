import { AnalystKpiFilters, AnalystPerformanceDashboardData } from "../types/reportingTypes";

function escapeHtml(str: string | null | undefined): string {
  if (str === null || str === undefined) return "—";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export function exportKpiPdf(
  data: AnalystPerformanceDashboardData,
  filters: AnalystKpiFilters,
  generatedBy: string
): void {
  const generatedAt = new Date().toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });

  const filterSummary = [
    `Date Range: ${filters.dateRange.toUpperCase()}`,
    filters.analystId && filters.analystId !== "All" ? `Analyst ID: ${filters.analystId}` : "All Analysts",
    filters.category && filters.category !== "All" ? `Category: ${filters.category}` : "All Categories",
    filters.location && filters.location !== "All" ? `Location: ${filters.location}` : null,
    filters.testCode && filters.testCode !== "All" ? `Test Code: ${filters.testCode}` : null
  ]
    .filter(Boolean)
    .join(" | ");

  const comparisonRowsHtml = data.analystComparison
    .map(
      (r, idx) => `
      <tr class="${idx % 2 === 0 ? "even" : "odd"}">
        <td><strong>${escapeHtml(r.analystName)}</strong></td>
        <td style="text-align: center;">${r.assigned}</td>
        <td style="text-align: center;">${r.completed}</td>
        <td style="text-align: center;">${r.workloadUnits}</td>
        <td style="text-align: center;">${r.completionRatePercent}%</td>
        <td style="text-align: center;">${r.onTimePercent != null ? `${r.onTimePercent}%` : "—"}</td>
        <td style="text-align: center;">${r.avgTestingTatDays}d</td>
        <td style="text-align: center;">${r.reviewReturns != null ? r.reviewReturns : "—"}</td>
        <td style="text-align: center;">${r.docCorrections != null ? r.docCorrections : "—"}</td>
        <td style="text-align: center;">${r.pending}</td>
        <td style="text-align: center; color: ${r.overdue > 0 ? "#dc2626" : "inherit"}; font-weight: ${r.overdue > 0 ? "bold" : "normal"};">${r.overdue}</td>
      </tr>`
    )
    .join("");

  const detail = data.selectedAnalystDetail;
  const detailSectionHtml = detail
    ? `
    <div class="section-title">Selected Analyst Detail: ${escapeHtml(detail.analystName)} (${escapeHtml(detail.username)})</div>
    <table class="grid-table">
      <tr>
        <th colspan="2">Workload Metrics</th>
        <th colspan="2">Timeliness &amp; Turnaround</th>
      </tr>
      <tr>
        <td>Assigned Tests:</td><td><strong>${detail.workload.assignedTests}</strong></td>
        <td>Avg Testing TAT:</td><td><strong>${detail.timeliness.avgTestingTatDays} Days</strong></td>
      </tr>
      <tr>
        <td>Completed Tests:</td><td><strong>${detail.workload.completedTests}</strong></td>
        <td>Median Testing TAT:</td><td><strong>${detail.timeliness.medianTestingTatDays} Days</strong></td>
      </tr>
      <tr>
        <td>Configured Workload Units:</td><td><strong>${detail.workload.configuredWorkloadUnits}</strong></td>
        <td>On-Time Rate:</td><td><strong>${detail.timeliness.onTimePercent}</strong></td>
      </tr>
      <tr>
        <td>Active Pending / Overdue:</td><td><strong>${detail.workload.pendingTests} / ${detail.workload.overdueTests}</strong></td>
        <td>Overdue Tests (>7d):</td><td><strong>${detail.timeliness.overdueCount}</strong></td>
      </tr>
      <tr>
        <th colspan="2">Documentation Quality</th>
        <th colspan="2">Compliance &amp; Competency</th>
      </tr>
      <tr>
        <td>Review Returns (Audit Edits):</td><td><strong>${detail.quality.reviewReturns}</strong></td>
        <td>Training Status:</td><td><strong>${detail.compliance.trainingStatus}</strong></td>
      </tr>
      <tr>
        <td>Documentation Corrections:</td><td><strong>${detail.quality.documentationCorrections}</strong></td>
        <td>Competency Status:</td><td><strong>${detail.compliance.competencyStatus}</strong></td>
      </tr>
      <tr>
        <td>Calculation Corrections:</td><td><strong>${detail.quality.calculationCorrections}</strong></td>
        <td>SOP Compliance Index:</td><td><strong>${detail.compliance.sopComplianceIndex}</strong></td>
      </tr>
    </table>`
    : "";

  const html = `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>Analyst Performance &amp; KPI Report</title>
  <style>
    @page { size: A4 landscape; margin: 12mm; }
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif; font-size: 10pt; color: #1e293b; margin: 0; padding: 12px; }
    .header { border-bottom: 2px solid #0f766e; padding-bottom: 8px; margin-bottom: 12px; }
    .title { font-size: 16pt; font-weight: bold; color: #0f766e; }
    .meta { font-size: 8.5pt; color: #64748b; margin-top: 4px; }
    .kpi-row { display: flex; gap: 8px; margin-bottom: 14px; }
    .kpi-card { flex: 1; border: 1px solid #cbd5e1; border-radius: 4px; padding: 8px; background: #f8fafc; text-align: center; }
    .kpi-title { font-size: 8pt; color: #64748b; font-weight: 600; text-transform: uppercase; }
    .kpi-val { font-size: 14pt; font-weight: 800; color: #0f766e; margin-top: 2px; }
    .section-title { font-size: 11pt; font-weight: bold; color: #0f766e; margin: 12px 0 6px 0; }
    table { width: 100%; border-collapse: collapse; font-size: 8.5pt; margin-bottom: 14px; }
    th { background: #0f766e; color: #ffffff; padding: 6px 8px; text-align: left; font-weight: 600; }
    td { padding: 5px 8px; border-bottom: 1px solid #e2e8f0; }
    tr.even { background: #ffffff; }
    tr.odd { background: #f8fafc; }
    .grid-table th { background: #334155; }
    .footer { margin-top: 16px; border-top: 1px solid #cbd5e1; padding-top: 8px; font-size: 7.5pt; color: #64748b; display: flex; justify-content: space-between; }
  </style>
</head>
<body>
  <div class="header">
    <div class="title">MicroLIMS — Analyst Performance &amp; KPI Report</div>
    <div class="meta"><strong>Scope:</strong> ${escapeHtml(filterSummary)} &nbsp;|&nbsp; <strong>Generated By:</strong> ${escapeHtml(generatedBy)} &nbsp;|&nbsp; <strong>Date:</strong> ${generatedAt}</div>
  </div>

  <div class="kpi-row">
    <div class="kpi-card">
      <div class="kpi-title">${data.testsAssigned.title}</div>
      <div class="kpi-val">${data.testsAssigned.value}</div>
    </div>
    <div class="kpi-card">
      <div class="kpi-title">${data.testsCompleted.title}</div>
      <div class="kpi-val">${data.testsCompleted.value}</div>
    </div>
    <div class="kpi-card">
      <div class="kpi-title">${data.completionRate.title}</div>
      <div class="kpi-val">${data.completionRate.value}</div>
    </div>
    <div class="kpi-card">
      <div class="kpi-title">${data.onTimeCompletion.title}</div>
      <div class="kpi-val">${data.onTimeCompletion.value}</div>
    </div>
    <div class="kpi-card">
      <div class="kpi-title">${data.averageTestingTat.title}</div>
      <div class="kpi-val">${data.averageTestingTat.value}</div>
    </div>
    <div class="kpi-card" style="border-color: #fca5a5; background: #fef2f2;">
      <div class="kpi-title" style="color: #dc2626;">${data.qualitySignalOos.title}</div>
      <div class="kpi-val" style="color: #dc2626;">${data.qualitySignalOos.value}</div>
    </div>
  </div>

  <div class="section-title">Analyst Comparison Summary</div>
  <table>
    <thead>
      <tr>
        <th>Analyst Name</th>
        <th style="text-align: center;">Assigned</th>
        <th style="text-align: center;">Completed</th>
        <th style="text-align: center;">Workload Units</th>
        <th style="text-align: center;">Completion %</th>
        <th style="text-align: center;">On-Time %</th>
        <th style="text-align: center;">Avg Testing TAT</th>
        <th style="text-align: center;">Review Returns</th>
        <th style="text-align: center;">Doc Corrections</th>
        <th style="text-align: center;">Pending</th>
        <th style="text-align: center;">Overdue</th>
      </tr>
    </thead>
    <tbody>
      ${comparisonRowsHtml}
    </tbody>
  </table>

  ${detailSectionHtml}

  <div class="footer">
    <div>GMP Audit Trail &amp; Data Integrity Standard — MicroLIMS Verified Report</div>
    <div>Page 1 of 1</div>
  </div>

  <script>
    window.addEventListener("load", () => {
      window.print();
    });
  </script>
</body>
</html>`;

  const printWindow = window.open("", "_blank");
  if (printWindow) {
    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();
  }
}
