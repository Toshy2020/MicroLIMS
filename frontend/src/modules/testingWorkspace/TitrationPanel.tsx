import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  FormHelperText,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import { SignatureDialog } from "../../components/SignatureDialog";
import { CriteriaCard, NumericCell, RegisterTable, ResultSection, VerdictBanner } from "../../components/lab";
import type { CriteriaRow, RegisterColumn, Verdict } from "../../components/lab";
import { UnitEntryGrid, UnitEntryGridColumn } from "../../components/UnitEntryGrid";
import { TitrantDueAcknowledgement } from "./TitrantDueAcknowledgement";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import type { ParameterResultDetail, ResultReadingDetail } from "./types/sampleSummaryTypes";
import type {
  RecordTitrationResultRequest,
  TitrantPreparationOption,
  TitrationContext
} from "./types/testWorkflowTypes";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";
import { meanAndRsd, previewResult, PreviewInput } from "./titrationPreview";

interface Props {
  testOrderId: number;
  displayName: string;
  testCode?: string;
  itemId?: number | null;
  sampleId?: number | null;
  current: {
    workflowType?: string;
    testName?: string;
    allStepsComplete?: boolean;
    finalResult?: string | null;
    returnInfo?: { reason?: string | null } | null;
    [key: string]: unknown;
  };
  onRecorded: () => Promise<void> | void;
  onClose?: () => void;
}

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

const TYPE_LABELS: Record<string, string> = {
  AcidBase: "Acid-base",
  Redox: "Redox",
  Complexometric: "Complexometric",
  Precipitation: "Precipitation",
  KarlFischer: "Karl Fischer (volumetric)"
};
const UNIT_LABELS: Record<string, string> = { Normal: "N", Molar: "M", MgWaterPerMl: "mg H2O/mL" };
const STATE_COLOR: Record<string, "success" | "warning" | "error" | "default"> = {
  Valid: "success",
  BeforeEachUse: "warning",
  Due: "warning",
  NotStandardized: "error"
};
const STATE_LABEL: Record<string, string> = {
  Valid: "Valid",
  BeforeEachUse: "Before each use",
  Due: "Due",
  NotStandardized: "Not standardized"
};

const num = (s: string): number | null => (s.trim() !== "" && !isNaN(Number(s)) ? Number(s) : null);
const validNum = (s: string, positive = true) => {
  const n = num(s);
  return n !== null && (positive ? n > 0 : n >= 0);
};
const fmt = (n: number | null | undefined, d = 2) => (n == null ? "—" : n.toFixed(d));

type StdRow = { weightMg: string; titreMl: string };

