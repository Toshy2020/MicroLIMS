import { useEffect, useState } from "react";
import { Alert, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { apiClient } from "../../../services/apiClient";
import { MonitoringPlanService } from "../services/MonitoringPlanService";

interface Machine { id: number; name: string }

// An After Cleaning occurrence is entered by hand when a machine is cleaned:
// pick the machine and when it is due (a day, any day in a week, or any day in
// a month). It is for the machine itself; the parts that were cleaned are
// chosen when the sample is prepared, so a cleaning of only some parts works.
export function ManualEntryDialog({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: () => void }) {
  const [machines, setMachines] = useState<Machine[]>([]);
  const [machineId, setMachineId] = useState<number | "">("");
  const [kind, setKind] = useState<"Day" | "Week" | "Month">("Day");
  const [date, setDate] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setMachineId("");
    setKind("Day");
    setDate("");
    setReason("");
    apiClient.get("/masterdata/machines").then((r) => setMachines(r.data.data)).catch(() => setError("Could not load machines."));
  }, [open]);

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      await MonitoringPlanService.createManual({
        machineId: machineId as number, targetDate: kind === "Month" ? `${date}-01` : date, kind, reason: reason || undefined
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
    <FloatingDialog
      open={open}
      title="Add After Cleaning occurrence"
      onClose={onClose}
      maxWidth="sm"
      actions={
        <>
          <Button onClick={onClose} disabled={busy}>Cancel</Button>
          <Button variant="contained" onClick={create} disabled={busy || machineId === "" || !date}>Add</Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
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
          helperText={kind === "Week" ? "Due on any working day of that lab week." : kind === "Month" ? "Due on any working day of that month." : "Choose a working day."}
        />
        <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
          The occurrence is for the whole machine. Choose the parts that were cleaned when you prepare the sample.
        </Typography>
        <TextField label="Reason (optional)" value={reason} onChange={(e) => setReason(e.target.value)} />
      </Stack>
    </FloatingDialog>
  );
}
