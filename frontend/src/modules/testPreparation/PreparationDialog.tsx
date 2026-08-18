import { FloatingDialog } from "../../components/FloatingDialog";
import { TestPreparationForm } from "./TestPreparationForm";
import { EMPreparationForm } from "../laboratoryConfiguration/environmentalMonitoring/EMPreparationForm";
import { AfterCleaningPreparationForm } from "../laboratoryConfiguration/afterCleaning/AfterCleaningPreparationForm";
import { WaterPreparationForm } from "../laboratoryConfiguration/water/WaterPreparationForm";

interface Props {
  open: boolean;
  sample: { sampleId: number; category: string; departmentId?: number | null; machineId?: number | null; waterDepartmentId?: number | null } | null;
  onClose: () => void;
}

// Opened directly from a Testing Workspace card when a sample "Needs
// Preparation" - routes to the right form by category, reusing the same
// forms the standalone EM/After Cleaning/Water/Test Preparation pages use.
export function PreparationDialog({ open, sample, onClose }: Props) {
  if (!sample) return null;

  return (
    <FloatingDialog open={open} title="Preparation" onClose={onClose}>
      {sample.category === "EnvironmentalMonitoring" && sample.departmentId != null && (
        <EMPreparationForm sampleId={sample.sampleId} departmentId={sample.departmentId} onComplete={onClose} />
      )}
      {sample.category === "AfterCleaning" && sample.machineId != null && (
        <AfterCleaningPreparationForm sampleId={sample.sampleId} machineId={sample.machineId} onComplete={onClose} />
      )}
      {sample.category === "Water" && sample.waterDepartmentId != null && (
        <WaterPreparationForm sampleId={sample.sampleId} waterDepartmentId={sample.waterDepartmentId} onComplete={onClose} />
      )}
      {sample.category !== "EnvironmentalMonitoring" && sample.category !== "AfterCleaning" && sample.category !== "Water" && (
        <TestPreparationForm sample={sample} onSaved={onClose} />
      )}
    </FloatingDialog>
  );
}
