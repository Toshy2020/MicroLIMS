import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, useNavigate, useSearchParams } from "react-router-dom";
import {
  Box,
  Stepper,
  Step,
  StepLabel,
  Alert,
  CircularProgress
} from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { i18n, useTranslation } from "../../i18n";
import { getErrorMessage } from "../../utils/errorMessage";
import { SignatureDialog } from "../../components/SignatureDialog";
import { DraftRestoreNotice } from "../../components/bench/DraftRestoreNotice";
import { RegistryBenchKeypad } from "../../components/bench/BenchKeypad";
import { useBenchDraft } from "../../hooks/useBenchDraft";
import { useBenchMode } from "../../theme/BenchModeContext";
import {
  OfflineDraftNotice,
  classifySaveFailure,
  draftHeaderArgs,
  useKeptOffline
} from "../testingWorkspace/benchSave";
import { extractServerFieldErrors } from "../../utils/serverFieldErrors";
import { SelectStep } from "./wizard/SelectStep";
import { LotsStep, type LotsStepDraftState, type SolutionFieldKey } from "./wizard/LotsStep";
import { ReviewStep } from "./wizard/ReviewStep";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import type { LabelTarget } from "../../services/labelService";
import { toast } from "sonner";
import type {
  SolutionType,
  SolutionPreparationResponse,
  PreparationComponentInput,
  AutoclaveRecordInput
} from "./types";
import { useBreadcrumb } from "../../components/breadcrumbContext";

interface SolutionDraftValues extends Partial<LotsStepDraftState> {
  activeStep: number;
}

// Explicit allow-list of fields stored in the tablet draft.
// Signature password and comment are never included.
function pickSolutionDraft(v: SolutionDraftValues): Partial<SolutionDraftValues> {
  return {
    activeStep: v.activeStep,
    ...(v.components !== undefined ? { components: v.components } : {}),
    ...(v.finalVolumeText !== undefined ? { finalVolumeText: v.finalVolumeText } : {}),
    ...(v.measuredPhText !== undefined ? { measuredPhText: v.measuredPhText } : {}),
    ...(v.autoclave !== undefined
      ? {
          autoclave: {
            equipmentId: v.autoclave.equipmentId,
            program: v.autoclave.program,
            loadType: v.autoclave.loadType,
            temperature: v.autoclave.temperature,
            cycleTime: v.autoclave.cycleTime,
            cycleNumber: v.autoclave.cycleNumber
          }
        }
      : {})
  };
}

const SOLUTION_FIELD_MAP: Record<string, SolutionFieldKey> = {
  finalvolumeml: "finalVolumeMl",
  measuredph: "measuredPh",
  autoclaveequipmentid: "autoclaveEquipmentId",
  autoclaveprogram: "autoclaveProgram",
  autoclavecyclenumber: "autoclaveCycleNumber"
};

function matchSolutionField(text: string): SolutionFieldKey | null {
  if (/\bvolume\b/i.test(text)) return "finalVolumeMl";
  if (/\bph\b/i.test(text)) return "measuredPh";
  if (/\bcycle\s*number\b/i.test(text)) return "autoclaveCycleNumber";
  if (/\bprogram\b/i.test(text)) return "autoclaveProgram";
  if (/\bautoclave\b/i.test(text)) return "autoclaveEquipmentId";
  return null;
}

