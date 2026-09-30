import { Stack } from "@mui/material";
import { ElutionMode, HplcDetectorType } from "../services/HplcMethodService";
import { GradientStepRowState, MobilePhaseRowState } from "./hplcMethodForm";
import { HplcElutionSection } from "./HplcElutionSection";
import { HplcDetectionSection } from "./HplcDetectionSection";

export interface HplcElutionDetectionTabProps {
  elutionMode: ElutionMode;
  flowRateMlPerMin: string | number;
  equilibrationMin: string | number;
  gradientSteps: GradientStepRowState[];
  mobilePhases: MobilePhaseRowState[];
  detectorType: HplcDetectorType;
  injectionVolumeUl: string | number;
  runTimeMin: string | number;
  onElutionModeChange: (val: ElutionMode) => void;
  onFlowRateMlPerMinChange: (val: string) => void;
  onEquilibrationMinChange: (val: string) => void;
  onGradientStepsChange: (steps: GradientStepRowState[]) => void;
  onDetectorTypeChange: (val: HplcDetectorType) => void;
  onInjectionVolumeUlChange: (val: string) => void;
  onRunTimeMinChange: (val: string) => void;
}

export function HplcElutionDetectionTab({
  elutionMode,
  flowRateMlPerMin,
  equilibrationMin,
  gradientSteps,
  mobilePhases,
  detectorType,
  injectionVolumeUl,
  runTimeMin,
  onElutionModeChange,
  onFlowRateMlPerMinChange,
  onEquilibrationMinChange,
  onGradientStepsChange,
  onDetectorTypeChange,
  onInjectionVolumeUlChange,
  onRunTimeMinChange
}: HplcElutionDetectionTabProps) {
  const handleGradientStepChange = (idx: number, field: keyof GradientStepRowState, val: string) => {
    const next = [...gradientSteps];
    next[idx] = { ...next[idx], [field]: val };
    onGradientStepsChange(next);
  };

  const handleAddGradientStep = () => {
    const last = gradientSteps[gradientSteps.length - 1];
    const nextTime = last ? Number(last.timeMin || 0) + 5 : 5;
    onGradientStepsChange([
      ...gradientSteps,
      { timeMin: String(nextTime), percentA: "0", percentB: "100", percentC: "0", percentD: "0" }
    ]);
  };

  const handleRemoveGradientStep = (idx: number) => {
    onGradientStepsChange(gradientSteps.filter((_, i) => i !== idx));
  };

  return (
    <Stack spacing={2}>
      <HplcElutionSection
        elutionMode={elutionMode}
        flowRateMlPerMin={flowRateMlPerMin}
        equilibrationMin={equilibrationMin}
        gradientSteps={gradientSteps}
        mobilePhaseChannels={mobilePhases.map((p) => p.channel)}
        onElutionModeChange={onElutionModeChange}
        onFlowRateMlPerMinChange={onFlowRateMlPerMinChange}
        onEquilibrationMinChange={onEquilibrationMinChange}
        onGradientStepChange={handleGradientStepChange}
        onAddGradientStep={handleAddGradientStep}
        onRemoveGradientStep={handleRemoveGradientStep}
      />

      <HplcDetectionSection
        detectorType={detectorType}
        injectionVolumeUl={injectionVolumeUl}
        runTimeMin={runTimeMin}
        onDetectorTypeChange={onDetectorTypeChange}
        onInjectionVolumeUlChange={onInjectionVolumeUlChange}
        onRunTimeMinChange={onRunTimeMinChange}
      />
    </Stack>
  );
}
