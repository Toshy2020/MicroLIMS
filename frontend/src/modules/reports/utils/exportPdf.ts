import { ResultRecordItem } from "../types/reportingTypes";

export interface PdfExportOptions {
  title?: string;
  criteriaSummary?: string;
  generatedBy?: string;
  isSelection?: boolean;
}

function escapeHtml(str: string | null | undefined): string {
  if (!str) return "—";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  try {
    const d = new Date(iso);
    return d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
  } catch {
    return iso;
  }
}

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  try {
    const d = new Date(iso);
    return `${d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })} ${d.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false })}`;
  } catch {
    return iso;
  }
}

function formatResultLevel(level: string | null | undefined): string {
  if (!level || level === "NotApplicable") return "—";
  if (level === "WithinLimit") return `<span class="badge badge-pass">Within Limit</span>`;
  if (level === "AlertLevel") return `<span class="badge badge-alert">Alert Level</span>`;
  if (level === "ActionLevel") return `<span class="badge badge-action">Action Level</span>`;
  if (level === "OutOfSpecification") return `<span class="badge badge-oos">Out of Specification</span>`;
  return escapeHtml(level);
}

function formatLimits(record: ResultRecordItem): string {
  const parts: string[] = [];
  if (record.specLimit) parts.push(`Spec: ${record.specLimit}`);
  if (record.alertLimit) parts.push(`Alert: ${record.alertLimit}`);
  if (record.actionLimit) parts.push(`Action: ${record.actionLimit}`);
  return parts.length > 0 ? parts.join(" | ") : "—";
}

