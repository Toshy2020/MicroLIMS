import { useId, useMemo } from "react";
import { Alert, Box, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography, useTheme } from "@mui/material";
import { FormDialog } from "../lab/FormDialog";

export interface PasteColumn {
  key: string;
  label: string;
  // Format check only (null = acceptable). The server still validates everything on Save.
  check(value: string): string | null;
  // Optional: the value written to the grid when the cell is accepted (e.g. a date in the wire format).
  normalize?(value: string): string;
}

export interface PasteMatchRow {
  target: number | null;
  values: Record<string, string>;
  problem?: string;
}

export interface PasteMatch {
  rows: PasteMatchRow[];
  unmatchedTargets: string[];
  // A reason the whole paste cannot be used (shown instead of the table's rows).
  refusal?: string;
}

export interface PastePlan {
  title: string;
  columns: PasteColumn[];
  maxRows: number;
  // Pairs pasted rows with grid rows by name instead of by position.
  match?(cells: string[][]): PasteMatch;
  // Receiving: columns the paste skipped because they are choices, listed for the user.
  notPasted?: string[];
  // A reason the grid cannot take a paste at all from here.
  refusal?: string;
  // Values already entered in a grid row that accepting would overwrite (null = nothing there).
  // Shown as a warning; it does not block the paste.
  replacing?(target: number): { label: string; count: number } | null;
}

export interface PasteAcceptedRow {
  target: number | null;
  values: Record<string, string>;
}

interface Props {
  open: boolean;
  plan: PastePlan;
  cells: string[][];
  onAccept(rows: PasteAcceptedRow[]): void;
  onCancel(): void;
}

interface PreviewRow {
  target: number | null;
  values: Record<string, string>;
  problems: string[];
}

interface Analysis {
  refusal: string | null;
  rows: PreviewRow[];
  unmatched: string[];
  problemCount: number;
  extraColumns: number;
}

function analyse(plan: PastePlan, cells: string[][]): Analysis {
  const empty = { rows: [], unmatched: [], problemCount: 0, extraColumns: 0 };
  if (plan.refusal) return { ...empty, refusal: plan.refusal };
  if (cells.length === 0) return { ...empty, refusal: "There is nothing to paste." };
  if (cells.length > plan.maxRows) {
    return {
      ...empty,
      refusal: `The paste has ${cells.length} rows; at most ${plan.maxRows} rows can be pasted at once.`
    };
  }

  let source: PasteMatchRow[];
  let unmatched: string[] = [];
  let extraColumns = 0;
  if (plan.match) {
    const matched = plan.match(cells);
    if (matched.refusal) return { ...empty, refusal: matched.refusal };
    source = matched.rows;
    unmatched = matched.unmatchedTargets;
  } else {
    if (plan.columns.length === 0) return { ...empty, refusal: "There is no text or date column to paste into from this cell." };
    extraColumns = cells.reduce((max, r) => Math.max(max, r.length - plan.columns.length), 0);
    source = cells.map((r) => ({
      target: null,
      values: Object.fromEntries(plan.columns.map((c, j) => [c.key, (r[j] ?? "").trim()]))
    }));
  }

  let problemCount = 0;
  const rows = source.map<PreviewRow>((r) => {
    const problems: string[] = [];
    if (r.problem) problems.push(r.problem);
    for (const col of plan.columns) {
      const value = r.values[col.key] ?? "";
      const message = col.check(value);
      if (message) problems.push(`${col.label}: ${message}`);
    }
    problemCount += problems.length;
    const values = Object.fromEntries(
      plan.columns.map((col) => {
        const value = r.values[col.key] ?? "";
        return [col.key, col.check(value) === null && col.normalize ? col.normalize(value) : value];
      })
    );
    return { target: r.target, values, problems };
  });

  return { refusal: null, rows, unmatched, problemCount, extraColumns };
}

