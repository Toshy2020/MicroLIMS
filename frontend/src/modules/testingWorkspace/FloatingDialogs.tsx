import { FloatingDialog } from "../../components/FloatingDialog";
import { TestOrderSummary } from "./types/workspaceTypes";
import { PathogenDialog } from "../pathogen/PathogenDialog";
import { WaterWorkflowDialog } from "../laboratoryConfiguration/water/WaterWorkflowDialog";
import { EMIncubationDialog } from "../laboratoryConfiguration/environmentalMonitoring/EMIncubationDialog";
import { AfterCleaningResultDialog } from "../laboratoryConfiguration/afterCleaning/AfterCleaningResultDialog";
import { ResultEntryDialog } from "./ResultEntryDialog";

interface Props {
  open: boolean;
  test: TestOrderSummary | null;
  category?: string;
  onClose: () => void;
}

// Routes to the frozen workflow dialog for this test - "No navigation
// between pages." Primarily routes by the sample's category (Water/EM/
// AfterCleaning/Product), since TestCode alone is ambiguous (a Water
// sampling point and a Product Item can both be configured with "TAMC").
// PATHOGEN_* is checked first since it applies across every category.
export function TestWorkflowDialog({ open, test, category, onClose }: Props) {
  if (!test) return null;

  const code = test.testCode.toUpperCase();
  const isPathogen = code.startsWith("PATHOGEN_");
  const isAfterCleaning = category === "AfterCleaning" || code.startsWith("TAMC:");
  const isEM = category === "EnvironmentalMonitoring" || code.startsWith("EM_");
  const isWater = category === "Water";

  const title = `${test.testCode} Workflow`;

  return (
    <FloatingDialog open={open} title={title} onClose={onClose}>
      {isPathogen && <PathogenDialog testOrderId={test.testOrderId} testCode={test.testCode} />}
      {!isPathogen && isEM && <EMIncubationDialog testOrderId={test.testOrderId} />}
      {!isPathogen && !isEM && isAfterCleaning && <AfterCleaningResultDialog testOrderId={test.testOrderId} />}
      {!isPathogen && !isEM && !isAfterCleaning && isWater && <WaterWorkflowDialog testOrderId={test.testOrderId} />}
      {!isPathogen && !isEM && !isAfterCleaning && !isWater && <ResultEntryDialog testOrderId={test.testOrderId} testCode={test.testCode} />}
    </FloatingDialog>
  );
}
