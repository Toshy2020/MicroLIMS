import { useState, useEffect, useCallback } from "react";
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
import { SignatureDialog } from "../../components/SignatureDialog";
import { SelectStep } from "./wizard/SelectStep";
import { LotsStep } from "./wizard/LotsStep";
import { ReviewStep } from "./wizard/ReviewStep";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import { toast } from "sonner";
import type {
  SolutionType,
  SolutionPreparationResponse,
  PreparationComponentInput
} from "./types";

const STEPS = ["Select Recipe & Method", "Lots & Quantities", "Review & Sign"];

export function PreparationWizardPage() {
  const { id } = useParams<{ id?: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  // Wizard state
  const [activeStep, setActiveStep] = useState(id ? 1 : 0);
  const [preparation, setPreparation] = useState<SolutionPreparationResponse | null>(null);
  const [loading, setLoading] = useState(Boolean(id));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Signature state
  const [signOpen, setSignOpen] = useState(false);
  const [signComment, setSignComment] = useState("");

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
        toast.info("This preparation is closed.");
        navigate(`/preparation/${prepId}`, { replace: true });
        return;
      }
      setPreparation(data);
      setActiveStep(1);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load preparation.");
    } finally {
      setLoading(false);
    }
  }, [navigate]);

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
      toast.success("Preparation started.");
      navigate(`/preparation/${data.id}/edit`, { replace: true });
      setActiveStep(1);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to start preparation.");
    } finally {
      setSaving(false);
    }
  };

  // Step 2: Save
  const handleSave = async (
    components: PreparationComponentInput[],
    finalVolumeMl: number | null,
    measuredPh: number | null
  ) => {
    if (!preparation) return;
    setSaving(true);
    try {
      const updated = await SolutionPreparationService.save(preparation.id, {
        components,
        finalVolumeMl,
        measuredPh
      });
      setPreparation(updated);
      toast.success("Draft saved successfully.");
    } catch (err: unknown) {
      setSaving(false);
      throw err;
    } finally {
      setSaving(false);
    }
  };

  // Step 4: Complete with Signature
  const handleConfirmSignature = async (password: string) => {
    if (!preparation) return;
    const res = await SolutionPreparationService.complete(preparation.id, {
      password,
      comment: signComment.trim() || null
    });
    setSignOpen(false);
    toast.success(`Preparation completed successfully. Code: ${res.code ?? "—"}`);
    navigate(`/preparation/${res.id}`);
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
        title={preparation ? `Solution Preparation: ${preparation.solutionMasterName}` : "New Solution Preparation"}
        subtitle="Prepare mobile phases, diluents, and titrants from master recipes against stock lots"
      />

      <Stepper activeStep={activeStep} sx={{ mb: 4, mt: 1 }}>
        {STEPS.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

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

      {/* 21 CFR Part 11 Electronic Signature Dialog */}
      <SignatureDialog
        open={signOpen}
        meaningStatement="I confirm that this solution was prepared according to the approved recipe and that all listed lot quantities were consumed."
        showComment
        comment={signComment}
        onCommentChange={setSignComment}
        onCancel={() => setSignOpen(false)}
        onConfirm={handleConfirmSignature}
      />
    </Box>
  );
}