function PreparationPicker({
  id,
  label,
  options,
  value,
  onChange
}: {
  id: string;
  label: string;
  options: TitrantPreparationOption[];
  value: number | "";
  onChange: (v: number | "") => void;
}) {
  const selected = options.find((o) => o.preparationId === value);
  return (
    <Stack spacing={1}>
      <FormControl size="small" fullWidth required>
        <InputLabel id={id}>{label}</InputLabel>
        <Select labelId={id} label={label} value={value} onChange={(e) => onChange(String(e.target.value) === "" ? "" : Number(e.target.value))}>
          {options.length === 0 && (
            <MenuItem disabled value="">
              <em>No prepared, in-date preparation available</em>
            </MenuItem>
          )}
          {options.map((o) => (
            <MenuItem key={o.preparationId} value={o.preparationId} disabled={!o.usable}>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
                <span>
                  {o.code} {"·"} factor {o.factor != null ? o.factor : "—"}
                </span>
                <Chip size="small" color={STATE_COLOR[o.factorState] ?? "default"} variant="outlined" label={STATE_LABEL[o.factorState] ?? o.factorState} />
                {!o.usable && o.blockReason && <Typography variant="caption" color="error">{o.blockReason}</Typography>}
              </Stack>
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      {selected && (
        <FormHelperText sx={{ mt: 0 }}>
          Factor {selected.factor ?? "—"}
          {selected.standardizationTemperatureC != null ? ` · standardized at ${selected.standardizationTemperatureC} °C` : ""}
        </FormHelperText>
      )}
    </Stack>
  );
}

export function TitrationPanel({ testOrderId, displayName, sampleId, current, onRecorded }: Props) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [ctx, setCtx] = useState<TitrationContext | null>(null);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [equipmentId, setEquipmentId] = useState<number | "">("");

  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [titrantPrepId, setTitrantPrepId] = useState<number | "">("");
  const [excessPrepId, setExcessPrepId] = useState<number | "">("");
  const [dueAcknowledged, setDueAcknowledged] = useState(false);
  const [dueJustification, setDueJustification] = useState("");
  const [blank, setBlank] = useState("");
  const [tempC, setTempC] = useState("");
  const [lod, setLod] = useState("");
  const [avgWt, setAvgWt] = useState("");
  const [lotId, setLotId] = useState<number | "">("");
  const [stdRows, setStdRows] = useState<StdRow[]>([{ weightMg: "", titreMl: "" }]);
  const [reps, setReps] = useState<Record<string, string>[]>([]);
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string } | null>(null);
  const [recorded, setRecorded] = useState<ParameterResultDetail[]>([]);

  const showCompleted = Boolean(current.allStepsComplete || outcome);

  useEffect(() => {
    if (!showCompleted || sampleId == null) return;
    let active = true;
    SampleSummaryService.getSummary(sampleId)
      .then((summary) => {
        const ord = summary.testOrders?.find((t) => t.testOrderId === testOrderId);
        if (active) setRecorded(ord?.analysis?.parameterResults ?? []);
      })
      .catch(() => {
        // non-blocking: the completed view still shows the headline result
      });
    return () => {
      active = false;
    };
  }, [showCompleted, sampleId, testOrderId]);

  useEffect(() => {
    let active = true;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const c = await TestWorkflowService.getTitrationContext(testOrderId);
        let instruments: ConfiguredEquipmentSummary[] = [];
        try {
          const [sections, allEquip] = await Promise.all([getSections(), EquipmentConfigurationService.getConfiguredSummary()]);
          const fpSec = (sections ?? []).find((s: LaboratorySection) => s.sectionCode === "FP");
          instruments = (allEquip ?? []).filter(
            (e) => (fpSec && e.sectionId === fpSec.sectionId) || e.section?.code === "FP" || e.section?.sectionCode === "FP"
          );
        } catch {
          // equipment is optional
        }
        if (!active) return;
        setCtx(c);
        setFpEquipment(instruments);
        setReps(Array.from({ length: c.replicateCount }, () => ({ weight: "", volume: "" })));
        const onlyLot = c.standardLots.length === 1 ? c.standardLots[0].materialId : "";
        setLotId(onlyLot);
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          "Could not load the titration configuration.";
        if (active) setError(msg);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [testOrderId]);

  const isRelative = ctx?.calculation === "Relative";
  const isResidual = ctx?.mode === "Residual";
  const needsLod = Boolean(ctx?.specifications.some((s) => s.resultBasis === "PercentDriedBasis" || s.resultBasis === "PercentAnhydrousBasis"));
  // Average unit weight is needed for label claim / mg per unit in both methods.
  const needsAvgWt = Boolean(ctx?.specifications.some((s) => s.resultBasis === "PercentLabelClaim" || s.resultBasis === "MgPerUnit"));
  const titrant = ctx?.titrantPreparations.find((p) => p.preparationId === titrantPrepId) ?? null;
  const excess = ctx?.excessPreparations.find((p) => p.preparationId === excessPrepId) ?? null;

  const warningMessages = useMemo(() => {
    const msgs: string[] = [];
    if (titrant?.warning) msgs.push(titrant.warning);
    if (isResidual && excess?.warning) msgs.push(excess.warning);
    return Array.from(new Set(msgs));
  }, [titrant, isResidual, excess]);

  const hasDueWarning = warningMessages.length > 0;

  const standards = useMemo(
    () =>
      lotId === ""
        ? []
        : stdRows
            .filter((r) => validNum(r.weightMg) && validNum(r.titreMl))
            .map((r) => ({ materialId: Number(lotId), weightMg: Number(r.weightMg), titreMl: Number(r.titreMl) })),
    [lotId, stdRows]
  );

  const previewInput: PreviewInput | null = ctx
    ? {
        ctx,
        titrant,
        excess,
        blankMl: num(blank),
        titrationTempC: num(tempC),
        lossPercent: num(lod),
        avgUnitWeightMg: num(avgWt),
        standards
      }
    : null;

  const columns: UnitEntryGridColumn[] = useMemo(
    () => [
      { key: "weight", label: "Sample weight (mg)" },
      { key: "volume", label: "Titrant volume (mL)" },
      ...(ctx?.specifications ?? []).map((s) => ({
        key: `r_${s.specificationId}`,
        label: `${s.parameterName} preview${s.unit ? ` (${s.unit})` : ""}`,
        readOnly: true
      }))
    ],
    [ctx]
  );

  const previews = useMemo(() => {
    if (!ctx || !previewInput) return [];
    return reps.map((r) => {
      const out: Record<string, string> = {};
      const w = num(r.weight);
      const v = num(r.volume);
      for (const s of ctx.specifications) {
        const val = w != null && v != null ? previewResult(previewInput, s, w, v) : null;
        out[`r_${s.specificationId}`] = val == null ? "—" : val.toFixed(2);
      }
      return out;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ctx, reps, titrantPrepId, excessPrepId, blank, tempC, lod, avgWt, standards]);

  const summaries = useMemo(() => {
    if (!ctx || !previewInput) return [];
    return ctx.specifications.map((s) => {
      const vals = reps
        .map((r) => {
          const w = num(r.weight);
          const v = num(r.volume);
          return w != null && v != null ? previewResult(previewInput, s, w, v) : null;
        })
        .filter((x): x is number => x != null);
      return { spec: s, complete: vals.length === reps.length, stats: vals.length ? meanAndRsd(vals) : null };
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ctx, reps, titrantPrepId, excessPrepId, blank, tempC, lod, avgWt, standards]);

  const isFormValid = useMemo(() => {
    if (!ctx) return false;
    const t = new Date(analysedAt);
    if (isNaN(t.getTime()) || t.getTime() > Date.now() + 5 * 60 * 1000) return false;
    if (titrantPrepId === "" || !titrant?.usable) return false;
    if (isResidual && (excessPrepId === "" || !excess?.usable)) return false;
    if (hasDueWarning) {
      if (!dueAcknowledged) return false;
      const trimmedJustification = dueJustification.trim();
      if (trimmedJustification.length < 10 || dueJustification.length > 500) return false;
    }
    if (ctx.blankRequired && !validNum(blank, false)) return false;
    if (ctx.tempCorrection && num(tempC) === null) return false;
    if (needsLod && !(num(lod) !== null && num(lod)! >= 0 && num(lod)! < 100)) return false;
    if (needsAvgWt && !validNum(avgWt)) return false;
    if (isRelative && (lotId === "" || stdRows.length < 1 || stdRows.some((r) => !validNum(r.weightMg) || !validNum(r.titreMl)))) return false;
    if (reps.length !== ctx.replicateCount) return false;
    return reps.every((r) => validNum(r.weight) && validNum(r.volume));
  }, [
    ctx, analysedAt, titrantPrepId, titrant, isResidual, excessPrepId, excess,
    hasDueWarning, dueAcknowledged, dueJustification,
    blank, tempC, needsLod, lod, needsAvgWt, avgWt, isRelative, lotId, stdRows, reps
  ]);

  const submit = async (password: string) => {
    if (!ctx) return;
    setError(null);
    try {
      const payload: RecordTitrationResultRequest = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: equipmentId !== "" ? Number(equipmentId) : null,
        titrantPreparationId: Number(titrantPrepId),
        excessPreparationId: isResidual && excessPrepId !== "" ? Number(excessPrepId) : null,
        blankVolumeMl: ctx.blankRequired ? num(blank) : null,
        titrationTemperatureC: ctx.tempCorrection ? num(tempC) : null,
        lossOnDryingPercent: needsLod ? num(lod) : null,
        averageUnitWeightMg: needsAvgWt ? num(avgWt) : null,
        standards: isRelative
          ? stdRows.map((r) => ({ materialId: Number(lotId), weightMg: Number(r.weightMg), titreMl: Number(r.titreMl) }))
          : null,
        specificationIds: ctx.specifications.map((s) => s.specificationId),
        replicates: reps.map((r) => ({ sampleWeightMg: Number(r.weight), titrantVolumeMl: Number(r.volume) })),
        dueTitrantAcknowledged: hasDueWarning ? Boolean(dueAcknowledged) : null,
        dueTitrantJustification: hasDueWarning ? dueJustification.trim() : null,
        password,
        comment: comment.trim() || null
      };
      const res = await TestWorkflowService.recordTitrationResult(testOrderId, payload);
      setSigning(false);
      setOutcome({ outcomeSummary: res?.outcomeSummary ?? res?.finalResult ?? undefined, status: res?.status ?? undefined });
      await onRecorded();
    } catch (err: unknown) {
      setSigning(false);
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "Failed to record titration result."
      );
    }
  };

  const criteriaRows: CriteriaRow[] = (ctx?.specifications ?? []).map((s) => ({
    parameter: s.parameterName,
    criterion: s.specLimit || "—",
    unit: s.unit || "%",
    source: "Specification"
  }));

  if (showCompleted) {
    const serverStatus = outcome?.status ?? "";
    const headline = `${displayName}: ${outcome?.outcomeSummary ?? current.finalResult ?? "Results Recorded"}`;
    const verdict: Verdict | null = !serverStatus
      ? null
      : serverStatus.includes("OutOfSpecification")
      ? "Fail"
      : serverStatus.includes("RequiresReview")
      ? "Pending"
      : serverStatus.includes("WithinLimits")
      ? "Pass"
      : "Pending";
    return (
      <Stack spacing={2}>
        {verdict ? (
          <VerdictBanner verdict={verdict} detail={`${headline} (${serverStatus})`} />
        ) : (
          <Typography sx={{ fontWeight: 600 }}>{headline}</Typography>
        )}
        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}
        {recorded.map((p, idx) => {
          const readings = (p.readings ?? []).filter((r) => r.kind === "Titration").sort((a, b) => a.index - b.index);
          if (readings.length === 0) return null;
          const cols: RegisterColumn<ResultReadingDetail>[] = [
            { key: "index", label: "Replicate", render: (r) => `Rep ${r.index}` },
            { key: "value1", label: "Sample weight (mg)", numeric: true, render: (r) => <NumericCell value={r.value1} /> },
            { key: "value2", label: "Titrant volume (mL)", numeric: true, render: (r) => <NumericCell value={r.value2} /> },
            { key: "value3", label: "Corrected volume (mL)", numeric: true, render: (r) => <NumericCell value={r.value3} /> },
            { key: "computedValue", label: "Result", numeric: true, render: (r) => <NumericCell value={r.computedValue} decimals={2} /> }
          ];
          return (
            <ResultSection key={p.id} title={`Recorded titrations · ${p.parameterName}`}>
              <RegisterTable columns={cols} rows={readings} getRowId={(r) => r.id ?? `${idx}-${r.index}`} empty={{ title: "No titrations recorded" }} />
            </ResultSection>
          );
        })}
      </Stack>
    );
  }

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
        <CircularProgress size={32} />
      </Box>
    );
  }

  if (!ctx) {
    return <Alert severity="error">{error ?? "The titration configuration is not available."}</Alert>;
  }

  let step = 1;

  return (
    <Box>
      {current.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason ? `Returned for revision: ${current.returnInfo.reason}` : "Returned by reviewer for revision"}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Stack spacing={2.5}>
        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

        <ResultSection step={step++} title="Method">
          <Stack useFlexGap direction="row" spacing={1} sx={{ flexWrap: "wrap", mb: 1.5 }}>
            <Chip size="small" variant="outlined" label={`${TYPE_LABELS[ctx.titrationType] ?? ctx.titrationType}${ctx.nonAqueous ? " (non-aqueous)" : ""}`} />
            <Chip size="small" variant="outlined" label={ctx.mode === "Residual" ? "Residual (back titration)" : "Direct"} />
            <Chip size="small" variant="outlined" label={ctx.calculation === "Relative" ? "Relative to standard" : "USP factor"} />
            <Chip size="small" variant="outlined" label={ctx.endpoint === "Visual" ? `Visual${ctx.indicator ? `: ${ctx.indicator}` : ""}` : "Potentiometric"} />
            <Chip size="small" variant="outlined" label={`Replicates: ${ctx.replicateCount}`} />
            {ctx.maxRsdPercent != null && <Chip size="small" variant="outlined" label={`Max RSD ${ctx.maxRsdPercent}%`} />}
          </Stack>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Titrant</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                {ctx.titrant.name} {"·"} {ctx.titrant.nominalStrength} {UNIT_LABELS[ctx.titrant.strengthUnit] ?? ctx.titrant.strengthUnit}
              </Typography>
            </Box>
            {ctx.calculation === "UspFactor" && ctx.titrationType !== "KarlFischer" && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equivalency factor F</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{ctx.equivalencyFactor ?? "—"} mg/mEq</Typography>
              </Box>
            )}
            {isResidual && ctx.excessTitrant && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Excess titrant</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {ctx.excessTitrant.name} {"·"} {ctx.excessVolumeMl} mL
                </Typography>
              </Box>
            )}
          </Stack>
        </ResultSection>

        <ResultSection step={step++} title="Analysis set-up">
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              type="datetime-local"
              label="Analysis time"
              value={analysedAt}
              onChange={(e) => setAnalysedAt(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              required
              fullWidth
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="titration-equipment-label">Equipment (titrator)</InputLabel>
              <Select
                labelId="titration-equipment-label"
                label="Equipment (titrator)"
                value={equipmentId}
                onChange={(e) => setEquipmentId(String(e.target.value) === "" ? "" : Number(e.target.value))}
              >
                <MenuItem value="">
                  <em>None (no instrument linked)</em>
                </MenuItem>
                {fpEquipment.map((eq) => (
                  <MenuItem key={eq.id} value={eq.id}>
                    {eq.name} ({eq.code})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <PreparationPicker
              id="titration-titrant-prep"
              label="Titrant preparation"
              options={ctx.titrantPreparations}
              value={titrantPrepId}
              onChange={setTitrantPrepId}
            />
            {isResidual && (
              <PreparationPicker
                id="titration-excess-prep"
                label="Excess titrant preparation"
                options={ctx.excessPreparations}
                value={excessPrepId}
                onChange={setExcessPrepId}
              />
            )}
            {hasDueWarning && (
              <Box sx={{ gridColumn: { xs: "1", sm: "1 / -1" } }}>
                <TitrantDueAcknowledgement
                  warnings={warningMessages}
                  acknowledged={dueAcknowledged}
                  onAcknowledgedChange={setDueAcknowledged}
                  justification={dueJustification}
                  onJustificationChange={setDueJustification}
                />
              </Box>
            )}
            {ctx.tempCorrection && (
              <TextField
                size="small"
                type="number"
                label="Titration temperature (°C)"
                value={tempC}
                onChange={(e) => setTempC(e.target.value)}
                helperText={
                  titrant?.standardizationTemperatureC != null
                    ? `Titrant standardized at ${titrant.standardizationTemperatureC} °C (from the standardization record)`
                    : "Titrant standardization temperature comes from the standardization record"
                }
                slotProps={{ htmlInput: { step: "any" } }}
                required
                fullWidth
              />
            )}
            {needsLod && (
              <TextField
                size="small"
                type="number"
                label="Loss on drying / water (%)"
                value={lod}
                onChange={(e) => setLod(e.target.value)}
                slotProps={{ htmlInput: { step: "any", min: 0, max: 99.99 } }}
                required
                fullWidth
              />
            )}
            {needsAvgWt && (
              <TextField
                size="small"
                type="number"
                label="Average unit weight (mg)"
                value={avgWt}
                onChange={(e) => setAvgWt(e.target.value)}
                slotProps={{ htmlInput: { step: "any", min: 0 } }}
                required
                fullWidth
              />
            )}
          </Box>
        </ResultSection>

        {ctx.blankRequired && (
          <ResultSection step={step++} title="Blank (one per series)">
            <TextField
              size="small"
              type="number"
              label="Blank volume (mL)"
              value={blank}
              onChange={(e) => setBlank(e.target.value)}
              slotProps={{ htmlInput: { step: "any", min: 0 } }}
              required
              sx={{ maxWidth: 260 }}
            />
          </ResultSection>
        )}

        {isRelative && (
          <ResultSection step={step++} title="Reference standard titrations (1-3, same lot)">
            <Stack spacing={1.5}>
              <FormControl size="small" fullWidth required>
                <InputLabel id="titration-lot-label">Standard lot</InputLabel>
                <Select labelId="titration-lot-label" label="Standard lot" value={lotId} onChange={(e) => setLotId(String(e.target.value) === "" ? "" : Number(e.target.value))}>
                  {ctx.standardLots.length === 0 && (
                    <MenuItem disabled value="">
                      <em>No usable standard lot in stock</em>
                    </MenuItem>
                  )}
                  {ctx.standardLots.map((l) => (
                    <MenuItem key={l.materialId} value={l.materialId}>
                      {l.lotLabel} {"·"} {l.kind === "WorkingStandard" ? "Working standard" : "Reference standard"} {"·"} purity {l.purityPercent}%
                      {l.moisturePercent != null ? ` · moisture ${l.moisturePercent}%` : ""} {"·"} {l.quantityRemaining} {l.unit} left
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {stdRows.map((r, i) => (
                <Stack key={i} direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
                  <Typography variant="body2" sx={{ width: 56 }}>Std {i + 1}</Typography>
                  <TextField
                    size="small"
                    type="number"
                    label="Standard weight (mg)"
                    value={r.weightMg}
                    onChange={(e) => setStdRows((p) => p.map((x, j) => (j === i ? { ...x, weightMg: e.target.value } : x)))}
                    slotProps={{ htmlInput: { step: "any", min: 0 } }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Titre (mL)"
                    value={r.titreMl}
                    onChange={(e) => setStdRows((p) => p.map((x, j) => (j === i ? { ...x, titreMl: e.target.value } : x)))}
                    slotProps={{ htmlInput: { step: "any", min: 0 } }}
                  />
                  <IconButton
                    size="small"
                    aria-label={`Remove standard ${i + 1}`}
                    disabled={stdRows.length <= 1}
                    onClick={() => setStdRows((p) => p.filter((_, j) => j !== i))}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Stack>
              ))}
              <Box>
                <Button
                  size="small"
                  startIcon={<AddIcon />}
                  disabled={stdRows.length >= 3}
                  onClick={() => setStdRows((p) => [...p, { weightMg: "", titreMl: "" }])}
                  sx={{ textTransform: "none" }}
                >
                  Add standard titration
                </Button>
              </Box>
            </Stack>
          </ResultSection>
        )}

        <ResultSection step={step} title={`Sample titrations (${ctx.replicateCount} replicate${ctx.replicateCount === 1 ? "" : "s"})`}>
          <UnitEntryGrid
            rowCount={ctx.replicateCount}
            rowLabel={(i) => `Rep ${i + 1}`}
            columns={columns}
            values={reps}
            onChange={setReps}
            readOnlyValues={previews}
          />
          <Box sx={{ mt: 1.5 }}>
            {summaries.map(({ spec, stats, complete }) => (
              <Typography key={spec.specificationId} variant="body2" sx={{ color: "text.secondary" }}>
                Preview {"·"} {spec.parameterName}: mean {complete && stats ? fmt(stats.mean) : "—"}
                {complete && stats?.rsd != null ? ` · RSD ${fmt(stats.rsd)} %` : ""}
                {complete && stats?.rsd != null && ctx.maxRsdPercent != null && stats.rsd > ctx.maxRsdPercent
                  ? ` (above the ${ctx.maxRsdPercent} % limit - the server will flag this for review)`
                  : ""}
                {" "}(display only - the server calculates the recorded result)
              </Typography>
            ))}
          </Box>
        </ResultSection>

        <TextField size="small" label="Comment (optional)" value={comment} onChange={(e) => setComment(e.target.value)} multiline rows={2} fullWidth />

        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button
            variant="contained"
            disabled={!isFormValid}
            onClick={() => setSigning(true)}
            sx={{ minWidth: 140, fontWeight: 700, textTransform: "none" }}
          >
            Sign and record result
          </Button>
        </Box>
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement={
          hasDueWarning
            ? "Your signature records the result and your acknowledgement of the due titrant."
            : "I entered these titration measurements and am recording this test result."
        }
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
