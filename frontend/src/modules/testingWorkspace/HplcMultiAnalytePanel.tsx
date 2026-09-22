import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
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
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";
import { SystemSuitabilityService, SystemSuitabilityRun } from "../systemSuitability/services/SystemSuitabilityService";
import {
  AnalysisDetail,
  ParameterResultDetail,
  TestOrderSummaryDetail,
  HplcMultiAnalyteCalculationData
} from "./types/sampleSummaryTypes";

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

type SampleMatrixChoice = "Solid" | "Liquid";

// The M1 SystemSuitabilityRunAnalyte row shape (backend
// SystemSuitabilityRunAnalyteView / SystemSuitabilityDtos.cs). The
// systemSuitability module (SystemSuitabilityService.ts) doesn't declare
// this on SystemSuitabilityRun yet - the field is present on the JSON the
// backend already returns for HplcMultiAnalyte runs, so it's typed locally
// here rather than editing that shared file.
interface SystemSuitabilityRunAnalyte {
  id: number;
  systemSuitabilityRunId: number;
  testAnalyteId: number;
  analyteName: string;
  wavelengthNm: number;
  standardPurityPercent: number;
  standardWeightMg: number;
  standardDilution: number;
  standardMeanArea: number;
  passed: boolean;
  failureReasons?: string | null;
}

type RunWithAnalytes = SystemSuitabilityRun & { analytes?: SystemSuitabilityRunAnalyte[] | null };

// Test Master's HPLC Multi-Analyte replicate configuration (M1 fields) -
// not yet on the shared TestDefinitionOption type (Test Master/
// useTestDefinitions is being edited in parallel), so declared locally.
interface HplcMultiAnalyteTestDefExtra {
  hplcPreparations?: number | null;
  hplcInjectionsPerPreparation?: number | null;
  hplcMaxPreparationRsdPercent?: number | null;
}

type HplcMultiTestDef = TestDefinitionOption & HplcMultiAnalyteTestDefExtra;

interface WvUnitWeight {
  valueMg: number;
  sourceTestOrderId: number;
}

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

const errorMessage = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

const colKey = (p: number, i: number) => `p${p}i${i}`;

function parseCalc(json: string | null): HplcMultiAnalyteCalculationData | null {
  if (!json) return null;
  try {
    return JSON.parse(json) as HplcMultiAnalyteCalculationData;
  } catch {
    return null;
  }
}

