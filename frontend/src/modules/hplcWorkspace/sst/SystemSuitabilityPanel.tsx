import { useState, useEffect } from "react";
import {
  Box,
  Button,
  Stack,
  Alert,
  CircularProgress,
  Tooltip,
  Divider,
  Typography
} from "@mui/material";
import SaveIcon from "@mui/icons-material/Save";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import { toast } from "sonner";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { MaterialService } from "../../inventory/materials/services/MaterialService";
import { SstStatusCard } from "./SstStatusCard";
import { SstValuesTable } from "./SstValuesTable";
import { StandardEntryForm, StandardMaterialOption } from "./StandardEntryForm";
import { ReportUploadPanel } from "../evidence/ReportUploadPanel";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import type {
  HplcRunDto,
  HplcSstRecordDto,
  SaveSstAnalyteInput
} from "../types";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

export interface SystemSuitabilityPanelProps {
  run: HplcRunDto;
  method?: HplcMethodResponse | null;
  canOperate: boolean;
  onRunUpdated: () => void;
}

export function SystemSuitabilityPanel({
  run,
  method,
  canOperate,
  onRunUpdated
}: SystemSuitabilityPanelProps) {
  const sst: HplcSstRecordDto | undefined = run.sst ?? undefined;

  // Reference standard lots
  const [standardLots, setStandardLots] = useState<StandardMaterialOption[]>([]);
  const [loadingLots, setLoadingLots] = useState(true);

  // Form state: analyteId -> SaveSstAnalyteInput
  const [analyteInputs, setAnalyteInputs] = useState<Record<number, SaveSstAnalyteInput>>({});

  // Actions state
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // E-Signature dialog state for confirm
  const [signOpen, setSignOpen] = useState(false);
  const [signComment, setSignComment] = useState("");

  // Abandon run dialog state
  const [abandonOpen, setAbandonOpen] = useState(false);
  const [abandonReason, setAbandonReason] = useState("");
  const [abandoning, setAbandoning] = useState(false);

  // Load usable reference standard lots
  useEffect(() => {
    let active = true;
    setLoadingLots(true);
    MaterialService.getUsableReferenceStandards()
      .then((data: unknown) => {
        if (!active) return;
        setStandardLots(Array.isArray(data) ? (data as StandardMaterialOption[]) : []);
      })
      .catch(() => {
        if (active) setStandardLots([]);
      })
      .finally(() => {
        if (active) setLoadingLots(false);
      });

    return () => {
      active = false;
    };
  }, []);

  // Initialize form values from current SST record
  useEffect(() => {
    if (!sst || !sst.analytes) return;

    setAnalyteInputs((prev) => {
      const next = { ...prev };
      sst.analytes.forEach((a) => {
        // If user has already entered something in local state, preserve it on update error
        if (next[a.id]) return;

        const methodAnalyte = method?.analytes.find(
          (ma) => ma.id === a.hplcMethodAnalyteId || ma.name === a.analyteName
        );
        const injectionCount = methodAnalyte?.standardInjections ?? 5;

        // Populate responses from existing injections
        const responses: number[] = Array.from({ length: injectionCount }, (_, idx) => {
          const matched = a.injections?.find((inj) => inj.injectionNo === idx + 1);
          return matched ? matched.response : 0;
        });

        next[a.id] = {
          hplcSstAnalyteId: a.id,
          standardMaterialId: a.standardMaterialId ?? 0,
          standardWeightMg: a.standardWeightMg ?? 0,
          responses,
          reportedRsdPercent: a.reportedRsdPercent ?? null,
          resolution: a.resolution ?? null,
          tailingFactor: a.tailingFactor ?? null,
          theoreticalPlates: a.theoreticalPlates ?? null,
          retentionFactor: a.retentionFactor ?? null,
          signalToNoise: a.signalToNoise ?? null,
          peakToValley: a.peakToValley ?? null
        };
      });
      return next;
    });
  }, [sst, method]);

  const isConfirmed = sst?.status === "Passed" || sst?.status === "Failed" || run.status !== "Open";
  const isPending = sst?.status === "Pending" && run.status === "Open";

  // Analyte input updater
  const handleAnalyteChange = (analyteId: number, patch: Partial<SaveSstAnalyteInput>) => {
    setAnalyteInputs((prev) => ({
      ...prev,
      [analyteId]: {
        ...prev[analyteId],
        ...patch
      }
    }));
  };

  // Save SST inputs
  const handleSaveSst = async () => {
    if (!sst) return;
    const analytesPayload: SaveSstAnalyteInput[] = Object.values(analyteInputs);

    setSaving(true);
    setError(null);
    try {
      await HplcWorkspaceService.saveSst(run.id, { analytes: analytesPayload });
      toast.success("System suitability data saved.");
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not save system suitability.");
    } finally {
      setSaving(false);
    }
  };

  // Confirm SST via E-Signature
  const handleConfirmSignature = async (password: string) => {
    setError(null);
    await HplcWorkspaceService.confirmSst(run.id, {
      password,
      comment: signComment.trim() || null
    });
    setSignOpen(false);
    toast.success("System suitability confirmed.");
    onRunUpdated();
  };

  // Abandon Run
  const handleAbandonRun = async () => {
    if (!abandonReason.trim()) return;
    setAbandoning(true);
    try {
      await HplcWorkspaceService.abandonRun(run.id, abandonReason.trim());
      setAbandonOpen(false);
      toast.info("Run has been marked as abandoned.");
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not abandon run.");
    } finally {
      setAbandoning(false);
    }
  };

  if (!sst) {
    return (
      <Alert severity="warning">
        No System Suitability record found for this run.
      </Alert>
    );
  }

  return (
    <Stack spacing={3}>
      {/* SST Status Banner */}
      <SstStatusCard
        code={sst.code}
        status={sst.status}
        failureReasons={sst.failureReasons}
        confirmedByUserName={sst.confirmedByUserName}
        confirmedAt={sst.confirmedAt}
      />

      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* Values & Criteria Comparison Table */}
      <SstValuesTable analytes={sst.analytes} method={method} />

      {/* Per-analyte standard entry forms */}
      <Box>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1.5 }}>
          Standard Entry & Injections
        </Typography>

        {loadingLots ? (
          <CircularProgress size={24} sx={{ my: 2 }} />
        ) : (
          <Stack spacing={2.5}>
            {sst.analytes.map((a) => {
              const methodAnalyte = method?.analytes.find(
                (ma) => ma.id === a.hplcMethodAnalyteId || ma.name === a.analyteName
              );
              const formVal = analyteInputs[a.id] || {
                hplcSstAnalyteId: a.id,
                standardMaterialId: a.standardMaterialId ?? 0,
                standardWeightMg: a.standardWeightMg ?? 0,
                responses: []
              };

              return (
                <StandardEntryForm
                  key={a.id}
                  analyte={a}
                  methodAnalyte={methodAnalyte}
                  standardLots={standardLots}
                  formValue={formVal}
                  onChange={(patch) => handleAnalyteChange(a.id, patch)}
                  disabled={isConfirmed || !canOperate}
                />
              );
            })}
          </Stack>
        )}
      </Box>

      {/* Standard report evidence upload */}
      <ReportUploadPanel
        runId={run.id}
        context="Sst"
        kind="StandardReport"
        evidenceList={run.evidence}
        onChanged={onRunUpdated}
      />

      <Divider />

      {/* Action Bar */}
      {isPending && canOperate && (
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 2 }}>
          <Button
            variant="outlined"
            color="error"
            startIcon={<CancelOutlinedIcon />}
            onClick={() => setAbandonOpen(true)}
            sx={{ textTransform: "none" }}
          >
            Abandon Run...
          </Button>

          <Stack direction="row" spacing={2}>
            <Button
              variant="outlined"
              startIcon={<SaveIcon />}
              onClick={handleSaveSst}
              disabled={saving}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : "Save System Suitability"}
            </Button>

            <Tooltip
              title={
                !run.canConfirmSst
                  ? run.canConfirmSstReason || "Standard report upload and valid criteria are required."
                  : ""
              }
            >
              <span>
                <Button
                  variant="contained"
                  color="success"
                  startIcon={<VerifiedUserIcon />}
                  disabled={!run.canConfirmSst || saving}
                  onClick={() => {
                    setSignComment("");
                    setSignOpen(true);
                  }}
                  sx={{ textTransform: "none", fontWeight: 600 }}
                >
                  Confirm System Suitability...
                </Button>
              </span>
            </Tooltip>
          </Stack>
        </Box>
      )}

      {/* E-Signature Dialog for SST Confirmation */}
      <SignatureDialog
        open={signOpen}
        meaningStatement="I confirm that the system suitability results meet the method acceptance criteria and the standard integration report has been verified."
        showComment
        comment={signComment}
        onCommentChange={setSignComment}
        onCancel={() => setSignOpen(false)}
        onConfirm={handleConfirmSignature}
      />

      {/* Reason Dialog for Abandon Run */}
      <ReasonDialog
        open={abandonOpen}
        title="Abandon HPLC Run"
        onClose={() => setAbandonOpen(false)}
        onConfirm={handleAbandonRun}
        confirmText="Abandon Run"
        confirmColor="error"
        loading={abandoning}
        loadingText="Abandoning..."
        reason={abandonReason}
        onReasonChange={setAbandonReason}
        label="Reason for Abandoning *"
        placeholder="Document why this run is being abandoned (e.g. system suitability failure, instrument pressure error)..."
      />
    </Stack>
  );
}
