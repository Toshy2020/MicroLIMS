import { useState, useEffect, useCallback, useMemo } from "react";
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
import { useTechnique } from "../useTechnique";
import type {
  HplcInstrumentDto,
  HplcMethodOptionDto,
  HplcMobilePhaseAssignmentInput
} from "../types";

const STEPS_HPLC = ["Select Method", "Select Column", "Assign Mobile Phases", "Review & Start"];
const STEPS_GC = ["Select Method", "Select Column", "Review & Start"];

export function StartHplcRunWizard() {
  const navigate = useNavigate();
  const { technique, label, routes } = useTechnique();
  const { instrumentId } = useParams<{ instrumentId: string }>();
  const equipmentId = Number(instrumentId);
  const steps = technique === "Gc" ? STEPS_GC : STEPS_HPLC;

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
      HplcWorkspaceService.getInstruments(technique),
      HplcWorkspaceService.getMethodOptions(technique),
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
  }, [equipmentId, technique]);

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

    let mobilePhases: HplcMobilePhaseAssignmentInput[] = [];
    if (technique !== "Gc") {
      // Check all mobile phases are assigned
      const missingChannel = selectedMethod.mobilePhases.find((mp) => !selectedMobilePhases[mp.channel]);
      if (missingChannel) {
        setError(`Please select a prepared mobile phase for Channel ${missingChannel.channel}.`);
        return;
      }

      mobilePhases = selectedMethod.mobilePhases.map((mp) => ({
        channel: mp.channel,
        solutionPreparationId: selectedMobilePhases[mp.channel]
      }));
    }

    setStarting(true);
    setError(null);
    try {
      const run = await HplcWorkspaceService.startRun({
        equipmentId,
        hplcMethodId: selectedMethodId,
        chromatographyColumnId: selectedColumnId,
        mobilePhases
      });
      navigate(routes.run(equipmentId, run.id), { replace: true });
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
    if (technique !== "Gc" && activeStep === 2) {
      if (!selectedMethod) return false;
      return selectedMethod.mobilePhases.every((mp) => Boolean(selectedMobilePhases[mp.channel]));
    }
    return true;
  };

  const filteredMethodOptions = useMemo(
    () => methodOptions.filter((m: HplcMethodOptionDto) => !m.technique || m.technique === technique),
    [methodOptions, technique]
  );

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  const selectedCol = columns.find((c) => c.id === selectedColumnId);
  const selectedMethodOption = filteredMethodOptions.find((m: HplcMethodOptionDto) => m.id === selectedMethodId);
  const targetDesignation = (selectedMethod?.columnDesignation ?? selectedMethodOption?.columnDesignation ?? "").trim();

  return (
    <Box sx={{ p: { xs: 2, md: 3 }, maxWidth: 1100, mx: "auto" }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate(routes.root)}
        sx={{ mb: 2, textTransform: "none", color: "text.secondary" }}
      >
        Back to Instruments
      </Button>

      <PageHeader
        title={`Start ${label} Run: ${instrument?.name || `Instrument #${equipmentId}`}`}
        subtitle={`Set up method and column${technique !== "Gc" ? ", and mobile phases" : ""} for ${instrument?.code || ""}`}
      />

      <Stepper activeStep={activeStep} sx={{ my: 3 }}>
        {steps.map((stepLabel) => (
          <Step key={stepLabel}>
            <StepLabel>{stepLabel}</StepLabel>
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
          methodOptions={filteredMethodOptions}
          selectedMethodId={selectedMethodId}
          selectedMethod={selectedMethod}
          technique={technique}
          onSelectMethod={(id) => setSelectedMethodId(id)}
        />
      )}

      {activeStep === 1 && (
        <WizardColumnStep
          columns={columns}
          selectedColumnId={selectedColumnId}
          equipmentId={equipmentId}
          targetDesignation={targetDesignation}
          technique={technique}
          onSelectColumn={(id) => setSelectedColumnId(id)}
        />
      )}

      {technique !== "Gc" && activeStep === 2 && selectedMethod && (
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

      {((technique === "Gc" && activeStep === 2) || (technique !== "Gc" && activeStep === 3)) && selectedMethod && (
        <WizardReviewStep
          instrumentName={instrument?.name}
          instrumentCode={instrument?.code}
          method={selectedMethod}
          columnName={selectedCol?.name}
          columnCode={selectedCol?.code}
          availablePreparations={availablePreparations}
          selectedMobilePhases={selectedMobilePhases}
          technique={technique}
        />
      )}

      {/* Navigation Buttons */}
      <Box sx={{ display: "flex", justifyContent: "space-between", mt: 4 }}>
        <Button
          disabled={activeStep === 0 || starting}
          onClick={() => setActiveStep((prev) => prev - 1)}
        >
          Back
        </Button>

        {activeStep < steps.length - 1 ? (
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
            {starting ? "Starting Run..." : `Start ${label} Run`}
          </Button>
        )}
      </Box>
    </Box>
  );
}
