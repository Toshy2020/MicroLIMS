import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Chip, CircularProgress, FormControl, IconButton, InputLabel, MenuItem, Paper, Select,
  Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Tooltip, Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import RefreshIcon from "@mui/icons-material/Refresh";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import { PageHeader } from "../../components/PageHeader";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { tableHeadSx } from "../../theme";
import { useTestDefinitions, TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { masterDataOptions } from "../../services/masterDataOptions";
import { EquipmentConfigurationService } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { ChromatographyColumnService, ChromatographyColumnDto } from "../laboratoryConfiguration/masterDataSimple/services/ChromatographyColumnService";
import { MaterialService } from "../inventory/materials/services/MaterialService";
import { SystemSuitabilityService, SystemSuitabilityRun } from "./services/SystemSuitabilityService";

interface HplcInstrument { id: number; code: string; name: string; sectionId: number; type: string }
interface ReferenceStandard { id: number; materialName: string; batchNumber: string; purity?: number | null; sectionId: number; expiryDate?: string | null }

// One editable row per active TestAnalyte of a StandardComparison method -
// the per-analyte standard, weigh-in, replicate responses and (read-only)
// criteria hints. `responses` holds one string per replicate injection
// (peak area for HPLC, titre in mL for titration) - see SC-5a.
interface AnalyteRunRow {
  testAnalyteId: number;
  element: string;
  wavelengthNm: number;
  sstMaxRsdPercent: number | null;
  sstMinResolution: number | null;
  sstMaxTailingFactor: number | null;
  sstMinTheoreticalPlates: number | null;
  referenceStandardMaterialId: string;
  theoreticalWeightMg: string;
  standardWeightMg: string;
  moisturePercent: string;
  standardDilution: string;
  responses: string[];
  resolution: string;
  tailingFactor: string;
  theoreticalPlates: string;
  blankTitreMl: string;
  weighInJustification: string;
}

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

// Standard weigh-in tolerance - mirrors backend
// SystemSuitabilityService.StandardWeighInTolerancePercent. The backend is
// the authority on pass/fail; this only decides when to prompt for a
// justification in the UI.
const WEIGH_IN_TOLERANCE_PERCENT = 5;

const weighInDeviation = (actualMg: string, theoreticalMg: string): number | null => {
  const act = Number(actualMg);
  const th = Number(theoreticalMg);
  if (!act || !th || th <= 0) return null;
  return ((act - th) / th) * 100;
};

// Sample mean + %RSD (n-1) of the numeric responses entered so far - shown
// live as information only. The backend recomputes this itself from the
// same responses and that computed RSD is what drives pass/fail.
const computeMeanRsd = (responses: string[]): { mean: number | null; rsd: number | null } => {
  const values = responses.map((r) => Number(r)).filter((n) => Number.isFinite(n) && n > 0);
  if (values.length === 0) return { mean: null, rsd: null };
  const mean = values.reduce((a, b) => a + b, 0) / values.length;
  if (values.length < 2 || mean === 0) return { mean, rsd: null };
  const variance = values.reduce((a, b) => a + (b - mean) ** 2, 0) / (values.length - 1);
  const rsd = (Math.sqrt(variance) / mean) * 100;
  return { mean, rsd };
};

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
  const [analyteRows, setAnalyteRows] = useState<AnalyteRunRow[]>([]);

  const sstMethods = useMemo(() => tests.filter((t) => t.requiresSystemSuitability), [tests]);
  const method: TestDefinitionOption | undefined = sstMethods.find((t) => String(t.id) === form.testDefinitionId);
  const methodSectionId = method?.sectionId;
  const isMulti = method?.workflowType === "StandardComparison";
  // Titration standardisation (SC-4/SC-5a): a titrator instead of an HPLC,
  // no chromatography column, and only the RSD criterion applies per analyte.
  const isTitrationRun = isMulti && method?.responseMode === "TitrationVolume";

  // One row per active analyte of the chosen analyte-based method - loaded
  // fresh whenever the method changes (see backend SystemSuitabilityService.
  // CreateAsync: exactly one row per active TestAnalyte, no more, no fewer).
  useEffect(() => {
    if (!dialogOpen) return;
    if (!method || method.workflowType !== "StandardComparison") {
      setAnalyteRows([]);
      return;
    }
    setAnalyteRows([]);
    masterDataOptions
      .getTestAnalytes(method.id)
      .then((all) => {
        const active = all.filter((a) => a.isActive).sort((a, b) => a.displayOrder - b.displayOrder);
        setAnalyteRows(active.map((a) => ({
          testAnalyteId: a.id,
          element: a.element,
          wavelengthNm: a.wavelengthNm,
          sstMaxRsdPercent: a.sstMaxRsdPercent ?? null,
          sstMinResolution: a.sstMinResolution ?? null,
          sstMaxTailingFactor: a.sstMaxTailingFactor ?? null,
          sstMinTheoreticalPlates: a.sstMinTheoreticalPlates ?? null,
          referenceStandardMaterialId: "",
          theoreticalWeightMg: "",
          standardWeightMg: "",
          moisturePercent: "",
          standardDilution: "",
          responses: ["", ""],
          resolution: "",
          tailingFactor: "",
          theoreticalPlates: "",
          blankTitreMl: "",
          weighInJustification: ""
        })));
      })
      .catch(() => setAnalyteRows([]));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dialogOpen, method?.id]);

  const updateAnalyteRow = (idx: number, patch: Partial<AnalyteRunRow>) =>
    setAnalyteRows((rows) => rows.map((r, i) => (i === idx ? { ...r, ...patch } : r)));

  const updateResponse = (rowIdx: number, respIdx: number, value: string) =>
    setAnalyteRows((rows) => rows.map((r, i) =>
      i === rowIdx ? { ...r, responses: r.responses.map((v, j) => (j === respIdx ? value : v)) } : r));

  const addResponseRow = (rowIdx: number) =>
    setAnalyteRows((rows) => rows.map((r, i) => (i === rowIdx ? { ...r, responses: [...r.responses, ""] } : r)));

  const removeResponseRow = (rowIdx: number, respIdx: number) =>
    setAnalyteRows((rows) => rows.map((r, i) =>
      i === rowIdx ? { ...r, responses: r.responses.filter((_, j) => j !== respIdx) } : r));

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
      // Loaded unfiltered (both HPLC instruments and titrators) - which type
      // applies is decided once a method is picked (isTitrationRun below),
      // since titration methods need a titrator instead of an HPLC.
      const [eq, cols, stds] = await Promise.all([
        EquipmentConfigurationService.getEquipmentList(),
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
  const requiredEquipmentType = isTitrationRun ? "Titrator" : "Hplc";
  const sectionInstruments = instruments.filter((i) =>
    (methodSectionId === undefined || i.sectionId === methodSectionId) && i.type === requiredEquipmentType);
  const sectionColumns = columns.filter((c) => methodSectionId === undefined || c.sectionId === methodSectionId);
  const sectionStandards = standards.filter((s) => methodSectionId === undefined || s.sectionId === methodSectionId);
  const selectedStandard = sectionStandards.find((s) => String(s.id) === form.standardId);

  const set = (key: keyof typeof emptyForm) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  const rowReady = (a: AnalyteRunRow) => {
    const validResponses = a.responses.filter((r) => r.trim() !== "" && Number(r) > 0);
    if (!a.referenceStandardMaterialId || !(Number(a.theoreticalWeightMg) > 0) || !(Number(a.standardWeightMg) > 0)) return false;
    if (a.moisturePercent.trim() === "") return false;
    if (validResponses.length === 0) return false;
    const deviation = weighInDeviation(a.standardWeightMg, a.theoreticalWeightMg);
    if (deviation !== null && Math.abs(deviation) > WEIGH_IN_TOLERANCE_PERCENT && !a.weighInJustification.trim()) return false;
    if (isTitrationRun) {
      if (a.blankTitreMl.trim() === "") return false;
    } else if (!(Number(a.standardDilution) > 0)) {
      return false;
    }
    return true;
  };

  const readyToSign = isMulti
    ? !!(form.testDefinitionId && form.equipmentId && (isTitrationRun || form.columnId) && analyteRows.length > 0 &&
        analyteRows.every(rowReady))
    : !!(form.testDefinitionId && form.equipmentId && form.columnId && form.standardId &&
        Number(form.standardWeightMg) > 0 && Number(form.standardDilution) > 0 && Number(form.standardMeanArea) > 0);

  const submit = async (password: string) => {
    const run = isMulti
      ? await SystemSuitabilityService.create({
          testDefinitionId: Number(form.testDefinitionId),
          equipmentId: Number(form.equipmentId),
          chromatographyColumnId: isTitrationRun ? null : Number(form.columnId),
          // Required non-null by the backend request type but not used when
          // analytes are supplied - the first row stands in for them.
          referenceStandardMaterialId: Number(analyteRows[0].referenceStandardMaterialId),
          standardWeightMg: Number(analyteRows[0].standardWeightMg),
          standardDilution: isTitrationRun ? 0 : Number(analyteRows[0].standardDilution),
          standardMeanArea: 0,
          rsdPercent: null,
          resolution: null,
          tailingFactor: null,
          theoreticalPlates: null,
          password,
          comment: comment.trim() || null,
          analytes: analyteRows.map((a) => ({
            testAnalyteId: a.testAnalyteId,
            referenceStandardMaterialId: Number(a.referenceStandardMaterialId),
            standardWeightMg: Number(a.standardWeightMg),
            standardDilution: isTitrationRun ? 0 : Number(a.standardDilution),
            standardMeanArea: 0,
            resolution: isTitrationRun ? null : numOrNull(a.resolution),
            tailingFactor: isTitrationRun ? null : numOrNull(a.tailingFactor),
            theoreticalPlates: isTitrationRun ? null : numOrNull(a.theoreticalPlates),
            theoreticalWeightMg: numOrNull(a.theoreticalWeightMg),
            moisturePercent: numOrNull(a.moisturePercent),
            weighInJustification: a.weighInJustification.trim() || null,
            responses: a.responses.map((r) => Number(r)).filter((n) => Number.isFinite(n) && n > 0),
            blankTitreMl: isTitrationRun ? numOrNull(a.blankTitreMl) : null
          }))
        })
      : await SystemSuitabilityService.create({
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
              {runs.map((r) => {
                const runIsMulti = !!(r.analytes && r.analytes.length > 0);
                return (
                <TableRow key={r.id}>
                  <TableCell sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>{r.code}</TableCell>
                  <TableCell>{r.testName}<Typography sx={{ fontSize: 12, color: "text.secondary" }}>{r.sectionName}</Typography></TableCell>
                  <TableCell>{r.equipmentCode}<Typography sx={{ fontSize: 12, color: "text.secondary" }}>{r.columnCode ?? "—"}</Typography></TableCell>
                  {runIsMulti ? (
                    // Run-level standard/weight/dilution/area/RSD/resolution/tailing/plates
                    // are just the first analyte's snapshot for a multi-analyte run and not
                    // meaningful on their own - show pass/fail per analyte instead.
                    <TableCell colSpan={8}>
                      <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap", gap: 0.5 }}>
                        {r.analytes!.map((a) => {
                          const respLabel = a.responses && a.responses.length > 0
                            ? ` [${a.responses.map((resp) => resp.response).join(", ")}]`
                            : "";
                          const titre = a.blankTitreMl != null ? `, blank titre ${a.blankTitreMl} mL` : "";
                          const weighIn = a.theoreticalWeightMg != null
                            ? `, Th.Wt.std ${a.theoreticalWeightMg}mg (dev ${fmt(a.standardWeighInDeviationPercent)}%${a.standardWeighInOutOfWindow ? " — OUT OF WINDOW" : ""})`
                            : "";
                          const mc = a.moisturePercent != null ? `, MC ${a.moisturePercent}%` : "";
                          const rsd = a.computedRsdPercent != null ? `, computed RSD ${a.computedRsdPercent}%` : "";
                          const justification = a.weighInJustification ? ` — justification: ${a.weighInJustification}` : "";
                          return (
                            <Tooltip
                              key={a.id}
                              title={`${a.analyteName}: wt ${a.standardWeightMg}mg${weighIn}${mc}${respLabel}${rsd}${titre}${a.failureReasons ? ` — ${a.failureReasons}` : ""}${justification}`}
                            >
                              <Chip
                                size="small"
                                color={a.passed ? "success" : "error"}
                                variant={a.passed ? "outlined" : "filled"}
                                label={a.analyteName}
                              />
                            </Tooltip>
                          );
                        })}
                      </Stack>
                    </TableCell>
                  ) : (
                    <>
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
                    </>
                  )}
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
                );
              })}
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
                <InputLabel>{isTitrationRun ? "Titrator" : "HPLC instrument"}</InputLabel>
                <Select label={isTitrationRun ? "Titrator" : "HPLC instrument"} value={form.equipmentId} onChange={(e) => set("equipmentId")(e.target.value)}>
                  {sectionInstruments.map((i) => <MenuItem key={i.id} value={String(i.id)}>{i.code} — {i.name}</MenuItem>)}
                </Select>
              </FormControl>
              {!isTitrationRun && (
                <FormControl size="small" fullWidth disabled={!method}>
                  <InputLabel>Column</InputLabel>
                  <Select label="Column" value={form.columnId} onChange={(e) => set("columnId")(e.target.value)}>
                    {sectionColumns.map((c) => <MenuItem key={c.id} value={String(c.id)}>{c.code} — {c.name}</MenuItem>)}
                  </Select>
                </FormControl>
              )}
            </Stack>
            {isTitrationRun && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Titration runs use a titrator and do not use a chromatography column.
              </Typography>
            )}

            {!isMulti && (
              <FormControl size="small" fullWidth disabled={!method}>
                <InputLabel>Reference standard</InputLabel>
                <Select label="Reference standard" value={form.standardId} onChange={(e) => set("standardId")(e.target.value)}>
                  {sectionStandards.map((s) => (
                    <MenuItem key={s.id} value={String(s.id)}>{s.materialName} — batch {s.batchNumber} (purity {s.purity}%)</MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
            {!isMulti && selectedStandard && (
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                Purity {selectedStandard.purity}% is taken from the standard's lot and saved with the run.
              </Typography>
            )}

            {!isMulti && (
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                <TextField size="small" fullWidth type="number" label="Standard weight (mg)" value={form.standardWeightMg} onChange={(e) => set("standardWeightMg")(e.target.value)} />
                <TextField size="small" fullWidth type="number" label="Standard dilution" value={form.standardDilution} onChange={(e) => set("standardDilution")(e.target.value)} />
                <TextField size="small" fullWidth type="number" label="Mean standard peak area" value={form.standardMeanArea} onChange={(e) => set("standardMeanArea")(e.target.value)} />
              </Stack>
            )}

            {!isMulti && (
              <>
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
              </>
            )}

            {isMulti && (
              <Box>
                <Typography sx={{ fontWeight: 600, fontSize: 14, mb: 1 }}>
                  Per-analyte standards (one card per active analyte, from the CDS report{isTitrationRun ? " / titrator" : ""})
                </Typography>
                {analyteRows.length === 0 && (
                  <Alert severity="warning" sx={{ mb: 1 }}>
                    No active analytes are configured for this test. Configure them in Test Master first.
                  </Alert>
                )}
                <Stack spacing={2}>
                  {analyteRows.map((a, idx) => {
                    const deviation = weighInDeviation(a.standardWeightMg, a.theoreticalWeightMg);
                    const outOfWindow = deviation !== null && Math.abs(deviation) > WEIGH_IN_TOLERANCE_PERCENT;
                    const { mean, rsd } = computeMeanRsd(a.responses);
                    return (
                      <Box key={a.testAnalyteId} sx={{ p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 1 }}>
                        <Typography sx={{ fontWeight: 600, mb: 1 }}>
                          {a.element} <Typography component="span" sx={{ fontSize: 11, color: "text.secondary" }}>({a.wavelengthNm} nm)</Typography>
                        </Typography>

                        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 1.5 }}>
                          <FormControl size="small" fullWidth>
                            <InputLabel>Reference standard</InputLabel>
                            <Select
                              label="Reference standard"
                              value={a.referenceStandardMaterialId}
                              onChange={(e) => updateAnalyteRow(idx, { referenceStandardMaterialId: e.target.value })}
                            >
                              {sectionStandards.map((s) => (
                                <MenuItem key={s.id} value={String(s.id)}>{s.materialName} — {s.batchNumber} (purity {s.purity}%)</MenuItem>
                              ))}
                            </Select>
                          </FormControl>
                          {!isTitrationRun && (
                            <TextField
                              size="small" fullWidth type="number" label="Standard dilution"
                              value={a.standardDilution} onChange={(e) => updateAnalyteRow(idx, { standardDilution: e.target.value })}
                            />
                          )}
                        </Stack>

                        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 0.5 }}>
                          <TextField
                            size="small" fullWidth type="number" label="Th.Wt.std (mg)"
                            value={a.theoreticalWeightMg} onChange={(e) => updateAnalyteRow(idx, { theoreticalWeightMg: e.target.value })}
                            helperText="Method target weighing"
                          />
                          <TextField
                            size="small" fullWidth type="number" label="Act.Wt.std (mg)"
                            value={a.standardWeightMg} onChange={(e) => updateAnalyteRow(idx, { standardWeightMg: e.target.value })}
                            helperText="Actual weighed"
                          />
                          <TextField
                            size="small" fullWidth type="number" label="MC (%)"
                            value={a.moisturePercent} onChange={(e) => updateAnalyteRow(idx, { moisturePercent: e.target.value })}
                            helperText="Working standard moisture - 0 is a valid (dry) value"
                          />
                        </Stack>

                        {deviation !== null && (
                          <Typography variant="caption" sx={{ display: "block", mb: outOfWindow ? 0.5 : 1.5, color: outOfWindow ? "warning.main" : "text.secondary" }}>
                            Weigh-in deviation from Th.Wt.std: {deviation.toFixed(2)}%
                            {outOfWindow ? ` — outside ±${WEIGH_IN_TOLERANCE_PERCENT}%, justification required` : ""}
                          </Typography>
                        )}
                        {outOfWindow && (
                          <TextField
                            size="small" fullWidth multiline rows={1} label="Weigh-in justification (required)"
                            value={a.weighInJustification} onChange={(e) => updateAnalyteRow(idx, { weighInJustification: e.target.value })}
                            sx={{ mb: 1.5 }}
                          />
                        )}

                        {isTitrationRun && (
                          <TextField
                            size="small" fullWidth type="number" label="Blank titre (mL)"
                            value={a.blankTitreMl} onChange={(e) => updateAnalyteRow(idx, { blankTitreMl: e.target.value })}
                            sx={{ mb: 1.5, maxWidth: 220 }}
                          />
                        )}

                        <Typography sx={{ fontSize: 13, fontWeight: 600, mb: 0.5 }}>
                          Standard replicate responses ({isTitrationRun ? "Titre, mL" : "Area"})
                        </Typography>
                        <Stack spacing={1} sx={{ mb: 1 }}>
                          {a.responses.map((resp, respIdx) => (
                            <Stack key={respIdx} direction="row" spacing={1} sx={{ alignItems: "center" }}>
                              <TextField
                                size="small" type="number" sx={{ width: 140 }}
                                label={isTitrationRun ? "Titre (mL)" : "Area"}
                                value={resp}
                                onChange={(e) => updateResponse(idx, respIdx, e.target.value)}
                              />
                              <IconButton
                                size="small"
                                disabled={a.responses.length <= 1}
                                onClick={() => removeResponseRow(idx, respIdx)}
                                title="Remove replicate"
                              >
                                <DeleteIcon fontSize="small" />
                              </IconButton>
                            </Stack>
                          ))}
                          <Button size="small" startIcon={<AddIcon />} onClick={() => addResponseRow(idx)} sx={{ alignSelf: "flex-start" }}>
                            Add replicate
                          </Button>
                        </Stack>
                        <Typography variant="caption" sx={{ display: "block", mb: 1.5, color: "text.secondary" }}>
                          Mean: {mean !== null ? mean.toFixed(2) : "—"} · Computed RSD: {rsd !== null ? `${rsd.toFixed(2)}%` : "—"}
                          {a.sstMaxRsdPercent != null ? ` (max ${a.sstMaxRsdPercent})` : ""} — informational; the server recomputes and decides pass/fail.
                        </Typography>

                        {!isTitrationRun && (
                          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                            <TextField
                              size="small" fullWidth type="number" label="Resolution"
                              value={a.resolution} onChange={(e) => updateAnalyteRow(idx, { resolution: e.target.value })}
                              helperText={a.sstMinResolution != null ? `Min ${a.sstMinResolution}` : "Not checked"}
                            />
                            <TextField
                              size="small" fullWidth type="number" label="Tailing factor"
                              value={a.tailingFactor} onChange={(e) => updateAnalyteRow(idx, { tailingFactor: e.target.value })}
                              helperText={a.sstMaxTailingFactor != null ? `Max ${a.sstMaxTailingFactor}` : "Not checked"}
                            />
                            <TextField
                              size="small" fullWidth type="number" label="Theoretical plates"
                              value={a.theoreticalPlates} onChange={(e) => updateAnalyteRow(idx, { theoreticalPlates: e.target.value })}
                              helperText={a.sstMinTheoreticalPlates != null ? `Min ${a.sstMinTheoreticalPlates}` : "Not checked"}
                            />
                          </Stack>
                        )}
                      </Box>
                    );
                  })}
                </Stack>
              </Box>
            )}
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
