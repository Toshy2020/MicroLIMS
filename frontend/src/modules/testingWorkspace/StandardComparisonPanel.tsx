import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { SignatureDialog } from "../../components/SignatureDialog";
import { StatusBadge } from "../../components/StatusBadge";
import { UnitEntryGrid, UnitEntryGridColumn } from "../../components/UnitEntryGrid";
import { TestWorkflowService, StandardComparisonContext } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";
import { SystemSuitabilityService, SystemSuitabilityRun, SystemSuitabilityRunAnalyteView } from "../systemSuitability/services/SystemSuitabilityService";
import { AnalysisDetail, ParameterResultDetail, TestOrderSummaryDetail } from "./types/sampleSummaryTypes";

interface Props {
  testOrderId: number;
  displayName: string;
  testCode?: string;
  itemId?: number | null;
  sampleId?: number | null;
  current: {
    workflowType?: string;
    testName?: string;
    sampleContext?: {
      sampleName?: string;
      [key: string]: unknown;
    };
    allStepsComplete?: boolean;
    finalResult?: string | null;
    returnInfo?: { reason?: string | null } | null;
  };
  onRecorded: () => Promise<void> | void;
  onClose?: () => void;
}

type ScRunAnalyte = SystemSuitabilityRunAnalyteView;
type RunWithAnalytes = SystemSuitabilityRun & { analytes?: ScRunAnalyte[] | null };

// Mirrors backend StandardComparisonCalculationData (camelCase JSON) -
// StandardComparisonCalculationData.cs. Exported so SampleSummaryDialog's
// "calculated against" summary can parse the same calculationJson shape
// without redeclaring it.
export interface StandardComparisonPreparationData {
  preparationIndex: number;
  theoreticalWeightMg: number;
  actualWeightMg: number;
  weighInDeviationPercent: number;
  weighInOutOfWindow: boolean;
  weighInJustification?: string | null;
  testResponse: number;
  percentAssay: number;
}

export interface StandardComparisonCalculationData {
  analyteName: string;
  testAnalyteId: number;
  systemSuitabilityRunAnalyteId: number;
  standardTheoreticalWeightMg: number;
  standardActualWeightMg: number;
  standardPurityPercent: number;
  moisturePercent: number;
  standardMeanArea: number;
  preparations: StandardComparisonPreparationData[];
  reportedPercentAssay: number;
  preparationRsdPercent?: number | null;
  maxPreparationRsdPercent?: number | null;
  rsdExceeded: boolean;
  reviewReason?: string | null;
  responseMode: string;
  blankTitreMl?: number | null;
}

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

const errorMessage = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

const prepKey = (p: number) => `p${p}`;

function parseCalc(json: string | null): StandardComparisonCalculationData | null {
  if (!json) return null;
  try {
    return JSON.parse(json) as StandardComparisonCalculationData;
  } catch {
    return null;
  }
}

