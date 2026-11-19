// Splits clipboard text copied from a spreadsheet into rows of cells.
// Cells are separated by tabs and rows by line breaks; a cell wrapped in double
// quotes may contain tabs and line breaks, and "" inside it is one quote.
// Trailing blank lines are dropped. A single cell (no tab, no line break) is
// not a table and returns [], so the browser's normal paste can handle it.
export function parseClipboardTable(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let inQuotes = false;
  let cellStart = true;

  const endCell = () => {
    row.push(cell);
    cell = "";
    cellStart = true;
  };
  const endRow = () => {
    endCell();
    rows.push(row);
    row = [];
  };

  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (inQuotes) {
      if (ch === '"') {
        if (text[i + 1] === '"') {
          cell += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        cell += ch;
      }
      continue;
    }
    if (ch === '"' && cellStart) {
      inQuotes = true;
      cellStart = false;
    } else if (ch === "\t") {
      endCell();
    } else if (ch === "\r" || ch === "\n") {
      if (ch === "\r" && text[i + 1] === "\n") i++;
      endRow();
    } else {
      cell += ch;
      cellStart = false;
    }
  }
  // Text that does not end with a line break still has a last row.
  if (cell !== "" || row.length > 0) endRow();

  while (rows.length > 0 && rows[rows.length - 1].every((c) => c === "")) rows.pop();
  if (rows.length === 1 && rows[0].length === 1) return [];
  return rows;
}
