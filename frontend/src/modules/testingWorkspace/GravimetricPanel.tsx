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
  TextField,
  Typography
} from "@mui/material";
import { SignatureDialog } from "../../components/SignatureDialog";
import { CriteriaCard, NumericCell, RegisterTable, ResultSection, VerdictBanner } from "../../components/lab";
import type { RegisterColumn } from "../../components/lab";
import type { CriteriaRow, Verdict } from "../../components/lab";
import { UnitEntryGrid, UnitEntryGridColumn } from "../../components/UnitEntryGrid";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SampleSummaryService } from "./services/SampleSummaryService";
import type { ParameterResultDetail, ResultReadingDetail } from "./types/sampleSummaryTypes";
import { SpecificationService, SpecificationDto } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { masterDataOptions } from "../../services/masterDataOptions";
import { TestDefinitionOption } from "../../hooks/useTestDefinitions";
import { EquipmentConfigurationService, ConfiguredEquipmentSummary } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import { getSections, LaboratorySection } from "../../services/laboratorySectionService";

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

const getLocalIsoString = (date: Date = new Date()): string => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

export function GravimetricPanel({
  testOrderId,
  displayName,
  testCode,
  itemId,
  sampleId,
  current,
  onRecorded
}: Props) {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [testDef, setTestDef] = useState<TestDefinitionOption | null>(null);
  const [specs, setSpecs] = useState<SpecificationDto[]>([]);
  const [fpEquipment, setFpEquipment] = useState<ConfiguredEquipmentSummary[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<number | "">("");

  const [analysedAt, setAnalysedAt] = useState(() => getLocalIsoString());
  const [conditions, setConditions] = useState<Record<string, string>>({});
  const [replicatesBySpecId, setReplicatesBySpecId] = useState<Record<number, Record<string, string>[]>>({});
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);

  // Outcome / completion view
  const [outcome, setOutcome] = useState<{ outcomeSummary?: string; status?: string } | null>(null);

  const effectiveTestCode = current.testName || testCode || "";

  // Recorded weights come back from the sample summary (ResultReading rows:
  // value1 = container, value2 = initial, value3 = final, computedValue = percent).
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
        // non-blocking: the completed view still shows the result without weights
      });
    return () => {
      active = false;
    };
  }, [showCompleted, sampleId, testOrderId]);

  useEffect(() => {
    let active = true;

    const loadData = async () => {
      setLoading(true);
      setError(null);

      try {
        // 1. Fetch test definitions
        const defs: TestDefinitionOption[] = await masterDataOptions.getTestDefinitions();
        const matchedDef = defs.find((d) => d.code === effectiveTestCode) ?? null;

        // 2. Resolve itemId if not provided
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

        // 3. Fetch specifications for this item and test
        let loadedSpecs: SpecificationDto[] = [];
        if (resolvedItemId != null) {
          const itemSpecs = await SpecificationService.getForItem(resolvedItemId);
          loadedSpecs = itemSpecs
            .filter((s) => s.testCode === effectiveTestCode)
            .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
        }

        if (loadedSpecs.length === 0) {
          if (active) {
            setError(`No specifications configured for test ${effectiveTestCode}.`);
            setLoading(false);
          }
          return;
        }

        // 4. Load FP section equipment
        let fpInstruments: ConfiguredEquipmentSummary[] = [];
        try {
          const [sections, allEquip] = await Promise.all([
            getSections(),
            EquipmentConfigurationService.getConfiguredSummary()
          ]);
          const fpSec = (sections ?? []).find((s: LaboratorySection) => s.sectionCode === "FP");
          fpInstruments = (allEquip ?? []).filter(
            (e) => (fpSec && e.sectionId === fpSec.sectionId) || e.section?.code === "FP" || e.section?.sectionCode === "FP"
          );
        } catch {
          // fallback
        }

        const repCount = matchedDef?.replicateCount && matchedDef.replicateCount >= 1 && matchedDef.replicateCount <= 30
          ? matchedDef.replicateCount
          : 1;

        // Initialize conditions from conditionFields
        const condLabels = matchedDef?.conditionFields
          ? matchedDef.conditionFields.split(",").map((s) => s.trim()).filter(Boolean)
          : [];
        const initConditions: Record<string, string> = {};
        for (const l of condLabels) {
          initConditions[l] = "";
        }

        // Initialize replicates per spec
        const initReplicates: Record<number, Record<string, string>[]> = {};
        for (const s of loadedSpecs) {
          initReplicates[s.id!] = Array.from({ length: repCount }, () => ({
            container: "",
            initial: "",
            final: ""
          }));
        }

        if (active) {
          setTestDef(matchedDef);
          setSpecs(loadedSpecs);
          setFpEquipment(fpInstruments);
          setConditions(initConditions);
          setReplicatesBySpecId(initReplicates);
        }
      } catch (err: unknown) {
        const msg =
          (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          "Could not load gravimetric test configuration or specifications.";
        if (active) setError(msg);
      } finally {
        if (active) setLoading(false);
      }
    };

    loadData();

    return () => {
      active = false;
    };
  }, [effectiveTestCode, itemId, current.sampleContext?.sampleName]);

  const replicateCount = useMemo(() => {
    return testDef?.replicateCount && testDef.replicateCount >= 1 && testDef.replicateCount <= 30
      ? testDef.replicateCount
      : 1;
  }, [testDef?.replicateCount]);

  const usesTare = Boolean(testDef?.usesTare);
  const isResidue = testDef?.equationType === "GravimetricResidue";
  const previewLabel = isResidue ? "Residue (%) Preview" : "Loss (%) Preview";

  const conditionLabels = useMemo(() => {
    if (!testDef?.conditionFields) return [];
    return testDef.conditionFields
      .split(",")
      .map((s) => s.trim())
      .filter(Boolean);
  }, [testDef?.conditionFields]);

  const columns: UnitEntryGridColumn[] = useMemo(() => {
    return [
      ...(usesTare ? [{ key: "container", label: "Container (g)" }] : []),
      { key: "initial", label: "Initial (g)" },
      { key: "final", label: "Final (g)" },
      { key: "preview", label: previewLabel, readOnly: true }
    ];
  }, [usesTare, previewLabel]);

  const computePreview = (row: Record<string, string>): string => {
    const c = usesTare ? Number(row?.container) : 0;
    const init = Number(row?.initial);
    const fin = Number(row?.final);
    if (isNaN(init) || isNaN(fin) || (usesTare && isNaN(c))) return "—";
    const w1 = init - c;
    const w2 = fin - c;
    if (w1 <= 0 || w2 < 0 || w2 > w1) return "—";
    const pct = isResidue ? (w2 * 100) / w1 : ((w1 - w2) * 100) / w1;
    return `${pct.toFixed(2)} %`;
  };

  const handleReplicatesChange = (specId: number, nextRows: Record<string, string>[]) => {
    setReplicatesBySpecId((prev) => ({
      ...prev,
      [specId]: nextRows
    }));
  };

  const isFormValid = useMemo(() => {
    if (specs.length === 0) return false;
    if (!analysedAt.trim()) return false;

    // Check analysis time is not more than 5 minutes in the future
    const parsedDate = new Date(analysedAt);
    if (isNaN(parsedDate.getTime()) || parsedDate.getTime() > Date.now() + 5 * 60 * 1000) {
      return false;
    }

    // Check conditions: every label must have a non-empty string
    for (const label of conditionLabels) {
      if (!conditions[label] || conditions[label].trim() === "") {
        return false;
      }
    }

    // Check replicates for each spec
    for (const s of specs) {
      const rows = replicatesBySpecId[s.id!] || [];
      if (rows.length !== replicateCount) return false;

      for (let r = 0; r < replicateCount; r++) {
        const row = rows[r];
        if (!row) return false;

        const init = Number(row.initial);
        const fin = Number(row.final);
        if (row.initial == null || row.initial.trim() === "" || isNaN(init)) return false;
        if (row.final == null || row.final.trim() === "" || isNaN(fin)) return false;

        let container = 0;
        if (usesTare) {
          const c = Number(row.container);
          if (row.container == null || row.container.trim() === "" || isNaN(c)) return false;
          container = c;
        }

        const w1 = init - container;
        const w2 = fin - container;
        if (w1 <= 0 || w2 < 0 || w2 > w1) {
          return false;
        }
      }
    }

    return true;
  }, [specs, replicatesBySpecId, replicateCount, analysedAt, conditionLabels, conditions, usesTare]);

  const submit = async (password: string) => {
    setError(null);
    try {
      const payload = {
        analysedAt: new Date(analysedAt).toISOString(),
        equipmentId: selectedEquipmentId !== "" ? Number(selectedEquipmentId) : null,
        conditions,
        parameters: specs.map((s) => ({
          specificationId: s.id!,
          replicates: (replicatesBySpecId[s.id!] || []).map((r) => ({
            container: usesTare ? Number(r.container) : null,
            initial: Number(r.initial),
            final: Number(r.final)
          }))
        })),
        password,
        comment: comment.trim() || null
      };

      const res = await TestWorkflowService.recordGravimetricResult(testOrderId, payload);
      setSigning(false);
      setOutcome({
        outcomeSummary: res?.outcomeSummary ?? res?.finalResult,
        status: res?.status
      });

      await onRecorded();
    } catch (err: unknown) {
      setSigning(false);
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Failed to record gravimetric result.";
      setError(msg);
    }
  };

  // Acceptance criteria from the specs already loaded for this test.
  const criteriaRows: CriteriaRow[] = specs.map((s) => ({
    parameter: s.parameterName || s.testCode,
    criterion:
      s.specLimit ||
      s.expectedResultText ||
      (s.lowerLimit != null || s.upperLimit != null ? `${s.lowerLimit ?? ""} \u2013 ${s.upperLimit ?? ""}` : "\u2014"),
    unit: s.unit || "%",
    source: "Specification"
  }));

  // Completion view
  if (showCompleted) {
    const serverStatus = outcome?.status ?? "";
    const headline = `${displayName}: ${outcome?.outcomeSummary ?? current.finalResult ?? "Results Recorded"}`;
    // Verdict comes only from the server's status field; when the status is not
    // known (page reopened) the recorded result is shown with no verdict.
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

        {testDef && (
          <ResultSection title="Analysis summary">
            <Stack direction="row" spacing={3} sx={{ flexWrap: "wrap" }}>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Equation Type</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {testDef.equationType === "GravimetricResidue" ? "Ash / Residue" : "Loss on Drying"}
                </Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Replicates Recorded</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>{replicateCount}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>Tare Mode</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{usesTare ? "Container Tare" : "Direct"}</Typography>
              </Box>
            </Stack>
          </ResultSection>
        )}

        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

        {recorded.map((p, idx) => {
          const weights = (p.readings ?? [])
            .filter((r) => r.kind === "Weight")
            .sort((a, b) => a.index - b.index);
          if (weights.length === 0) return null;
          const weightColumns: RegisterColumn<ResultReadingDetail>[] = [
            { key: "index", label: "Replicate", render: (r) => `Rep ${r.index}` },
            ...(usesTare
              ? [{ key: "value1", label: "Container (g)", numeric: true, render: (r: ResultReadingDetail) => <NumericCell value={r.value1} /> }]
              : []),
            { key: "value2", label: "Initial (g)", numeric: true, render: (r) => <NumericCell value={r.value2} /> },
            { key: "value3", label: "Final (g)", numeric: true, render: (r) => <NumericCell value={r.value3} /> },
            {
              key: "computedValue",
              label: isResidue ? "Residue (%)" : "Loss (%)",
              numeric: true,
              render: (r) => <NumericCell value={r.computedValue} decimals={2} unit="%" />
            }
          ];
          return (
            <ResultSection key={p.id} title={`Recorded weights · ${p.parameterName}`}>
              <RegisterTable
                columns={weightColumns}
                rows={weights}
                getRowId={(r) => r.id ?? `${idx}-${r.index}`}
                empty={{ title: "No weights recorded" }}
              />
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

  return (
    <Box>
      {current.returnInfo && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {current.returnInfo.reason
            ? `Returned for revision: ${current.returnInfo.reason}`
            : "Returned by reviewer for revision"}
        </Alert>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Stack spacing={2.5}>
        {criteriaRows.length > 0 && <CriteriaCard rows={criteriaRows} />}

        {/* Section 1: Analysis Configuration */}
        <ResultSection
          step={1}
          title="Analysis configuration"
          actions={
            <Stack direction="row" spacing={1}>
              <Chip size="small" variant="outlined" label={isResidue ? "Ash / Residue" : "Loss on Drying"} />
              <Chip size="small" variant="outlined" label={`Replicates: ${replicateCount}`} />
              {usesTare && <Chip size="small" color="primary" variant="outlined" label="Uses Tare" />}
            </Stack>
          }
        >
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
            <TextField
              size="small"
              type="datetime-local"
              label="Analysis time *"
              value={analysedAt}
              onChange={(e) => setAnalysedAt(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              required
              fullWidth
            />

            <FormControl size="small" fullWidth>
              <InputLabel id="equipment-select-label">Equipment (Physicochemical)</InputLabel>
              <Select
                labelId="equipment-select-label"
                label="Equipment (Physicochemical)"
                value={selectedEquipmentId}
                onChange={(e) => setSelectedEquipmentId(String(e.target.value) === "" ? "" : Number(e.target.value))}
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
          </Box>

          {/* Condition Fields Form */}
          {conditionLabels.length > 0 && (
            <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
              <Typography sx={{ fontWeight: 600, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                Test Conditions (All required)
              </Typography>
              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: conditionLabels.length > 1 ? "1fr 1fr" : "1fr" }, gap: 1.5 }}>
                {conditionLabels.map((label) => (
                  <TextField
                    key={label}
                    size="small"
                    label={`${label} *`}
                    value={conditions[label] ?? ""}
                    onChange={(e) => setConditions((prev) => ({ ...prev, [label]: e.target.value }))}
                    required
                    fullWidth
                  />
                ))}
              </Box>
            </Box>
          )}
        </ResultSection>

        {/* Section 3: Gravimetric Replicates Grid per Specification */}
        {specs.map((s, specIdx) => {
          const specRows = replicatesBySpecId[s.id!] || [];
          const readOnlyPreviews = specRows.map((r) => ({ preview: computePreview(r) }));

          return (
            <ResultSection
              key={s.id}
              step={specIdx + 2}
              title={`Gravimetric weights \u00b7 ${s.parameterName || s.testCode} (${replicateCount} replicate${replicateCount === 1 ? "" : "s"})`}
            >

              <UnitEntryGrid
                rowCount={replicateCount}
                rowLabel={(i) => `Rep ${i + 1}`}
                columns={columns}
                values={specRows}
                onChange={(next) => handleReplicatesChange(s.id!, next)}
                readOnlyValues={readOnlyPreviews}
              />
            </ResultSection>
          );
        })}

        {/* Section 4: Comment */}
        <TextField
          size="small"
          label="Comment (optional)"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          multiline
          rows={2}
          fullWidth
        />

        {/* Section 5: Action button */}
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
        meaningStatement="I entered these gravimetric measurements and am recording this test result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