// Server-recorded per-analyte result table shown after signing / when
// re-opening an already-recorded test - every value is the server's stored
// one, not recomputed here.
function ResultsTable({ analysis }: { analysis: AnalysisDetail }) {
  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Analyte</TableCell>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Reported</TableCell>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Specification</TableCell>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Prep. RSD</TableCell>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }} align="center">Status</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {analysis.parameterResults.map((p: ParameterResultDetail) => {
          const calc = parseCalc(p.calculationJson);
          return (
            <TableRow key={p.id}>
              <TableCell sx={{ fontSize: 13, fontWeight: 600 }}>{p.parameterName}</TableCell>
              <TableCell sx={{ fontSize: 13, fontWeight: 700 }}>{p.reportedDisplay}</TableCell>
              <TableCell sx={{ fontSize: 13 }}>{p.specLimit ? `${p.specLimit}${p.unit ? ` ${p.unit}` : ""}` : "—"}</TableCell>
              <TableCell sx={{ fontSize: 13 }}>
                {calc?.preparationRsdPercent != null ? `${calc.preparationRsdPercent.toFixed(2)} %` : "—"}
                {calc?.reviewReason && (
                  <Typography sx={{ fontSize: 11, color: "warning.main", mt: 0.25 }}>{calc.reviewReason}</Typography>
                )}
              </TableCell>
              <TableCell align="center">
                <StatusBadge status={p.comparisonStatus} />
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

// Standard-Comparison Assay result entry (SC-5b, replaces the retired
// HplcAssay/HplcMultiAnalyte screens): link a passed Standard-Comparison
// suitability run, weigh in each sample preparation (Th.Wt.test/Act.Wt.test,
// justified if outside the ±window), enter each analyte's response
// (peak area or titre) per preparation, and sign once. The server computes
// %Assay per analyte/preparation, the preparation RSD, and the reported
// value - nothing here is authoritative, the grids below only preview.
export function StandardComparisonPanel({
  testOrderId,
  displayName,
  testCode,
  itemId,
  sampleId,
  current,
  onRecorded,
  onClose
}: Props) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [context, setContext] = useState<StandardComparisonContext | null>(null);
  const [specs, setSpecs] = useState<SpecificationDto[]>([]);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  const [linked, setLinked] = useState<RunWithAnalytes | null>(null);
  const [selectable, setSelectable] = useState<RunWithAnalytes[]>([]);
  const [runChoice, setRunChoice] = useState<number | "">("");
  const [changingRun, setChangingRun] = useState(false);

  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [preparations, setPreparations] = useState<{ theoreticalWeightMg: string; actualWeightMg: string; weighInJustification: string }[]>([]);
  const [responses, setResponses] = useState<Record<string, string>[]>([]);

  const [activeAnalysis, setActiveAnalysis] = useState<AnalysisDetail | null>(null);

  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string | null } | null>(null);

  const effectiveTestCode = current.testName || testCode || "";

  const isTitration = context?.responseMode === "TitrationVolume";
  const expectedPreps = context?.sampleReplicates ?? 0;
  const weighInTolerance = context?.sampleWeighInTolerancePercent ?? 10;
  const blockingMessage = context?.message ?? null;

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const ctx = await TestWorkflowService.getStandardComparisonContext(testOrderId);
      const preps = ctx.sampleReplicates ?? 0;

      let resolvedItemId = itemId;
      if (resolvedItemId == null) {
        try {
          const allItems = await ItemService.getAll();
          const matched = allItems.find(
            (i) =>
              i.name === current.sampleContext?.sampleName ||
              i.assignedTests?.some((t) => t.testCode === effectiveTestCode)
          );
          resolvedItemId = matched?.id ?? null;
        } catch {
          // fallback
        }
      }

      let matchedSpecs: SpecificationDto[] = [];
      if (resolvedItemId != null) {
        const itemSpecs = await SpecificationService.getForItem(resolvedItemId);
        matchedSpecs = itemSpecs
          .filter((s) => s.testCode === effectiveTestCode && s.testAnalyteId != null)
          .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
      }

      let l: RunWithAnalytes | null = null;
      let s: RunWithAnalytes[] = [];
      try {
        [l, s] = await Promise.all([
          SystemSuitabilityService.getLinkedForTestOrder(testOrderId),
          SystemSuitabilityService.getSelectableForTestOrder(testOrderId)
        ]);
      } catch {
        // non-blocking
      }

      // Analytes to enter = the item's spec'd analytes for this test; fall
      // back to the linked run's analyte rows so the grid still has
      // something to show while specs haven't been configured yet
      // (submission will still fail server-side until they are).
      if (matchedSpecs.length === 0 && l?.analytes?.length) {
        matchedSpecs = l.analytes.map((a) => ({
          testCode: effectiveTestCode,
          parameterName: a.analyteName,
          testAnalyteId: a.testAnalyteId
        }));
      }

      let fpInstruments: ConfiguredEquipmentSummary[] = [];
      try {
        const [sections, allEquip] = await Promise.all([
          getSections(),
          EquipmentConfigurationService.getConfiguredSummary()
        ]);
        const fpSec = (sections ?? []).find((sec: LaboratorySection) => sec.sectionCode === "FP");
        fpInstruments = (allEquip ?? []).filter(
          (e) =>
            (fpSec && e.sectionId === fpSec.sectionId) ||
            e.section?.code === "FP" ||
            e.section?.sectionCode === "FP"
        );
        fpInstruments.sort((a, b) => {
          const aHplc = String(a.type).toUpperCase().includes("HPLC") ? 0 : 1;
          const bHplc = String(b.type).toUpperCase().includes("HPLC") ? 0 : 1;
          return aHplc - bHplc;
        });
      } catch {
        // fallback
      }

      if (sampleId != null) {
        try {
          const summary = await SampleSummaryService.getSummary(sampleId);
          const ord = summary.testOrders?.find((t: TestOrderSummaryDetail) => t.testOrderId === testOrderId) ?? null;
          if (ord?.analysis) {
            setActiveAnalysis(ord.analysis);
          }
        } catch {
          // non-blocking
        }
      }

      setContext(ctx);
      setSpecs(matchedSpecs);
      setFpEquipment(fpInstruments);
      setLinked(l);
      setSelectable(s);
      setRunChoice("");
      setChangingRun(false);

      setPreparations((prev) =>
        prev.length === preps
          ? prev
          : Array.from({ length: preps }, () => ({ theoreticalWeightMg: "", actualWeightMg: "", weighInJustification: "" }))
      );
      setResponses((prev) => {
        if (prev.length === matchedSpecs.length && prev.every((r) => Object.keys(r).length === preps)) return prev;
        return Array.from({ length: matchedSpecs.length }, () => {
          const row: Record<string, string> = {};
          for (let p = 1; p <= preps; p++) row[prepKey(p)] = "";
          return row;
        });
      });
    } catch (err: unknown) {
      setError(errorMessage(err, "Could not load standard-comparison test configuration or specifications."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [testOrderId, effectiveTestCode, itemId, sampleId]);

  const link = async () => {
    if (!runChoice) return;
    setError(null);
    try {
      await SystemSuitabilityService.linkTestOrders(Number(runChoice), [testOrderId]);
      await loadData();
    } catch (e) {
      setError(errorMessage(e, "Could not link this test to the suitability run."));
    }
  };

  const runAnalyteFor = (s: SpecificationDto): ScRunAnalyte | undefined =>
    linked?.analytes?.find((a) => a.testAnalyteId === s.testAnalyteId) ?? undefined;

  // Client-side gate mirroring the server's own rejections for a bad run
  // analyte (RecordStandardComparisonResultAsync) - shown so the analyst
  // isn't surprised on submit, not a substitute for the server check.
  const runAnalyteWarning = (a: ScRunAnalyte | undefined): string | null => {
    if (!a) return "No matching analyte on the linked suitability run.";
    if (!a.passed) return "This analyte did not pass the suitability run.";
    if (a.theoreticalWeightMg == null || a.theoreticalWeightMg <= 0)
      return "Missing theoretical standard weight on the run — this run cannot be used until corrected.";
    if (a.moisturePercent == null) return "Missing moisture % on the run — this run cannot be used until corrected.";
    if (isTitration && a.blankTitreMl == null) return "This run has no blank titre — it cannot be used for a titration test.";
    if (!isTitration && a.blankTitreMl != null) return "This run was recorded as a titration (has a blank titre) — it cannot be used for a peak-area test.";
    return null;
  };

  const allRunAnalytesValid = specs.length > 0 && specs.every((s) => runAnalyteWarning(runAnalyteFor(s)) === null);

  const prepColumns: UnitEntryGridColumn[] = useMemo(
    () => [
      { key: "theoreticalWeightMg", label: "Th.Wt.test", unit: "mg" },
      { key: "actualWeightMg", label: "Act.Wt.test", unit: "mg" }
    ],
    []
  );

  const responseColumns: UnitEntryGridColumn[] = useMemo(
    () =>
      Array.from({ length: expectedPreps }, (_, i) => ({
        key: prepKey(i + 1),
        label: `P${i + 1} ${isTitration ? "Titre (mL)" : "Peak area"}`,
        width: 130
      })),
    [expectedPreps, isTitration]
  );

  const responseRowLabel = (i: number) => specs[i]?.parameterName || `Analyte ${i + 1}`;

  const deviationPercent = (p: { theoreticalWeightMg: string; actualWeightMg: string }): number | null => {
    const th = Number(p.theoreticalWeightMg);
    const act = Number(p.actualWeightMg);
    if (!(th > 0) || !(act > 0) || isNaN(th) || isNaN(act)) return null;
    return ((act - th) / th) * 100;
  };

  const needsJustification = (p: { theoreticalWeightMg: string; actualWeightMg: string }): boolean => {
    const dev = deviationPercent(p);
    return dev !== null && Math.abs(dev) > weighInTolerance;
  };

  const updatePreparation = (i: number, field: "theoreticalWeightMg" | "actualWeightMg" | "weighInJustification", value: string) => {
    setPreparations((prev) => prev.map((p, idx) => (idx === i ? { ...p, [field]: value } : p)));
  };

  const preparationsValid =
    expectedPreps > 0 &&
    preparations.length === expectedPreps &&
    preparations.every((p) => {
      const th = Number(p.theoreticalWeightMg);
      const act = Number(p.actualWeightMg);
      if (!(th > 0) || !(act > 0) || isNaN(th) || isNaN(act)) return false;
      if (needsJustification(p) && !p.weighInJustification.trim()) return false;
      return true;
    });

  const responsesValid =
    expectedPreps > 0 &&
    specs.length > 0 &&
    responses.length === specs.length &&
    responses.every((row) =>
      Array.from({ length: expectedPreps }, (_, i) => prepKey(i + 1)).every((k) => {
        const v = Number(row[k]);
        return row[k]?.trim() !== "" && !isNaN(v) && v > 0;
      })
    );

  const readyToSign =
    !!linked &&
    !blockingMessage &&
    allRunAnalytesValid &&
    specs.length > 0 &&
    analysedAt.trim() !== "" &&
    preparationsValid &&
    responsesValid;

  // Client-side preview only, for the analyst's information - matches the
  // formula in StandardComparisonCalculator.CalculatePreparationAssay. The
  // saved %Assay always comes back from the server response.
  const previewAssay = (a: ScRunAnalyte | undefined, prepIdx: number, responseValue: string): number | null => {
    if (!a || a.theoreticalWeightMg == null || a.moisturePercent == null) return null;
    const p = preparations[prepIdx];
    if (!p) return null;
    const thWtTest = Number(p.theoreticalWeightMg);
    const actWtTest = Number(p.actualWeightMg);
    const respTest = Number(responseValue);
    if (!(thWtTest > 0) || !(actWtTest > 0) || !(respTest > 0) || isNaN(thWtTest) || isNaN(actWtTest) || isNaN(respTest)) return null;

    const actWtStd = a.standardWeightMg;
    const thWtStd = a.theoreticalWeightMg;
    const purity = a.standardPurityPercent;
    const mc = a.moisturePercent;
    let respTestAdj = respTest;
    let respStdAdj = a.standardMeanArea;
    if (isTitration && a.blankTitreMl != null) {
      respTestAdj = respTest - a.blankTitreMl;
      respStdAdj = a.standardMeanArea - a.blankTitreMl;
    }
    if (!(respStdAdj > 0) || !(respTestAdj > 0) || !(actWtStd > 0) || !(thWtStd > 0)) return null;

    return (respTestAdj / respStdAdj) * (actWtStd / thWtStd) * (thWtTest / actWtTest) * ((100 - mc) / 100) * purity;
  };

  const submit = async (password: string) => {
    setError(null);
    try {
      const preparationsPayload = preparations.map((p) => ({
        theoreticalWeightMg: Number(p.theoreticalWeightMg),
        actualWeightMg: Number(p.actualWeightMg),
        weighInJustification: p.weighInJustification.trim() || undefined
      }));

      const responsesPayload: { testAnalyteId: number; preparationIndex: number; response: number }[] = [];
      specs.forEach((s, rowIdx) => {
        for (let p = 1; p <= expectedPreps; p++) {
          responsesPayload.push({
            testAnalyteId: s.testAnalyteId!,
            preparationIndex: p,
            response: Number(responses[rowIdx]?.[prepKey(p)])
          });
        }
      });

      const payload = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: selectedEquipmentId !== "" ? Number(selectedEquipmentId) : null,
        preparations: preparationsPayload,
        responses: responsesPayload,
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordStandardComparisonResult(testOrderId, payload);
      setSigning(false);
      setOutcome({ outcomeSummary: res?.outcomeSummary ?? res?.finalResult ?? undefined, status: res?.status ?? undefined });

      if (sampleId != null) {
        try {
          const summary = await SampleSummaryService.getSummary(sampleId);
          const ord = summary.testOrders.find((t) => t.testOrderId === testOrderId);
          if (ord?.analysis) setActiveAnalysis(ord.analysis);
        } catch {
          // ignore
        }
      }

      await onRecorded();
    } catch (err: unknown) {
      setSigning(false);
      setError(errorMessage(err, "Failed to record standard-comparison result."));
    }
  };

  // Completion view
  if (current.allStepsComplete || outcome) {
    const isOos = (outcome?.status ?? "").includes("OutOfSpecification");
    const isReview = (outcome?.status ?? "").includes("RequiresReview");
    const severity = isOos ? "error" : isReview ? "warning" : "success";

    return (
      <Box>
        <Alert severity={severity} sx={{ mb: 2 }}>
          {displayName}: <strong>{outcome?.outcomeSummary ?? current.finalResult ?? "Results recorded"}</strong>
          {outcome?.status && ` (${outcome.status})`}
        </Alert>
        {linked && <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>Suitability run {linked.code}</Typography>}

        {activeAnalysis && activeAnalysis.parameterResults.length > 0 && (
          <Box sx={{ mb: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5, p: 2 }}>
            <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1 }}>
              Standard-Comparison Assay Results
            </Typography>
            <ResultsTable analysis={activeAnalysis} />
          </Box>
        )}

        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <StatusBadge status="ResultRecorded" label="Result Recorded — Pending Review" />
          {onClose && <Button variant="contained" onClick={onClose}>Done / Close</Button>}
        </Box>
      </Box>
    );
  }

  if (loading) {
    return (
      <Box sx={{ p: 4, textAlign: "center" }}>
        <CircularProgress size={32} />
        <Typography sx={{ mt: 1, fontSize: 13, color: "text.secondary" }}>
          Loading standard-comparison configuration...
        </Typography>
      </Box>
    );
  }

  return (
    <Box>
      {current.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason ? `Returned for revision: ${current.returnInfo.reason}` : "Returned by reviewer for revision"}
        </Alert>
      )}
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
      {blockingMessage && (
        <Alert severity="warning" sx={{ mb: 2 }}>{blockingMessage}</Alert>
      )}
      {specs.length === 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          No specifications configured for this test and item yet — every analyte needs a specification linked
          to a test analyte before a result can be recorded.
        </Alert>
      )}

      <Typography sx={{ fontWeight: 700, mb: 1 }}>1. System suitability run</Typography>
      {linked && !changingRun ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1, flexWrap: "wrap" }}>
          <Chip color="success" label={`${linked.code} · Passed`} />
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {linked.equipmentCode} / {linked.columnCode} · {linked.analytes?.length ?? 0} analyte row(s)
          </Typography>
          {selectable.length > 1 && <Button size="small" onClick={() => setChangingRun(true)}>Change</Button>}
        </Stack>
      ) : (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 1 }}>
          <Select size="small" displayEmpty value={runChoice} onChange={(e) => setRunChoice(e.target.value as number)} sx={{ minWidth: 320 }}>
            <MenuItem value="" disabled>{selectable.length ? "Choose a passed run" : "No passed run for this method yet"}</MenuItem>
            {selectable.map((r) => (
              <MenuItem key={r.id} value={r.id}>
                {r.code} — {r.equipmentCode}, {new Date(r.performedAt).toLocaleDateString()} ({r.analytes?.length ?? 0} analytes)
              </MenuItem>
            ))}
          </Select>
          <Button variant="contained" disabled={!runChoice} onClick={link}>Link</Button>
          {changingRun && <Button onClick={() => setChangingRun(false)}>Cancel</Button>}
        </Stack>
      )}
      {!linked && selectable.length === 0 && (
        <Alert severity="info" sx={{ mb: 2 }}>
          Perform a passing System Suitability run for this method first (Laboratory → System Suitability).
        </Alert>
      )}

      {linked && specs.length > 0 && (
        <Box sx={{ mb: 2.5, border: "1px solid", borderColor: "divider", borderRadius: 1.5, p: 1.5 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 1 }}>Linked run — standard values (read-only)</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Analyte</TableCell>
                <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Th.Wt.std</TableCell>
                <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Act.Wt.std</TableCell>
                <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>P (purity %)</TableCell>
                <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>MC %</TableCell>
                <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Mean std {isTitration ? "titre" : "response"}</TableCell>
                <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Computed RSD</TableCell>
                {isTitration && <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Blank titre</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {specs.map((s) => {
                const a = runAnalyteFor(s);
                const warning = runAnalyteWarning(a);
                return (
                  <TableRow key={s.testAnalyteId}>
                    <TableCell sx={{ fontSize: 12, fontWeight: 600 }}>
                      {s.parameterName}
                      {warning && <Typography sx={{ fontSize: 11, color: "error.main" }}>{warning}</Typography>}
                    </TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{a?.theoreticalWeightMg ?? "—"}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{a?.standardWeightMg ?? "—"}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{a?.standardPurityPercent ?? "—"}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{a?.moisturePercent ?? "—"}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{a?.standardMeanArea ?? "—"}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>{a?.computedRsdPercent != null ? `${a.computedRsdPercent.toFixed(2)} %` : "—"}</TableCell>
                    {isTitration && <TableCell sx={{ fontSize: 12 }}>{a?.blankTitreMl ?? "—"}</TableCell>}
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Box>
      )}

      <Typography sx={{ fontWeight: 700, mb: 1, color: linked ? "text.primary" : "text.disabled" }}>
        2. Sample preparations & responses
      </Typography>
      <Stack spacing={2} sx={{ opacity: linked ? 1 : 0.5, pointerEvents: linked ? "auto" : "none", mb: 2.5 }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <Select
            size="small"
            displayEmpty
            value={selectedEquipmentId}
            onChange={(e) => setSelectedEquipmentId(e.target.value as number | "")}
            sx={{ flex: 1.5 }}
          >
            <MenuItem value=""><em>Select instrument (optional)</em></MenuItem>
            {fpEquipment.map((eq) => (
              <MenuItem key={eq.id} value={eq.id}>{eq.code} — {eq.name} ({eq.type})</MenuItem>
            ))}
          </Select>
          <TextField
            size="small"
            type="datetime-local"
            label="Analysis Time (Local) *"
            value={analysedAt}
            onChange={(e) => setAnalysedAt(e.target.value)}
            slotProps={{ inputLabel: { shrink: true } }}
            required
            sx={{ flex: 1 }}
          />
        </Stack>

        {expectedPreps > 0 && (
          <Box>
            <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
              Sample preparations ({expectedPreps}) — weigh-in window ±{weighInTolerance}%
            </Typography>
            <UnitEntryGrid
              rowCount={expectedPreps}
              rowLabel={(i) => `Preparation ${i + 1}`}
              columns={prepColumns}
              values={preparations}
              onChange={(vals) =>
                setPreparations((prev) =>
                  vals.map((v, idx) => ({
                    theoreticalWeightMg: v.theoreticalWeightMg ?? "",
                    actualWeightMg: v.actualWeightMg ?? "",
                    weighInJustification: prev[idx]?.weighInJustification ?? ""
                  }))
                )
              }
            />
            <Stack spacing={1} sx={{ mt: 1 }}>
              {preparations.map((p, i) => {
                const dev = deviationPercent(p);
                const outOfWindow = needsJustification(p);
                if (dev === null) return null;
                return (
                  <Box key={i}>
                    <Typography sx={{ fontSize: 12, color: outOfWindow ? "warning.main" : "text.secondary" }}>
                      Preparation {i + 1} deviation: {dev.toFixed(2)}%{outOfWindow ? " — outside window, justification required" : ""}
                    </Typography>
                    {outOfWindow && (
                      <TextField
                        size="small"
                        fullWidth
                        label={`Justification — preparation ${i + 1} *`}
                        value={p.weighInJustification}
                        onChange={(e) => updatePreparation(i, "weighInJustification", e.target.value)}
                        error={!p.weighInJustification.trim()}
                        sx={{ mt: 0.5 }}
                      />
                    )}
                  </Box>
                );
              })}
            </Stack>
          </Box>
        )}

        {expectedPreps === 0 && !blockingMessage && (
          <Alert severity="warning">Replicate counts are not configured for this sample's stage.</Alert>
        )}

        {specs.length > 0 && expectedPreps > 0 && (
          <Box>
            <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
              {isTitration ? "Titres (mL)" : "Peak areas"} — analytes × preparation (paste from the CDS export)
            </Typography>
            <UnitEntryGrid
              rowCount={specs.length}
              rowLabel={responseRowLabel}
              columns={responseColumns}
              values={responses}
              onChange={setResponses}
            />
          </Box>
        )}

        {specs.length > 0 && expectedPreps > 0 && (
          <Box sx={{ border: "1px dashed", borderColor: "divider", borderRadius: 1.5, p: 1.5 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, mb: 1 }}>
              Preview — %Assay per preparation (client-side estimate; the saved value is always calculated by the server)
            </Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontSize: 11, fontWeight: 700 }}>Analyte</TableCell>
                  {Array.from({ length: expectedPreps }, (_, i) => (
                    <TableCell key={i} sx={{ fontSize: 11, fontWeight: 700 }}>P{i + 1}</TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {specs.map((s, rowIdx) => {
                  const a = runAnalyteFor(s);
                  return (
                    <TableRow key={s.testAnalyteId}>
                      <TableCell sx={{ fontSize: 12 }}>{s.parameterName}</TableCell>
                      {Array.from({ length: expectedPreps }, (_, i) => {
                        const val = previewAssay(a, i, responses[rowIdx]?.[prepKey(i + 1)] ?? "");
                        return (
                          <TableCell key={i} sx={{ fontSize: 12 }}>
                            {val != null ? `${val.toFixed(2)} %` : "—"}
                          </TableCell>
                        );
                      })}
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </Box>
        )}

        <TextField size="small" label="Comment (optional)" value={comment} onChange={(e) => setComment(e.target.value)} multiline rows={2} />
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button variant="contained" disabled={!readyToSign} onClick={() => setSigning(true)}>Sign and calculate</Button>
        </Box>
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement="I entered these sample values from the analytical data and am recording this standard-comparison assay result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
