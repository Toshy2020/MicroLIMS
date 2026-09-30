import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Stepper,
  Step,
  StepLabel,
  Button,
  Alert,
  CircularProgress
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { PageHeader } from "../../../components/PageHeader";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import {
  HplcMethodService,
  HplcMethodResponse
} from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import {
  ChromatographyColumnService,
  ChromatographyColumnDto
} from "../../laboratoryConfiguration/masterDataSimple/services/ChromatographyColumnService";
import { SolutionPreparationService } from "../../solutionPreparation/services/SolutionPreparationService";
import type { SolutionPreparationListItem } from "../../solutionPreparation/types";
import { WizardMethodStep } from "./WizardMethodStep";
import { WizardColumnStep } from "./WizardColumnStep";
import { WizardMobilePhasesStep } from "./WizardMobilePhasesStep";
import { WizardReviewStep } from "./WizardReviewStep";
import type {
  HplcInstrumentDto,
  HplcMethodOptionDto,
  HplcMobilePhaseAssignmentInput
} from "../types";

const STEPS = ["Select Method", "Select Column", "Assign Mobile Phases", "Review & Start"];

export function StartHplcRunWizard() {
  const navigate = useNavigate();
  const { instrumentId } = useParams<{ instrumentId: string }>();
  const equipmentId = Number(instrumentId);

  const [activeStep, setActiveStep] = useState(0);
  const [instrument, setInstrument] = useState<HplcInstrumentDto | null>(null);

  // Step 1: Method
  const [methodOptions, setMethodOptions] = useState<HplcMethodOptionDto[]>([]);
  const [selectedMethodId, setSelectedMethodId] = useState<number | null>(null);
  const [selectedMethod, setSelectedMethod] = useState<HplcMethodResponse | null>(null);

  // Step 2: Column
  const [columns, setColumns] = useState<ChromatographyColumnDto[]>([]);
  const [selectedColumnId, setSelectedColumnId] = useState<number | null>(null);

  // Step 3: Mobile Phases
  const [availablePreparations, setAvailablePreparations] = useState<Record<string, SolutionPreparationListItem[]>>({});
  const [selectedMobilePhases, setSelectedMobilePhases] = useState<Record<string, number>>({});
  const [loadingPreps, setLoadingPreps] = useState(false);

  // General state
  const [loading, setLoading] = useState(true);
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load initial data
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);

    Promise.all([
      HplcWorkspaceService.getInstruments(),
      HplcWorkspaceService.getMethodOptions(),
      ChromatographyColumnService.getAll(true)
    ])
      .then(([instList, methods, cols]) => {
        if (!active) return;
        const matchedInst = instList.find((i) => i.equipmentId === equipmentId);
        if (matchedInst) {
          setInstrument(matchedInst);
        }
        setMethodOptions(methods);
        setColumns(cols);
      })
      .catch((err: unknown) => {
        if (!active) return;
        const e = err as { response?: { data?: { message?: string } }; message?: string };
        setError(e.response?.data?.message ?? e.message ?? "Could not load run prerequisites.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [equipmentId]);

  // Load full method details when method is selected
  useEffect(() => {
    if (!selectedMethodId) {
      setSelectedMethod(null);
      return;
    }
    let active = true;
    HplcMethodService.getById(selectedMethodId)
      .then((data) => {
        if (active) setSelectedMethod(data);
      })
      .catch(() => {
        if (active) setSelectedMethod(null);
      });
    return () => {
      active = false;
    };
  }, [selectedMethodId]);

  // Reset selected column if method changes
  useEffect(() => {
    setSelectedColumnId(null);
  }, [selectedMethodId]);

  // Fetch available mobile phase preparations for each channel
  const loadAvailablePreparations = useCallback(async (m: HplcMethodResponse) => {
    setLoadingPreps(true);
    try {
      const results: Record<string, SolutionPreparationListItem[]> = {};
      await Promise.all(
        m.mobilePhases.map(async (mp) => {
          const preps = await SolutionPreparationService.getAvailable(mp.solutionMasterId, m.id);
          results[mp.channel] = preps;
        })
      );
      setAvailablePreparations(results);
    } catch {
      // Keep existing
    } finally {
      setLoadingPreps(false);
    }
  }, []);

  useEffect(() => {
    if (activeStep === 2 && selectedMethod) {
      loadAvailablePreparations(selectedMethod);
    }
  }, [activeStep, selectedMethod, loadAvailablePreparations]);

  // Handle Start Run
  const handleStartRun = async () => {
    if (!selectedMethodId || !selectedColumnId || !selectedMethod) return;

    // Check all mobile phases are assigned
    const missingChannel = selectedMethod.mobilePhases.find((mp) => !selectedMobilePhases[mp.channel]);
    if (missingChannel) {
      setError(`Please select a prepared mobile phase for Channel ${missingChannel.channel}.`);
      return;
    }

    const mobilePhases: HplcMobilePhaseAssignmentInput[] = selectedMethod.mobilePhases.map((mp) => ({
      channel: mp.channel,
      solutionPreparationId: selectedMobilePhases[mp.channel]
    }));

    setStarting(true);
    setError(null);
    try {
      const run = await HplcWorkspaceService.startRun({
        equipmentId,
        hplcMethodId: selectedMethodId,
        chromatographyColumnId: selectedColumnId,
        mobilePhases
      });
      navigate(`/hplc-workspace/${equipmentId}/run/${run.id}`, { replace: true });
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Failed to start run.");
    } finally {
      setStarting(false);
    }
  };

  // Validation per step
  const canProceed = () => {
    if (activeStep === 0) return Boolean(selectedMethodId);
    if (activeStep === 1) return Boolean(selectedColumnId);
    if (activeStep === 2) {
      if (!selectedMethod) return false;
      return selectedMethod.mobilePhases.every((mp) => Boolean(selectedMobilePhases[mp.channel]));
    }
    return true;
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  const selectedCol = columns.find((c) => c.id === selectedColumnId);
  const selectedMethodOption = methodOptions.find((m) => m.id === selectedMethodId);
  const targetDesignation = (selectedMethod?.columnDesignation ?? selectedMethodOption?.columnDesignation ?? "").trim();

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 1100, mx: "auto" }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate("/hplc-workspace")}
        sx={{ mb: 2, textTransform: "none", color: "text.secondary" }}
      >
        Back to Instruments
      </Button>

      <PageHeader
        title={`Start HPLC Run — ${instrument?.name || `Instrument #${equipmentId}`}`}
        subtitle={`Set up method, column, and mobile phases for ${instrument?.code || ""}`}
      />

      <Stepper activeStep={activeStep} sx={{ my: 3 }}>
        {STEPS.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      {error && (
        <Alert severity="error" sx={{ mb: 3 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      {activeStep === 0 && (
        <WizardMethodStep
          methodOptions={methodOptions}
          selectedMethodId={selectedMethodId}
          selectedMethod={selectedMethod}
          onSelectMethod={(id) => setSelectedMethodId(id)}
        />
      )}

      {activeStep === 1 && (
        <WizardColumnStep
          columns={columns}
          selectedColumnId={selectedColumnId}
          equipmentId={equipmentId}
          targetDesignation={targetDesignation}
          onSelectColumn={(id) => setSelectedColumnId(id)}
        />
      )}

      {activeStep === 2 && selectedMethod && (
        <WizardMobilePhasesStep
          method={selectedMethod}
          availablePreparations={availablePreparations}
          selectedMobilePhases={selectedMobilePhases}
          loadingPreps={loadingPreps}
          onSelectPreparation={(ch, prepId) =>
            setSelectedMobilePhases((prev) => ({ ...prev, [ch]: prepId }))
          }
          onRefreshPreparations={() => loadAvailablePreparations(selectedMethod)}
        />
      )}

      {activeStep === 3 && selectedMethod && (
        <WizardReviewStep
          instrumentName={instrument?.name}
          instrumentCode={instrument?.code}
          method={selectedMethod}
          columnName={selectedCol?.name}
          columnCode={selectedCol?.code}
          availablePreparations={availablePreparations}
          selectedMobilePhases={selectedMobilePhases}
        />
      )}

      {/* Navigation Buttons */}
      <Box sx={{ display: "flex", justifyContent: "space-between", mt: 4 }}>
        <Button
          disabled={activeStep === 0 || starting}
          onClick={() => setActiveStep((prev) => prev - 1)}
          sx={{ textTransform: "none" }}
        >
          Back
        </Button>

        {activeStep < STEPS.length - 1 ? (
          <Button
            variant="contained"
            disabled={!canProceed()}
            onClick={() => setActiveStep((prev) => prev + 1)}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            Next
          </Button>
        ) : (
          <Button
            variant="contained"
            color="primary"
            disabled={starting || !canProceed()}
            onClick={handleStartRun}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            {starting ? "Starting Run..." : "Start HPLC Run"}
          </Button>
        )}
      </Box>
    </Box>
  );
}
