import { useEffect, useState } from "react";
import { Paper, Box, TextField, Select, MenuItem, Button, Table, TableHead, TableRow, TableCell, TableBody, Alert, IconButton } from "@mui/material";
import HistoryIcon from "@mui/icons-material/History";
import EditIcon from "@mui/icons-material/Edit";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { AuditHistoryDialog } from "../../../components/AuditHistoryDialog";
import { OrganismPicker } from "../../../components/OrganismPicker";
import { formatLabDate } from "../../../utils/formatDate";
import { useAuth } from "../../../contexts/AuthContext";
import { MaterialService } from "./services/MaterialService";

const MATERIAL_TYPES = [
  "DehydratedMedia", "LyophilizedMicroorganism", "Supplement", "AntibioticDisc", "IdentificationKit",
  "IdentificationReagent", "Chemical", "Indicator", "ReferenceBuffer", "DisposableTool", "Other"
];
const UNITS = ["Gram", "Kilogram", "Milliliter", "Liter", "Disc", "Vial", "Kit", "Piece", "Bottle", "Pack"];

// Materials Stock register under Inventory - mirrors the paper/Excel
// "List of materials in Microbiology Lab". Cryovial batches (see the
// Cryovials module) are prepared directly from LyophilizedMicroorganism
// rows here, same as Media Preparation consumes DehydratedMedia rows.
//
// QuantityReceived is fixed at receiving; QuantityRemaining is the live
// balance that MediaPreparationService.PrepareAsync / CryovialService.
// PrepareCryovialsAsync decrement whenever a lot is prepared from a row
// here - it is never edited directly from this screen.
export function MaterialsPage() {
  const { role } = useAuth();
  const canSeeHistory = role === "SectionHead" || role === "SystemAdministrator";

  const [list, setList] = useState<any[]>([]);
  const [printList, setPrintList] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({ materialType: "DehydratedMedia", unit: "Gram" });
  const [editingId, setEditingId] = useState<number | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [historyFor, setHistoryFor] = useState<number | null>(null);

  const load = () => {
    MaterialService.getAll().then(setList);
    MaterialService.getForPrint().then(setPrintList);
  };
  useEffect(() => { load(); }, []);

  const onMaterialTypeChange = async (materialType: string) => {
    const unit = await MaterialService.getDefaultUnit(materialType);
    setForm((f) => ({ ...f, materialType, unit }));
  };

  const startEdit = (row: any) => {
    setEditingId(row.id);
    setForm({
      materialType: row.materialType, materialName: row.materialName, manufacturerName: row.manufacturerName,
      batchNumber: row.batchNumber, receivingDate: row.receivingDate?.slice(0, 10), expiryDate: row.expiryDate?.slice(0, 10) ?? "",
      code: row.code ?? "", location: row.location, quantityReceived: row.quantityReceived, unit: row.unit,
      minimumStockLevel: row.minimumStockLevel ?? "", atccNumber: row.atccNumber ?? "", organismId: row.organismId ?? null
    });
  };

  const cancelEdit = () => { setEditingId(null); setForm({ materialType: "DehydratedMedia", unit: "Gram" }); };

  const save = async () => {
    setMessage(null);
    if (!form.materialName || !form.batchNumber || !form.receivingDate || !form.location || form.quantityReceived == null || form.quantityReceived === "") {
      setMessage({ text: "Material name, batch/lot number, receiving date, location, and quantity received are required.", ok: false });
      return;
    }
    const payload = {
      materialType: form.materialType, materialName: form.materialName, manufacturerName: form.manufacturerName ?? "",
      batchNumber: form.batchNumber, receivingDate: form.receivingDate, expiryDate: form.expiryDate || null,
      code: form.code || null, location: form.location, quantityReceived: Number(form.quantityReceived),
      unit: form.unit, minimumStockLevel: form.minimumStockLevel === "" ? null : Number(form.minimumStockLevel),
      atccNumber: form.materialType === "LyophilizedMicroorganism" ? (form.atccNumber || null) : null,
      organismId: form.materialType === "LyophilizedMicroorganism" ? (form.organismId || null) : null
    };
    try {
      if (editingId) {
        await MaterialService.update(editingId, payload);
        setMessage({ text: "Material stock updated.", ok: true });
      } else {
        await MaterialService.create(payload);
        setMessage({ text: "Material added to stock.", ok: true });
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this item.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Materials Stock" subtitle="Media, discs, kits, reagents, chemicals, disposables — receiving, expiry, and quantity received/remaining." />
      {message && <Alert className="no-print" severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Box className="no-print">
        <SectionTitle>{editingId ? "Edit Material" : "Receive Material"}</SectionTitle>
        <Paper sx={{ p: 2.5, mb: 3 }}>
          <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2 }}>
            <Select size="small" value={form.materialType} onChange={(e) => onMaterialTypeChange(e.target.value)}>
              {MATERIAL_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
            </Select>
            <TextField size="small" label="Material Name" value={form.materialName ?? ""} onChange={(e) => setForm({ ...form, materialName: e.target.value })} />
            <TextField size="small" label="Manufacturer" value={form.manufacturerName ?? ""} onChange={(e) => setForm({ ...form, manufacturerName: e.target.value })} />
            <TextField size="small" label="Batch / Lot No." value={form.batchNumber ?? ""} onChange={(e) => setForm({ ...form, batchNumber: e.target.value })} />
            <TextField size="small" type="date" label="Receiving Date" InputLabelProps={{ shrink: true }} value={form.receivingDate ?? ""} onChange={(e) => setForm({ ...form, receivingDate: e.target.value })} />
            <TextField size="small" type="date" label="Expiry Date" InputLabelProps={{ shrink: true }} value={form.expiryDate ?? ""} onChange={(e) => setForm({ ...form, expiryDate: e.target.value })} />
            <TextField size="small" label="Code" value={form.code ?? ""} onChange={(e) => setForm({ ...form, code: e.target.value })} />
            <TextField size="small" label="Location" value={form.location ?? ""} onChange={(e) => setForm({ ...form, location: e.target.value })} />
            {form.materialType === "LyophilizedMicroorganism" && (
              <>
                <OrganismPicker value={form.organismId ?? null} onChange={(id) => setForm({ ...form, organismId: id })} />
                <TextField size="small" label="ATCC No." value={form.atccNumber ?? ""} onChange={(e) => setForm({ ...form, atccNumber: e.target.value })} />
              </>
            )}
            <TextField size="small" type="number" label={editingId ? "Quantity Received" : "Quantity Received"} value={form.quantityReceived ?? ""} onChange={(e) => setForm({ ...form, quantityReceived: e.target.value })} />
            <Select size="small" value={form.unit ?? "Gram"} onChange={(e) => setForm({ ...form, unit: e.target.value })}>
              {UNITS.map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
            </Select>
            <TextField size="small" type="number" label="Min. Stock Level (optional)" value={form.minimumStockLevel ?? ""} onChange={(e) => setForm({ ...form, minimumStockLevel: e.target.value })} />
          </Box>
          {editingId && (
            <Box sx={{ mt: 1 }}>
              <em style={{ fontSize: 12, color: "#6b7280" }}>
                Changing Quantity Received adjusts Quantity Remaining by the same amount (a receiving correction) —
                it does not reset consumption already recorded by Media Preparation.
              </em>
            </Box>
          )}
          <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1, mt: 2 }}>
            {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
            <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add to Stock"}</Button>
          </Box>
        </Paper>

        <SectionTitle>Materials in Stock</SectionTitle>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 1 }}><PrintButton label="Print (excludes expired / depleted)" /></Box>
        <Paper sx={{ p: 2.5 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Type</TableCell><TableCell>Name</TableCell><TableCell>Manufacturer</TableCell><TableCell>Batch/Lot</TableCell>
                <TableCell>Received</TableCell><TableCell>Expiry</TableCell><TableCell>Code</TableCell><TableCell>Organism</TableCell><TableCell>ATCC</TableCell><TableCell>Location</TableCell>
                <TableCell>Qty Received</TableCell><TableCell>Qty Remaining</TableCell><TableCell>Status</TableCell><TableCell></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {list.map((m) => (
                <TableRow key={m.id}>
                  <TableCell>{m.materialType}</TableCell><TableCell>{m.materialName}</TableCell><TableCell>{m.manufacturerName}</TableCell>
                  <TableCell>{m.batchNumber}</TableCell><TableCell>{formatLabDate(m.receivingDate)}</TableCell>
                  <TableCell>{m.expiryDate ? formatLabDate(m.expiryDate) : "—"}</TableCell>
                  <TableCell>{m.code ?? "—"}</TableCell><TableCell>{m.organism?.scientificName ?? "—"}</TableCell><TableCell>{m.atccNumber ?? "—"}</TableCell><TableCell>{m.location}</TableCell>
                  <TableCell>{m.quantityReceived} {m.unit}</TableCell>
                  <TableCell>{m.quantityRemaining} {m.unit}</TableCell>
                  <TableCell><StatusBadge status={m.status} /></TableCell>
                  <TableCell>
                    <IconButton size="small" onClick={() => startEdit(m)}><EditIcon fontSize="small" /></IconButton>
                    {canSeeHistory && <IconButton size="small" onClick={() => setHistoryFor(m.id)}><HistoryIcon fontSize="small" /></IconButton>}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      </Box>

      <PrintableTable
        title="Materials in Stock — Microbiology Lab"
        subtitle="Expired and depleted items are excluded from this list."
        rows={printList}
        getRowId={(m) => m.id}
        columns={[
          { label: "Type", render: (m) => m.materialType },
          { label: "Name", render: (m) => m.materialName },
          { label: "Manufacturer", render: (m) => m.manufacturerName },
          { label: "Batch/Lot", render: (m) => m.batchNumber },
          { label: "Received", render: (m) => formatLabDate(m.receivingDate) },
          { label: "Expiry", render: (m) => (m.expiryDate ? formatLabDate(m.expiryDate) : "—") },
          { label: "Code", render: (m) => m.code ?? "—" },
          { label: "Location", render: (m) => m.location },
          { label: "Qty Remaining", render: (m) => `${m.quantityRemaining} ${m.unit}` }
        ]}
      />

      <AuditHistoryDialog open={historyFor != null} onClose={() => setHistoryFor(null)} entityName="Material" entityId={historyFor} />
    </>
  );
}
