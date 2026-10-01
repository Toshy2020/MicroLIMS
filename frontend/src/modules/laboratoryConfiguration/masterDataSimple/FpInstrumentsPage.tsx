import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Button,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  TextField,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
import { LabPage, FilterBar, RegisterTable, RegisterColumn, FormDialog } from "../../../components/lab";
import { StatusBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
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
  { value: "IcpOes", label: "ICP-OES" },
  { value: "PhMeter", label: "pH Meter" },
  { value: "Balance", label: "Balance" },
  { value: "UvVis", label: "UV-Vis" },
  { value: "DissolutionTester", label: "Dissolution tester" },
  { value: "DisintegrationTester", label: "Disintegration tester" },
  { value: "KarlFischer", label: "Karl Fischer" },
  { value: "Titrator", label: "Titrator" },
  { value: "Viscometer", label: "Viscometer" },
  { value: "Refractometer", label: "Refractometer" },
  { value: "Polarimeter", label: "Polarimeter" },
  { value: "ConductivityMeter", label: "Conductivity meter" },
  { value: "MeltingPoint", label: "Melting point" },
  { value: "Oven", label: "Oven" },
  { value: "Furnace", label: "Furnace" },
  { value: "Gc", label: "GC" },
  { value: "Aas", label: "AAS" },
  { value: "DigestionMicrowave", label: "Digestion microwave" },
  { value: "Caliper", label: "Caliper" }
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
  // Row version, sent back as If-Match when this record is edited.
  version?: number;
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

// Infers the FP instrument type from the Equipment Inventory asset's own
// (free-text) instrument-type field, so picking e.g. an "AAS" asset doesn't
// default the type/name to HPLC. Falls back to keeping the current type when
// nothing recognizable is found.
const inferTypeFromInventoryText = (text: string | null | undefined): string | null => {
  if (!text) return null;
  const t = text.toLowerCase();
  if (t.includes("aas") || t.includes("atomic absorption")) return "Aas";
  if (t.includes("icp")) return "IcpOes";
  if (t.includes("hplc")) return "Hplc";
  if (t.includes("ph")) return "PhMeter";
  if (t.includes("balance")) return "Balance";
  return null;
};

export function FpInstrumentsPage() {
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
  // Client-side validation errors, shown on the field instead of the top alert.
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<"inventory" | "name" | "cds", string>>>({});
  const [search, setSearch] = useState("");
  const [saving, setSaving] = useState(false);
  // Tracks whether form.name is still the auto-suggested "<manufacturer> <type>"
  // value (so a later type change can refresh it) versus text the user typed.
  const [autoName, setAutoName] = useState(true);

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
      if (!fp) setError("The Physicochemical laboratory section (code FP) was not found.");
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setError(err?.response?.data?.message ?? "Could not load Physicochemical instruments.");
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
    setFieldErrors({});
    setAutoName(true);
    setDialogOpen(true);
  };

  const openEdit = (inst: FpInstrument) => {
    setEditing(inst);
    setForm({ inventoryId: "", name: inst.name, type: String(inst.type), cdsSoftware: inst.cdsSoftware ?? "" });
    setDialogError(null);
    setFieldErrors({});
    setAutoName(false);
    setDialogOpen(true);
  };

  const pickInventory = (id: number) => {
    const inv = inventory.find((i) => i.id === id);
    setForm((f) => {
      const inferredType = inferTypeFromInventoryText(inv?.instrumentType) ?? f.type;
      const needsCds = inferredType === "Hplc" || inferredType === "IcpOes";
      return {
        ...f,
        inventoryId: id,
        type: inferredType,
        cdsSoftware: needsCds ? f.cdsSoftware : "",
        name: inv ? [inv.manufacturerName, typeLabel(inferredType)].filter(Boolean).join(" ") : f.name
      };
    });
    setAutoName(true);
    setFieldErrors((e) => ({ ...e, inventory: undefined }));
  };

  const save = async () => {
    if (!fpSection) return;
    const errors: typeof fieldErrors = {};
    if (!editing && !selectedInventory) errors.inventory = "Select the instrument from the Equipment Inventory.";
    if (!form.name.trim()) errors.name = "Instrument name is required.";
    if ((form.type === "Hplc" || form.type === "IcpOes") && !form.cdsSoftware) {
      errors.cds = `CDS software is required for an ${form.type === "Hplc" ? "HPLC" : "ICP-OES"}.`;
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    const source = editing ? inventoryByCode.get(editing.code.toLowerCase()) ?? null : selectedInventory;
    const payload = {
      name: form.name.trim(),
      code: editing ? editing.code : selectedInventory!.code,
      type: form.type,
      location: source?.location ?? editing?.location ?? null,
      setPointTemperature: null,
      calibrationDueDate: source?.calibrationDueDate ?? editing?.calibrationDueDate ?? null,
      vendor: source?.manufacturerName ?? editing?.vendor ?? null,
      cdsSoftware: (form.type === "Hplc" || form.type === "IcpOes") ? form.cdsSoftware : null,
      connectionSettings: null,
      sectionId: fpSection.sectionId
    };

    setSaving(true);
    setDialogError(null);
    try {
      if (editing) {
        await EquipmentConfigurationService.updateEquipment(editing.id, payload as never, editing.version);
        toast.success("Instrument updated.");
      } else {
        await EquipmentConfigurationService.createEquipment(payload as never);
        toast.success("Instrument added to the Physicochemical laboratory.");
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

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return instruments;
    return instruments.filter((inst) => {
      const inv = inventoryByCode.get(inst.code.toLowerCase());
      return [inst.code, inst.name, typeLabel(String(inst.type)), inv?.manufacturerName ?? inst.vendor, inv?.serialNumber, inv?.location ?? inst.location]
        .some((v) => v?.toLowerCase().includes(q));
    });
  }, [instruments, inventoryByCode, search]);

  const addButton = (
    <Button variant="contained" startIcon={<AddIcon />} onClick={openAdd} disabled={!fpSection} sx={{ fontWeight: 600, textTransform: "none" }}>
      Add Instrument
    </Button>
  );

  const columns: RegisterColumn<FpInstrument>[] = [
    {
      key: "code",
      label: "Code",
      sortable: true,
      render: (inst) => <Typography component="span" sx={{ fontWeight: 700, fontFamily: monospaceFontFamily, fontSize: 13 }}>{inst.code}</Typography>
    },
    { key: "name", label: "Instrument", sortable: true },
    {
      key: "type",
      label: "Type",
      sortable: true,
      sortValue: (inst) => typeLabel(String(inst.type)),
      render: (inst) => <StatusBadge status={String(inst.type)} label={typeLabel(String(inst.type))} />
    },
    {
      key: "manufacturer",
      label: "Manufacturer",
      sortable: true,
      sortValue: (inst) => inventoryByCode.get(inst.code.toLowerCase())?.manufacturerName ?? inst.vendor,
      render: (inst) => inventoryByCode.get(inst.code.toLowerCase())?.manufacturerName ?? inst.vendor ?? "—"
    },
    {
      key: "serial",
      label: "Serial No.",
      render: (inst) => inventoryByCode.get(inst.code.toLowerCase())?.serialNumber ?? "—"
    },
    {
      key: "cdsSoftware",
      label: "CDS Software",
      render: (inst) => {
        const inv = inventoryByCode.get(inst.code.toLowerCase());
        return inst.type === "Hplc" || inst.type === "IcpOes" ? (
          <>
            <Typography sx={{ fontSize: 13 }}>{cdsLabel(inst.cdsSoftware)}</Typography>
            {inv?.firmwareVersion && <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{inv.firmwareVersion}</Typography>}
          </>
        ) : "—";
      }
    },
    {
      key: "location",
      label: "Location",
      sortable: true,
      sortValue: (inst) => inventoryByCode.get(inst.code.toLowerCase())?.location ?? inst.location,
      render: (inst) => inventoryByCode.get(inst.code.toLowerCase())?.location ?? inst.location ?? "—"
    },
    {
      key: "calibrationDueDate",
      label: "Calibration Due",
      sortable: true,
      sortValue: (inst) => inventoryByCode.get(inst.code.toLowerCase())?.calibrationDueDate ?? inst.calibrationDueDate,
      render: (inst) => formatDate(inventoryByCode.get(inst.code.toLowerCase())?.calibrationDueDate ?? inst.calibrationDueDate)
    }
  ];

  return (
    <LabPage
      title="Physicochemical Instruments"
      subtitle="Physicochemical laboratory instruments (HPLC, ICP-OES, pH meters, balances). Assets come from the Equipment Inventory."
      actions={addButton}
      filters={
        <FilterBar
          search={search}
          onSearch={setSearch}
          placeholder="Search code, instrument, manufacturer or serial"
          resultCount={filtered.length}
          onRefresh={loadData}
          refreshing={loading}
        />
      }
    >
      {error && <Alert severity="error">{error}</Alert>}

      <RegisterTable
        columns={columns}
        rows={filtered}
        getRowId={(inst) => inst.id}
        loading={loading}
        onRowClick={openEdit}
        rowActions={(inst) => [{ label: "Edit", onClick: () => openEdit(inst) }]}
        empty={
          search
            ? { title: "No matching instruments", description: "Try a different search term." }
            : { title: "No Physicochemical instruments yet", description: "Add one from the Equipment Inventory.", action: addButton }
        }
      />

      <FormDialog
        open={dialogOpen}
        title={editing ? `Edit Instrument: ${editing.code}` : "Add Physicochemical Instrument"}
        onClose={() => setDialogOpen(false)}
        onSubmit={save}
        submitLabel={saving ? "Saving…" : "Save"}
        submitting={saving}
        error={dialogError}
      >
        {!editing && (
          <FormControl fullWidth size="small" required error={!!fieldErrors.inventory}>
            <InputLabel id="fp-inventory-label">Equipment Inventory asset</InputLabel>
            <Select<number | "">
              labelId="fp-inventory-label"
              label="Equipment Inventory asset"
              value={form.inventoryId}
              onChange={(e) => e.target.value !== "" && pickInventory(Number(e.target.value))}
              inputProps={{ "aria-label": "Equipment Inventory asset" }}
            >
              {availableInventory.length === 0 && <MenuItem value="" disabled>No unconfigured assets in the inventory</MenuItem>}
              {availableInventory.map((i) => (
                <MenuItem key={i.id} value={i.id}>
                  {i.code} — {i.instrumentType}{i.manufacturerName ? ` (${i.manufacturerName})` : ""}
                </MenuItem>
              ))}
            </Select>
            {fieldErrors.inventory && <FormHelperText>{fieldErrors.inventory}</FormHelperText>}
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
            onChange={(e) => {
              const newType = e.target.value;
              setFieldErrors((fe) => ({ ...fe, cds: undefined }));
              setForm((f) => {
                const needsCds = newType === "Hplc" || newType === "IcpOes";
                const name = autoName && shownInventory
                  ? [shownInventory.manufacturerName, typeLabel(newType)].filter(Boolean).join(" ")
                  : f.name;
                return { ...f, type: newType, cdsSoftware: needsCds ? f.cdsSoftware : "", name };
              });
            }}
          >
            {FP_INSTRUMENT_TYPES.map((t) => <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>)}
          </Select>
        </FormControl>

        {(form.type === "Hplc" || form.type === "IcpOes") && (
          <FormControl fullWidth size="small" required error={!!fieldErrors.cds}>
            <InputLabel id="fp-cds-label">CDS software</InputLabel>
            <Select
              labelId="fp-cds-label"
              label="CDS software"
              value={form.cdsSoftware}
              onChange={(e) => {
                setFieldErrors((fe) => ({ ...fe, cds: undefined }));
                setForm((f) => ({ ...f, cdsSoftware: e.target.value }));
              }}
            >
              {CDS_SOFTWARE_OPTIONS.map((c) => <MenuItem key={c.value} value={c.value}>{c.label}</MenuItem>)}
            </Select>
            {fieldErrors.cds && <FormHelperText>{fieldErrors.cds}</FormHelperText>}
          </FormControl>
        )}

        <TextField
          size="small"
          label="Instrument name"
          required
          value={form.name}
          error={!!fieldErrors.name}
          onChange={(e) => {
            setAutoName(false);
            setFieldErrors((fe) => ({ ...fe, name: undefined }));
            setForm((f) => ({ ...f, name: e.target.value }));
          }}
          helperText={fieldErrors.name ?? "As it should appear on suitability runs, e.g. Agilent HPLC 1"}
          slotProps={{ htmlInput: { maxLength: 100 } }}
        />
      </FormDialog>
    </LabPage>
  );
}
