import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Chip, CircularProgress, FormControl, InputLabel, MenuItem, Paper, Select,
  Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Tooltip, Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import { PageHeader } from "../../components/PageHeader";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { tableHeadSx } from "../../theme";
import { useTestDefinitions, TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { ChromatographyColumnService, ChromatographyColumnDto } from "../laboratoryConfiguration/masterDataSimple/services/ChromatographyColumnService";
import { MaterialService } from "../inventory/materials/services/MaterialService";
import { SystemSuitabilityService, SystemSuitabilityRun } from "./services/SystemSuitabilityService";

interface HplcInstrument { id: number; code: string; name: string; sectionId: number; type: string }
interface ReferenceStandard { id: number; materialName: string; batchNumber: string; purity?: number | null; sectionId: number; expiryDate?: string | null }

type StatusFilter = "all" | "passed" | "failed";

const emptyForm = {
  testDefinitionId: "", equipmentId: "", columnId: "", standardId: "",
  standardWeightMg: "", standardDilution: "", standardMeanArea: "",
  rsdPercent: "", resolution: "", tailingFactor: "", theoreticalPlates: ""
};

const errorMessage = (e: unknown, fallback: string) => {
  const err = e as { response?: { data?: { message?: string } }; message?: string };
  return err.response?.data?.message ?? err.message ?? fallback;
};

const numOrNull = (v: string) => (v.trim() === "" ? null : Number(v));
const fmt = (v?: number | null) => (v === null || v === undefined ? "—" : String(v));

// System Suitability Runs (REQ-FP-001/001a/002). The analyst types the four
// values already calculated by the CDS; Pass/Fail is decided by the server
// against the method's acceptance criteria - this page only displays it.
export function SystemSuitabilityRunsPage() {
  const { activeOptions: tests } = useTestDefinitions();
  const [runs, setRuns] = useState<SystemSuitabilityRun[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [methodFilter, setMethodFilter] = useState<string>("all");

  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [comment, setComment] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [signing, setSigning] = useState(false);
  const [created, setCreated] = useState<SystemSuitabilityRun | null>(null);

  const [instruments, setInstruments] = useState<HplcInstrument[]>([]);
  const [columns, setColumns] = useState<ChromatographyColumnDto[]>([]);
  const [standards, setStandards] = useState<ReferenceStandard[]>([]);

  const sstMethods = useMemo(() => tests.filter((t) => t.requiresSystemSuitability), [tests]);
  const method: TestDefinitionOption | undefined = sstMethods.find((t) => String(t.id) === form.testDefinitionId);
  const methodSectionId = method?.sectionId;

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    SystemSuitabilityService.getAll({
      passed: statusFilter === "all" ? undefined : statusFilter === "passed",
      testDefinitionId: methodFilter === "all" ? undefined : Number(methodFilter)
    })
      .then(setRuns)
      .catch((e) => setError(errorMessage(e, "Could not load suitability runs.")))
      .finally(() => setLoading(false));
  }, [statusFilter, methodFilter]);

  useEffect(() => { load(); }, [load]);

  const openDialog = async () => {
    setForm(emptyForm);
    setComment("");
    setFormError(null);
    setCreated(null);
    setDialogOpen(true);
    try {
      const [eq, cols, stds] = await Promise.all([
        EquipmentConfigurationService.getEquipmentList("Hplc"),
        ChromatographyColumnService.getAll(true),
        MaterialService.getUsableReferenceStandards()
      ]);
      setInstruments(eq as HplcInstrument[]);
      setColumns(cols);
      setStandards(stds as ReferenceStandard[]);
    } catch (e) {
      setFormError(errorMessage(e, "Could not load instruments, columns or reference standards."));
    }
  };

  // Everything used in one run must belong to the method's section; the
  // server enforces this too, the filter just keeps the pickers honest.
  const sectionInstruments = instruments.filter((i) => methodSectionId === undefined || i.sectionId === methodSectionId);
  const sectionColumns = columns.filter((c) => methodSectionId === undefined || c.sectionId === methodSectionId);
  const sectionStandards = standards.filter((s) => methodSectionId === undefined || s.sectionId === methodSectionId);
  const selectedStandard = sectionStandards.find((s) => String(s.id) === form.standardId);

  const set = (key: keyof typeof emptyForm) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  const readyToSign =
    form.testDefinitionId && form.equipmentId && form.columnId && form.standardId &&
    Number(form.standardWeightMg) > 0 && Number(form.standardDilution) > 0 && Number(form.standardMeanArea) > 0;

  const submit = async (password: string) => {
    const run = await SystemSuitabilityService.create({
      testDefinitionId: Number(form.testDefinitionId),
      equipmentId: Number(form.equipmentId),
      chromatographyColumnId: Number(form.columnId),
      referenceStandardMaterialId: Number(form.standardId),
      standardWeightMg: Number(form.standardWeightMg),
      standardDilution: Number(form.standardDilution),
      standardMeanArea: Number(form.standardMeanArea),
      rsdPercent: numOrNull(form.rsdPercent),
      resolution: numOrNull(form.resolution),
      tailingFactor: numOrNull(form.tailingFactor),
      theoreticalPlates: numOrNull(form.theoreticalPlates),
      password,
      comment: comment.trim() || null
    });
    setSigning(false);
    setCreated(run);
    load();
  };

  const criterion = (label: string, limit?: number | null) =>
    limit === null || limit === undefined ? `${label} (not checked)` : label;

  return (
    <>
      <PageHeader
        title="System Suitability Runs"
        subtitle="Signed suitability runs for HPLC methods. Only a passed run can be linked to samples."
      >
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" startIcon={<RefreshIcon />} onClick={load} disabled={loading}>Refresh</Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={openDialog}>New Run</Button>
        </Stack>
      </PageHeader>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Paper sx={{ p: 2.5 }}>
        <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel>Result</InputLabel>
            <Select label="Result" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}>
              <MenuItem value="all">All</MenuItem>
              <MenuItem value="passed">Passed</MenuItem>
              <MenuItem value="failed">Failed</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 220 }}>
            <InputLabel>Method</InputLabel>
            <Select label="Method" value={methodFilter} onChange={(e) => setMethodFilter(e.target.value)}>
              <MenuItem value="all">All methods</MenuItem>
              {sstMethods.map((t) => <MenuItem key={t.id} value={String(t.id)}>{t.displayName}</MenuItem>)}
            </Select>
          </FormControl>
        </Stack>

        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}><CircularProgress size={32} /></Box>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>Code</TableCell>
                <TableCell>Method</TableCell>
                <TableCell>Instrument / Column</TableCell>
                <TableCell>Reference standard</TableCell>
                <TableCell align="right">Std weight (mg)</TableCell>
                <TableCell align="right">Std dilution</TableCell>
                <TableCell align="right">Mean peak area</TableCell>
                <TableCell align="right">RSD %</TableCell>
                <TableCell align="right">Resolution</TableCell>
                <TableCell align="right">Tailing</TableCell>
                <TableCell align="right">Plates</TableCell>
                <TableCell>Result</TableCell>
                <TableCell>Performed</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {runs.map((r) => (
                <TableRow key={r.id}>
                  <TableCell sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>{r.code}</TableCell>
                  <TableCell>{r.testName}<Typography sx={{ fontSize: 12, color: "text.secondary" }}>{r.sectionName}</Typography></TableCell>
                  <TableCell>{r.equipmentCode}<Typography sx={{ fontSize: 12, color: "text.secondary" }}>{r.columnCode}</Typography></TableCell>
                  <TableCell>
                    {r.referenceStandardName}
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      Batch {r.referenceStandardBatch} · purity {r.standardPurityPercent}%
                    </Typography>
                  </TableCell>
                  <TableCell align="right">{fmt(r.standardWeightMg)}</TableCell>
                  <TableCell align="right">{fmt(r.standardDilution)}</TableCell>
                  <TableCell align="right">{fmt(r.standardMeanArea)}</TableCell>
                  <TableCell align="right">{fmt(r.rsdPercent)}</TableCell>
                  <TableCell align="right">{fmt(r.resolution)}</TableCell>
                  <TableCell align="right">{fmt(r.tailingFactor)}</TableCell>
                  <TableCell align="right">{fmt(r.theoreticalPlates)}</TableCell>
                  <TableCell>
                    {r.passed ? (
                      <Chip size="small" color="success" label="Passed" />
                    ) : (
                      <Tooltip title={r.failureReasons ?? ""}>
                        <Chip size="small" color="error" label="Failed" />
                      </Tooltip>
                    )}
                  </TableCell>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>
                    {r.performedByName}
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{new Date(r.performedAt).toLocaleString()}</Typography>
                  </TableCell>
                  <TableCell>
                    <Button
                      size="small"
                      startIcon={<DescriptionOutlinedIcon />}
                      href={`/laboratory/system-suitability/${r.id}/report`}
                      target="_blank"
                      rel="noopener"
                    >
                      Report
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
              {runs.length === 0 && (
                <TableRow>
                  <TableCell colSpan={14} align="center" sx={{ py: 3, color: "text.secondary" }}>No suitability runs yet.</TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        )}
      </Paper>

      <FloatingDialog
        open={dialogOpen}
        title={created ? `Run ${created.code}` : "New System Suitability Run"}
        onClose={() => setDialogOpen(false)}
        maxWidth="md"
        actions={
          created ? (
            <Button variant="contained" onClick={() => setDialogOpen(false)}>Close</Button>
          ) : (
            <>
              <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
              <Button variant="contained" disabled={!readyToSign} onClick={() => setSigning(true)}>Sign and save</Button>
            </>
          )
        }
      >
        {created ? (
          <Stack spacing={2}>
            <Alert severity={created.passed ? "success" : "error"}>
              {created.passed
                ? `${created.code} passed. It can now be linked to samples for ${created.testName}.`
                : `${created.code} failed: ${created.failureReasons}. The run is kept for traceability but cannot be used.`}
            </Alert>
          </Stack>
        ) : (
          <Stack spacing={2}>
            {formError && <Alert severity="error">{formError}</Alert>}
            <FormControl size="small" fullWidth>
              <InputLabel>Method</InputLabel>
              <Select
                label="Method" value={form.testDefinitionId}
                onChange={(e) => setForm({ ...emptyForm, testDefinitionId: e.target.value })}
              >
                {sstMethods.map((t) => (
                  <MenuItem key={t.id} value={String(t.id)}>{t.displayName} ({t.methodAbbreviation})</MenuItem>
                ))}
              </Select>
            </FormControl>
            {sstMethods.length === 0 && (
              <Alert severity="info">No active test in your sections requires system suitability. Configure one in Test Master first.</Alert>
            )}

            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <FormControl size="small" fullWidth disabled={!method}>
                <InputLabel>HPLC instrument</InputLabel>
                <Select label="HPLC instrument" value={form.equipmentId} onChange={(e) => set("equipmentId")(e.target.value)}>
                  {sectionInstruments.map((i) => <MenuItem key={i.id} value={String(i.id)}>{i.code} — {i.name}</MenuItem>)}
                </Select>
              </FormControl>
              <FormControl size="small" fullWidth disabled={!method}>
                <InputLabel>Column</InputLabel>
                <Select label="Column" value={form.columnId} onChange={(e) => set("columnId")(e.target.value)}>
                  {sectionColumns.map((c) => <MenuItem key={c.id} value={String(c.id)}>{c.code} — {c.name}</MenuItem>)}
                </Select>
              </FormControl>
            </Stack>

            <FormControl size="small" fullWidth disabled={!method}>
              <InputLabel>Reference standard</InputLabel>
              <Select label="Reference standard" value={form.standardId} onChange={(e) => set("standardId")(e.target.value)}>
                {sectionStandards.map((s) => (
                  <MenuItem key={s.id} value={String(s.id)}>{s.materialName} — batch {s.batchNumber} (purity {s.purity}%)</MenuItem>
                ))}
              </Select>
            </FormControl>
            {selectedStandard && (
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                Purity {selectedStandard.purity}% is taken from the standard's lot and saved with the run.
              </Typography>
            )}

            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField size="small" fullWidth type="number" label="Standard weight (mg)" value={form.standardWeightMg} onChange={(e) => set("standardWeightMg")(e.target.value)} />
              <TextField size="small" fullWidth type="number" label="Standard dilution" value={form.standardDilution} onChange={(e) => set("standardDilution")(e.target.value)} />
              <TextField size="small" fullWidth type="number" label="Mean standard peak area" value={form.standardMeanArea} onChange={(e) => set("standardMeanArea")(e.target.value)} />
            </Stack>

            <Typography sx={{ fontWeight: 600, fontSize: 14 }}>Values from the CDS report</Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField size="small" fullWidth type="number" label={criterion("RSD %", method?.sstMaxRsdPercent)} value={form.rsdPercent} onChange={(e) => set("rsdPercent")(e.target.value)}
                helperText={method?.sstMaxRsdPercent != null ? `Max ${method.sstMaxRsdPercent}` : " "} />
              <TextField size="small" fullWidth type="number" label={criterion("Resolution", method?.sstMinResolution)} value={form.resolution} onChange={(e) => set("resolution")(e.target.value)}
                helperText={method?.sstMinResolution != null ? `Min ${method.sstMinResolution}` : " "} />
              <TextField size="small" fullWidth type="number" label={criterion("Tailing factor", method?.sstMaxTailingFactor)} value={form.tailingFactor} onChange={(e) => set("tailingFactor")(e.target.value)}
                helperText={method?.sstMaxTailingFactor != null ? `Max ${method.sstMaxTailingFactor}` : " "} />
              <TextField size="small" fullWidth type="number" label={criterion("Theoretical plates", method?.sstMinTheoreticalPlates)} value={form.theoreticalPlates} onChange={(e) => set("theoreticalPlates")(e.target.value)}
                helperText={method?.sstMinTheoreticalPlates != null ? `Min ${method.sstMinTheoreticalPlates}` : " "} />
            </Stack>
            <TextField size="small" fullWidth multiline rows={2} label="Comment (optional)" value={comment} onChange={(e) => setComment(e.target.value)} />
          </Stack>
        )}
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement="I performed this system suitability run and the values entered match the CDS report."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </>
  );
}