export function exportResultsPdf(records: ResultRecordItem[], options: PdfExportOptions = {}) {
  const title = options.title || (options.isSelection ? "Laboratory Results Export (Selected Records)" : "Laboratory Results Export");
  const generatedAt = new Date().toLocaleString("en-GB", {
    day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit"
  });
  const generatedBy = options.generatedBy || "Authorized User";
  const criteria = options.criteriaSummary || "All Active Results";
  const count = records.length;

  const rowsHtml = records.map((r, idx) => `
    <tr class="${idx % 2 === 0 ? "even" : "odd"}">
      <td class="ref-col"><strong>${escapeHtml(r.referenceNumber)}</strong><br/><small class="text-muted">${formatDateTime(r.resultEnteredAt)}</small></td>
      <td><span class="category-chip">${escapeHtml(r.category)}</span></td>
      <td><strong>${escapeHtml(r.subjectName)}</strong>${r.subjectDetail ? `<br/><small class="text-muted">${escapeHtml(r.subjectDetail)}</small>` : ""}</td>
      <td><strong>${escapeHtml(r.testDisplayName || r.testCode)}</strong></td>
      <td class="result-col"><strong>${escapeHtml(r.reportedValue)}</strong> ${escapeHtml(r.unit || "")}</td>
      <td>${formatResultLevel(r.resultLevel)}</td>
      <td class="limits-col"><small>${escapeHtml(formatLimits(r))}</small></td>
      <td><span class="status-chip status-${(r.approvalStatus || "pending").toLowerCase()}">${escapeHtml(r.approvalStatus)}</span></td>
      <td><small>${escapeHtml(r.resultEnteredByName || "—")}</small></td>
      <td><small>${escapeHtml(r.approvedByName || "—")}<br/>${formatDate(r.approvedAt)}</small></td>
    </tr>
  `).join("");

  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>${escapeHtml(title)} — MicroLIMS</title>
  <style>
    @page {
      size: A4 landscape;
      margin: 12mm;
      @bottom-right {
        content: "Page " counter(page);
        font-size: 8pt;
        color: #666;
      }
    }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      font-size: 9pt;
      color: #1f2937;
      line-height: 1.3;
      margin: 0;
      padding: 10px;
    }
    .header-container {
      border-bottom: 2px solid #5c1477;
      padding-bottom: 8px;
      margin-bottom: 12px;
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
    }
    .brand-title {
      font-size: 14pt;
      font-weight: 800;
      color: #5c1477;
      margin: 0 0 2px 0;
    }
    .doc-title {
      font-size: 12pt;
      font-weight: 700;
      color: #111827;
      margin: 0;
    }
    .meta-box {
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 4px;
      padding: 8px 12px;
      margin-bottom: 12px;
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 8px;
      font-size: 8.5pt;
    }
    .meta-item strong {
      color: #4b5563;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 8pt;
      margin-bottom: 15px;
    }
    th {
      background-color: #5c1477;
      color: #ffffff;
      font-weight: 700;
      text-align: left;
      padding: 6px 8px;
      border: 1px solid #4a0f61;
      white-space: nowrap;
    }
    td {
      padding: 5px 8px;
      border: 1px solid #e5e7eb;
      vertical-align: top;
    }
    tr.odd { background-color: #ffffff; }
    tr.even { background-color: #f9fafb; }
    .text-muted { color: #6b7280; }
    .ref-col { min-width: 90px; }
    .result-col { min-width: 80px; }
    .limits-col { font-size: 7.5pt; color: #4b5563; }
    .badge {
      display: inline-block;
      padding: 2px 6px;
      border-radius: 3px;
      font-size: 7.5pt;
      font-weight: 700;
      text-align: center;
    }
    .badge-pass { background: #dcfce7; color: #166534; }
    .badge-alert { background: #fef9c3; color: #854d0e; }
    .badge-action { background: #ffedd5; color: #9a3412; }
    .badge-oos { background: #fee2e2; color: #991b1b; }
    .category-chip {
      background: #f3e8ff;
      color: #6b21a8;
      padding: 2px 5px;
      border-radius: 3px;
      font-size: 7.5pt;
      font-weight: 600;
    }
    .status-chip {
      padding: 2px 5px;
      border-radius: 3px;
      font-size: 7.5pt;
      font-weight: 600;
    }
    .status-approved { background: #dcfce7; color: #166534; }
    .status-pending { background: #fef3c7; color: #92400e; }
    .status-rejected { background: #fee2e2; color: #991b1b; }
    .footer {
      border-top: 1px solid #e5e7eb;
      padding-top: 8px;
      margin-top: 12px;
      font-size: 7.5pt;
      color: #6b7280;
      display: flex;
      justify-content: space-between;
    }
    @media print {
      body { padding: 0; }
      .no-print { display: none !important; }
    }
  </style>
</head>
<body>
  <div class="header-container">
    <div>
      <div class="brand-title">MicroLIMS</div>
      <div class="doc-title">${escapeHtml(title)}</div>
    </div>
    <div style="text-align: right; font-size: 8pt; color: #6b7280;">
      <div>Laboratory Information Management System</div>
      <div>Confidential & Proprietary Data</div>
    </div>
  </div>

  <div class="meta-box">
    <div class="meta-item">
      <strong>Generated Date / Time:</strong> ${escapeHtml(generatedAt)}
    </div>
    <div class="meta-item">
      <strong>Generated By:</strong> ${escapeHtml(generatedBy)}
    </div>
    <div class="meta-item">
      <strong>Record Count:</strong> ${count} Records
    </div>
    <div class="meta-item" style="grid-column: span 3;">
      <strong>Search Criteria / Scope:</strong> ${escapeHtml(criteria)}
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th>Sample / Ref</th>
        <th>Category</th>
        <th>Item / Location</th>
        <th>Test</th>
        <th>Result</th>
        <th>Result Level</th>
        <th>Limits</th>
        <th>Status</th>
        <th>Analyst</th>
        <th>Approved By</th>
      </tr>
    </thead>
    <tbody>
      ${rowsHtml || `<tr><td colspan="10" style="text-align: center; padding: 20px; color: #6b7280;">No matching records found.</td></tr>`}
    </tbody>
  </table>

  <div class="footer">
    <div>Laboratory Results Export — Generated for analytical and reporting review. Not a formal Certificate of Analysis.</div>
    <div>MicroLIMS Quality Assurance</div>
  </div>

  <script>
    window.onload = function() {
      setTimeout(function() {
        window.print();
      }, 250);
    };
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
