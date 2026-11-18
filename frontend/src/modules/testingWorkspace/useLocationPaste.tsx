import { useCallback, useMemo, useState } from "react";
import type { ClipboardEvent, ReactElement } from "react";
import { parseClipboardTable } from "../../components/entry/parseClipboardTable";
import { PastePreviewDialog } from "../../components/entry/PastePreviewDialog";
import type { PasteAcceptedRow, PastePlan } from "../../components/entry/PastePreviewDialog";
import { matchRowsByName } from "../../components/entry/pasteMatching";
import { parseCount } from "../../components/bench/NumericEntryField";
import { useTranslation } from "../../i18n";

export const MAX_PASTE_ROWS = 500;

interface NamedLocation {
  name: string;
  // Readings already entered in this row; the preview warns that accepting replaces them.
  enteredReadings?: number;
}

// Paste of "location name, count" rows into a location result grid. Counts are
// matched to rows by name only. Accepting calls apply(rowIndex, count) for each
// matched row, which replaces every reading in that row with the pasted count;
// the preview warns how many entered readings that replaces. Nothing is saved.
export function useLocationPaste(
  locations: NamedLocation[] | null,
  nameLabel: string,
  apply: (rowIndex: number, count: string) => void
): { onPaste(e: ClipboardEvent): void; pasteDialog: ReactElement | null } {
  const { t } = useTranslation("testingWorkspace");
  const [cells, setCells] = useState<string[][] | null>(null);

  const plan = useMemo<PastePlan>(
    () => ({
      title: t("locationGrid.paste.title"),
      maxRows: MAX_PASTE_ROWS,
      columns: [
        { key: "name", label: nameLabel, check: () => null },
        {
          key: "count",
          label: t("locationGrid.paste.countCol"),
          check: (v) => (parseCount(v.trim()) === null ? t("locationGrid.paste.invalidCount") : null)
        }
      ],
      match: (pasted) =>
        matchRowsByName(
          pasted,
          (locations ?? []).map((l, index) => ({ index, name: l.name })),
          { name: "name", value: "count" }
        ),
      replacing: (target) => {
        const location = locations?.[target];
        const count = location?.enteredReadings ?? 0;
        return location && count > 0 ? { label: location.name, count } : null;
      }
    }),
    [locations, nameLabel, t]
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
