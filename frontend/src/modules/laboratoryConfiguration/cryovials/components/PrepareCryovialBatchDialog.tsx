import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Typography,
  Box,
  Alert,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  IconButton,
  Divider,
  Stack,
  Paper,
  useTheme
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ScienceIcon from "@mui/icons-material/Science";
import AddIcon from "@mui/icons-material/Add";
import { PanelRow, PrepareCryovialPayload } from "../types/cryovialTypes";
import { CryovialService } from "../services/CryovialService";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import { EquipmentInventoryService } from "../../../inventory/equipment/services/EquipmentInventoryService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { brandColors } from "../../../../theme";

interface PrepareCryovialBatchDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

const emptyRow = (): PanelRow => ({
  mediaId: "",
  incubatorEquipmentId: "",
  incubationStart: "",
  incubationEnd: "",
  observationText: ""
});

export function PrepareCryovialBatchDialog({
  open,
  onClose,
  onSuccess
}: PrepareCryovialBatchDialogProps) {
  const theme = useTheme();
  const [materials, setMaterials] = useState<any[]>([]);
  const [releasedMedia, setReleasedMedia] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
  const [equipmentList, setEquipmentList] = useState<any[]>([]);
  const [equipmentLoading, setEquipmentLoading] = useState(false);
  const [form, setForm] = useState<Record<string, any>>({});
  const [panel, setPanel] = useState<PanelRow[]>([emptyRow()]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      setError(null);
      setForm({});
      setPanel([emptyRow()]);
      MaterialService.getAll("LyophilizedMicroorganism").then(setMaterials);
      masterDataOptions.getReleasedMedia().then(setReleasedMedia);
      masterDataOptions.getEquipment("Incubator").then(setIncubators);
      setEquipmentLoading(true);
      EquipmentInventoryService.getAll()
        .then((data: any[]) => setEquipmentList(data || []))
        .catch(() => setEquipmentList([]))
        .finally(() => setEquipmentLoading(false));
    }
  }, [open]);

  const usableMaterials = materials.filter((m) => m.status === "InStock");
  const selectedMaterial = usableMaterials.find((m) => m.id === form.materialId);

  // Eligible storage equipment: Deep Freezer or Freezer in service
  const eligibleFreezers = equipmentList.filter((e) => {
    const type = (e.instrumentType || "").trim().toLowerCase();
    const isFreezer = type === "deep freezer" || type === "freezer" || type.includes("freezer");
    const isInService = e.status === "InService" || e.status === 0 || e.status === "0";
    return isFreezer && isInService;
  });

  const selectedStorageEquipment = eligibleFreezers.find(
    (e) => e.id === Number(form.storageEquipmentId)
  );

  const setField = (k: string, v: any) => setForm((f) => ({ ...f, [k]: v }));
  const updateRow = (i: number, k: keyof PanelRow, v: string) =>
    setPanel((p) => p.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));
  const addRow = () => setPanel((p) => [...p, emptyRow()]);
  const removeRow = (i: number) =>
    setPanel((p) => (p.length > 1 ? p.filter((_, idx) => idx !== i) : p));

  const validate = (): string | null => {
    if (!form.materialId) return "Please select a Lyophilized Microorganism material.";
    if (!form.numberOfVialsPrepared || Number(form.numberOfVialsPrepared) <= 0)
      return "Please enter a valid number of vials prepared (> 0).";
    if (form.discsUsed === undefined || form.discsUsed === "" || Number(form.discsUsed) < 0)
      return "Please enter the number of discs used (0 or more).";
    if (!form.expiryDate) return "Please enter an expiry date.";
    if (!form.storageEquipmentId) return "Please select a Storage Equipment (Freezer / Deep Freezer).";
    if (panel.length === 0) return "At least one Identity Confirmation row is required.";
    for (let i = 0; i < panel.length; i++) {
      const r = panel[i];
      if (!r.mediaId) return `Row ${i + 1}: Please select a GPT-released media.`;
      if (!r.incubatorEquipmentId) return `Row ${i + 1}: Please select an incubator.`;
      if (!r.incubationStart) return `Row ${i + 1}: Please specify the incubation start date.`;
      if (!r.incubationEnd) return `Row ${i + 1}: Please specify the incubation end date.`;
    }
    return null;
  };

  const handleSave = async () => {
    const valError = validate();
    if (valError) {
      setError(valError);
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const storageConditionValue = selectedStorageEquipment
        ? `${selectedStorageEquipment.instrumentType} — ${selectedStorageEquipment.manufacturerName} (${selectedStorageEquipment.code})`
        : "";

      const payload: PrepareCryovialPayload = {
        materialId: Number(form.materialId),
        numberOfVialsPrepared: Number(form.numberOfVialsPrepared),
        expiryDate: form.expiryDate,
        storageCondition: storageConditionValue,
        physicalCheckText: form.physicalCheckText || "",
        discsUsed: Number(form.discsUsed),
        panel: panel.map((r) => ({
          mediaId: Number(r.mediaId),
          incubatorEquipmentId: Number(r.incubatorEquipmentId),
          incubationStart: r.incubationStart,
          incubationEnd: r.incubationEnd,
          observationText: r.observationText || ""
        }))
      };

      const result = await CryovialService.prepare(payload);
      const code = result?.code ? ` (${result.code})` : "";
      onSuccess(`Cryovial batch prepared successfully${code}.`);
      onClose();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not prepare cryovial batch.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={submitting ? undefined : onClose}
      maxWidth="md"
      fullWidth
      PaperProps={{
        sx: {
          borderRadius: 2,
          p: 0.5
        }
      }}
    >
      <DialogTitle sx={{ pb: 1, display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <Box
            sx={{
              width: 34,
              height: 34,
              borderRadius: 1.5,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              bgcolor: theme.custom.status.purple.bg,
              color: theme.palette.primary.main
            }}
          >
            <ScienceIcon fontSize="small" />
          </Box>
          <Typography sx={{ fontSize: 18, fontWeight: 700, color: "text.primary" }}>
            Prepare Cryovial Batch
          </Typography>
        </Box>
        <IconButton onClick={onClose} disabled={submitting} size="small">
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>

      <DialogContent dividers sx={{ pt: 2.5 }}>
        <Stack spacing={3}>
          {error && <Alert severity="error">{error}</Alert>}

          {/* SECTION 1: Source / Batch Information */}
          <Box>
            <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5, textTransform: "uppercase", letterSpacing: "0.5px" }}>
              Section 1: Source &amp; Batch Information
            </Typography>

            <Box sx={{ mb: 2 }}>
              <FormControl fullWidth size="small">
                <InputLabel id="source-material-label">Lyophilized Microorganism Stock (Inventory) *</InputLabel>
                <Select
                  labelId="source-material-label"
                  label="Lyophilized Microorganism Stock (Inventory) *"
                  value={form.materialId ?? ""}
                  onChange={(e) => setField("materialId", e.target.value)}
                >
                  <MenuItem value="">
                    <em>Select source material</em>
                  </MenuItem>
                  {usableMaterials.map((m) => (
                    <MenuItem key={m.id} value={m.id}>
                      {m.materialName} — batch {m.batchNumber} ({m.quantityRemaining} {m.unit} left)
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {selectedMaterial && (
                <Paper
                  variant="outlined"
                  sx={{
                    mt: 1,
                    p: 1.5,
                    bgcolor: "background.default",
                    borderColor: "divider",
                    borderRadius: 1
                  }}
                >
                  <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                    Organism:{" "}
                    <span style={{ color: theme.palette.primary.main, fontWeight: 700 }}>
                      {selectedMaterial.organism?.scientificName ?? "— (set an Organism on this Material first)"}
                    </span>
                    {selectedMaterial.organism?.atccNumber ? ` (ATCC ${selectedMaterial.organism.atccNumber})` : ""}
                    {" · "}
                    Manufacturer: {selectedMaterial.manufacturerName || "—"}
                  </Typography>
                </Paper>
              )}
            </Box>

            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: {
                  xs: "1fr",
                  sm: "repeat(2, 1fr)",
                  md: "repeat(3, 1fr)"
                },
                gap: 2
              }}
            >
              <TextField
                size="small"
                label="Vials Prepared *"
                type="number"
                placeholder="e.g. 15"
                value={form.numberOfVialsPrepared ?? ""}
                onChange={(e) => setField("numberOfVialsPrepared", e.target.value)}
                inputProps={{ min: 1 }}
                required
              />
              <TextField
                size="small"
                label="Discs Used *"
                type="number"
                placeholder="e.g. 1"
                value={form.discsUsed ?? ""}
                onChange={(e) => setField("discsUsed", e.target.value)}
                inputProps={{ min: 0 }}
                required
              />
              <TextField
                size="small"
                type="date"
                label="Expiry Date *"
                InputLabelProps={{ shrink: true }}
                value={form.expiryDate ?? ""}
                onChange={(e) => setField("expiryDate", e.target.value)}
                required
              />
              <FormControl size="small" required sx={{ gridColumn: { sm: "span 2", md: "span 2" } }}>
                <InputLabel id="storage-equipment-label">Storage Equipment *</InputLabel>
                <Select
                  labelId="storage-equipment-label"
                  label="Storage Equipment *"
                  value={form.storageEquipmentId ?? ""}
                  onChange={(e) => setField("storageEquipmentId", e.target.value)}
                  disabled={equipmentLoading || eligibleFreezers.length === 0}
                >
                  <MenuItem value="">
                    <em>{equipmentLoading ? "Loading equipment..." : "Select freezer / deep freezer..."}</em>
                  </MenuItem>
                  {eligibleFreezers.map((eq) => (
                    <MenuItem key={eq.id} value={eq.id}>
                      {eq.instrumentType} — {eq.manufacturerName} ({eq.code})
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <TextField
                size="small"
                label="Physical Check"
                placeholder="e.g. Clear, intact cryovials with color code"
                value={form.physicalCheckText ?? ""}
                onChange={(e) => setField("physicalCheckText", e.target.value)}
              />
            </Box>

            {eligibleFreezers.length === 0 && !equipmentLoading && (
              <Alert severity="warning" sx={{ mt: 1.5 }}>
                No available freezer/deep freezer is currently in service.
              </Alert>
            )}

            {selectedStorageEquipment && (
              <Paper
                variant="outlined"
                sx={{
                  mt: 1.5,
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
                      Storage Equipment
                    </Typography>
                    <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main }}>
                      {selectedStorageEquipment.instrumentType} — {selectedStorageEquipment.manufacturerName || "Asset"}
                    </Typography>
                  </Box>
                  <Box>
                    <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                      Code
                    </Typography>
                    <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                      {selectedStorageEquipment.code}
                    </Typography>
                  </Box>
                  <Box>
                    <Typography sx={{ fontSize: 10.5, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                      Location
                    </Typography>
                    <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                      {selectedStorageEquipment.location || "—"}
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
          </Box>

          <Divider />

          {/* SECTION 2: Identity Confirmation Panel */}
          <Box>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, textTransform: "uppercase", letterSpacing: "0.5px" }}>
                Section 2: Identity Confirmation Panel
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                Mandatory qualification before GPT reference
              </Typography>
            </Box>

            <Table size="small" sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, overflow: "hidden" }}>
              <TableHead sx={{ bgcolor: "background.default" }}>
                <TableRow>
                  <TableCell sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary" }}>Media (GPT-released) *</TableCell>
                  <TableCell sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary" }}>Incubator *</TableCell>
                  <TableCell sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary" }}>Start *</TableCell>
                  <TableCell sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary" }}>End *</TableCell>
                  <TableCell sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary" }}>Observation</TableCell>
                  <TableCell sx={{ width: 40 }}></TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {panel.map((row, i) => (
                  <TableRow key={i}>
                    <TableCell sx={{ py: 1 }}>
                      <Select
                        size="small"
                        fullWidth
                        displayEmpty
                        value={row.mediaId}
                        onChange={(e) => updateRow(i, "mediaId", e.target.value)}
                      >
                        <MenuItem value="">
                          <em>Select Media</em>
                        </MenuItem>
                        {releasedMedia.map((m) => (
                          <MenuItem key={m.id} value={m.id}>
                            {m.lotNumber} ({m.material?.materialName || m.mediaType?.class || "Media"})
                          </MenuItem>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <Select
                        size="small"
                        fullWidth
                        displayEmpty
                        value={row.incubatorEquipmentId}
                        onChange={(e) => updateRow(i, "incubatorEquipmentId", e.target.value)}
                      >
                        <MenuItem value="">
                          <em>Select Incubator</em>
                        </MenuItem>
                        {incubators.map((i2) => (
                          <MenuItem key={i2.id} value={i2.id}>
                            {i2.name} ({i2.code})
                          </MenuItem>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <TextField
                        size="small"
                        type="date"
                        value={row.incubationStart}
                        onChange={(e) => updateRow(i, "incubationStart", e.target.value)}
                        fullWidth
                      />
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <TextField
                        size="small"
                        type="date"
                        value={row.incubationEnd}
                        onChange={(e) => updateRow(i, "incubationEnd", e.target.value)}
                        fullWidth
                      />
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <TextField
                        size="small"
                        placeholder="Observation notes"
                        value={row.observationText}
                        onChange={(e) => updateRow(i, "observationText", e.target.value)}
                        fullWidth
                      />
                    </TableCell>
                    <TableCell sx={{ py: 1 }}>
                      <IconButton
                        size="small"
                        onClick={() => removeRow(i)}
                        disabled={panel.length <= 1}
                        sx={{ color: panel.length <= 1 ? "text.disabled" : "error.main" }}
                      >
                        <CloseIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <Button
              size="small"
              onClick={addRow}
              startIcon={<AddIcon fontSize="small" />}
              sx={{ mt: 1.5, color: theme.palette.primary.main }}
            >
              Add Media Row
            </Button>
          </Box>
        </Stack>
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting} sx={{ color: "text.secondary" }}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={submitting || eligibleFreezers.length === 0}
          sx={{
            bgcolor: brandColors.sectionTitle,
            "&:hover": { bgcolor: brandColors.pageTitle }
          }}
        >
          {submitting ? "Saving Batch..." : "Save Batch"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
