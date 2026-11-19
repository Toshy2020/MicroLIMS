import { toast } from "sonner";
import { getErrorMessage } from "../../../utils/errorMessage";
import { buildCsv, downloadCsv } from "./exportCsv";
import { exportLogService } from "./services/exportLogService";
import { TABLES } from "./tableRegistry";

export const MAX_EXPORT_ROWS = 5000;

export interface ExportColumn<T> {
  key: string;
  label: string;
  // The column title in the file. Printed and exported output is English whatever the interface language.
  exportLabel?: string;
  // Used when the row has no value under `key`.
  sortValue?: (row: T) => string | number | null | undefined;
  // What the CSV carries for this column, as the screen shows it (formatted dates, labels, text of a rendered
  // cell). Takes precedence over the raw row value and over sortValue, which may be an epoch or an enum.
  exportValue?: (row: T) => string | number | null | undefined;
}

export interface ExportRowsOptions<T> {
  tableKey: string;
  rows: T[];
  columns: ExportColumn<T>[];
  getRowId: (row: T) => string | number;
  filters: Record<string, string | string[] | undefined>;
  search: string;
  t: (key: string) => string;
}

// Logs the export with the server first (the audit entry holds who, what and when, and the server's time stamps
// the file), then builds and downloads the CSV. A refused log means no file. Shared by every table that offers
// "Export selected", so each export is audited the same way.
export async function exportRegisterRows<T>({ tableKey, rows, columns, getRowId, filters, search, t }: ExportRowsOptions<T>): Promise<void> {
  if (rows.length > MAX_EXPORT_ROWS) {
    toast.error(t("table.exportLimit"));
    return;
  }
  const filterJson = JSON.stringify({ filters, search });

  let logResult;
  try {
    logResult = await exportLogService.log({
      tableKey,
      filterJson,
      columns: columns.map((c) => c.key),
      rowCount: rows.length,
      recordIds: rows.map((r) => String(getRowId(r)))
    });
  } catch (err) {
    toast.error(getErrorMessage(err, t("table.exportFailed")));
    return;
  }

  const def = TABLES[tableKey];
  const filterParts = Object.entries(filters)
    .filter(([, v]) => (Array.isArray(v) ? v.length > 0 : Boolean(v)))
    .map(([k, v]) => `${k}=${Array.isArray(v) ? v.join("|") : v}`);
  if (search) filterParts.push(`q=${search}`);

  const headerRows: string[][] = [
    ["Exported by", logResult.exportedBy],
    ["Exported at", `${logResult.serverTimeUtc} (UTC)`],
    ["List", def?.title ?? tableKey],
    ["Filters", filterParts.length > 0 ? filterParts.join("; ") : "None"],
    ["Rows", String(rows.length)],
    ["Export ID", String(logResult.exportId)]
  ];

  const dataRows = rows.map((row) =>
    columns.map((col) => {
      if (col.exportValue) {
        const ev = col.exportValue(row);
        return ev !== undefined && ev !== null ? String(ev) : "";
      }
      const raw = (row as Record<string, unknown>)[col.key];
      if (raw !== undefined && raw !== null) return String(raw);
      if (col.sortValue) {
        const sv = col.sortValue(row);
        return sv !== undefined && sv !== null ? String(sv) : "";
      }
      return "";
    })
  );

  downloadCsv(`${tableKey}-export-${logResult.exportId}.csv`, buildCsv(headerRows, columns.map((c) => c.exportLabel ?? c.label), dataRows));
}
