import { useMemo } from "react";
import { PendingBatchList } from "../../../../components/entry/PendingBatchList";
import type { PendingBatchColumn } from "../../../../components/entry/PendingBatchList";
import { monospaceFontFamily } from "../../../../theme/palette";
import type { PendingWaterPoint } from "../waterSamplingPointPaste";
import { plural } from "../waterConfigTypes";

interface Props {
  rows: PendingWaterPoint[];
  saving: boolean;
  error: string | null;
  onSave: () => void;
  onDiscard: () => void;
  onRemoveRow: (index: number) => void;
}

/**
 * Preview list for accepted pasted water sampling points prior to batch save.
 * Nothing is created until Save; server row errors are displayed against each row.
 */
export function WaterLocationPendingList({ rows, saving, error, onSave, onDiscard, onRemoveRow }: Props) {
  const columns = useMemo<PendingBatchColumn<PendingWaterPoint>[]>(
    () => [
      {
        key: "code",
        label: "Point code",
        sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
        render: (r) => r.code
      },
      { key: "waterDepartmentName", label: "Water system", render: (r) => r.waterDepartmentName },
      { key: "location", label: "Point name", render: (r) => r.location || "—" },
      { key: "testingFrequency", label: "Frequency", render: (r) => r.testingFrequency || "—" },
      {
        key: "assignedTestCodes",
        label: "Assigned tests",
        render: (r) => (r.assignedTestCodes.length > 0 ? r.assignedTestCodes.join(", ") : "—")
      }
    ],
    []
  );

  return (
    <PendingBatchList
      title={`Pending sample locations (${rows.length})`}
      subtitle={`Review the ${plural(rows.length, "location")} below. Nothing is saved until you click Save.`}
      ariaLabel="Pending sample locations"
      columns={columns}
      rows={rows}
      getRowKey={(r, i) => `${r.code}-${i}`}
      getRowLabel={(r) => r.code}
      saving={saving}
      error={error}
      onSave={onSave}
      onDiscard={onDiscard}
      onRemoveRow={onRemoveRow}
    />
  );
}
