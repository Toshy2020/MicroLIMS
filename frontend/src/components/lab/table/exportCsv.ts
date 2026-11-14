const FORMULA_PREFIX_RE = /^[=+\-@\t\r]/;

/**
 * Neutralise cells that could be interpreted as formulas by spreadsheet
 * applications (CSV injection defence).
 */
export function neutralise(cell: string): string {
  if (cell.length > 0 && FORMULA_PREFIX_RE.test(cell)) {
    return `'${cell}`;
  }
  return cell;
}

function escapeCsvCell(value: string): string {
  if (/[",\n\r]/.test(value)) {
    return `"${value.replace(/"/g, '""')}"`;
  }
  return value;
}

function formatCsvRow(cells: string[]): string {
  return cells.map((c) => escapeCsvCell(neutralise(c))).join(",");
}

export function buildCsv(
  header: string[][],
  columns: string[],
  rows: string[][]
): string {
  const lines: string[] = [];

  if (header.length > 0) {
    for (const metaRow of header) {
      lines.push(formatCsvRow(metaRow));
    }
    lines.push("");
  }

  lines.push(formatCsvRow(columns));
  for (const row of rows) {
    lines.push(formatCsvRow(row));
  }

  return `\uFEFF${lines.join("\r\n")}\r\n`;
}

export function downloadCsv(filename: string, csvContent: string): void {
  const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
