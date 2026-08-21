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
  ListSubheader,
  FormControl,
  InputLabel,
  Typography,
  Divider,
  Alert,
  IconButton,
  Paper,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { OrganismPicker } from "../../../../components/OrganismPicker";
import { MaterialService } from "../services/MaterialService";
import { EquipmentInventoryService } from "../../equipment/services/EquipmentInventoryService";
import { MaterialFormState, MaterialItem, MaterialType, MaterialUnit } from "../types/materialTypes";
import { MATERIAL_TYPE_OPTIONS } from "./MaterialFilterBar";
import { brandColors } from "../../../../theme";

export const MATERIAL_UNITS: MaterialUnit[] = [
  "Gram",
  "Kilogram",
  "Milliliter",
  "Liter",
  "Disc",
  "Vial",
  "Kit",
  "Piece",
  "Bottle",
  "Pack"
];

interface AddMaterialDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  editingItem: MaterialItem | null;
}

const INITIAL_FORM: MaterialFormState = {
  materialType: "DehydratedMedia",
  materialName: "",
  manufacturerName: "",
  batchNumber: "",
  receivingDate: new Date().toISOString().slice(0, 10),
  expiryDate: "",
  code: "",
  location: "",
  quantityReceived: "",
  unit: "Gram",
  minimumStockLevel: "",
  atccNumber: "",
  organismId: null
};

