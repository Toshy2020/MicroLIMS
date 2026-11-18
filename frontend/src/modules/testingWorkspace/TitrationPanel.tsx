import { useCallback, useEffect, useMemo, useState } from "react";
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
import { Trans } from "react-i18next";
import { SignatureDialog } from "../../components/SignatureDialog";
import { CriteriaCard, NumericCell, RegisterTable, ResultSection, VerdictBanner } from "../../components/lab";
import type { CriteriaRow, RegisterColumn, Verdict } from "../../components/lab";
import { UnitEntryGrid, UnitEntryGridColumn } from "../../components/UnitEntryGrid";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { TitrantDueAcknowledgement } from "./TitrantDueAcknowledgement";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import type { ParameterResultDetail, ResultReadingDetail } from "./types/sampleSummaryTypes";
import type {
  RecordTitrationResultRequest,
  TitrantPreparationOption,
  TitrationContext,
  SampleMoistureResult
} from "./types/testWorkflowTypes";
import { formatMoistureLabel, getMissingMoistureMessage } from "./utils/moistureRetentionHelpers";
import { getErrorMessage } from "../../utils/errorMessage";
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

const UNIT_LABELS: Record<string, string> = { Normal: "N", Molar: "M", MgWaterPerMl: "mg H2O/mL" };
const STATE_COLOR: Record<string, "success" | "warning" | "error" | "default"> = {
  Valid: "success",
  BeforeEachUse: "warning",
  Due: "warning",
  NotStandardized: "error"
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
  const { t } = useTranslation("testingWorkspace");
  const selected = options.find((o) => o.preparationId === value);
  return (
    <Stack spacing={1}>
      <FormControl size="small" fullWidth required>
        <InputLabel id={id}>{label}</InputLabel>
        <Select labelId={id} label={label} value={value} onChange={(e) => onChange(String(e.target.value) === "" ? "" : Number(e.target.value))}>
          {options.length === 0 && (
            <MenuItem disabled value="">
              <em>{t("titration.noPrepAvailable")}</em>
            </MenuItem>
          )}
          {options.map((o) => (
            <MenuItem key={o.preparationId} value={o.preparationId} disabled={!o.usable}>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
                <span>
                  <Trans
                    i18nKey="titration.optionFactor"
                    ns="testingWorkspace"
                    values={{ code: o.code, factor: o.factor != null ? o.factor.toFixed(4) : "—" }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </span>
                <Chip size="small" color={STATE_COLOR[o.factorState] ?? "default"} variant="outlined" label={t(`titration.state.${o.factorState}`, { defaultValue: o.factorState })} />
                {!o.usable && o.blockReason && <Typography variant="caption" color="error">{o.blockReason}</Typography>}
              </Stack>
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      {selected && (
        <FormHelperText sx={{ mt: 0 }}>
          {selected.standardizationTemperatureC != null
            ? t("titration.helperFactorTemp", {
                factor: selected.factor != null ? selected.factor.toFixed(4) : "—",
                temp: selected.standardizationTemperatureC
              })
            : t("titration.helperFactor", {
                factor: selected.factor != null ? selected.factor.toFixed(4) : "—"
              })}
        </FormHelperText>
      )}
    </Stack>
  );
}

export function TitrationPanel({ testOrderId, displayName, sampleId, current, onRecorded }: Props) {
  const { t } = useTranslation("testingWorkspace");
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
  const [moisture, setMoisture] = useState<SampleMoistureResult | null>(null);
  const [moistureStatus, setMoistureStatus] = useState<"idle" | "loading" | "loaded" | "error">("idle");
  const [moistureError, setMoistureError] = useState("");
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
        const hasLod = c.specifications.some(
          (s) => s.resultBasis === "PercentDriedBasis" || s.resultBasis === "PercentAnhydrousBasis"
        );
        if (hasLod) {
          setMoistureStatus("loading");
          try {
            const m = await TestWorkflowService.getMoisture(testOrderId);
            if (active) {
              setMoisture(m);
              setMoistureStatus("loaded");
            }
          } catch (err: unknown) {
            if (active) {
              setMoisture(null);
              setMoistureError(getErrorMessage(err, t("titration.moistureLoadFallback")));
              setMoistureStatus("error");
            }
          }
        } else {
          setMoisture(null);
          setMoistureStatus("idle");
        }
        setReps(Array.from({ length: c.replicateCount }, () => ({ weight: "", volume: "" })));
        const onlyLot = c.standardLots.length === 1 ? c.standardLots[0].materialId : "";
        setLotId(onlyLot);
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          t("titration.loadFailed");
        if (active) setError(msg);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [testOrderId, t]);

  const handleRetryMoisture = useCallback(async () => {
    setMoistureStatus("loading");
    try {
      const m = await TestWorkflowService.getMoisture(testOrderId);
      setMoisture(m);
      setMoistureStatus("loaded");
    } catch (err: unknown) {
      setMoisture(null);
      setMoistureError(getErrorMessage(err, t("titration.moistureLoadFallback")));
      setMoistureStatus("error");
    }
  }, [testOrderId, t]);

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
        lossPercent: needsLod && moistureStatus === "loaded" && moisture && moisture.percent >= 0 && moisture.percent < 100 ? moisture.percent : null,
        avgUnitWeightMg: num(avgWt),
        standards
      }
    : null;

  const columns: UnitEntryGridColumn[] = useMemo(
    () => [
      { key: "weight", label: t("titration.sampleWeightCol") },
      { key: "volume", label: t("titration.titrantVolumeCol") },
      ...(ctx?.specifications ?? []).map((s) => ({
        key: `r_${s.specificationId}`,
        label: t("titration.previewHeader", { parameter: s.parameterName, unitSuffix: s.unit ? ` (${s.unit})` : "" }),
        readOnly: true
      }))
    ],
    [ctx, t]
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
  }, [ctx, reps, titrantPrepId, excessPrepId, blank, tempC, moisture, avgWt, standards]);

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
  }, [ctx, reps, titrantPrepId, excessPrepId, blank, tempC, moisture, avgWt, standards]);

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
    if (needsLod && (moistureStatus !== "loaded" || moisture === null || moisture.percent < 0 || moisture.percent >= 100)) return false;
    if (needsAvgWt && !validNum(avgWt)) return false;
    if (isRelative && (lotId === "" || stdRows.length < 1 || stdRows.some((r) => !validNum(r.weightMg) || !validNum(r.titreMl)))) return false;
    if (reps.length !== ctx.replicateCount) return false;
    return reps.every((r) => validNum(r.weight) && validNum(r.volume));
  }, [
    ctx, analysedAt, titrantPrepId, titrant, isResidual, excessPrepId, excess,
    hasDueWarning, dueAcknowledged, dueJustification,
    blank, tempC, needsLod, moistureStatus, moisture, needsAvgWt, avgWt, isRelative, lotId, stdRows, reps
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
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t("titration.recordFailed")
      );
    }
  };

  const criteriaRows: CriteriaRow[] = (ctx?.specifications ?? []).map((s) => ({
    parameter: s.parameterName,
    criterion: s.specLimit || "—",
    unit: s.unit || "%",
    source: t("titration.sourceSpecification")
  }));

  if (showCompleted) {
    const serverStatus = outcome?.status ?? "";
    const headline = `${displayName}: ${outcome?.outcomeSummary ?? current.finalResult ?? t("titration.resultsRecordedFallback")}`;
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
            { key: "index", label: t("titration.replicateCol"), render: (r) => t("titration.repLabel", { index: r.index }) },
            { key: "value1", label: t("titration.sampleWeightCol"), numeric: true, render: (r) => <NumericCell value={r.value1} /> },
            { key: "value2", label: t("titration.titrantVolumeCol"), numeric: true, render: (r) => <NumericCell value={r.value2} /> },
            { key: "value3", label: t("titration.correctedVolumeCol"), numeric: true, render: (r) => <NumericCell value={r.value3} /> },
            { key: "computedValue", label: t("titration.resultCol"), numeric: true, render: (r) => <NumericCell value={r.computedValue} decimals={2} /> }
          ];
          return (
            <ResultSection key={p.id} title={t("titration.recordedTitrationsTitle", { parameter: p.parameterName })}>
              <RegisterTable columns={cols} rows={readings} getRowId={(r) => r.id ?? `${idx}-${r.index}`} empty={{ title: t("titration.noTitrationsRecorded") }} />
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
    return <Alert severity="error">{error ?? t("titration.configUnavailable")}</Alert>;
  }

  let step = 1;
  const typeLabel = t(`titration.type.${ctx.titrationType}`, { defaultValue: ctx.titrationType });

  return (
    <Box>
      {current.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason
            ? t("titration.returnedWithReason", { reason: current.returnInfo.reason })
            : t("titration.returnedDefault")}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Stack spacing={2.5}>
        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

        <ResultSection step={step++} title={t("titration.methodHeading")}>
          <Stack useFlexGap direction="row" spacing={1} sx={{ flexWrap: "wrap", mb: 1.5 }}>
            <Chip
              size="small"
              variant="outlined"
              label={ctx.nonAqueous ? t("titration.nonAqueousSuffix", { type: typeLabel }) : typeLabel}
            />
            <Chip size="small" variant="outlined" label={ctx.mode === "Residual" ? t("titration.modeResidual") : t("titration.modeDirect")} />
            <Chip size="small" variant="outlined" label={ctx.calculation === "Relative" ? t("titration.calcRelative") : t("titration.calcUspFactor")} />
            <Chip
              size="small"
              variant="outlined"
              label={
                ctx.endpoint === "Visual"
                  ? ctx.indicator
                    ? t("titration.endpointVisualIndicator", { indicator: ctx.indicator })
                    : t("titration.endpointVisual")
                  : t("titration.endpointPotentiometric")
              }
            />
            <Chip size="small" variant="outlined" label={t("titration.replicatesChip", { count: ctx.replicateCount })} />
            {ctx.maxRsdPercent != null && <Chip size="small" variant="outlined" label={t("titration.maxRsdChip", { value: ctx.maxRsdPercent })} />}
          </Stack>
          <Stack useFlexGap direction="row" spacing={3} sx={{ flexWrap: "wrap" }}>
            <Box>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("titration.titrantLabel")}</Typography>
              <Typography variant="body2" sx={{ fontWeight: 600 }}>
                <Ltr>{ctx.titrant.name} · {ctx.titrant.nominalStrength} {UNIT_LABELS[ctx.titrant.strengthUnit] ?? ctx.titrant.strengthUnit}</Ltr>
              </Typography>
            </Box>
            {ctx.calculation === "UspFactor" && ctx.titrationType !== "KarlFischer" && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("titration.equivalencyFactorLabel")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Trans
                    i18nKey="titration.mgPerMeq"
                    ns="testingWorkspace"
                    values={{ value: ctx.equivalencyFactor ?? "—" }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </Typography>
              </Box>
            )}
            {isResidual && ctx.excessTitrant && (
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>{t("titration.excessTitrantLabel")}</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  <Ltr>{ctx.excessTitrant.name} · {ctx.excessVolumeMl} mL</Ltr>
                </Typography>
              </Box>
            )}
          </Stack>
        </ResultSection>

        <ResultSection step={step++} title={t("titration.setupHeading")}>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              type="datetime-local"
              label={t("titration.analysisTime")}
              value={analysedAt}
              onChange={(e) => setAnalysedAt(e.target.value)}
              slotProps={{ inputLabel: { shrink: true }, htmlInput: { dir: "ltr" } }}
              required
              fullWidth
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="titration-equipment-label">{t("titration.equipmentLabel")}</InputLabel>
              <Select
                labelId="titration-equipment-label"
                label={t("titration.equipmentLabel")}
                value={equipmentId}
                onChange={(e) => setEquipmentId(String(e.target.value) === "" ? "" : Number(e.target.value))}
              >
                <MenuItem value="">
                  <em>{t("titration.noInstrument")}</em>
                </MenuItem>
                {fpEquipment.map((eq) => (
                  <MenuItem key={eq.id} value={eq.id}>
                    {eq.name} (<Ltr>{eq.code}</Ltr>)
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <PreparationPicker
              id="titration-titrant-prep"
              label={t("titration.titrantPrepLabel")}
              options={ctx.titrantPreparations}
              value={titrantPrepId}
              onChange={setTitrantPrepId}
            />
            {isResidual && (
              <PreparationPicker
                id="titration-excess-prep"
                label={t("titration.excessPrepLabel")}
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
                label={t("titration.tempLabel")}
                value={tempC}
                onChange={(e) => setTempC(e.target.value)}
                helperText={
                  titrant?.standardizationTemperatureC != null
                    ? t("titration.tempStandardizedAt", { temp: titrant.standardizationTemperatureC })
                    : t("titration.tempStandardizedFallback")
                }
                slotProps={{ htmlInput: { step: "any", dir: "ltr" } }}
                required
                fullWidth
              />
            )}
            {needsLod && (
              moistureStatus === "loading" ? (
                <Typography sx={{ fontSize: 13, color: "text.secondary", py: 0.5 }}>
                  {t("titration.loadingMoisture")}
                </Typography>
              ) : moistureStatus === "error" ? (
                <Alert
                  severity="error"
                  action={
                    <Button color="inherit" size="small" onClick={handleRetryMoisture}>
                      {t("titration.retry")}
                    </Button>
                  }
                  sx={{ width: "100%", fontSize: 13, py: 0.5 }}
                >
                  {moistureError || t("titration.moistureLoadFallback")}
                </Alert>
              ) : moisture ? (
                <Box sx={{ width: "100%" }}>
                  <Typography sx={{ fontSize: 13, color: "text.primary", py: 0.5, fontWeight: 500 }}>
                    {formatMoistureLabel(moisture)}
                  </Typography>
                  {(moisture.percent < 0 || moisture.percent >= 100) && (
                    <Alert severity="error" sx={{ width: "100%", fontSize: 13, py: 0.5, mt: 0.5 }}>
                      {t("titration.moistureOutOfRange")}
                    </Alert>
                  )}
                </Box>
              ) : (
                <Alert severity="warning" sx={{ width: "100%", fontSize: 13, py: 0.5 }}>
                  {getMissingMoistureMessage(ctx?.specifications ?? [])}
                </Alert>
              )
            )}
            {needsAvgWt && (
              <TextField
                size="small"
                type="number"
                label={t("titration.avgUnitWeightLabel")}
                value={avgWt}
                onChange={(e) => setAvgWt(e.target.value)}
                slotProps={{ htmlInput: { step: "any", min: 0, dir: "ltr" } }}
                required
                fullWidth
              />
            )}
          </Box>
        </ResultSection>

        {ctx.blankRequired && (
          <ResultSection step={step++} title={t("titration.blankHeading")}>
            <TextField
              size="small"
              type="number"
              label={t("titration.blankVolumeLabel")}
              value={blank}
              onChange={(e) => setBlank(e.target.value)}
              slotProps={{ htmlInput: { step: "any", min: 0, dir: "ltr" } }}
              required
              sx={{ maxWidth: 260 }}
            />
          </ResultSection>
        )}

        {isRelative && (
          <ResultSection step={step++} title={t("titration.standardsHeading")}>
            <Stack spacing={1.5}>
              <FormControl size="small" fullWidth required>
                <InputLabel id="titration-lot-label">{t("titration.standardLotLabel")}</InputLabel>
                <Select labelId="titration-lot-label" label={t("titration.standardLotLabel")} value={lotId} onChange={(e) => setLotId(String(e.target.value) === "" ? "" : Number(e.target.value))}>
                  {ctx.standardLots.length === 0 && (
                    <MenuItem disabled value="">
                      <em>{t("titration.noStandardLot")}</em>
                    </MenuItem>
                  )}
                  {ctx.standardLots.map((l) => (
                    <MenuItem key={l.materialId} value={l.materialId}>
                      {t("titration.standardLotOption", {
                        lot: l.lotLabel,
                        kind: l.kind === "WorkingStandard" ? t("titration.workingStandard") : t("titration.referenceStandard"),
                        purity: l.purityPercent,
                        moisturePart: l.moisturePercent != null ? t("titration.moisturePart", { percent: l.moisturePercent }) : "",
                        qty: l.quantityRemaining,
                        unit: l.unit
                      })}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {stdRows.map((r, i) => (
                <Stack key={i} direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
                  <Typography variant="body2" sx={{ width: 56 }}>{t("titration.stdLabel", { index: i + 1 })}</Typography>
                  <TextField
                    size="small"
                    type="number"
                    label={t("titration.standardWeightLabel")}
                    value={r.weightMg}
                    onChange={(e) => setStdRows((p) => p.map((x, j) => (j === i ? { ...x, weightMg: e.target.value } : x)))}
                    slotProps={{ htmlInput: { step: "any", min: 0, dir: "ltr" } }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label={t("titration.titreLabel")}
                    value={r.titreMl}
                    onChange={(e) => setStdRows((p) => p.map((x, j) => (j === i ? { ...x, titreMl: e.target.value } : x)))}
                    slotProps={{ htmlInput: { step: "any", min: 0, dir: "ltr" } }}
                  />
                  <IconButton
                    size="small"
                    aria-label={t("titration.removeStandardAria", { index: i + 1 })}
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
                  {t("titration.addStandardBtn")}
                </Button>
              </Box>
            </Stack>
          </ResultSection>
        )}

        <ResultSection step={step} title={t("titration.sampleTitrationsTitle", { count: ctx.replicateCount })}>
          <UnitEntryGrid
            rowCount={ctx.replicateCount}
            rowLabel={(i) => t("titration.repLabel", { index: i + 1 })}
            columns={columns}
            values={reps}
            onChange={setReps}
            readOnlyValues={previews}
          />
          <Box sx={{ mt: 1.5 }}>
            {summaries.map(({ spec, stats, complete }) => (
              <Typography key={spec.specificationId} variant="body2" sx={{ color: "text.secondary" }}>
                {t("titration.summaryPreview", {
                  parameter: spec.parameterName,
                  mean: complete && stats ? fmt(stats.mean) : "—",
                  rsdPart: complete && stats?.rsd != null ? t("titration.summaryRsdPart", { rsd: fmt(stats.rsd) }) : "",
                  warningPart:
                    complete && stats?.rsd != null && ctx.maxRsdPercent != null && stats.rsd > ctx.maxRsdPercent
                      ? t("titration.summaryRsdAboveLimit", { max: ctx.maxRsdPercent })
                      : ""
                })}
              </Typography>
            ))}
          </Box>
        </ResultSection>

        <TextField size="small" label={t("titration.commentOptional")} value={comment} onChange={(e) => setComment(e.target.value)} multiline rows={2} fullWidth />

        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button
            variant="contained"
            disabled={!isFormValid}
            onClick={() => setSigning(true)}
            sx={{ minWidth: 140, fontWeight: 700, textTransform: "none" }}
          >
            {t("titration.signAndRecord")}
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
        meaningStatementTranslation={
          hasDueWarning ? t("titration.signatureMeaningDue") : t("titration.signatureMeaning")
        }
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
