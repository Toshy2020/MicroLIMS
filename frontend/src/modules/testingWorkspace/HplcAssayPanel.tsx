import { useEffect, useState } from "react";
import { Alert, Box, Button, Chip, IconButton, MenuItem, Select, Stack, TextField, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import { StatusBadge } from "../../components/StatusBadge";
import { SignatureDialog } from "../../components/SignatureDialog";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { SystemSuitabilityService, SystemSuitabilityRun } from "../systemSuitability/services/SystemSuitabilityService";

interface Props {
  testOrderId: number;
  displayName: string;
  // current-step response for this order (allStepsComplete / finalResult / returnInfo)
  current: { allStepsComplete?: boolean; finalResult?: string | null; returnInfo?: { reason?: string | null } | null };
  onRecorded: () => Promise<void> | void;
  onClose?: () => void;
}

const errorMessage = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

// HPLC assay result entry (REQ-FP-003/033): link the test to a PASSED
// suitability run of its method, then enter sample weight, dilution and
// replicate peak areas. The % assay per replicate, the mean and the spec
// comparison are calculated by the server - nothing is computed here.
export function HplcAssayPanel({ testOrderId, displayName, current, onRecorded, onClose }: Props) {
  const [linked, setLinked] = useState<SystemSuitabilityRun | null>(null);
  const [selectable, setSelectable] = useState<SystemSuitabilityRun[]>([]);
  const [runChoice, setRunChoice] = useState<number | "">("");
  const [changingRun, setChangingRun] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [sampleWeightMg, setSampleWeightMg] = useState("");
  const [sampleDilution, setSampleDilution] = useState("");
  const [areas, setAreas] = useState<string[]>(["", ""]);
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);
  const [outcome, setOutcome] = useState<{ reportedResult?: string; status?: string } | null>(null);

  const loadRuns = async () => {
    setError(null);
    try {
      const [l, s] = await Promise.all([
        SystemSuitabilityService.getLinkedForTestOrder(testOrderId),
        SystemSuitabilityService.getSelectableForTestOrder(testOrderId)
      ]);
      setLinked(l);
      setSelectable(s);
      setRunChoice("");
      setChangingRun(false);
    } catch (e) {
      setError(errorMessage(e, "Could not load suitability runs for this test."));
    }
  };

  useEffect(() => {
    loadRuns();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [testOrderId]);

  const link = async () => {
    if (!runChoice) return;
    setError(null);
    try {
      await SystemSuitabilityService.linkTestOrders(Number(runChoice), [testOrderId]);
      await loadRuns();
    } catch (e) {
      setError(errorMessage(e, "Could not link this test to the run."));
    }
  };

  const filledAreas = areas.filter((a) => a.trim() !== "");
  const readyToSign = !!linked && Number(sampleWeightMg) > 0 && Number(sampleDilution) > 0 && filledAreas.length > 0;

  const submit = async (password: string) => {
    const result = await TestWorkflowService.recordHplcResult(testOrderId, {
      sampleWeightMg: Number(sampleWeightMg),
      sampleDilution: Number(sampleDilution),
      sampleAreas: filledAreas.map(Number),
      password,
      comment: comment.trim() || null
    });
    setSigning(false);
    setOutcome({ reportedResult: result?.finalResult ?? result?.outcomeSummary, status: result?.status });
    await onRecorded();
  };

  if (current.allStepsComplete || outcome) {
    const reported = outcome?.reportedResult ?? current.finalResult;
    const outOfSpec = (outcome?.status ?? reported ?? "").includes("OutOfSpecification");
    return (
      <Box>
        <Alert severity={outOfSpec ? "error" : "success"} sx={{ mb: 2 }}>
          {displayName}: <strong>{reported}</strong>
          {outcome?.status && ` (${outcome.status})`}
        </Alert>
        {linked && <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 2 }}>Suitability run {linked.code}</Typography>}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <StatusBadge status="ResultRecorded" label="Result Recorded — Pending Review" />
          {onClose && <Button variant="contained" onClick={onClose}>Done / Close</Button>}
        </Box>
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
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Typography sx={{ fontWeight: 700, mb: 1 }}>1. System suitability run</Typography>
      {linked && !changingRun ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2 }}>
          <Chip color="success" label={`${linked.code} · Passed`} />
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            {linked.equipmentCode} / {linked.columnCode} · standard {linked.referenceStandardName} ({linked.standardPurityPercent}%)
          </Typography>
          {selectable.length > 1 && <Button size="small" onClick={() => setChangingRun(true)}>Change</Button>}
        </Stack>
      ) : (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 2 }}>
          <Select size="small" displayEmpty value={runChoice} onChange={(e) => setRunChoice(e.target.value as number)} sx={{ minWidth: 320 }}>
            <MenuItem value="" disabled>{selectable.length ? "Choose a passed run" : "No passed run for this method yet"}</MenuItem>
            {selectable.map((r) => (
              <MenuItem key={r.id} value={r.id}>{r.code} — {r.equipmentCode}, {new Date(r.performedAt).toLocaleDateString()}</MenuItem>
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

      <Typography sx={{ fontWeight: 700, mb: 1, color: linked ? "text.primary" : "text.disabled" }}>2. Sample result</Typography>
      <Stack spacing={2} sx={{ opacity: linked ? 1 : 0.5, pointerEvents: linked ? "auto" : "none" }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <TextField size="small" type="number" label="Sample weight (mg)" value={sampleWeightMg} onChange={(e) => setSampleWeightMg(e.target.value)} fullWidth />
          <TextField size="small" type="number" label="Sample dilution" value={sampleDilution} onChange={(e) => setSampleDilution(e.target.value)} fullWidth />
        </Stack>
        <Box>
          <Typography sx={{ fontSize: 13, color: "text.secondary", mb: 1 }}>Sample peak area per injection</Typography>
          <Stack spacing={1}>
            {areas.map((a, i) => (
              <Stack key={i} direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <TextField size="small" type="number" label={`Replicate ${i + 1}`} value={a}
                  onChange={(e) => setAreas((prev) => prev.map((v, idx) => (idx === i ? e.target.value : v)))} />
                {areas.length > 1 && (
                  <IconButton size="small" onClick={() => setAreas((prev) => prev.filter((_, idx) => idx !== i))}><CloseIcon fontSize="small" /></IconButton>
                )}
              </Stack>
            ))}
          </Stack>
          <Button size="small" startIcon={<AddIcon />} onClick={() => setAreas((prev) => [...prev, ""])} sx={{ mt: 1 }}>Add replicate</Button>
        </Box>
        <TextField size="small" label="Comment (optional)" value={comment} onChange={(e) => setComment(e.target.value)} multiline rows={2} />
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button variant="contained" disabled={!readyToSign} onClick={() => setSigning(true)}>Sign and calculate</Button>
        </Box>
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement="I entered these sample values from the chromatography data and am recording this HPLC assay result."
        onCancel={() => setSigning(false)}
        onConfirm={submit}
      />
    </Box>
  );
}