export function AddMaterialDialog({ open, onClose, onSuccess, editingItem }: AddMaterialDialogProps) {
  const theme = useTheme();
  const [form, setForm] = useState<MaterialFormState>(INITIAL_FORM);
  const [equipmentList, setEquipmentList] = useState<any[]>([]);
  const [equipmentLoading, setEquipmentLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (editingItem) {
      setForm({
        materialType: editingItem.materialType,
        materialName: editingItem.materialName,
        manufacturerName: editingItem.manufacturerName ?? "",
        batchNumber: editingItem.batchNumber,
        receivingDate: editingItem.receivingDate?.slice(0, 10) ?? "",
        expiryDate: editingItem.expiryDate?.slice(0, 10) ?? "",
        code: editingItem.code ?? "",
        location: editingItem.location,
        quantityReceived: editingItem.quantityReceived,
        unit: editingItem.unit,
        minimumStockLevel: editingItem.minimumStockLevel ?? "",
        atccNumber: editingItem.atccNumber ?? "",
        organismId: editingItem.organismId ?? null
      });
    } else {
      setForm({
        ...INITIAL_FORM,
        receivingDate: new Date().toISOString().slice(0, 10)
      });
    }
    setError(null);

    if (open) {
      setEquipmentLoading(true);
      EquipmentInventoryService.getAll()
        .then((data: any[]) => setEquipmentList(data || []))
        .catch(() => setEquipmentList([]))
        .finally(() => setEquipmentLoading(false));
    }
  }, [editingItem, open]);

  // Eligible storage equipment: Status = InService
  const inServiceEquipment = equipmentList.filter(
    (e) => e.status === "InService" || e.status === 0 || e.status === "0"
  );

  const refrigerators = inServiceEquipment.filter((e) =>
    (e.instrumentType || "").toLowerCase().includes("refrigerator")
  );

  const deepFreezers = inServiceEquipment.filter((e) =>
    (e.instrumentType || "").toLowerCase().includes("deep freezer")
  );

  const freezers = inServiceEquipment.filter(
    (e) =>
      (e.instrumentType || "").toLowerCase().includes("freezer") &&
      !(e.instrumentType || "").toLowerCase().includes("deep freezer")
  );

  const selectedEquipment = inServiceEquipment.find((eq) => {
    const fullTag = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
    return (
      form.location === fullTag ||
      form.location === eq.code ||
      (form.location && form.location.includes(eq.code))
    );
  });

  const onMaterialTypeChange = async (type: MaterialType) => {
    try {
      const defaultUnit = await MaterialService.getDefaultUnit(type);
      setForm((f) => ({ ...f, materialType: type, unit: defaultUnit as MaterialUnit }));
    } catch {
      setForm((f) => ({ ...f, materialType: type }));
    }
  };

  const handleSave = async () => {
    setError(null);
    if (
      !form.materialName.trim() ||
      !form.batchNumber.trim() ||
      !form.receivingDate ||
      !form.location.trim() ||
      form.quantityReceived === "" ||
      form.quantityReceived == null
    ) {
      setError("Material name, batch/lot number, receiving date, storage location, and quantity received are required.");
      return;
    }

    if (Number(form.quantityReceived) <= 0) {
      setError("Quantity received must be greater than zero.");
      return;
    }

    const payload = {
      materialType: form.materialType,
      materialName: form.materialName.trim(),
      manufacturerName: form.manufacturerName.trim(),
      batchNumber: form.batchNumber.trim(),
      receivingDate: form.receivingDate,
      expiryDate: form.expiryDate || null,
      code: form.code.trim() || null,
      location: form.location.trim(),
      quantityReceived: Number(form.quantityReceived),
      unit: form.unit,
      minimumStockLevel: form.minimumStockLevel === "" ? null : Number(form.minimumStockLevel),
      atccNumber: form.materialType === "LyophilizedMicroorganism" ? form.atccNumber.trim() || null : null,
      organismId: form.materialType === "LyophilizedMicroorganism" ? form.organismId || null : null
    };

    setSaving(true);
    try {
      if (editingItem) {
        await MaterialService.update(editingItem.id, payload);
        onSuccess("Material stock updated successfully.");
      } else {
        await MaterialService.create(payload);
        onSuccess("Material added to stock successfully.");
      }
      onClose();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not save material stock.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", pb: 1.5 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
          {editingItem ? "Edit Material" : "Add Material to Stock"}
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

        {/* SECTION 1 — Material Information */}
        <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
          1. Material Information
        </Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2, mb: 3 }}>
          <FormControl size="small" fullWidth required>
            <InputLabel id="dialog-material-type-label">Material Type</InputLabel>
            <Select
              labelId="dialog-material-type-label"
              label="Material Type"
              value={form.materialType}
              onChange={(e) => onMaterialTypeChange(e.target.value as MaterialType)}
            >
              {MATERIAL_TYPE_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {opt.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            size="small"
            required
            label="Material Name"
            placeholder="e.g. Tryptic Soy Agar Powder"
            value={form.materialName}
            onChange={(e) => setForm({ ...form, materialName: e.target.value })}
          />

          <TextField
            size="small"
            label="Manufacturer"
            placeholder="e.g. Oxoid, Merck, Difco"
            value={form.manufacturerName}
            onChange={(e) => setForm({ ...form, manufacturerName: e.target.value })}
          />

          <TextField
            size="small"
            label="Code / Catalog No."
            placeholder="e.g. CM0131B"
            value={form.code}
            onChange={(e) => setForm({ ...form, code: e.target.value })}
          />

          {form.materialType === "LyophilizedMicroorganism" && (
            <>
              <Box sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
                <OrganismPicker
                  value={form.organismId}
                  onChange={(id) => setForm({ ...form, organismId: id })}
                />
              </Box>
              <TextField
                size="small"
                label="ATCC No."
                placeholder="e.g. ATCC 6538"
                value={form.atccNumber}
                onChange={(e) => setForm({ ...form, atccNumber: e.target.value })}
              />
            </>
          )}
        </Box>

        <Divider sx={{ my: 2.5 }} />

        {/* SECTION 2 — Batch & Quantity */}
        <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
          2. Batch &amp; Quantity
        </Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2, mb: 1.5 }}>
          <TextField
            size="small"
            required
            label="Batch / Lot No."
            placeholder="e.g. 3458921"
            value={form.batchNumber}
            onChange={(e) => setForm({ ...form, batchNumber: e.target.value })}
          />

          <FormControl size="small" fullWidth required sx={{ gridColumn: { sm: "span 2", md: "span 2" } }}>
            <InputLabel id="storage-location-label">Storage Location</InputLabel>
            <Select
              labelId="storage-location-label"
              label="Storage Location"
              value={form.location}
              onChange={(e) => setForm({ ...form, location: e.target.value })}
              disabled={equipmentLoading}
            >
              <MenuItem value="">
                <em>{equipmentLoading ? "Loading storage locations..." : "Select storage location..."}</em>
              </MenuItem>

              <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
                Equipment — Refrigerator
              </ListSubheader>
              {refrigerators.length > 0 ? (
                refrigerators.map((eq) => {
                  const val = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
                  return (
                    <MenuItem key={eq.id} value={val}>
                      {val}
                    </MenuItem>
                  );
                })
              ) : (
                <MenuItem disabled value="no-ref" sx={{ fontSize: 12, fontStyle: "italic" }}>
                  No in-service refrigerator configured
                </MenuItem>
              )}

              <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
                Equipment — Deep Freezer
              </ListSubheader>
              {deepFreezers.length > 0 ? (
                deepFreezers.map((eq) => {
                  const val = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
                  return (
                    <MenuItem key={eq.id} value={val}>
                      {val}
                    </MenuItem>
                  );
                })
              ) : (
                <MenuItem disabled value="no-df" sx={{ fontSize: 12, fontStyle: "italic" }}>
                  No in-service deep freezer configured
                </MenuItem>
              )}

              <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
                Equipment — Freezer
              </ListSubheader>
              {freezers.length > 0 ? (
                freezers.map((eq) => {
                  const val = `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})`;
                  return (
                    <MenuItem key={eq.id} value={val}>
                      {val}
                    </MenuItem>
                  );
                })
              ) : (
                <MenuItem disabled value="no-fr" sx={{ fontSize: 12, fontStyle: "italic" }}>
                  No in-service freezer configured
                </MenuItem>
              )}

              <ListSubheader sx={{ fontWeight: 700, fontSize: 11, color: "text.secondary", textTransform: "uppercase", lineHeight: "28px" }}>
                Other
              </ListSubheader>
              <MenuItem value="Microbiology Lab">
                Microbiology Lab
              </MenuItem>

              {/* Fallback for legacy material locations when editing */}
              {form.location &&
                form.location !== "Microbiology Lab" &&
                !inServiceEquipment.some(
                  (eq) => `${eq.instrumentType} — ${eq.manufacturerName} (${eq.code})` === form.location
                ) && (
                  <MenuItem value={form.location}>
                    {form.location} (Current Location)
                  </MenuItem>
                )}
            </Select>
          </FormControl>

          <TextField
            size="small"
            required
            type="number"
            label="Quantity Received"
            value={form.quantityReceived}
            onChange={(e) => setForm({ ...form, quantityReceived: e.target.value })}
            inputProps={{ min: 0, step: "any" }}
          />

          <FormControl size="small" fullWidth required>
            <InputLabel id="dialog-unit-label">Unit</InputLabel>
            <Select
              labelId="dialog-unit-label"
              label="Unit"
              value={form.unit}
              onChange={(e) => setForm({ ...form, unit: e.target.value as MaterialUnit })}
            >
              {MATERIAL_UNITS.map((u) => (
                <MenuItem key={u} value={u}>
                  {u}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            size="small"
            type="number"
            label="Min. Stock Level (optional)"
            placeholder="e.g. 500"
            value={form.minimumStockLevel}
            onChange={(e) => setForm({ ...form, minimumStockLevel: e.target.value })}
            inputProps={{ min: 0, step: "any" }}
          />
        </Box>

        {/* Selected Equipment Read-Only Details Card */}
        {selectedEquipment && (
          <Paper
            variant="outlined"
            sx={{
              mb: 3,
              p: 1.5,
              bgcolor: "background.default",
              borderColor: "divider",
              borderRadius: 1
            }}
          >
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(4, 1fr)"
                },
                gap: 1.5
              }}
            >
              <Box>
                <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                  Selected Storage
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle }}>
                  {selectedEquipment.instrumentType} — {selectedEquipment.manufacturerName || "Asset"}
                </Typography>
              </Box>
              <Box>
                <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                  Code
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                  {selectedEquipment.code}
                </Typography>
              </Box>
              <Box>
                <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                  Location
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                  {selectedEquipment.location || "—"}
                </Typography>
              </Box>
              <Box>
                <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                  Status
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "success.main" }}>
                  In Service
                </Typography>
              </Box>
            </Box>
          </Paper>
        )}

        {/* Microbiology Lab Read-Only Details Card */}
        {form.location === "Microbiology Lab" && (
          <Paper
            variant="outlined"
            sx={{
              mb: 3,
              p: 1.5,
              bgcolor: "background.default",
              borderColor: "divider",
              borderRadius: 1
            }}
          >
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(3, 1fr)"
                },
                gap: 1.5
              }}
            >
              <Box>
                <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                  Selected Storage
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: brandColors.sectionTitle }}>
                  Microbiology Lab
                </Typography>
              </Box>
              <Box>
                <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                  Storage Type
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                  Ambient / Room Storage
                </Typography>
              </Box>
              <Box>
                <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                  Location
                </Typography>
                <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                  Microbiology Laboratory
                </Typography>
              </Box>
            </Box>
          </Paper>
        )}

        <Divider sx={{ my: 2.5 }} />

        {/* SECTION 3 — Dates */}
        <Typography sx={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", color: "text.secondary", mb: 1.5 }}>
          3. Dates & Traceability
        </Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
          <TextField
            size="small"
            required
            type="date"
            label="Receiving Date"
            InputLabelProps={{ shrink: true }}
            value={form.receivingDate}
            onChange={(e) => setForm({ ...form, receivingDate: e.target.value })}
          />

          <TextField
            size="small"
            type="date"
            label="Expiry Date"
            InputLabelProps={{ shrink: true }}
            value={form.expiryDate}
            onChange={(e) => setForm({ ...form, expiryDate: e.target.value })}
          />
        </Box>

        {editingItem && (
          <Box sx={{ mt: 2.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              <strong>Note:</strong> Changing Quantity Received adjusts Quantity Remaining by the difference (a receiving correction) —
              it preserves consumption already recorded by Media Preparation or Cryovials.
            </Typography>
          </Box>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2, display: "flex", justifyContent: "space-between" }}>
        <Button onClick={onClose} disabled={saving} color="inherit">
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={saving}
          sx={{
            bgcolor: brandColors.sectionTitle,
            px: 3,
            "&:hover": { bgcolor: brandColors.pageTitle }
          }}
        >
          {saving ? "Saving..." : editingItem ? "Save Changes" : "Add to Stock"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