// One preview for every paste: the pasted block is shown as a table, every
// problem is listed beside its row, and Accept stays disabled until there is none.
// Accepting only hands the values back; saving stays with the grid's own Save.
export function PastePreviewDialog({ open, plan, cells, onAccept, onCancel }: Props) {
  const theme = useTheme();
  const captionId = useId();
  const analysis = useMemo(() => analyse(plan, cells), [plan, cells]);
  const blocked = analysis.refusal !== null || analysis.problemCount > 0;
  const replaced = useMemo(() => {
    if (analysis.refusal !== null || !plan.replacing) return [];
    const seen = new Set<number>();
    const list: { label: string; count: number }[] = [];
    for (const r of analysis.rows) {
      if (r.target === null || seen.has(r.target)) continue;
      seen.add(r.target);
      const existing = plan.replacing(r.target);
      if (existing && existing.count > 0) list.push(existing);
    }
    return list;
  }, [analysis, plan]);
  const replacedCount = replaced.reduce((sum, r) => sum + r.count, 0);

  const error =
    analysis.refusal ??
    (analysis.problemCount > 0
      ? `${analysis.problemCount} ${analysis.problemCount === 1 ? "problem" : "problems"} must be fixed in the source data before this paste can be accepted. Nothing has been entered.`
      : null);

  const submit = () => {
    if (blocked) return;
    onAccept(analysis.rows.map((r) => ({ target: r.target, values: r.values })));
  };

  return (
    <FormDialog
      open={open}
      title={plan.title}
      onClose={onCancel}
      onSubmit={submit}
      submitLabel="Accept"
      submitDisabled={blocked}
      error={error}
      maxWidth="md"
      initialFocus="none"
    >
      <Typography variant="body2" color="text.secondary">
        Check the pasted values. Accepting fills the grid only; nothing is saved until you save as usual.
      </Typography>

      {analysis.refusal === null && (
        <TableContainer
          tabIndex={0}
          role="region"
          aria-label="Pasted rows, scrollable"
          sx={{ maxHeight: "45vh", border: "1px solid", borderColor: "divider", borderRadius: 1 }}
        >
          <Table size="small" stickyHeader aria-labelledby={captionId}>
            <caption id={captionId} style={{ captionSide: "top", textAlign: "left", padding: "8px 16px" }}>
              Paste preview, {analysis.rows.length} {analysis.rows.length === 1 ? "row" : "rows"}
            </caption>
            <TableHead>
              <TableRow>
                <TableCell>Row</TableCell>
                {plan.columns.map((c) => (
                  <TableCell key={c.key}>{c.label}</TableCell>
                ))}
                <TableCell>Problem</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {analysis.rows.map((r, i) => (
                <TableRow
                  key={i}
                  sx={r.problems.length > 0 ? { bgcolor: theme.custom.status.detected.bg } : undefined}
                >
                  <TableCell component="th" scope="row" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {i + 1}
                  </TableCell>
                  {plan.columns.map((c) => (
                    <TableCell key={c.key} sx={{ fontVariantNumeric: "tabular-nums" }}>
                      {r.values[c.key] || "—"}
                    </TableCell>
                  ))}
                  <TableCell sx={r.problems.length > 0 ? { color: theme.custom.status.detected.text } : undefined}>
                    {r.problems.length > 0 ? r.problems.join(" ") : "None"}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {analysis.extraColumns > 0 && (
        <Typography variant="body2" role="status">
          {analysis.extraColumns} more {analysis.extraColumns === 1 ? "column" : "columns"} in the paste than can be filled from this cell; the extra {analysis.extraColumns === 1 ? "column was" : "columns were"} not pasted.
        </Typography>
      )}

      {replaced.length > 0 && (
        <Alert severity="warning" role="status">
          {`This will replace ${replacedCount} ${replacedCount === 1 ? "reading" : "readings"} already entered in ${replaced.length} ${replaced.length === 1 ? "row" : "rows"}. Check them before accepting.`}
        </Alert>
      )}
      <ListBlock
        title="Readings that will be replaced"
        items={replaced.map((r) => `${r.label} (${r.count} ${r.count === 1 ? "reading" : "readings"})`)}
      />
      <ListBlock title="Left empty (not in the paste)" items={analysis.unmatched} />
      <ListBlock title="Not pasted (choice columns, choose these by hand)" items={plan.notPasted ?? []} />
    </FormDialog>
  );
}

function ListBlock({ title, items }: { title: string; items: string[] }) {
  const id = useId();
  if (items.length === 0) return null;
  return (
    <Box>
      <Typography id={id} variant="subtitle2" component="p">
        {title}
      </Typography>
      <Box component="ul" aria-labelledby={id} sx={{ m: 0, pl: 3 }}>
        {items.map((item) => (
          <li key={item}>
            <Typography variant="body2" component="span">
              {item}
            </Typography>
          </li>
        ))}
      </Box>
    </Box>
  );
}
