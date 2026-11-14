import { useEffect, useMemo, useState } from "react";
import type { ClipboardEvent as ReactClipboardEvent } from "react";
import { Button, Paper, Stack, TextField } from "@mui/material";
import { toast } from "sonner";
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

const EQUIPMENT_PENDING_COLUMNS: PendingBatchColumn<PendingEquipment>[] = [
  { key: "name", label: "Equipment name", render: (r) => r.name },
  {
    key: "code",
    label: "Equipment code",
    sx: { fontFamily: monospaceFontFamily, fontWeight: 600 },
    render: (r) => r.code
  },
  { key: "typeLabel", label: "Equipment type", render: (r) => r.typeLabel },
  { key: "vendor", label: "Vendor / Manufacturer", render: (r) => r.vendor || "—" },
  { key: "sectionName", label: "Laboratory section", render: (r) => r.sectionName || "—" },
  { key: "location", label: "Location", render: (r) => r.location || "—" },
  { key: "inventoryLabel", label: "Asset register entry", render: (r) => r.inventoryLabel || "—" },
  { key: "connectionSettings", label: "Connection settings", render: (r) => r.connectionSettings || "—" },
  {
    key: "setPointTemperature",
    label: "Set point temp (°C)",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => (r.setPointTemperature !== null ? String(r.setPointTemperature) : "—")
  },
  {
    key: "calibrationDueDate",
    label: "Calibration due date",
    sx: { fontVariantNumeric: "tabular-nums" },
    render: (r) => r.calibrationDueDate || "—"
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
  const [previewCells, setPreviewCells] = useState<string[][] | null>(null);
  const [pendingRows, setPendingRows] = useState<PendingEquipment[]>([]);
  const [saving, setSaving] = useState(false);
  const [batchError, setBatchError] = useState<string | null>(null);

  const pastePlan = useMemo(
    () => buildEquipmentPastePlan({ existingEquipment, mySections, inventoryList }),
    [existingEquipment, mySections, inventoryList]
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
    setPendingRows(toPendingEquipmentRows(rows, mySections, inventoryList));
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
      toast.success(`Created ${count} equipment item${count === 1 ? "" : "s"}.`);
      await onCreated();
    } catch (err: unknown) {
      const { summary, rowErrors } = extractBatchSaveErrors(
        err,
        "Could not create equipment. Nothing was created."
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
              label="Paste equipment rows"
              placeholder="Paste rows copied from a spreadsheet (Equipment name, Equipment code, Equipment type, Vendor / Manufacturer, Laboratory section, Location, Asset register entry, Connection settings, Set point temp (°C), Calibration due date)"
              value=""
              onChange={() => {}}
              onPaste={handlePasteInput}
              helperText="Columns in order: Equipment name, Equipment code, Equipment type, Vendor / Manufacturer, Laboratory section, Location, Asset register entry, Connection settings, Set point temp (°C), Calibration due date (YYYY-MM-DD). Add set points and programs after creating the equipment. HPLC, ICP-OES, pH Meter, and Balance are not supported by paste here."
            />
            <Button onClick={onClosePasteBar} sx={{ textTransform: "none", flexShrink: 0 }}>
              Close
            </Button>
          </Stack>
        </Paper>
      )}

      <PendingBatchList
        title={`Pending equipment (${pendingRows.length})`}
        subtitle={`Review the ${pendingRows.length} equipment item${pendingRows.length === 1 ? "" : "s"} below. Nothing is saved until you click Save.`}
        ariaLabel="Pending equipment"
        columns={EQUIPMENT_PENDING_COLUMNS}
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
