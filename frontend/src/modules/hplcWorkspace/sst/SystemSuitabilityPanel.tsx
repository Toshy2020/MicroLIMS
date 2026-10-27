import { useState, useEffect } from "react";
import { Box, Button, Stack, Alert, CircularProgress, Tooltip, Divider, Typography } from "@mui/material";
import SaveIcon from "@mui/icons-material/Save";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import { toast } from "sonner";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import { MaterialService } from "../../inventory/materials/services/MaterialService";
import { SstStatusCard } from "./SstStatusCard";
import { SstValuesTable } from "./SstValuesTable";
import { StandardEntryForm, StandardMaterialOption } from "./StandardEntryForm";
import { findMissingSstFields } from "./sstValidation";
import { ReportUploadPanel } from "../evidence/ReportUploadPanel";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { ReasonDialog } from "../../laboratoryConfiguration/masterDataSimple/solutionMaster/ReasonDialog";
import { useTechnique } from "../useTechnique";
import type { HplcRunDto, HplcSstRecordDto, SaveSstAnalyteInput, HplcMethodSnapshot } from "../types";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";

export interface SystemSuitabilityPanelProps {
  run: HplcRunDto;
  method?: HplcMethodSnapshot | HplcMethodResponse | null;
  canOperate: boolean;
  onRunUpdated: () => void;
}

