import { useEffect, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
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
  IconButton,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { EquipmentInventoryService } from "../services/EquipmentInventoryService";
import { EquipmentFormState, EquipmentItem, EquipmentStatus } from "../types/equipmentTypes";
import { brandColors } from "../../../../theme";

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
  statusChangeComment: ""
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
        statusChangeComment: ""
      });
    } else {
      setForm(INITIAL_FORM);
    }
    setError(null);
  }, [editingItem, open]);

  const handleSave = async () => {
    setError(null);
    if (!form.instrumentType.trim() || !form.code.trim() || !form.location.trim()) {
      setError("Instrument type, equipment code, and location are required.");
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
      statusChangeComment: isStatusChanged ? form.statusChangeComment?.trim() : undefined
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
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", pb: 1.5 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
          {editingItem ? "Edit Equipment" : "Register Equipment"}
        </Typography>
        <IconButton size="small" onClick={onClose} disabled={saving}>
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>

      <DialogContent dividers sx={{ p: 3 }}>
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
            InputLabelProps={{ shrink: true }}
            value={form.calibrationDueDate}
            onChange={(e) => setForm({ ...form, calibrationDueDate: e.target.value })}
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
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2, display: "flex", justifyContent: "space-between" }}>
        <Button onClick={onClose} disabled={saving} color="inherit">
          Cancel
        </Button>
        <Button
          id="dialog-equip-save-btn"
          variant="contained"
          onClick={handleSave}
          disabled={saving || (isStatusChanged && (!form.statusChangeComment || !form.statusChangeComment.trim()))}
          sx={{
            bgcolor: brandColors.sectionTitle,
            px: 3,
            "&:hover": { bgcolor: brandColors.pageTitle }
          }}
        >
          {saving ? "Saving..." : editingItem ? "Save Changes" : "Register Equipment"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
