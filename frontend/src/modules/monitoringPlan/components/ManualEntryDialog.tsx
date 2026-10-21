import { useEffect, useState } from "react";
import {
  Alert, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Stack, TextField, Typography
} from "@mui/material";
import { apiClient } from "../../../services/apiClient";
import { MonitoringPlanService } from "../services/MonitoringPlanService";

interface Config { id: number; testType: string; testCode: string }
interface Part { id: number; name: string; configs: Config[] }
interface Machine { id: number; name: string; parts?: { id: number; name: string }[] }

// An After Cleaning occurrence is entered by hand when a machine is cleaned:
// pick the machine, when it is due (a day, any day in a week, or any day in
// a month) and the parts to be tested.
export function ManualEntryDialog({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: () => void }) {
  const [machines, setMachines] = useState<Machine[]>([]);
  const [machineId, setMachineId] = useState<number | "">("");
  const [parts, setParts] = useState<Part[]>([]);
  const [checked, setChecked] = useState<Record<number, boolean>>({});
  const [kind, setKind] = useState<"Day" | "Week" | "Month">("Day");
  const [date, setDate] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setError(null);
    apiClient.get("/masterdata/machines").then((r) => setMachines(r.data.data)).catch(() => setError("Could not load machines."));
  }, [open]);

  useEffect(() => {
    setParts([]);
    setChecked({});
    if (machineId === "") return;
    const machine = machines.find((m) => m.id === machineId);
    Promise.all(
      (machine?.parts ?? []).map(async (p) => ({
        ...p,
        configs: (await apiClient.get("/masterdata/machine-part-configurations", { params: { machinePartId: p.id } })).data.data as Config[]
      }))
    ).then(setParts).catch(() => setError("Could not load machine parts."));
  }, [machineId, machines]);

  const ids = parts.filter((p) => checked[p.id]).flatMap((p) => p.configs.map((c) => c.id));

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      await MonitoringPlanService.createManual({
        machineId: machineId as number, targetDate: kind === "Month" ? `${date}-01` : date, kind, machinePartConfigurationIds: ids, reason: reason || undefined
      });
      onCreated();
      onClose();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not create the occurrence.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Add After Cleaning occurrence</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField select label="Machine" value={machineId} onChange={(e) => setMachineId(Number(e.target.value))}>
            {machines.map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
          </TextField>
          <TextField select label="Due" value={kind} onChange={(e) => { setKind(e.target.value as "Day" | "Week" | "Month"); setDate(""); }}>
            <MenuItem value="Day">On a specific day</MenuItem>
            <MenuItem value="Week">Any day in a week</MenuItem>
            <MenuItem value="Month">Any day in a month</MenuItem>
          </TextField>
          <TextField
            label={kind === "Day" ? "Target date" : kind === "Week" ? "Any date in the week" : "Month"}
            type={kind === "Month" ? "month" : "date"}
            slotProps={{ inputLabel: { shrink: true } }}
            value={date}
            onChange={(e) => setDate(e.target.value)}
            helperText={kind === "Week" ? "The lab week runs Sunday to Saturday." : undefined}
          />
          {parts.length > 0 && <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Machine parts to test</Typography>}
          {parts.map((p) => (
            <FormControlLabel
              key={p.id}
              control={<Checkbox checked={!!checked[p.id]} onChange={(e) => setChecked({ ...checked, [p.id]: e.target.checked })} />}
              label={`${p.name} (${p.configs.map((c) => c.testCode).join(", ") || "no tests configured"})`}
            />
          ))}
          <TextField label="Reason (optional)" value={reason} onChange={(e) => setReason(e.target.value)} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button variant="contained" onClick={create} disabled={busy || machineId === "" || !date || ids.length === 0}>Add</Button>
      </DialogActions>
    </Dialog>
  );
}
