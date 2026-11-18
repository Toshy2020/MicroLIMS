import { useEffect, useMemo, useState } from "react";
import type { ClipboardEvent as ReactClipboardEvent } from "react";
import { Button, Paper, Stack, TextField } from "@mui/material";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";
import { parseClipboardTable } from "../../../components/entry/parseClipboardTable";
import { PastePreviewDialog, type PasteAcceptedRow } from "../../../components/entry/PastePreviewDialog";
import { PendingBatchList, type PendingBatchColumn } from "../../../components/entry/PendingBatchList";
import { monospaceFontFamily } from "../../../theme/palette";
import type { LaboratorySection } from "../../../services/laboratorySectionService";
import { extractBatchSaveErrors } from "../../../utils/fieldErrors";
import type { EquipmentItem } from "../../inventory/equipment/types/equipmentTypes";
import {
  buildEquipmentPastePlan,
  toCreateEquipmentPayload,
  toPendingEquipmentRows,
  type PendingEquipment
} from "./equipmentPaste";
import {
  EquipmentConfigurationService,
  type ConfiguredEquipmentSummary
} from "./services/EquipmentConfigurationService";

const getEquipmentPendingColumns = (
  t: ReturnType<typeof useTranslation>["t"]
): PendingBatchColumn<PendingEquipment>[] => [
  { key: "name", label: t("equipmentPaste.columns.name"), render: (r) => r.name },
  {
    key: "code",
    label: t("equipmentPaste.columns.code"),
    sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
    render: (r) => r.code
  },
  { key: "typeLabel", label: t("equipmentPaste.columns.type"), render: (r) => r.typeLabel },
  { key: "vendor", label: t("equipmentPaste.columns.vendor"), render: (r) => r.vendor || t("common.dash") },
  { key: "sectionName", label: t("equipmentPaste.columns.section"), render: (r) => r.sectionName || t("common.dash") },
  { key: "location", label: t("equipmentPaste.columns.location"), render: (r) => r.location || t("common.dash") },
  { key: "inventoryLabel", label: t("equipmentPaste.columns.inventory"), render: (r) => r.inventoryLabel || t("common.dash") },
  { key: "connectionSettings", label: t("equipmentPaste.columns.connection"), render: (r) => r.connectionSettings || t("common.dash") },
  {
    key: "setPointTemperature",
    label: t("equipmentPaste.columns.setPoint"),
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.setPointTemperature !== null ? String(r.setPointTemperature) : t("common.dash"))
  },
  {
    key: "calibrationDueDate",
    label: t("equipmentPaste.columns.calibrationDue"),
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.calibrationDueDate || t("common.dash")
  }
];

interface EquipmentPasteSectionProps {
  pasteBarOpen: boolean;
  onClosePasteBar: () => void;
  modalOpen: boolean;
  existingEquipment: ConfiguredEquipmentSummary[];
  mySections: LaboratorySection[];
  inventoryList: EquipmentItem[];
  onCreated: () => Promise<void>;
}

/**
 * Paste bar, preview dialog, and pending-batch table for Equipment Master creation.
 * Nothing is created until Save; Save sends a single all-or-nothing batch request.
 */
export function EquipmentPasteSection({
  pasteBarOpen,
  onClosePasteBar,
  modalOpen,
  existingEquipment,
  mySections,
  inventoryList,
  onCreated
}: EquipmentPasteSectionProps) {
  const { t } = useTranslation("masterData");
  const [previewCells, setPreviewCells] = useState<string[][] | null>(null);
  const [pendingRows, setPendingRows] = useState<PendingEquipment[]>([]);
  const [saving, setSaving] = useState(false);
  const [batchError, setBatchError] = useState<string | null>(null);

  const pendingColumns = useMemo(() => getEquipmentPendingColumns(t), [t]);

  const pastePlan = useMemo(
    () => buildEquipmentPastePlan({ existingEquipment, mySections, inventoryList, t }),
    [existingEquipment, mySections, inventoryList, t]
  );

  const anyModalOpen = modalOpen || previewCells !== null;

  useEffect(() => {
    if (anyModalOpen || !pasteBarOpen) return;
    const handleDocumentPaste = (e: ClipboardEvent) => {
      if (e.defaultPrevented) return;
      const target = e.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable)
      ) {
        return;
      }
      const text = e.clipboardData?.getData("text/plain") ?? "";
      const table = parseClipboardTable(text);
      if (table.length === 0) return;
      e.preventDefault();
      setPreviewCells(table);
    };
    document.addEventListener("paste", handleDocumentPaste);
    return () => document.removeEventListener("paste", handleDocumentPaste);
  }, [anyModalOpen, pasteBarOpen]);

  const handlePasteInput = (e: ReactClipboardEvent<HTMLElement>) => {
    if (!pasteBarOpen) return;
    const table = parseClipboardTable(e.clipboardData.getData("text/plain"));
    if (table.length === 0) return;
    e.preventDefault();
    e.stopPropagation();
    setPreviewCells(table);
  };

  const handleAccept = (rows: PasteAcceptedRow[]) => {
    setPendingRows(toPendingEquipmentRows(rows, mySections, inventoryList, t));
    setBatchError(null);
    setPreviewCells(null);
    onClosePasteBar();
  };

  const handleSave = async () => {
    if (pendingRows.length === 0) return;
    setSaving(true);
    setBatchError(null);
    try {
      const payload = pendingRows.map(toCreateEquipmentPayload);
      await EquipmentConfigurationService.createEquipmentBatch(payload);
      const count = pendingRows.length;
      setPendingRows([]);
      toast.success(t("equipmentPaste.createdToast", { count }));
      await onCreated();
    } catch (err: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        err,
        t("equipmentPaste.saveError")
      );
      if (rowErrors) {
        setPendingRows((prev) => prev.map((row, idx) => ({ ...row, errors: rowErrors[idx] })));
      }
      setBatchError(summary);
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      {pasteBarOpen && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2, borderRadius: 2 }}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: { sm: "flex-start" } }}>
            <TextField
              autoFocus
              size="small"
              fullWidth
              label={t("equipmentPaste.pasteLabel")}
              placeholder={t("equipmentPaste.pastePlaceholder")}
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText={t("equipmentPaste.pasteHelper")}
            />
            <Button onClick={onClosePasteBar} sx={{ textTransform: "none", flexShrink: 0 }}>
              {t("common.close")}
            </Button>
          </Stack>
        </Paper>
      )}

      <PendingBatchList
        title={t("equipmentPaste.pendingTitle", { count: pendingRows.length })}
        subtitle={t("equipmentPaste.pendingSubtitle", { count: pendingRows.length })}
        ariaLabel={t("equipmentPaste.pendingAria")}
        columns={pendingColumns}
        rows={pendingRows}
        getRowKey={(r, i) => `${r.code}-${i}`}
        getRowLabel={(r) => (r.code ? `${r.code} (${r.name})` : r.name)}
        saving={saving}
        error={batchError}
        onSave={() => void handleSave()}
        onDiscard={() => {
          setPendingRows([]);
          setBatchError(null);
        }}
        onRemoveRow={(index) => {
          setPendingRows((prev) => prev.filter((_, i) => i !== index).map((r) => ({ ...r, errors: undefined })));
          setBatchError(null);
        }}
      />

      <PastePreviewDialog
        open={previewCells !== null}
        plan={pastePlan}
        cells={previewCells ?? []}
        onAccept={handleAccept}
        onCancel={() => setPreviewCells(null)}
      />
    </>
  );
}
