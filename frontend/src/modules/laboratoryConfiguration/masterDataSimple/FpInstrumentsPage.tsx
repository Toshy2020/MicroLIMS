import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import RefreshIcon from "@mui/icons-material/Refresh";
import { toast } from "sonner";
import { PageHeader } from "../../../components/PageHeader";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { tableHeadSx } from "../../../theme";
import { EquipmentConfigurationService } from "./services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import { getSections, LaboratorySection } from "../../../services/laboratorySectionService";
import { CDS_SOFTWARE_OPTIONS } from "./EquipmentPage";

// Finished Product (chemistry) instruments. The physical asset - code,
// serial, manufacturer, CDS version, calibration - lives in the Equipment
// Inventory; this page only adds what the chemistry workflows need: the
// instrument type and, for HPLC, the CDS software. These are the
// instruments offered on Chromatography Columns and System Suitability.
const FP_SECTION_CODE = "FP";

export const FP_INSTRUMENT_TYPES = [
  { value: "Hplc", label: "HPLC" },
  { value: "PhMeter", label: "pH Meter" },
  { value: "Balance", label: "Balance" }
];
const FP_TYPE_VALUES = FP_INSTRUMENT_TYPES.map((t) => t.value);

interface InventoryItem {
  id: number;
  code: string;
  instrumentType: string;
  manufacturerName: string | null;
  serialNumber: string | null;
  firmwareVersion: string | null;
  location: string | null;
  calibrationDueDate: string | null;
  status: string;
}

interface FpInstrument {
  id: number;
  name: string;
  code: string;
  type: string;
  location: string | null;
  vendor: string | null;
  cdsSoftware: string | null;
  calibrationDueDate: string | null;
  sectionId: number;
}

interface FormState {
  inventoryId: number | "";
  name: string;
  type: string;
  cdsSoftware: string;
}

const emptyForm: FormState = { inventoryId: "", name: "", type: "Hplc", cdsSoftware: "" };

const typeLabel = (type: string) => FP_INSTRUMENT_TYPES.find((t) => t.value === type)?.label ?? type;
const cdsLabel = (cds: string | null) => CDS_SOFTWARE_OPTIONS.find((c) => c.value === cds)?.label ?? cds ?? "—";
const formatDate = (d: string | null) => (d ? new Date(d).toLocaleDateString() : "—");

