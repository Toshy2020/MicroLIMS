import { useEffect, useState } from "react";
import {
  Button,
  Box,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Typography,
  Divider,
  Alert,
  useTheme
} from "@mui/material";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import { EquipmentFormState, EquipmentItem, EquipmentStatus } from "../types/equipmentTypes";
import { brandColors } from "../../../../theme";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { useMyLabs } from "../../../../hooks/useMyLabs";
import { useLaboratorySections } from "../../../../hooks/useLaboratorySections";

const STATUS_OPTIONS: { label: string; value: EquipmentStatus }[] = [
  { label: "In Service", value: "InService" },
  { label: "Out of Service", value: "OutOfService" },
  { label: "Retired", value: "Retired" }
];

interface RegisterEquipmentDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  editingItem: EquipmentItem | null;
}

const INITIAL_FORM: EquipmentFormState = {
  instrumentType: "",
  manufacturerName: "",
  serialNumber: "",
  firmwareVersion: "",
  code: "",
  location: "",
  calibrationDueDate: "",
  status: "InService",
  statusChangeComment: "",
  sectionId: ""
};

export function RegisterEquipmentDialog({
  open,
  onClose,
  onSuccess,
  editingItem
}: RegisterEquipmentDialogProps) {
  const theme = useTheme();
  const [form, setForm] = useState<EquipmentFormState>(INITIAL_FORM);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // The laboratory select only offers the labs the caller belongs to (an
  // admin's useMyLabs codes cover both, per-lab users see just their own);
  // id/name are resolved from the full section list since useMyLabs's own
  // `labs` array doesn't always carry a real membership row for an admin.
  const { codes: myLabCodes } = useMyLabs();
  const { sections } = useLaboratorySections();
  const myLabSections = sections.filter((s) => myLabCodes.includes(s.sectionCode));

  const isStatusChanged = editingItem != null && form.status !== editingItem.status;

  useEffect(() => {
    if (editingItem) {
      setForm({
        instrumentType: editingItem.instrumentType,
        manufacturerName: editingItem.manufacturerName ?? "",
        serialNumber: editingItem.serialNumber ?? "",
        firmwareVersion: editingItem.firmwareVersion ?? "",
        code: editingItem.code,
        location: editingItem.location,
        calibrationDueDate: editingItem.calibrationDueDate?.slice(0, 10) ?? "",
        status: editingItem.status,
        statusChangeComment: "",
        sectionId: editingItem.sectionId ?? ""
      });
    } else {
      setForm({
        ...INITIAL_FORM,
        sectionId: myLabSections.length === 1 ? myLabSections[0].sectionId : ""
      });
    }
    setError(null);
    // myLabSections is intentionally left out - it's derived from data that
    // loads asynchronously after this effect's first run, and re-running on
    // its every reference change would fight the user's own selection.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editingItem, open, myLabCodes.join(","), sections.length]);

  const handleSave = async () => {
    setError(null);
    if (!form.instrumentType.trim() || !form.code.trim() || !form.location.trim()) {
      setError("Instrument type, equipment code, and location are required.");
      return;
    }

    if (!form.sectionId) {
      setError("Choose the laboratory this asset belongs to.");
      return;
    }

    if (isStatusChanged && (!form.statusChangeComment || !form.statusChangeComment.trim())) {
      setError("A comment explaining the operational status change is required.");
      return;
    }

    const payload = {
      instrumentType: form.instrumentType.trim(),
      manufacturerName: form.manufacturerName.trim(),
      serialNumber: form.serialNumber.trim() || null,
      firmwareVersion: form.firmwareVersion.trim() || null,
      code: form.code.trim(),
      location: form.location.trim(),
      calibrationDueDate: form.calibrationDueDate || null,
      status: form.status,
      statusChangeComment: isStatusChanged ? form.statusChangeComment?.trim() : undefined,
      sectionId: Number(form.sectionId)
    };

    setSaving(true);
    try {
      if (editingItem) {
        await EquipmentInventoryService.update(editingItem.id, payload);
        onSuccess("Equipment updated successfully.");
      } else {
        await EquipmentInventoryService.create(payload);
        onSuccess("Equipment registered successfully.");
      }
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not save equipment.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      titleSx={{ pb: 1.5 }}
      title={
        <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
          {editingItem ? "Edit Equipment" : "Register Equipment"}
        </Typography>
      }
      actions={
        <Box sx={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
          <Button onClick={onClose} disabled={saving} color="inherit">
            Cancel
          </Button>
          <Button
            id="dialog-equip-save-btn"
            variant="contained"
            onClick={handleSave}
            disabled={saving || !form.sectionId || (isStatusChanged && (!form.statusChangeComment || !form.statusChangeComment.trim()))}
            sx={{
              bgcolor: brandColors.sectionTitle,
              px: 3,
              "&:hover": { bgcolor: brandColors.pageTitle }
            }}
          >
            {saving ? "Saving..." : editingItem ? "Save Changes" : "Register Equipment"}
          </Button>
        </Box>
      }
    >
      {error && (
        <Alert severity="error" sx={{ mb: 2.5 }}>
          {error}
        </Alert>
      )}

      {/* SECTION 1 — Equipment Information */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        1. Instrument Information
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2, mb: 3 }}>
        <TextField
          size="small"
          required
          label="Instrument Type"
          placeholder="e.g. Incubator, Pipette, pH Meter, Balance"
          value={form.instrumentType}
          onChange={(e) => setForm({ ...form, instrumentType: e.target.value })}
        />

        <TextField
          size="small"
          label="Manufacturer"
          placeholder="e.g. Memmert, Mettler Toledo, Sartorius"
          value={form.manufacturerName}
          onChange={(e) => setForm({ ...form, manufacturerName: e.target.value })}
        />

        <TextField
          size="small"
          required
          label="Equipment Code"
          placeholder="e.g. INC-F-ML-F-01-003"
          value={form.code}
          onChange={(e) => setForm({ ...form, code: e.target.value })}
        />

        <TextField
          size="small"
          required
          label="Location"
          placeholder="e.g. Microbiology Lab Room 102"
          value={form.location}
          onChange={(e) => setForm({ ...form, location: e.target.value })}
        />

        <TextField
          size="small"
          label="Serial Number"
          placeholder="e.g. SN-8823941"
          value={form.serialNumber}
          onChange={(e) => setForm({ ...form, serialNumber: e.target.value })}
        />

        <TextField
          size="small"
          label="Firmware Version"
          placeholder="e.g. v2.4.1"
          value={form.firmwareVersion}
          onChange={(e) => setForm({ ...form, firmwareVersion: e.target.value })}
        />

        <FormControl size="small" fullWidth required>
          <InputLabel id="dialog-equip-section-label">Laboratory</InputLabel>
          <Select<number | "">
            labelId="dialog-equip-section-label"
            id="dialog-equip-section-select"
            label="Laboratory"
            value={form.sectionId}
            onChange={(e) => setForm({ ...form, sectionId: e.target.value === "" ? "" : Number(e.target.value) })}
          >
            {myLabSections.length === 0 && (
              <MenuItem disabled value="">
                <em>No laboratory available</em>
              </MenuItem>
            )}
            {myLabSections.map((s) => (
              <MenuItem key={s.sectionId} value={s.sectionId}>
                {s.sectionName}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      <Divider sx={{ my: 2.5 }} />

      {/* SECTION 2 — Calibration & Operational Status */}
      <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
        2. Calibration & Operational Status
      </Typography>
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2, mb: isStatusChanged ? 2 : 0 }}>
        <TextField
          size="small"
          type="date"
          label="Calibration Due Date"
          value={form.calibrationDueDate}
          onChange={(e) => setForm({ ...form, calibrationDueDate: e.target.value })}
          slotProps={{
            inputLabel: { shrink: true }
          }}
        />

        <FormControl size="small" fullWidth required>
          <InputLabel id="dialog-equip-status-label">Operational Status</InputLabel>
          <Select
            labelId="dialog-equip-status-label"
            id="dialog-equip-status-select"
            label="Operational Status"
            value={form.status}
            onChange={(e) => setForm({ ...form, status: e.target.value as EquipmentStatus })}
          >
            {STATUS_OPTIONS.map((opt) => (
              <MenuItem key={opt.value} value={opt.value}>
                {opt.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      {isStatusChanged && (
        <Box sx={{ mt: 2 }}>
          <TextField
            id="equip-status-change-comment"
            label="Status Change Comment *"
            placeholder="Provide a mandatory reason for changing the operational status (e.g., Sent for calibration, Returned from vendor maintenance, Decommissioned)"
            fullWidth
            size="small"
            required
            multiline
            rows={2}
            value={form.statusChangeComment || ""}
            onChange={(e) => setForm({ ...form, statusChangeComment: e.target.value })}
            helperText="Required whenever the operational status changes."
          />
        </Box>
      )}
    </FloatingDialog>
  );
}
