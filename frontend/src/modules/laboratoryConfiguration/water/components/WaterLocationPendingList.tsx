import { useMemo } from "react";
import { useTranslation } from "../../../../i18n";
import { PendingBatchList } from "../../../../components/entry/PendingBatchList";
import type { PendingBatchColumn } from "../../../../components/entry/PendingBatchList";
import { monospaceFontFamily } from "../../../../theme/palette";
import type { PendingWaterPoint } from "../waterSamplingPointPaste";

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
  const { t } = useTranslation("labConfig");
  const columns = useMemo<PendingBatchColumn<PendingWaterPoint>[]>(
    () => [
      {
        key: "code",
        label: t("water.pending.columns.code"),
        sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
        render: (r) => r.code
      },
      { key: "waterDepartmentName", label: t("water.pending.columns.system"), render: (r) => r.waterDepartmentName },
      { key: "location", label: t("water.pending.columns.name"), render: (r) => r.location || "—" },
      { key: "testingFrequency", label: t("water.pending.columns.frequency"), render: (r) => r.testingFrequency || "—" },
      {
        key: "assignedTestCodes",
        label: t("water.pending.columns.tests"),
        render: (r) => (r.assignedTestCodes.length > 0 ? r.assignedTestCodes.join(", ") : "—")
      }
    ],
    [t]
  );

  return (
    <PendingBatchList
      title={t("water.pending.title", { count: rows.length })}
      subtitle={t("water.pending.subtitle", { locations: t("water.locationCount", { count: rows.length }) })}
      ariaLabel={t("water.pending.aria")}
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