export function FpInstrumentsPage() {
  const theme = useTheme();
  const [fpSection, setFpSection] = useState<LaboratorySection | null>(null);
  const [instruments, setInstruments] = useState<FpInstrument[]>([]);
  const [inventory, setInventory] = useState<InventoryItem[]>([]);
  const [configuredCodes, setConfiguredCodes] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<FpInstrument | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [sections, equipment, inv] = await Promise.all([
        getSections(),
        EquipmentConfigurationService.getEquipmentList(),
        EquipmentInventoryService.getAll()
      ]);
      const fp = (sections ?? []).find((s: LaboratorySection) => s.sectionCode === FP_SECTION_CODE) ?? null;
      setFpSection(fp);
      setInstruments(
        (equipment as FpInstrument[]).filter((e) => FP_TYPE_VALUES.includes(String(e.type)) && (!fp || e.sectionId === fp.sectionId))
      );
      setConfiguredCodes(new Set((equipment as FpInstrument[]).map((e) => e.code.toLowerCase())));
      setInventory(Array.isArray(inv) ? inv : []);
      if (!fp) setError("The Finished Product laboratory section (code FP) was not found.");
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not load Finished Product instruments.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const inventoryByCode = useMemo(() => {
    const map = new Map<string, InventoryItem>();
    for (const i of inventory) map.set(i.code.toLowerCase(), i);
    return map;
  }, [inventory]);

  // An inventory asset can be configured once, in any section.
  const availableInventory = inventory.filter((i) => !configuredCodes.has(i.code.toLowerCase()));

  const selectedInventory = form.inventoryId === "" ? null : inventory.find((i) => i.id === form.inventoryId) ?? null;
  const shownInventory = editing ? inventoryByCode.get(editing.code.toLowerCase()) ?? null : selectedInventory;

  const openAdd = () => {
    setEditing(null);
    setForm(emptyForm);
    setDialogError(null);
    setDialogOpen(true);
  };

  const openEdit = (inst: FpInstrument) => {
    setEditing(inst);
    setForm({ inventoryId: "", name: inst.name, type: String(inst.type), cdsSoftware: inst.cdsSoftware ?? "" });
    setDialogError(null);
    setDialogOpen(true);
  };

  const pickInventory = (id: number) => {
    const inv = inventory.find((i) => i.id === id);
    setForm((f) => ({
      ...f,
      inventoryId: id,
      name: inv ? [inv.manufacturerName, typeLabel(f.type)].filter(Boolean).join(" ") : f.name
    }));
  };

  const save = async () => {
    if (!fpSection) return;
    if (!editing && !selectedInventory) {
      setDialogError("Select the instrument from the Equipment Inventory.");
      return;
    }
    if (!form.name.trim()) {
      setDialogError("Instrument name is required.");
      return;
    }
    if (form.type === "Hplc" && !form.cdsSoftware) {
      setDialogError("CDS software is required for an HPLC.");
      return;
    }

    const source = editing ? inventoryByCode.get(editing.code.toLowerCase()) ?? null : selectedInventory;
    const payload = {
      name: form.name.trim(),
      code: editing ? editing.code : selectedInventory!.code,
      type: form.type,
      location: source?.location ?? editing?.location ?? null,
      setPointTemperature: null,
      calibrationDueDate: source?.calibrationDueDate ?? editing?.calibrationDueDate ?? null,
      vendor: source?.manufacturerName ?? editing?.vendor ?? null,
      cdsSoftware: form.type === "Hplc" ? form.cdsSoftware : null,
      connectionSettings: null,
      sectionId: fpSection.sectionId
    };

    setSaving(true);
    setDialogError(null);
    try {
      if (editing) {
        await EquipmentConfigurationService.updateEquipment(editing.id, payload as never);
        toast.success("Instrument updated.");
      } else {
        await EquipmentConfigurationService.createEquipment(payload as never);
        toast.success("Instrument added to the Finished Product laboratory.");
      }
      setDialogOpen(false);
      await loadData();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string; error?: string } }; message?: string };
      setDialogError(err?.response?.data?.message ?? err?.response?.data?.error ?? err?.message ?? "Could not save the instrument.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <PageHeader
        title="FP Instruments"
        subtitle="Finished Product laboratory instruments (HPLC, pH meters, balances). Assets come from the Equipment Inventory."
      />

      {error && <Alert severity="error" sx={{ mb: 3 }}>{error}</Alert>}

      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack direction="row" spacing={1} sx={{ justifyContent: "space-between", alignItems: "center" }}>
          <Tooltip title="Refresh">
            <IconButton onClick={loadData} size="small"><RefreshIcon /></IconButton>
          </Tooltip>
          <Button variant="contained" startIcon={<AddIcon />} onClick={openAdd} disabled={!fpSection}
            sx={{ fontWeight: 600, textTransform: "none" }}>
            Add Instrument
          </Button>
        </Stack>
      </Paper>

      <TableContainer component={Paper} elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell sx={{ fontWeight: 600 }}>Code</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Instrument</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Type</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Manufacturer</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Serial No.</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>CDS Software</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Location</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Calibration Due</TableCell>
              <TableCell sx={{ fontWeight: 600 }} align="center">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow><TableCell colSpan={9} align="center" sx={{ py: 4 }}><CircularProgress size={24} /></TableCell></TableRow>
            ) : instruments.length === 0 ? (
              <TableRow>
                <TableCell colSpan={9} align="center" sx={{ py: 4, color: "text.secondary" }}>
                  No Finished Product instruments yet. Add one from the Equipment Inventory.
                </TableCell>
              </TableRow>
            ) : (
              instruments.map((inst) => {
                const inv = inventoryByCode.get(inst.code.toLowerCase());
                return (
                  <TableRow key={inst.id} hover>
                    <TableCell sx={{ fontWeight: 700 }}>{inst.code}</TableCell>
                    <TableCell>{inst.name}</TableCell>
                    <TableCell><Chip size="small" label={typeLabel(String(inst.type))} /></TableCell>
                    <TableCell>{inv?.manufacturerName ?? inst.vendor ?? "—"}</TableCell>
                    <TableCell>{inv?.serialNumber ?? "—"}</TableCell>
                    <TableCell>
                      {inst.type === "Hplc" ? (
                        <>
                          <Typography sx={{ fontSize: 13 }}>{cdsLabel(inst.cdsSoftware)}</Typography>
                          {inv?.firmwareVersion && (
                            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{inv.firmwareVersion}</Typography>
                          )}
                        </>
                      ) : "—"}
                    </TableCell>
                    <TableCell>{inv?.location ?? inst.location ?? "—"}</TableCell>
                    <TableCell>{formatDate(inv?.calibrationDueDate ?? inst.calibrationDueDate)}</TableCell>
                    <TableCell align="center">
                      <Tooltip title="Edit">
                        <IconButton size="small" onClick={() => openEdit(inst)}><EditIcon fontSize="small" /></IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <FloatingDialog
        open={dialogOpen}
        title={editing ? `Edit Instrument: ${editing.code}` : "Add Finished Product Instrument"}
        onClose={() => setDialogOpen(false)}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} disabled={saving} sx={{ textTransform: "none" }}>Cancel</Button>
            <Button variant="contained" onClick={save} disabled={saving} sx={{ textTransform: "none" }}>
              {saving ? "Saving…" : "Save"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          {dialogError && <Alert severity="error">{dialogError}</Alert>}

          {!editing && (
            <FormControl fullWidth size="small" required>
              <InputLabel id="fp-inventory-label">Equipment Inventory asset</InputLabel>
              <Select<number | "">
                labelId="fp-inventory-label"
                label="Equipment Inventory asset"
                value={form.inventoryId}
                onChange={(e) => e.target.value !== "" && pickInventory(Number(e.target.value))}
              >
                {availableInventory.length === 0 && <MenuItem value="" disabled>No unconfigured assets in the inventory</MenuItem>}
                {availableInventory.map((i) => (
                  <MenuItem key={i.id} value={i.id}>
                    {i.code} — {i.instrumentType}{i.manufacturerName ? ` (${i.manufacturerName})` : ""}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {shownInventory && (
            <Paper variant="outlined" sx={{ p: 1.5 }}>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>From Equipment Inventory</Typography>
              <Typography sx={{ fontSize: 13 }}>
                {shownInventory.manufacturerName ?? "—"} · S/N {shownInventory.serialNumber ?? "—"} · {shownInventory.location ?? "—"}
              </Typography>
              <Typography sx={{ fontSize: 13 }}>
                Software/firmware: {shownInventory.firmwareVersion ?? "—"} · Calibration due {formatDate(shownInventory.calibrationDueDate)}
              </Typography>
            </Paper>
          )}

          <FormControl fullWidth size="small" required>
            <InputLabel id="fp-type-label">Instrument type</InputLabel>
            <Select
              labelId="fp-type-label"
              label="Instrument type"
              value={form.type}
              onChange={(e) => setForm((f) => ({ ...f, type: e.target.value, cdsSoftware: e.target.value === "Hplc" ? f.cdsSoftware : "" }))}
            >
              {FP_INSTRUMENT_TYPES.map((t) => <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>)}
            </Select>
          </FormControl>

          {form.type === "Hplc" && (
            <FormControl fullWidth size="small" required>
              <InputLabel id="fp-cds-label">CDS software</InputLabel>
              <Select
                labelId="fp-cds-label"
                label="CDS software"
                value={form.cdsSoftware}
                onChange={(e) => setForm((f) => ({ ...f, cdsSoftware: e.target.value }))}
              >
                {CDS_SOFTWARE_OPTIONS.map((c) => <MenuItem key={c.value} value={c.value}>{c.label}</MenuItem>)}
              </Select>
            </FormControl>
          )}

          <TextField
            size="small"
            label="Instrument name"
            required
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
            helperText="As it should appear on suitability runs, e.g. Agilent HPLC 1"
            slotProps={{ htmlInput: { maxLength: 100 } }}
          />
        </Stack>
      </FloatingDialog>
    </Box>
  );
}
