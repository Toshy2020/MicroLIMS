import { useEffect, useState } from "react";
import { i18n, useTranslation } from "../../../../i18n";
import {
  Alert, Box, Button, Checkbox, IconButton, MenuItem, Select, Stack, Table, TableBody, TableCell, TableHead,
  TableRow, TextField, Typography
} from "@mui/material";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import AddIcon from "@mui/icons-material/Add";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { DateTimeField24 } from "../../../../components/DateTimeField24";
import { SignatureDialog } from "../../../../components/SignatureDialog";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { EquipmentConfigurationService, type AutoclaveProgram } from "../../masterDataSimple/services/EquipmentConfigurationService";
import { BiologicalWasteService } from "../services/BiologicalWasteService";
import { serverMessage } from "../../../../utils/errorMessage";
import { tableHeadSx } from "../../../../theme";
import type { BiologicalWasteEligible } from "../types/biologicalWasteTypes";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";

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

const dt = (v: string) => formatDateTimeWithTimeZone(v);

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
  const { t } = useTranslation("labConfig");
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
      .catch((e) => setError(serverMessage(e) ?? t("biologicalWaste.dialog.loadError")));
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
    if (autoclaveId === "") return t("biologicalWaste.dialog.selectAutoclave");
    if (programId === "") return t("biologicalWaste.dialog.selectProgram");
    if (!cycleNumber.trim()) return t("biologicalWaste.dialog.enterCycleNumber");
    if (!cycleStartedAt) return t("biologicalWaste.dialog.enterCycleStart");
    if (itemCount === 0) return t("biologicalWaste.dialog.needItem");
    if (others.some((o) => !o.description.trim() || !(Number(o.quantity) > 0) || !o.unit.trim()))
      return t("biologicalWaste.dialog.otherInvalid");
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
        title={t("biologicalWaste.dialog.title")}
        actions={
          <>
            <Button onClick={onClose}>{t("biologicalWaste.dialog.cancel")}</Button>
            <Button variant="contained" color="error" onClick={startSigning}>
              {t("biologicalWaste.dialog.signRecord", { count: itemCount })}
            </Button>
          </>
        }
      >
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}

          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>{t("biologicalWaste.dialog.cycleHeading")}</Typography>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 1.5 }}>
            <Select size="small" displayEmpty value={autoclaveId}
              onChange={(e) => changeAutoclave(e.target.value)}
              inputProps={{ "aria-label": t("biologicalWaste.dialog.autoclave") }}>
              <MenuItem value=""><em>{t("biologicalWaste.dialog.autoclave")}</em></MenuItem>
              {autoclaves.map((a) => <MenuItem key={a.id} value={a.id}>{a.name}{a.code ? ` (${a.code})` : ""}</MenuItem>)}
            </Select>
            <Select size="small" displayEmpty value={programId} disabled={autoclaveId === ""}
              onChange={(e) => setProgramId(e.target.value)}
              inputProps={{ "aria-label": t("biologicalWaste.dialog.programAria") }}>
              <MenuItem value=""><em>{t("biologicalWaste.dialog.program")}</em></MenuItem>
              {programs.map((p) => (
                <MenuItem key={p.id} value={p.id}>{p.programCode}: {p.programName} ({p.loadType})</MenuItem>
              ))}
            </Select>
            <TextField size="small" label={t("biologicalWaste.dialog.cycleNumber")} value={cycleNumber} onChange={(e) => setCycleNumber(e.target.value)} />
            <DateTimeField24
              label={t("biologicalWaste.dialog.cycleStarted")}
              value={cycleStartedAt}
              onChange={setCycleStartedAt}
            />
          </Box>
          {program && (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {t("biologicalWaste.dialog.programSummary", {
                temp: program.temperature,
                minutes: program.cycleTimeMinutes,
                loadType: program.loadType
              })}
            </Typography>
          )}

          <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary" }}>
            {t("biologicalWaste.dialog.vialsHeading")}
          </Typography>
          {eligible.thawedVials.length === 0 ? (
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              {t("biologicalWaste.dialog.noEligible")}
            </Typography>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell padding="checkbox">
                    <Checkbox size="small" checked={allThawSelected}
                      indeterminate={thawIds.length > 0 && !allThawSelected}
                      onChange={() => setThawIds(allThawSelected ? [] : eligible.thawedVials.map((v) => v.thawEventId))}
                      slotProps={{ input: { "aria-label": t("biologicalWaste.dialog.selectAllAria") } }} />
                  </TableCell>
                  <TableCell>{t("biologicalWaste.dialog.columns.batch")}</TableCell><TableCell>{t("biologicalWaste.dialog.columns.organism")}</TableCell><TableCell>{t("biologicalWaste.dialog.columns.thawed")}</TableCell>
                  <TableCell>{t("biologicalWaste.dialog.columns.eligibleBecause")}</TableCell><TableCell>{t("biologicalWaste.dialog.columns.usedIn")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {eligible.thawedVials.map((v) => (
                  <TableRow key={v.thawEventId} hover onClick={() => toggle(thawIds, setThawIds, v.thawEventId)} sx={{ cursor: "pointer" }}>
                    <TableCell padding="checkbox">
                      <Checkbox size="small" checked={thawIds.includes(v.thawEventId)}
                        slotProps={{ input: { "aria-label": t("biologicalWaste.dialog.destroyVialAria", { code: v.cryovialCode, date: dt(v.thawedAt) }) } }} />
                    </TableCell>
                    <TableCell>{v.cryovialCode}</TableCell>
                    <TableCell>{v.organismName}</TableCell>
                    <TableCell>{t("biologicalWaste.dialog.thawedBy", { date: dt(v.thawedAt), name: v.thawedByName })}</TableCell>
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
                {t("biologicalWaste.dialog.withdrawnHeading")}
              </Typography>
              <Table size="small">
                <TableHead>
                  <TableRow sx={tableHeadSx}>
                    <TableCell padding="checkbox" />
                    <TableCell>{t("biologicalWaste.dialog.columns.batch")}</TableCell><TableCell>{t("biologicalWaste.dialog.columns.organism")}</TableCell><TableCell>{t("biologicalWaste.dialog.columns.vials")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {eligible.withdrawnBatches.map((b) => (
                    <TableRow key={b.cryovialId} hover onClick={() => toggle(batchIds, setBatchIds, b.cryovialId)} sx={{ cursor: "pointer" }}>
                      <TableCell padding="checkbox">
                        <Checkbox size="small" checked={batchIds.includes(b.cryovialId)}
                          slotProps={{ input: { "aria-label": t("biologicalWaste.dialog.destroyUnthawedAria", { code: b.cryovialCode }) } }} />
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
            {t("biologicalWaste.dialog.otherHeading")}
          </Typography>
          {others.map((o, i) => (
            <Stack key={i} direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <TextField size="small" label={t("biologicalWaste.dialog.description")} placeholder={t("biologicalWaste.dialog.descriptionPlaceholder")} value={o.description} sx={{ flex: 1 }}
                onChange={(e) => setOthers(others.map((x, j) => (j === i ? { ...x, description: e.target.value } : x)))} />
              <TextField size="small" label={t("biologicalWaste.dialog.quantity")} type="number" value={o.quantity} sx={{ width: 110 }}
                onChange={(e) => setOthers(others.map((x, j) => (j === i ? { ...x, quantity: e.target.value } : x)))} />
              <TextField size="small" label={t("biologicalWaste.dialog.unit")} placeholder={t("biologicalWaste.dialog.unitPlaceholder")} value={o.unit} sx={{ width: 120 }}
                onChange={(e) => setOthers(others.map((x, j) => (j === i ? { ...x, unit: e.target.value } : x)))} />
              <IconButton aria-label={t("biologicalWaste.dialog.removeItem")} onClick={() => setOthers(others.filter((_, j) => j !== i))}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Stack>
          ))}
          <Box>
            <Button size="small" startIcon={<AddIcon />} onClick={() => setOthers([...others, { description: "", quantity: "", unit: "" }])}>
              {t("biologicalWaste.dialog.addOther")}
            </Button>
          </Box>

          <TextField size="small" label={t("biologicalWaste.dialog.notes")} multiline minRows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={i18n.getFixedT("en", "labConfig")("biologicalWaste.dialog.signatureMeaning", {
          count: itemCount,
          cycle: cycleNumber.trim()
        })}
        meaningStatementTranslation={t("biologicalWaste.dialog.signatureMeaning", {
          count: itemCount,
          cycle: cycleNumber.trim()
        })}
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </>
  );
}
