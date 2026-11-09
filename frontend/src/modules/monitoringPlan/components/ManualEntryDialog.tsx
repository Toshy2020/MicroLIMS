import { useEffect, useMemo, useState } from "react";
import { Alert, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { apiClient } from "../../../services/apiClient";
import { useTestDefinitions } from "../../../hooks/useTestDefinitions";
import { laboratorySectionService } from "../../../services/laboratorySectionService";
import { MonitoringPlanService } from "../services/MonitoringPlanService";

interface Machine { id: number; name: string }
interface WaterPoint { id: number; code?: string; location?: string; name?: string; assignedTestCodes?: string[] }

// An occurrence can be entered by hand:
// 1. After Cleaning: pick the machine and when it is due (a day, any day in a week,
//    or any day in a month). It is for the machine itself; parts are chosen at preparation.
// 2. Water: pick the water sampling point, the lab section, and when it is due.
export function ManualEntryDialog({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: () => void }) {
  const { options } = useTestDefinitions();
  const [targetType, setTargetType] = useState<"Machine" | "Water">("Machine");
  const [machines, setMachines] = useState<Machine[]>([]);
  const [machineId, setMachineId] = useState<number | "">("");
  const [waterPoints, setWaterPoints] = useState<WaterPoint[]>([]);
  const [waterPointId, setWaterPointId] = useState<number | "">("");
  const [labSectionId, setLabSectionId] = useState<number | "">("");
  const [sections, setSections] = useState<{ id: number; code: string; name?: string }[]>([]);

  const [kind, setKind] = useState<"Day" | "Week" | "Month">("Day");
  const [date, setDate] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setTargetType("Machine");
    setMachineId("");
    setWaterPointId("");
    setLabSectionId("");
    setKind("Day");
    setDate("");
    setReason("");
    apiClient.get("/masterdata/machines").then((r) => setMachines(r.data.data)).catch(() => setError("Could not load machines."));
    apiClient.get("/masterdata/water-sampling-points").then((r) => setWaterPoints(r.data.data)).catch(() => {});
    laboratorySectionService
      .getSections()
      .then((list) => {
        if (Array.isArray(list)) {
          setSections(list.map((s) => ({ id: s.sectionId, code: s.sectionCode, name: s.sectionName })));
        }
      })
      .catch(() => {});
  }, [open]);

  const availableLabs = useMemo(() => {
    if (targetType !== "Water") return [];
    const selectedPoint = waterPoints.find((p) => p.id === waterPointId);
    const map = new Map<number, { id: number; code: string; name?: string }>();
    if (selectedPoint?.assignedTestCodes?.length) {
      for (const code of selectedPoint.assignedTestCodes) {
        const def = options.find((o) => o.code === code);
        const secId = def?.section?.id ?? def?.sectionId;
        const secCode = def?.section?.code ?? (secId === 1 ? "MICRO" : secId === 2 ? "FP" : "");
        if (secId != null) {
          map.set(secId, { id: secId, code: secCode || `Section ${secId}`, name: def?.section?.name });
        }
      }
    }
    if (map.size > 0) return Array.from(map.values());
    if (sections.length > 0) return sections;
    return [
      { id: 1, code: "MICRO", name: "Microbiology" },
      { id: 2, code: "FP", name: "Physicochemical" }
    ];
  }, [targetType, waterPointId, waterPoints, options, sections]);

  const isFormValid =
    Boolean(date) &&
    (targetType === "Machine"
      ? machineId !== ""
      : waterPointId !== "" && labSectionId !== "");

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const targetDate = kind === "Month" ? `${date}-01` : date;
      if (targetType === "Water") {
        await MonitoringPlanService.createManual({
          waterSamplingPointId: Number(waterPointId),
          labSectionId: Number(labSectionId),
          targetDate,
          kind,
          reason: reason || undefined
        });
      } else {
        await MonitoringPlanService.createManual({
          machineId: Number(machineId),
          targetDate,
          kind,
          reason: reason || undefined
        });
      }
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
      title={targetType === "Water" ? "Add manual water occurrence" : "Add After Cleaning occurrence"}
      onClose={onClose}
      maxWidth="sm"
      actions={
        <>
          <Button onClick={onClose} disabled={busy}>Cancel</Button>
          <Button variant="contained" onClick={create} disabled={busy || !isFormValid}>Add</Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}
        <TextField
          select
          label="Target type"
          value={targetType}
          onChange={(e) => {
            setTargetType(e.target.value as "Machine" | "Water");
            setError(null);
          }}
        >
          <MenuItem value="Machine">Machine (After Cleaning)</MenuItem>
          <MenuItem value="Water">Water sampling point</MenuItem>
        </TextField>

        {targetType === "Machine" ? (
          <>
            <TextField select label="Machine" value={machineId} onChange={(e) => setMachineId(Number(e.target.value))}>
              {machines.map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
            </TextField>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              The occurrence is for the whole machine. Choose the parts that were cleaned when you prepare the sample.
            </Typography>
          </>
        ) : (
          <>
            <TextField select label="Water point" value={waterPointId} onChange={(e) => setWaterPointId(Number(e.target.value))}>
              {waterPoints.map((p) => (
                <MenuItem key={p.id} value={p.id}>
                  {p.code ? (p.location ? `${p.code} – ${p.location}` : p.code) : p.name || `Point ${p.id}`}
                </MenuItem>
              ))}
            </TextField>
            <TextField select label="Lab" value={labSectionId} onChange={(e) => setLabSectionId(Number(e.target.value))}>
              {availableLabs.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.code || s.name}
                </MenuItem>
              ))}
            </TextField>
          </>
        )}

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
        <TextField label="Reason (optional)" value={reason} onChange={(e) => setReason(e.target.value)} />
      </Stack>
    </FloatingDialog>
  );
}
