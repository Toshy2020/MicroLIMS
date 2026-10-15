import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Checkbox, IconButton, MenuItem, Select, Stack, Table, TableBody, TableCell, TableHead,
  TableRow, TextField, Typography
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import AddIcon from "@mui/icons-material/Add";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { EquipmentConfigurationService, type AutoclaveProgram } from "../../masterDataSimple/services/EquipmentConfigurationService";
import { BiologicalWasteService } from "../services/BiologicalWasteService";
import { serverMessage } from "../../../../utils/errorMessage";
import { tableHeadSx } from "../../../../theme";
import type { BiologicalWasteEligible } from "../types/biologicalWasteTypes";

interface Props {
  open: boolean;
  onClose: () => void;
  onRecorded: (reference: string) => void;
}

interface OtherRow {
  description: string;
  quantity: string;
  unit: string;
}

const dt = (v: string) => new Date(v).toLocaleString();

// "YYYY-MM-DDTHH:mm" in local time, for a datetime-local input.
const localNow = () => {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
};

// One autoclave cycle: the eligible cryovial vials (thawed over 24 h ago,
// or from a withdrawn batch) chosen in bulk, plus any other waste in the
// same load. Signed electronically; eligibility is enforced server-side.
export function RecordDestructionDialog({ open, onClose, onRecorded }: Props) {
  const [eligible, setEligible] = useState<BiologicalWasteEligible>({ thawedVials: [], withdrawnBatches: [] });
  const [autoclaves, setAutoclaves] = useState<{ id: number; name: string; code?: string }[]>([]);
  const [programs, setPrograms] = useState<AutoclaveProgram[]>([]);
  const [autoclaveId, setAutoclaveId] = useState<number | "">("");
  const [programId, setProgramId] = useState<number | "">("");
  const [cycleNumber, setCycleNumber] = useState("");
  const [cycleStartedAt, setCycleStartedAt] = useState(localNow());
  const [thawIds, setThawIds] = useState<number[]>([]);
  const [batchIds, setBatchIds] = useState<number[]>([]);
  const [others, setOthers] = useState<OtherRow[]>([]);
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [signing, setSigning] = useState(false);

  useEffect(() => {
    if (!open) return;
    setAutoclaveId(""); setProgramId(""); setPrograms([]); setCycleNumber(""); setCycleStartedAt(localNow());
    setThawIds([]); setBatchIds([]); setOthers([]); setNotes(""); setError(null);
    BiologicalWasteService.getEligible().then(setEligible)
      .catch((e) => setError(serverMessage(e) ?? "Could not load the vials awaiting destruction."));
    masterDataOptions.getEquipment("Autoclave").then(setAutoclaves).catch(() => setAutoclaves([]));
  }, [open]);

  const changeAutoclave = async (id: number | "") => {
    setAutoclaveId(id);
    setProgramId("");
    setPrograms([]);
    if (id === "") return;
    try {
      setPrograms(await EquipmentConfigurationService.getAutoclavePrograms(id, true));
    } catch {
      setPrograms([]);
    }
  };

  const toggle = (list: number[], set: (v: number[]) => void, id: number) =>
    set(list.includes(id) ? list.filter((x) => x !== id) : [...list, id]);

  const allThawSelected = eligible.thawedVials.length > 0 && thawIds.length === eligible.thawedVials.length;
  const program = programs.find((p) => p.id === programId);
  const itemCount = thawIds.length + batchIds.length + others.length;

  // Checked client-side for a quicker message only; the server repeats every check.
  const problem = (): string | null => {
    if (autoclaveId === "") return "Select the autoclave.";
    if (programId === "") return "Select the autoclave program.";
    if (!cycleNumber.trim()) return "Enter the autoclave cycle number.";
    if (!cycleStartedAt) return "Enter when the cycle started.";
    if (itemCount === 0) return "Select at least one vial or add another waste item.";
    if (others.some((o) => !o.description.trim() || !(Number(o.quantity) > 0) || !o.unit.trim()))
      return "Every other waste item needs a description, a quantity above zero and a unit.";
    return null;
  };

  const startSigning = () => {
    const p = problem();
    setError(p);
    if (!p) setSigning(true);
  };

  // Errors propagate to SignatureDialog, which shows them and stays open.
  const sign = async (password: string) => {
    const recorded = await BiologicalWasteService.record({
      autoclaveEquipmentId: Number(autoclaveId),
      autoclaveProgramId: Number(programId),
      cycleNumber: cycleNumber.trim(),
      cycleStartedAt: new Date(cycleStartedAt).toISOString(),
      thawEventIds: thawIds,
      withdrawnCryovialIds: batchIds,
      otherItems: others.map((o) => ({ description: o.description.trim(), quantity: Number(o.quantity), unit: o.unit.trim() })),
      notes: notes.trim() || null,
      password
    });
    setSigning(false);
    onRecorded(recorded.reference);
  };

  return (
    <>
      <FloatingDialog
        open={open && !signing}
        onClose={onClose}
        maxWidth="md"
        title="Record Biological Waste Destruction"
        actions={
          <>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" color="error" onClick={startSigning}>
              Sign &amp; record ({itemCount} item{itemCount === 1 ? "" : "s"})
            </Button>
          </>
        }
      >
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}

          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>AUTOCLAVE CYCLE</Typography>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 1.5 }}>
            <Select size="small" displayEmpty value={autoclaveId}
              onChange={(e) => changeAutoclave(e.target.value)}
              inputProps={{ "aria-label": "Autoclave" }}>
              <MenuItem value=""><em>Autoclave</em></MenuItem>
              {autoclaves.map((a) => <MenuItem key={a.id} value={a.id}>{a.name}{a.code ? ` (${a.code})` : ""}</MenuItem>)}
            </Select>
            <Select size="small" displayEmpty value={programId} disabled={autoclaveId === ""}
              onChange={(e) => setProgramId(e.target.value)}
              inputProps={{ "aria-label": "Autoclave program" }}>
              <MenuItem value=""><em>Program</em></MenuItem>
              {programs.map((p) => (
                <MenuItem key={p.id} value={p.id}>{p.programCode}: {p.programName} ({p.loadType})</MenuItem>
              ))}
            </Select>
            <TextField size="small" label="Cycle number" value={cycleNumber} onChange={(e) => setCycleNumber(e.target.value)} />
            <TextField size="small" label="Cycle started" type="datetime-local" value={cycleStartedAt}
              onChange={(e) => setCycleStartedAt(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          </Box>
          {program && (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {program.temperature}°C for {program.cycleTimeMinutes} min, load type {program.loadType}
            </Typography>
          )}

          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
            THAWED CRYOVIAL VIALS ELIGIBLE FOR DESTRUCTION
          </Typography>
          {eligible.thawedVials.length === 0 ? (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              No thawed vials are eligible. A thawed vial becomes eligible 24 hours after thawing, or at once when its batch is withdrawn.
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell padding="checkbox">
                    <Checkbox size="small" checked={allThawSelected}
                      indeterminate={thawIds.length > 0 && !allThawSelected}
                      onChange={() => setThawIds(allThawSelected ? [] : eligible.thawedVials.map((v) => v.thawEventId))}
                      slotProps={{ input: { "aria-label": "Select all thawed vials" } }} />
                  </TableCell>
                  <TableCell>Batch</TableCell><TableCell>Organism</TableCell><TableCell>Thawed</TableCell>
                  <TableCell>Eligible because</TableCell><TableCell>Used in</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {eligible.thawedVials.map((v) => (
                  <TableRow key={v.thawEventId} hover onClick={() => toggle(thawIds, setThawIds, v.thawEventId)} sx={{ cursor: "pointer" }}>
                    <TableCell padding="checkbox">
                      <Checkbox size="small" checked={thawIds.includes(v.thawEventId)}
                        slotProps={{ input: { "aria-label": `Destroy ${v.cryovialCode} vial thawed ${dt(v.thawedAt)}` } }} />
                    </TableCell>
                    <TableCell>{v.cryovialCode}</TableCell>
                    <TableCell>{v.organismName}</TableCell>
                    <TableCell>{dt(v.thawedAt)} by {v.thawedByName}</TableCell>
                    <TableCell>{v.eligibleBecause}</TableCell>
                    <TableCell>{v.usedInMediaLots.join(", ") || "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}

          {eligible.withdrawnBatches.length > 0 && (
            <>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
                UNTHAWED VIALS OF WITHDRAWN BATCHES
              </Typography>
              <Table size="small">
                <TableHead>
                  <TableRow sx={tableHeadSx}>
                    <TableCell padding="checkbox" />
                    <TableCell>Batch</TableCell><TableCell>Organism</TableCell><TableCell>Vials</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {eligible.withdrawnBatches.map((b) => (
                    <TableRow key={b.cryovialId} hover onClick={() => toggle(batchIds, setBatchIds, b.cryovialId)} sx={{ cursor: "pointer" }}>
                      <TableCell padding="checkbox">
                        <Checkbox size="small" checked={batchIds.includes(b.cryovialId)}
                          slotProps={{ input: { "aria-label": `Destroy the unthawed vials of ${b.cryovialCode}` } }} />
                      </TableCell>
                      <TableCell>{b.cryovialCode}</TableCell>
                      <TableCell>{b.organismName}</TableCell>
                      <TableCell>{b.vialsRemaining}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </>
          )}

          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
            OTHER WASTE IN THE SAME CYCLE
          </Typography>
          {others.map((o, i) => (
            <Stack key={i} direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <TextField size="small" label="Description" placeholder="e.g. Used GPT plates" value={o.description} sx={{ flex: 1 }}
                onChange={(e) => setOthers(others.map((x, j) => (j === i ? { ...x, description: e.target.value } : x)))} />
              <TextField size="small" label="Quantity" type="number" value={o.quantity} sx={{ width: 110 }}
                onChange={(e) => setOthers(others.map((x, j) => (j === i ? { ...x, quantity: e.target.value } : x)))} />
              <TextField size="small" label="Unit" placeholder="plates" value={o.unit} sx={{ width: 120 }}
                onChange={(e) => setOthers(others.map((x, j) => (j === i ? { ...x, unit: e.target.value } : x)))} />
              <IconButton aria-label="Remove item" onClick={() => setOthers(others.filter((_, j) => j !== i))}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Stack>
          ))}
          <Box>
            <Button size="small" startIcon={<AddIcon />} onClick={() => setOthers([...others, { description: "", quantity: "", unit: "" }])}>
              Add other waste
            </Button>
          </Box>

          <TextField size="small" label="Notes (optional)" multiline minRows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={`I confirm that the ${itemCount} selected item(s) were destroyed as biological waste in autoclave cycle ${cycleNumber.trim()}.`}
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </>
  );
}
