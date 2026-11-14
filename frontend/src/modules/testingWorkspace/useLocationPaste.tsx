import { useCallback, useMemo, useState } from "react";
import type { ClipboardEvent, ReactElement } from "react";
import { parseClipboardTable } from "../../components/entry/parseClipboardTable";
import { PastePreviewDialog } from "../../components/entry/PastePreviewDialog";
import type { PasteAcceptedRow, PastePlan } from "../../components/entry/PastePreviewDialog";
import { matchRowsByName } from "../../components/entry/pasteMatching";
import { parseCount } from "../../components/bench/NumericEntryField";

export const MAX_PASTE_ROWS = 500;

interface NamedLocation {
  name: string;
}

// Paste of "location name, count" rows into a location result grid. Counts are
// matched to rows by name only. Accepting calls apply(rowIndex, count) for each
// matched row; the grid is filled but nothing is saved.
export function useLocationPaste(
  locations: NamedLocation[] | null,
  nameLabel: string,
  apply: (rowIndex: number, count: string) => void
): { onPaste(e: ClipboardEvent): void; pasteDialog: ReactElement | null } {
  const [cells, setCells] = useState<string[][] | null>(null);

  const plan = useMemo<PastePlan>(
    () => ({
      title: "Paste preview",
      maxRows: MAX_PASTE_ROWS,
      columns: [
        { key: "name", label: nameLabel, check: () => null },
        {
          key: "count",
          label: "Count",
          check: (v) => (parseCount(v.trim()) === null ? "Enter a whole number, 0 or more, using digits only." : null)
        }
      ],
      match: (pasted) =>
        matchRowsByName(
          pasted,
          (locations ?? []).map((l, index) => ({ index, name: l.name })),
          { name: "name", value: "count" }
        )
    }),
    [locations, nameLabel]
  );

  const onPaste = useCallback(
    (e: ClipboardEvent) => {
      if (!locations) return;
      const table = parseClipboardTable(e.clipboardData.getData("text/plain"));
      // A single value is an ordinary paste into the focused field.
      if (table.length === 0) return;
      e.preventDefault();
      setCells(table);
    },
    [locations]
  );

  const accept = (rows: PasteAcceptedRow[]) => {
    for (const r of rows) {
      if (r.target !== null) apply(r.target, r.values.count.trim());
    }
    setCells(null);
  };

  const pasteDialog = cells ? (
    <PastePreviewDialog open plan={plan} cells={cells} onAccept={accept} onCancel={() => setCells(null)} />
  ) : null;

  return { onPaste, pasteDialog };
}