export function SystemSuitabilityPanel({
  run,
  method,
  canOperate,
  onRunUpdated
}: SystemSuitabilityPanelProps) {
  const { label } = useTechnique();
  const isResidualSolvents = method?.resultMode === "ResidualSolvents";
  const hasInternalStandard = Boolean((method as HplcMethodSnapshot)?.internalStandardName);
  const [currentRun, setCurrentRun] = useState<HplcRunDto>(run);

  useEffect(() => {
    setCurrentRun(run);
  }, [run]);

  const sst: HplcSstRecordDto | undefined = currentRun.sst ?? undefined;

  // Reference standard lots
  const [standardLots, setStandardLots] = useState<StandardMaterialOption[]>([]);
  const [loadingLots, setLoadingLots] = useState(true);

  // Form state: analyteId -> SaveSstAnalyteInput
  const [analyteInputs, setAnalyteInputs] = useState<Record<number, SaveSstAnalyteInput>>({});

  // Actions state
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Set by the first save attempt; the missing list then updates as fields are filled.
  const [checkMissing, setCheckMissing] = useState(false);

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

        const internalStandardResponses: number[] = Array.from({ length: injectionCount }, (_, idx) => {
          const matched = a.injections?.find((inj) => inj.injectionNo === idx + 1);
          return matched?.internalStandardResponse ?? 0;
        });

        next[a.id] = {
          hplcSstAnalyteId: a.id,
          standardMaterialId: a.standardMaterialId ?? 0,
          standardWeightMg: a.standardWeightMg ?? 0,
          responses,
          internalStandardResponses: hasInternalStandard ? internalStandardResponses : undefined,
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
  }, [sst, method, hasInternalStandard]);

  const isConfirmed = sst?.status === "Passed" || sst?.status === "Failed" || currentRun.status !== "Open";
  const isPending = sst?.status === "Pending" && currentRun.status === "Open";

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

  const methodAnalyteFor = (a: { hplcMethodAnalyteId: number; analyteName: string }) =>
    method?.analytes.find((ma) => ma.id === a.hplcMethodAnalyteId || ma.name === a.analyteName);

  // Blank entries per analyte, so the analyst is warned before saving.
  const findMissing = (): string[] =>
    findMissingSstFields(sst?.analytes ?? [], analyteInputs, method, isResidualSolvents, hasInternalStandard);

  // Save SST inputs
  // refresh=false keeps the page mounted (the parent reload shows a spinner),
  // so the signature dialog can open straight after the save.
  const handleSaveSst = async (refresh = true): Promise<boolean> => {
    if (!sst) return false;
    setCheckMissing(true);
    const gaps = findMissing();
    if (gaps.length > 0) {
      setError(`Please complete all required fields before saving: ${gaps.join("; ")}`);
      return false;
    }
    setSaving(true);
    setError(null);

    const payload: SaveSstAnalyteInput[] = Object.values(analyteInputs);

    try {
      await HplcWorkspaceService.saveSst(currentRun.id, { analytes: payload });
      toast.success("System suitability data saved.");
      if (refresh) onRunUpdated();
      return true;
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not save system suitability.");
      return false;
    } finally {
      setSaving(false);
    }
  };

  // Confirm SST via signature
  const handleConfirmSignature = async (password: string) => {
    if (!sst) return;
    try {
      await HplcWorkspaceService.confirmSst(currentRun.id, {
        password,
        comment: signComment.trim() || null
      });
      toast.success("System suitability confirmed.");
      setSignOpen(false);
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(e.response?.data?.message ?? e.message ?? "Confirmation failed.");
    }
  };

  // Abandon run
  const handleAbandonRun = async () => {
    if (!abandonReason.trim()) return;
    setAbandoning(true);
    try {
      await HplcWorkspaceService.abandonRun(currentRun.id, abandonReason.trim());
      toast.success(`${label} run abandoned.`);
      setAbandonOpen(false);
      onRunUpdated();
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      toast.error(e.response?.data?.message ?? e.message ?? `Could not abandon ${label} run.`);
    } finally {
      setAbandoning(false);
    }
  };

  const missing = checkMissing ? findMissing() : [];

  return (
    <Stack spacing={2.5}>
      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {/* SST Overall Status Card */}
      {sst && (
        <SstStatusCard
          code={sst.code}
          status={sst.status}
          failureReasons={sst.failureReasons}
          confirmedByUserName={sst.confirmedByUserName}
          confirmedAt={sst.confirmedAt}
        />
      )}

      {/* SST Criteria and Results Table */}
      {sst && (
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1.5 }}>
            Suitability Criteria & Results
          </Typography>
          <SstValuesTable analytes={sst.analytes} method={method as unknown as HplcMethodResponse} />
        </Box>
      )}

      {/* Analyte Calibration & Injection Input Forms */}
      <Box>
        <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1.5 }}>
          Standard Calibration & Injections
        </Typography>

        {loadingLots ? (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 3 }}>
            <CircularProgress size={20} />
            <Typography variant="body2" sx={{ color: "text.secondary" }}>
              Loading reference standards...
            </Typography>
          </Box>
        ) : (
          <Stack spacing={2.5}>
            {sst?.analytes.map((a) => {
              const methodAnalyte = methodAnalyteFor(a);
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
                  showMissing={missing.length > 0}
                  isResidualSolvents={isResidualSolvents}
                  hasInternalStandard={hasInternalStandard}
                  internalStandardName={(method as HplcMethodSnapshot)?.internalStandardName}
                />
              );
            })}
          </Stack>
        )}
      </Box>

      {/* Standard report evidence upload */}
      <ReportUploadPanel
        runId={currentRun.id}
        context="Sst"
        kind="StandardReport"
        evidenceList={currentRun.evidence}
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
          >
            Abandon Run...
          </Button>

          <Stack direction="row" spacing={2} sx={{ alignItems: "flex-start" }}>
            <Button
              variant="outlined"
              startIcon={<SaveIcon />}
              onClick={() => handleSaveSst()}
              disabled={saving}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : "Save System Suitability"}
            </Button>

            <Box sx={{ display: "flex", flexDirection: "column", alignItems: "flex-end" }}>
              <Tooltip
                title={
                  !currentRun.canConfirmSst
                    ? currentRun.canConfirmSstReason || "Standard report upload and valid criteria are required."
                    : ""
                }
              >
                <span>
                  <Button
                    variant="contained"
                    color="success"
                    startIcon={<VerifiedUserIcon />}
                    disabled={!currentRun.canConfirmSst || saving}
                    onClick={async () => {
                      // Confirm signs what is stored, so save the screen first;
                      // otherwise unsaved entries are signed as blanks.
                      if (!(await handleSaveSst(false))) return;
                      setSignComment("");
                      setSignOpen(true);
                    }}
                    sx={{ textTransform: "none", fontWeight: 600 }}
                  >
                    Confirm System Suitability...
                  </Button>
                </span>
              </Tooltip>
              {!currentRun.canConfirmSst && currentRun.canConfirmSstReason && (
                <Typography variant="caption" sx={{ color: "text.secondary", mt: 0.5, maxWidth: 320, textAlign: "right" }}>
                  {currentRun.canConfirmSstReason}
                </Typography>
              )}
            </Box>
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
        title={`Abandon ${label} Run`}
        onClose={() => setAbandonOpen(false)}
        onConfirm={handleAbandonRun}
        confirmText="Abandon Run"
        confirmColor="error"
        loading={abandoning}
        loadingText="Abandoning..."
        reason={abandonReason}
        onReasonChange={setAbandonReason}
        label="Reason for Abandoning"
        placeholder="Document why this run is being abandoned (e.g. system suitability failure, instrument pressure error)..."
      />
    </Stack>
  );
}