// Per-analyte result table shown after signing / when re-opening an
// already-recorded test - every value is the server's stored one.
function ResultsTable({ analysis }: { analysis: AnalysisDetail }) {
  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Analyte</TableCell>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Reported</TableCell>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Specification</TableCell>
          <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>%LC</TableCell>
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
              <TableCell sx={{ fontSize: 13 }}>{calc?.percentLabelClaim != null ? `${calc.percentLabelClaim.toFixed(1)} %` : "—"}</TableCell>
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

// HPLC assay (multi-vitamin) result entry: link a passed multi-analyte
// suitability run, enter sample preparations/dilutions, average unit
// weight (or the sample's own Weight Variation result when one exists),
// and a grid of peak areas (analytes x preparation/injection). One
// signed action for the whole test - the server computes C_s, amount per
// unit and %LC per analyte; nothing is computed here.
export function HplcMultiAnalytePanel({
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

  const [testDef, setTestDef] = useState<HplcMultiTestDef | null>(null);
  const [specs, setSpecs] = useState<SpecificationDto[]>([]);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  const [linked, setLinked] = useState<RunWithAnalytes | null>(null);
  const [selectable, setSelectable] = useState<RunWithAnalytes[]>([]);
  const [runChoice, setRunChoice] = useState<number | "">("");
  const [changingRun, setChangingRun] = useState(false);

  const [sampleMatrix, setSampleMatrix] = useState<SampleMatrixChoice>("Solid");
  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [unitAmountTyped, setUnitAmountTyped] = useState("");
  const [wvUnitWeight, setWvUnitWeight] = useState<WvUnitWeight | null>(null);

  const [preparations, setPreparations] = useState<Record<string, string>[]>([]);
  const [areas, setAreas] = useState<Record<string, string>[]>([]);

  const [activeAnalysis, setActiveAnalysis] = useState<AnalysisDetail | null>(null);

  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string } | null>(null);

  const effectiveTestCode = current.testName || testCode || "";

  const expectedPreps = testDef?.hplcPreparations ?? 2;
  const expectedInjections = testDef?.hplcInjectionsPerPreparation ?? 2;

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const defs: HplcMultiTestDef[] = await masterDataOptions.getTestDefinitions();
      const matchedDef = defs.find((d) => d.code === effectiveTestCode) ?? null;
      const preps = matchedDef?.hplcPreparations ?? 2;
      const injections = matchedDef?.hplcInjectionsPerPreparation ?? 2;

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

      // Analytes to enter = the item's spec'd analytes for this test;
      // fall back to the linked run's analyte rows so the grid still has
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

          // Weight variation's mean unit weight, from a different, already
          // finished test order on the same sample (D4) - same rule the
          // server applies (active, not NextStageRequired, ReportedValue > 0).
          const wvCandidates = (summary.testOrders ?? [])
            .filter(
              (t: TestOrderSummaryDetail) =>
                t.testOrderId !== testOrderId &&
                t.analysis?.analysisType === "WeightVariation" &&
                t.analysis.parameterResults?.[0]?.comparisonStatus !== "NextStageRequired" &&
                (t.analysis.parameterResults?.[0]?.reportedValue ?? 0) > 0
            )
            .sort((a: TestOrderSummaryDetail, b: TestOrderSummaryDetail) => {
              const aAt = a.analysis?.analysedAt ?? "";
              const bAt = b.analysis?.analysedAt ?? "";
              return bAt.localeCompare(aAt) || b.testOrderId - a.testOrderId;
            });
          const wv = wvCandidates[0];
          if (wv?.analysis?.parameterResults?.[0]?.reportedValue != null) {
            setWvUnitWeight({
              valueMg: wv.analysis.parameterResults[0].reportedValue,
              sourceTestOrderId: wv.testOrderId
            });
          } else {
            setWvUnitWeight(null);
          }
        } catch {
          // non-blocking
        }
      }

      setTestDef(matchedDef);
      setSpecs(matchedSpecs);
      setFpEquipment(fpInstruments);
      setLinked(l);
      setSelectable(s);
      setRunChoice("");
      setChangingRun(false);

      setPreparations((prev) =>
        prev.length === preps ? prev : Array.from({ length: preps }, () => ({ sampleAmount: "", sampleDilutionMl: "" }))
      );
      setAreas((prev) => {
        // Keep typed areas only while the grid shape (analytes x preps x injections) is unchanged.
        if (prev.length === matchedSpecs.length && prev.every((r) => Object.keys(r).length === preps * injections && colKey(preps, injections) in r)) return prev;
        const cols = preps * injections;
        return Array.from({ length: matchedSpecs.length }, () => {
          const row: Record<string, string> = {};
          for (let p = 1; p <= preps; p++) {
            for (let i = 1; i <= injections; i++) {
              row[colKey(p, i)] = "";
            }
          }
          return cols > 0 ? row : {};
        });
      });
    } catch (err: unknown) {
      setError(errorMessage(err, "Could not load HPLC multi-analyte test configuration or specifications."));
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

  const prepColumns: UnitEntryGridColumn[] = useMemo(
    () => [
      { key: "sampleAmount", label: "Sample amount", unit: sampleMatrix === "Liquid" ? "mL" : "mg" },
      { key: "sampleDilutionMl", label: "Dilution", unit: "mL" }
    ],
    [sampleMatrix]
  );

  const areaColumns: UnitEntryGridColumn[] = useMemo(() => {
    const cols: UnitEntryGridColumn[] = [];
    for (let p = 1; p <= expectedPreps; p++) {
      for (let i = 1; i <= expectedInjections; i++) {
        cols.push({ key: colKey(p, i), label: `P${p}/I${i}`, width: 90 });
      }
    }
    return cols;
  }, [expectedPreps, expectedInjections]);

  const areaRowLabel = (i: number) => specs[i]?.parameterName || `Analyte ${i + 1}`;

  const unitAmountFromWv = sampleMatrix === "Solid" && wvUnitWeight != null;

  const preparationsValid =
    preparations.length === expectedPreps &&
    preparations.every((p) => {
      const a = Number(p.sampleAmount);
      const d = Number(p.sampleDilutionMl);
      return p.sampleAmount?.trim() !== "" && !isNaN(a) && a > 0 && p.sampleDilutionMl?.trim() !== "" && !isNaN(d) && d > 0;
    });

  const areasValid =
    specs.length > 0 &&
    areas.length === specs.length &&
    areas.every((row) =>
      areaColumns.every((c) => {
        const v = Number(row[c.key]);
        return row[c.key]?.trim() !== "" && !isNaN(v) && v > 0;
      })
    );

  const unitAmountValid = unitAmountFromWv || (Number(unitAmountTyped) > 0);

  const readyToSign =
    !!linked &&
    specs.length > 0 &&
    analysedAt.trim() !== "" &&
    preparationsValid &&
    unitAmountValid &&
    areasValid;

  const submit = async (password: string) => {
    setError(null);
    try {
      const preparationsPayload = preparations.map((p) => ({
        sampleAmount: Number(p.sampleAmount),
        sampleDilutionMl: Number(p.sampleDilutionMl)
      }));

      const areasPayload: { testAnalyteId: number; preparationIndex: number; injectionIndex: number; area: number }[] = [];
      specs.forEach((s, rowIdx) => {
        for (let p = 1; p <= expectedPreps; p++) {
          for (let i = 1; i <= expectedInjections; i++) {
            areasPayload.push({
              testAnalyteId: s.testAnalyteId!,
              preparationIndex: p,
              injectionIndex: i,
              area: Number(areas[rowIdx]?.[colKey(p, i)])
            });
          }
        }
      });

      const payload = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: selectedEquipmentId !== "" ? Number(selectedEquipmentId) : null,
        sampleMatrix,
        preparations: preparationsPayload,
        unitAmount: unitAmountFromWv ? null : Number(unitAmountTyped),
        areas: areasPayload,
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordHplcMultiAnalyteResult(testOrderId, payload);
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
      setError(errorMessage(err, "Failed to record HPLC multi-analyte result."));
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
              HPLC Assay (Multi-Vitamin) Results
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
          Loading HPLC multi-analyte configuration...
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
      {specs.length === 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          No specifications configured for this test and item yet - every vitamin needs a specification
          (analyte + result basis + label claim) before a result can be recorded.
        </Alert>
      )}

      <Typography sx={{ fontWeight: 700, mb: 1 }}>1. System suitability run</Typography>
      {linked && !changingRun ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2, flexWrap: "wrap" }}>
          <Chip color="success" label={`${linked.code} · Passed`} />
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {linked.equipmentCode} / {linked.columnCode} · {linked.analytes?.length ?? 0} analyte row(s)
          </Typography>
          {selectable.length > 1 && <Button size="small" onClick={() => setChangingRun(true)}>Change</Button>}
        </Stack>
      ) : (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2 }}>
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

      <Typography sx={{ fontWeight: 700, mb: 1, color: linked ? "text.primary" : "text.disabled" }}>
        2. Sample & analysis parameters
      </Typography>
      <Stack spacing={2} sx={{ opacity: linked ? 1 : 0.5, pointerEvents: linked ? "auto" : "none", mb: 2.5 }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <FormControl size="small" sx={{ flex: 1 }}>
            <InputLabel id="hplc-multi-matrix-label">Sample Matrix</InputLabel>
            <Select
              labelId="hplc-multi-matrix-label"
              label="Sample Matrix"
              value={sampleMatrix}
              onChange={(e) => setSampleMatrix(e.target.value as SampleMatrixChoice)}
            >
              <MenuItem value="Solid">Solid</MenuItem>
              <MenuItem value="Liquid">Liquid</MenuItem>
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ flex: 1.5 }}>
            <InputLabel id="hplc-multi-equipment-label">Instrument</InputLabel>
            <Select
              labelId="hplc-multi-equipment-label"
              label="Instrument"
              value={selectedEquipmentId}
              onChange={(e) => setSelectedEquipmentId(e.target.value as number | "")}
            >
              <MenuItem value=""><em>Select instrument (optional)</em></MenuItem>
              {fpEquipment.map((eq) => (
                <MenuItem key={eq.id} value={eq.id}>{eq.code} — {eq.name} ({eq.type})</MenuItem>
              ))}
            </Select>
          </FormControl>
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

        {sampleMatrix === "Solid" && unitAmountFromWv ? (
          <Alert severity="info" sx={{ py: 0.5 }}>
            Unit weight: <strong>{wvUnitWeight!.valueMg} mg</strong> — from Weight Variation (test order #{wvUnitWeight!.sourceTestOrderId})
          </Alert>
        ) : (
          <TextField
            size="small"
            type="number"
            label={sampleMatrix === "Liquid" ? "Volume per dose (mL) *" : "Mean unit weight (mg) *"}
            value={unitAmountTyped}
            onChange={(e) => setUnitAmountTyped(e.target.value)}
            slotProps={{ htmlInput: { step: "any", min: "0.000001" } }}
            required
            sx={{ maxWidth: 320 }}
          />
        )}

        <Box>
          <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
            Sample preparations ({expectedPreps})
          </Typography>
          <UnitEntryGrid
            rowCount={expectedPreps}
            rowLabel={(i) => `Preparation ${i + 1}`}
            columns={prepColumns}
            values={preparations}
            onChange={setPreparations}
          />
        </Box>

        <Box>
          <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>
            Peak areas — analytes × preparation/injection (paste from the CDS export)
          </Typography>
          {specs.length > 0 && (
            <UnitEntryGrid
              rowCount={specs.length}
              rowLabel={areaRowLabel}
              columns={areaColumns}
              values={areas}
              onChange={setAreas}
            />
          )}
        </Box>

        <TextField size="small" label="Comment (optional)" value={comment} onChange={(e) => setComment(e.target.value)} multiline rows={2} />
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button variant="contained" disabled={!readyToSign} onClick={() => setSigning(true)}>Sign and calculate</Button>
        </Box>
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement="I entered these sample values from the chromatography data and am recording this HPLC multi-analyte assay result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
