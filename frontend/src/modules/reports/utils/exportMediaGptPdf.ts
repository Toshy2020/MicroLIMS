import { MediaGptListItem } from "../types/mediaGptTypes";

export interface MediaGptPdfExportOptions {
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

export function exportMediaGptPdf(records: MediaGptListItem[], options: MediaGptPdfExportOptions = {}) {
  const title = options.title || (options.isSelection ? "Media & GPT Report (Selected Lots)" : "Media & Growth Promotion Test (GPT) Report");
  const generatedAt = new Date().toLocaleString("en-GB", {
    day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit"
  });
  const generatedBy = options.generatedBy || "Authorized User";
  const criteria = options.criteriaSummary || "All Prepared Media Lots";
  const count = records.length;

  const rowsHtml = records.map((r, idx) => `
    <tr class="${idx % 2 === 0 ? "even" : "odd"}">
      <td class="lot-col"><strong>${escapeHtml(r.lotNumber)}</strong></td>
      <td><strong>${escapeHtml(r.mediaType)}</strong></td>
      <td>${formatDate(r.preparedAt)}</td>
      <td>${formatDate(r.expiryDate)}</td>
      <td><span class="type-chip">${escapeHtml(r.evaluationType)}</span></td>
      <td>
        <span class="badge ${r.evaluationOutcome === "Conform" ? "badge-pass" : r.evaluationOutcome === "NonConform" ? "badge-oos" : "badge-pending"}">
          ${escapeHtml(r.evaluationOutcome || "Pending")}
        </span>
        ${r.challengeCount > 0 ? `<br/><small class="text-muted">${r.conformedChallengeCount}/${r.challengeCount} Challenges Conform</small>` : ""}
      </td>
      <td>
        <span class="status-chip status-${(r.approvalStatus || "pending").toLowerCase()}">
          ${escapeHtml(r.approvalStatus)}
        </span>
        ${r.isReleasedForUse ? `<br/><small style="color: #166534; font-weight: 700;">Released</small>` : ""}
      </td>
      <td><small>${escapeHtml(r.preparedByName || "—")}</small></td>
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
    .text-muted { color: #6b7280; font-size: 7.5pt; }
    .lot-col { min-width: 90px; }
    .badge {
      display: inline-block;
      padding: 2px 6px;
      border-radius: 3px;
      font-size: 7.5pt;
      font-weight: 700;
      text-align: center;
    }
    .badge-pass { background: #dcfce7; color: #166534; }
    .badge-oos { background: #fee2e2; color: #991b1b; }
    .badge-pending { background: #fef3c7; color: #92400e; }
    .type-chip {
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
    .status-pendingreview { background: #fef3c7; color: #92400e; }
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
      <div>GMP Media Preparation & Growth Promotion Verification</div>
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
      <strong>Total Lots:</strong> ${count} Lots
    </div>
    <div class="meta-item" style="grid-column: span 3;">
      <strong>Search Criteria / Scope:</strong> ${escapeHtml(criteria)}
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th>Lot Number</th>
        <th>Media Type</th>
        <th>Prep Date</th>
        <th>Expiry Date</th>
        <th>Evaluation Type</th>
        <th>Outcome</th>
        <th>Approval Status</th>
        <th>Prepared By</th>
        <th>Approved By</th>
      </tr>
    </thead>
    <tbody>
      ${rowsHtml || `<tr><td colspan="9" style="text-align: center; padding: 20px; color: #6b7280;">No matching media lots found.</td></tr>`}
    </tbody>
  </table>

  <div class="footer">
    <div>Media & GPT Report — Generated for microbiological quality control and media qualification review.</div>
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