export function PreparationWizardPage() {
  const { id } = useParams<{ id?: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { benchMode } = useBenchMode();
  const { t } = useTranslation("solutionPreparation");
  const tEn = i18n.getFixedT("en", "solutionPreparation");
  const steps = [t("wizard.steps.select"), t("wizard.steps.lots"), t("wizard.steps.review")];

  // Wizard state
  const [activeStep, setActiveStep] = useState(id ? 1 : 0);
  const [preparation, setPreparation] = useState<SolutionPreparationResponse | null>(null);
  const [loading, setLoading] = useState(Boolean(id));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // The route id, not the in-memory record: a new preparation keeps its fixed label once step 1 has saved it.
  useBreadcrumb(!id ? t("wizard.breadcrumbNew") : preparation ? [preparation.code, preparation.solutionMasterName].filter(Boolean).join(" ") : error ? t("record.unavailable") : null);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<SolutionFieldKey, string>>>({});
  const [keptOffline, setKeptOffline] = useKeptOffline();
  const [lotsDraft, setLotsDraft] = useState<LotsStepDraftState | null>(null);
  const [restoredLotsDraft, setRestoredLotsDraft] = useState<Partial<LotsStepDraftState> | null>(null);

  // Signature state
  const [signOpen, setSignOpen] = useState(false);
  const [signComment, setSignComment] = useState("");

  const draftValues = useMemo<SolutionDraftValues>(
    () => ({ activeStep, ...(lotsDraft ?? {}) }),
    [activeStep, lotsDraft]
  );

  const draft = useBenchDraft<SolutionDraftValues>({
    formKey: "solution-preparation",
    recordKey: id ? String(id) : preparation ? String(preparation.id) : "new",
    recordVersion: preparation?.version ?? null,
    title: preparation ? t("wizard.draftTitle", { name: preparation.solutionMasterName }) : t("wizard.draftTitleNew"),
    values: draftValues,
    pick: pickSolutionDraft,
    enabled: benchMode
  });

  const handleRestore = () => {
    const restored = draft.acceptRestore();
    if (!restored) return;
    if (typeof restored.activeStep === "number") {
      setActiveStep(restored.activeStep);
    }
    setRestoredLotsDraft({
      components: restored.components,
      finalVolumeText: restored.finalVolumeText,
      measuredPhText: restored.measuredPhText,
      autoclave: restored.autoclave
    });
  };

  // Initial query params for new preparation
  const queryType = (searchParams.get("type") as SolutionType) || "MobilePhase";
  const querySolutionId = searchParams.get("solutionId")
    ? Number(searchParams.get("solutionId"))
    : null;
  const queryMethodId = searchParams.get("methodId")
    ? Number(searchParams.get("methodId"))
    : null;

  // Load existing preparation if editing
  const loadExisting = useCallback(async (prepId: number) => {
    setLoading(true);
    setError(null);
    try {
      const data = await SolutionPreparationService.getById(prepId);
      if (data.effectiveStatus !== "InProgress") {
        toast.info(t("wizard.closedInfo"));
        navigate(`/preparation/${prepId}`, { replace: true });
        return;
      }
      setPreparation(data);
      setActiveStep(1);
    } catch (err: unknown) {
      setError(getErrorMessage(err, t("wizard.loadError")));
    } finally {
      setLoading(false);
    }
  }, [navigate, t]);

  useEffect(() => {
    if (id) {
      loadExisting(Number(id));
    }
  }, [id, loadExisting]);

  // Step 1: Start
  const handleStart = async (solutionMasterId: number, hplcMethodId?: number | null) => {
    setSaving(true);
    setError(null);
    try {
      const data = await SolutionPreparationService.start({
        solutionMasterId,
        hplcMethodId
      });
      setPreparation(data);
      toast.success(t("wizard.startedToast"));
      navigate(`/preparation/${data.id}/edit`, { replace: true });
      setActiveStep(1);
    } catch (err: unknown) {
      setError(getErrorMessage(err, t("wizard.startFailed")));
    } finally {
      setSaving(false);
    }
  };

  // Step 2: Save
  const handleSave = async (
    components: PreparationComponentInput[],
    finalVolumeMl: number | null,
    measuredPh: number | null,
    autoclave: AutoclaveRecordInput | null
  ) => {
    if (!preparation) return;
    setSaving(true);
    setError(null);
    setFieldErrors({});
    setKeptOffline(false);
    try {
      const updated = await SolutionPreparationService.save(
        preparation.id,
        {
          components,
          finalVolumeMl,
          measuredPh,
          ...(autoclave ?? {})
        },
        ...draftHeaderArgs(draft.submitHeaders())
      );
      setPreparation(updated);
      toast.success(t("wizard.draftSavedToast"));
    } catch (err: unknown) {
      const failure = classifySaveFailure(err);
      if (benchMode && failure.kind === "network") {
        setKeptOffline(true);
        throw err;
      }
      if (benchMode && failure.kind === "conflict") {
        await draft.onRejected(failure.message);
        setError(failure.message);
        throw err;
      }
      const nextFieldErrors = extractServerFieldErrors(err, SOLUTION_FIELD_MAP, matchSolutionField);
      if (Object.keys(nextFieldErrors).length > 0) {
        setFieldErrors(nextFieldErrors);
      }
      throw err;
    } finally {
      setSaving(false);
    }
  };

  // Step 4: Complete with Signature
  const handleConfirmSignature = async (password: string) => {
    if (!preparation) return;
    try {
      const res = await SolutionPreparationService.complete(preparation.id, {
        password,
        comment: signComment.trim() || null
      });
      await draft.onSubmitted();
      setSignOpen(false);
      toast.success(t("wizard.completedToast", { code: res.code ?? "—" }));
      const offerPrintTarget: LabelTarget | undefined =
        res.id > 0 && res.code && res.code.trim().length > 0
          ? {
              entityType: "SolutionPreparation",
              entityId: res.id,
              code: res.code,
              displayName: res.solutionMasterName || res.code
            }
          : undefined;
      navigate(`/preparation/${res.id}`, offerPrintTarget ? { state: { offerPrintTarget } } : undefined);
    } catch (err: unknown) {
      const failure = classifySaveFailure(err);
      if (benchMode && failure.kind === "conflict") {
        await draft.onRejected(failure.message);
      }
      throw err;
    }
  };

  if (loading) {
    return (
      <Box sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center" }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box sx={{ p: 3, maxWidth: 1000, mx: "auto" }}>
      <PageHeader
        title={preparation ? t("wizard.pageTitleExisting", { name: preparation.solutionMasterName }) : t("wizard.pageTitleNew")}
        subtitle={t("wizard.pageSubtitle")}
      />

      <Stepper activeStep={activeStep} sx={{ mb: 4, mt: 1 }}>
        {steps.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      {benchMode && (
        <DraftRestoreNotice
          startedAt={draft.restoredStartedAt}
          onRestore={handleRestore}
          onDiscard={() => void draft.discard()}
        />
      )}

      {benchMode && keptOffline && (
        <OfflineDraftNotice onTryAgain={() => setKeptOffline(false)} />
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      {activeStep === 0 && (
        <SelectStep
          initialType={queryType}
          initialSolutionId={querySolutionId}
          initialMethodId={queryMethodId}
          onStart={handleStart}
          loading={saving}
        />
      )}

      {activeStep === 1 && preparation && (
        <LotsStep
          preparation={preparation}
          onSave={handleSave}
          onNext={() => setActiveStep(2)}
          saving={saving}
          restoredDraft={restoredLotsDraft}
          onDraftChange={setLotsDraft}
          fieldErrors={fieldErrors}
          onClearFieldError={(key) => setFieldErrors((prev) => (prev[key] ? { ...prev, [key]: undefined } : prev))}
        />
      )}

      {activeStep === 2 && preparation && (
        <ReviewStep
          preparation={preparation}
          onBack={() => setActiveStep(1)}
          onSign={() => setSignOpen(true)}
          signing={saving}
        />
      )}

      {benchMode && activeStep === 1 && (
        <Box sx={{ mt: 2 }}>
          <RegistryBenchKeypad />
        </Box>
      )}

      {/* 21 CFR Part 11 Electronic Signature Dialog */}
      <SignatureDialog
        open={signOpen}
        meaningStatement={tEn("wizard.signatureMeaning")}
        meaningStatementTranslation={t("wizard.signatureMeaning")}
        showComment
        comment={signComment}
        onCommentChange={setSignComment}
        onCancel={() => setSignOpen(false)}
        onConfirm={handleConfirmSignature}
      />
    </Box>
  );
}
