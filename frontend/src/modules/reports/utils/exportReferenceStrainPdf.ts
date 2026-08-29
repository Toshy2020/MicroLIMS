import { ReferenceStrainListItem } from "../types/referenceStrainTypes";

export interface ReferenceStrainPdfExportOptions {
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
  if (!iso || iso.startsWith("0001")) return "—";
  try {
    const d = new Date(iso);
    return d.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
  } catch {
    return iso;
  }
}

export function exportReferenceStrainPdf(records: ReferenceStrainListItem[], options: ReferenceStrainPdfExportOptions = {}) {
  const title = options.title || (options.isSelection ? "Reference Strains Report (Selected Batches)" : "Reference Microorganism Strains Report");
  const generatedAt = new Date().toLocaleString("en-GB", {
    day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit"
  });
  const generatedBy = options.generatedBy || "Authorized User";
  const criteria = options.criteriaSummary || "All Reference Strain Batches";
  const count = records.length;

  const rowsHtml = records.map((r, idx) => `
    <tr class="${idx % 2 === 0 ? "even" : "odd"}">
      <td>
        <strong>${escapeHtml(r.strainName)}</strong>
        ${r.atccNumber ? `<br/><small class="text-muted">ATCC ${escapeHtml(r.atccNumber)}</small>` : ""}
      </td>
      <td><strong>${escapeHtml(r.cryovialCode)}</strong></td>
      <td>
        ${escapeHtml(r.manufacturerName || "—")}
        ${r.sourceMaterialBatchNumber ? `<br/><small class="text-muted">Batch: ${escapeHtml(r.sourceMaterialBatchNumber)}</small>` : ""}
      </td>
      <td>${formatDate(r.receiptDate)}</td>
      <td>${formatDate(r.preparedAt)}</td>
      <td>${formatDate(r.expiryDate)}</td>
      <td style="text-align: center;"><strong>${r.vialsRemaining}</strong> / ${r.numberOfVialsPrepared}</td>
      <td><small>${escapeHtml(r.storageCondition || "—")}</small></td>
      <td>
        <span class="status-chip status-${(r.approvalStatus || "pending").toLowerCase()}">
          ${escapeHtml(r.approvalStatus)}
        </span>
        ${r.isDestroyed ? `<br/><small style="color: #991b1b; font-weight: 700;">Destroyed</small>` : ""}
      </td>
      <td><small>${escapeHtml(r.preparedByName || "—")}</small></td>
      <td><small>${escapeHtml(r.approvedByName || "—")}<br/>${formatDate(r.approvedAt)}</small></td>
      <td style="text-align: center;"><strong>${r.directUsageCount}</strong></td>
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
      <div>Reference Microorganism Working Cultures Register</div>
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
      <strong>Total Batches:</strong> ${count} Batches
    </div>
    <div class="meta-item" style="grid-column: span 3;">
      <strong>Search Criteria / Scope:</strong> ${escapeHtml(criteria)}
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th>Strain / ATCC</th>
        <th>Cryovial Code</th>
        <th>Manufacturer / Source</th>
        <th>Receipt Date</th>
        <th>Prep Date</th>
        <th>Expiry Date</th>
        <th>Vials (Rem / Prep)</th>
        <th>Storage</th>
        <th>Status</th>
        <th>Prepared By</th>
        <th>Approved By</th>
        <th>GPT Usage</th>
      </tr>
    </thead>
    <tbody>
      ${rowsHtml || `<tr><td colspan="12" style="text-align: center; padding: 20px; color: #6b7280;">No matching reference strain batches found.</td></tr>`}
    </tbody>
  </table>

  <div class="footer">
    <div>Reference Strains Report — Working cultures traceability and qualification usage history.</div>
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
