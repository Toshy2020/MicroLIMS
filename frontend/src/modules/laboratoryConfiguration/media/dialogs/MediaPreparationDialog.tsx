import { useEffect, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Box,
  Typography,
  Button,
  Alert,
  TextField,
  Select,
  MenuItem,
  Divider,
  Stack
} from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { MediaPreparationService } from "../services/MediaPreparationService";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import { masterDataOptions, mediaClassLabel } from "../../../../services/masterDataOptions";
import { brandColors } from "../../../../theme";

interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: (newLot: any) => void;
}

export function MediaPreparationDialog({ open, onClose, onSuccess }: Props) {
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [autoclaves, setAutoclaves] = useState<any[]>([]);
  const [dehydratedMedia, setDehydratedMedia] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({});
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      setError(null);
      setForm({});
      masterDataOptions.getMediaTypes().then(setMediaTypes).catch(() => setMediaTypes([]));
      masterDataOptions.getEquipment("Autoclave").then(setAutoclaves).catch(() => setAutoclaves([]));
      MaterialService.getAll("DehydratedMedia").then(setDehydratedMedia).catch(() => setDehydratedMedia([]));
    }
  }, [open]);

  const usableStock = dehydratedMedia.filter((m) => m.status === "InStock");
  const selectedMaterial = usableStock.find((m) => m.id === form.materialId);

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Basic required field validation
    if (!form.mediaTypeId) {
      setError("Please select a Media Type.");
      return;
    }
    if (!form.materialId) {
      setError("Please select a Dehydrated Media Stock item from inventory.");
      return;
    }
    if (!form.totalWeight) {
      setError("Please enter the total weight.");
      return;
    }
    if (!form.totalVolume) {
      setError("Please enter the total volume.");
      return;
    }
    if (!form.autoclaveEquipmentId) {
      setError("Please select an Autoclave.");
      return;
    }
    if (!form.autoclaveProgram) {
      setError("Please specify the Autoclave Program / Load.");
      return;
    }
    if (!form.loadType) {
      setError("Please specify the Load Type.");
      return;
    }
    if (!form.temperature) {
      setError("Please specify the sterilization temperature.");
      return;
    }
    if (!form.cycleTime) {
      setError("Please specify the cycle time.");
      return;
    }
    if (!form.cycleNumber) {
      setError("Please specify the cycle number.");
      return;
    }
    if (!form.ph) {
      setError("Please specify the pH.");
      return;
    }
    if (!form.expiryDate) {
      setError("Please specify the Expiry Date.");
      return;
    }

    setSaving(true);
    try {
      const result = await MediaPreparationService.prepare({
        mediaTypeId: Number(form.mediaTypeId),
        materialId: Number(form.materialId),
        totalWeight: Number(form.totalWeight),
        totalVolume: form.totalVolume,
        autoclaveEquipmentId: Number(form.autoclaveEquipmentId),
        autoclaveProgram: form.autoclaveProgram,
        loadType: form.loadType,
        temperature: Number(form.temperature),
        cycleTime: Number(form.cycleTime),
        cycleNumber: Number(form.cycleNumber),
        ph: Number(form.ph),
        expiryDate: form.expiryDate
      });

      onSuccess(result);
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not prepare media lot. Please verify all inputs.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md">
      <form onSubmit={handleSubmit}>
        <DialogTitle sx={{ pb: 1 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <ScienceOutlinedIcon sx={{ color: brandColors.sectionTitle }} />
            <Typography sx={{ fontSize: 18, fontWeight: 700, color: brandColors.pageTitle }}>
              Prepare New Media Lot
            </Typography>
          </Box>
          <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
            Record formulation, autoclave sterilization parameters, pH, and expiration for the new media lot.
          </Typography>
        </DialogTitle>

        <DialogContent dividers>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Stack spacing={2.5}>
            {/* Section 1: Preparation Details */}
            <Box>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle, mb: 1.5 }}>
                1. PREPARATION DETAILS & INVENTORY STOCK
              </Typography>

              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
                <Select
                  size="small"
                  displayEmpty
                  value={form.mediaTypeId ?? ""}
                  onChange={(e) => setField("mediaTypeId", e.target.value)}
                >
                  <MenuItem value="">
                    <em>Select Media Type *</em>
                  </MenuItem>
                  {mediaTypes.map((m) => (
                    <MenuItem key={m.id} value={m.id}>
                      {mediaClassLabel(m.class)}
                    </MenuItem>
                  ))}
                </Select>

                <Box>
                  <Select
                    size="small"
                    displayEmpty
                    fullWidth
                    value={form.materialId ?? ""}
                    onChange={(e) => setField("materialId", e.target.value)}
                  >
                    <MenuItem value="">
                      <em>Dehydrated Media Stock (Inventory) *</em>
                    </MenuItem>
                    {usableStock.map((m) => (
                      <MenuItem key={m.id} value={m.id}>
                        {m.materialName} — batch {m.batchNumber} ({m.quantityRemaining} {m.unit} left)
                      </MenuItem>
                    ))}
                  </Select>
                  {selectedMaterial && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                      Manufacturer: {selectedMaterial.manufacturerName} · Batch: {selectedMaterial.batchNumber}
                    </Typography>
                  )}
                </Box>

                <TextField
                  size="small"
                  label="Total Weight (g) *"
                  type="number"
                  placeholder="e.g. 40"
                  value={form.totalWeight ?? ""}
                  onChange={(e) => setField("totalWeight", e.target.value)}
                />

                <TextField
                  size="small"
                  label="Total Volume *"
                  placeholder="e.g. 1000 mL or 2 L"
                  value={form.totalVolume ?? ""}
                  onChange={(e) => setField("totalVolume", e.target.value)}
                />
              </Box>
            </Box>

            <Divider />

            {/* Section 2: Sterilization */}
            <Box>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle, mb: 1.5 }}>
                2. STERILIZATION & AUTOCLAVE PARAMETERS
              </Typography>

              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
                <Select
                  size="small"
                  displayEmpty
                  value={form.autoclaveEquipmentId ?? ""}
                  onChange={(e) => setField("autoclaveEquipmentId", e.target.value)}
                >
                  <MenuItem value="">
                    <em>Select Autoclave *</em>
                  </MenuItem>
                  {autoclaves.map((a) => (
                    <MenuItem key={a.id} value={a.id}>
                      {a.name}
                    </MenuItem>
                  ))}
                </Select>

                <TextField
                  size="small"
                  label="Program / Load *"
                  placeholder="e.g. Media Cycle 1"
                  value={form.autoclaveProgram ?? ""}
                  onChange={(e) => setField("autoclaveProgram", e.target.value)}
                />

                <TextField
                  size="small"
                  label="Load Type *"
                  placeholder="e.g. Liquid Media"
                  value={form.loadType ?? ""}
                  onChange={(e) => setField("loadType", e.target.value)}
                />

                <TextField
                  size="small"
                  label="Temperature (°C) *"
                  type="number"
                  placeholder="e.g. 121"
                  value={form.temperature ?? ""}
                  onChange={(e) => setField("temperature", e.target.value)}
                />

                <TextField
                  size="small"
                  label="Cycle Time (min) *"
                  type="number"
                  placeholder="e.g. 15"
                  value={form.cycleTime ?? ""}
                  onChange={(e) => setField("cycleTime", e.target.value)}
                />

                <TextField
                  size="small"
                  label="Cycle Number *"
                  type="number"
                  placeholder="e.g. 1"
                  value={form.cycleNumber ?? ""}
                  onChange={(e) => setField("cycleNumber", e.target.value)}
                />
              </Box>
            </Box>

            <Divider />

            {/* Section 3: Quality & Expiry */}
            <Box>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle, mb: 1.5 }}>
                3. QUALITY SPECIFICATIONS & EXPIRY
              </Typography>

              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
                <TextField
                  size="small"
                  label="pH (at 25°C) *"
                  type="number"
                  inputProps={{ step: "0.01" }}
                  placeholder="e.g. 7.30"
                  value={form.ph ?? ""}
                  onChange={(e) => setField("ph", e.target.value)}
                />

                <TextField
                  size="small"
                  label="Expiry Date *"
                  type="date"
                  InputLabelProps={{ shrink: true }}
                  value={form.expiryDate ?? ""}
                  onChange={(e) => setField("expiryDate", e.target.value)}
                />
              </Box>
            </Box>
          </Stack>
        </DialogContent>

        <DialogActions sx={{ p: 2 }}>
          <Button onClick={onClose} variant="outlined" disabled={saving}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={saving}
            sx={{
              bgcolor: brandColors.sectionTitle,
              fontWeight: 700,
              "&:hover": { bgcolor: "#632273" }
            }}
          >
            {saving ? "Saving…" : "Save Prepared Lot"}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
