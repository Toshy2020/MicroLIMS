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
import { formatLabDate } from "../../../utils/formatDate";
import { useAuth } from "../../../contexts/AuthContext";
import { EquipmentInventoryService } from "./services/EquipmentInventoryService";

const STATUSES = ["InService", "OutOfService", "Retired"];

// Equipment register under Inventory (Microbiology lab) - mirrors the
// paper/Excel "List of instruments & equipment in QC laboratories",
// scoped to Microbiology only per Mohamed's confirmed decision.
export function EquipmentInventoryPage() {
  const { role } = useAuth();
  const canSeeHistory = role === "SectionHead" || role === "SystemAdministrator";

  const [list, setList] = useState<any[]>([]);
  const [printList, setPrintList] = useState<any[]>([]);
  const [form, setForm] = useState<Record<string, any>>({ status: "InService" });
  const [editingId, setEditingId] = useState<number | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [historyFor, setHistoryFor] = useState<number | null>(null);

  const load = () => {
    EquipmentInventoryService.getAll().then(setList);
    EquipmentInventoryService.getForPrint().then(setPrintList);
  };
  useEffect(() => { load(); }, []);

  const startEdit = (row: any) => {
    setEditingId(row.id);
    setForm({
      instrumentType: row.instrumentType, manufacturerName: row.manufacturerName ?? "", serialNumber: row.serialNumber ?? "",
      firmwareVersion: row.firmwareVersion ?? "", code: row.code, location: row.location,
      calibrationDueDate: row.calibrationDueDate?.slice(0, 10) ?? "", status: row.status
    });
  };

  const cancelEdit = () => { setEditingId(null); setForm({ status: "InService" }); };

  const save = async () => {
    setMessage(null);
    if (!form.instrumentType || !form.code || !form.location) {
      setMessage({ text: "Instrument type, code, and location are required.", ok: false });
      return;
    }
    const payload = {
      instrumentType: form.instrumentType, manufacturerName: form.manufacturerName ?? "",
      serialNumber: form.serialNumber || null, firmwareVersion: form.firmwareVersion || null,
      code: form.code, location: form.location, calibrationDueDate: form.calibrationDueDate || null,
      status: form.status
    };
    try {
      if (editingId) {
        await EquipmentInventoryService.update(editingId, payload);
        setMessage({ text: "Equipment updated.", ok: true });
      } else {
        await EquipmentInventoryService.create(payload);
        setMessage({ text: "Equipment added.", ok: true });
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save this equipment.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Equipment" subtitle="QC/Microbiology lab instrument register — serial number, firmware, and calibration due date." />
      {message && <Alert className="no-print" severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <Box className="no-print">
        <SectionTitle>{editingId ? "Edit Equipment" : "Register Equipment"}</SectionTitle>
        <Paper sx={{ p: 2.5, mb: 3 }}>
          <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", gap: 2 }}>
            <TextField size="small" label="Instrument Type" placeholder="e.g. Incubator, Pipette" value={form.instrumentType ?? ""} onChange={(e) => setForm({ ...form, instrumentType: e.target.value })} />
            <TextField size="small" label="Manufacturer" value={form.manufacturerName ?? ""} onChange={(e) => setForm({ ...form, manufacturerName: e.target.value })} />
            <TextField size="small" label="Serial No." value={form.serialNumber ?? ""} onChange={(e) => setForm({ ...form, serialNumber: e.target.value })} />
            <TextField size="small" label="Firmware Version" value={form.firmwareVersion ?? ""} onChange={(e) => setForm({ ...form, firmwareVersion: e.target.value })} />
            <TextField size="small" label="Code" value={form.code ?? ""} onChange={(e) => setForm({ ...form, code: e.target.value })} />
            <TextField size="small" label="Location" value={form.location ?? ""} onChange={(e) => setForm({ ...form, location: e.target.value })} />
            <TextField size="small" type="date" label="Calibration Due" InputLabelProps={{ shrink: true }} value={form.calibrationDueDate ?? ""} onChange={(e) => setForm({ ...form, calibrationDueDate: e.target.value })} />
            <Select size="small" value={form.status ?? "InService"} onChange={(e) => setForm({ ...form, status: e.target.value })}>
              {STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
            </Select>
          </Box>
          <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1, mt: 2 }}>
            {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
            <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Add Equipment"}</Button>
          </Box>
        </Paper>

        <SectionTitle>Equipment Register</SectionTitle>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 1 }}><PrintButton label="Print (excludes out-of-service / retired)" /></Box>
        <Paper sx={{ p: 2.5 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Type</TableCell><TableCell>Manufacturer</TableCell><TableCell>Serial No.</TableCell><TableCell>Firmware</TableCell>
                <TableCell>Code</TableCell><TableCell>Location</TableCell><TableCell>Calibration Due</TableCell><TableCell>Status</TableCell><TableCell></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {list.map((eq) => (
                <TableRow key={eq.id}>
                  <TableCell>{eq.instrumentType}</TableCell><TableCell>{eq.manufacturerName || "—"}</TableCell>
                  <TableCell>{eq.serialNumber || "—"}</TableCell><TableCell>{eq.firmwareVersion || "—"}</TableCell>
                  <TableCell>{eq.code}</TableCell><TableCell>{eq.location}</TableCell>
                  <TableCell>
                    {eq.calibrationDueDate ? formatLabDate(eq.calibrationDueDate) : "—"}
                    {eq.isCalibrationOverdue && <StatusBadge status="Overdue" />}
                  </TableCell>
                  <TableCell><StatusBadge status={eq.status} /></TableCell>
                  <TableCell>
                    <IconButton size="small" onClick={() => startEdit(eq)}><EditIcon fontSize="small" /></IconButton>
                    {canSeeHistory && <IconButton size="small" onClick={() => setHistoryFor(eq.id)}><HistoryIcon fontSize="small" /></IconButton>}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      </Box>

      <PrintableTable
        title="Equipment Register — Microbiology Lab"
        subtitle="Out-of-service and retired instruments are excluded from this list."
        rows={printList}
        getRowId={(eq) => eq.id}
        columns={[
          { label: "Type", render: (eq) => eq.instrumentType },
          { label: "Manufacturer", render: (eq) => eq.manufacturerName || "—" },
          { label: "Serial No.", render: (eq) => eq.serialNumber || "—" },
          { label: "Firmware", render: (eq) => eq.firmwareVersion || "—" },
          { label: "Code", render: (eq) => eq.code },
          { label: "Location", render: (eq) => eq.location },
          { label: "Calibration Due", render: (eq) => (eq.calibrationDueDate ? formatLabDate(eq.calibrationDueDate) : "—") }
        ]}
      />

      <AuditHistoryDialog open={historyFor != null} onClose={() => setHistoryFor(null)} entityName="EquipmentInventory" entityId={historyFor} />
    </>
  );
}
