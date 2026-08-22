import { FloatingDialog } from "../../components/FloatingDialog";
import { SampleCard, TestOrderSummary } from "./types/workspaceTypes";
import { ResultEntryDialog } from "./ResultEntryDialog";
import { TestWorkflowDialog } from "./TestWorkflowDialog";

interface Props {
  open: boolean;
  test: TestOrderSummary | null;
  sample: SampleCard | null;
  onClose: () => void;
}

// Routes to the frozen workflow dialog for this test - "No navigation
// between pages." Every test code - Water's, AfterCleaning's, and EM's,
// each of which used to have its own bypass dialog that skipped straight
// to a raw result entry with no incubation step at all - goes through
// the single generic TestWorkflowDialog, which reads the test's
// configured workflow template from Test Master (see TestWorkflowEngine)
// instead of the code or category. CountTest/Observation/DualPlate are
// the only workflow shapes; EM's trend check against RoomTestConfiguration
// is folded into TestWorkflowEngine's CountTest path (see
// RecordCountTestAsync) rather than living in a separate engine/dialog.
// If a test code has no template configured, the dialog surfaces that
// explicitly rather than silently bypassing it.
export function TestWorkflowDialogRouter({ open, test, sample, onClose }: Props) {
  if (!test || !sample) return null;

  const title = `${test.testCode} Workflow`;

  return (
    <FloatingDialog open={open} title={title} onClose={onClose}>
      <TestWorkflowDialog
        testOrderId={test.testOrderId}
        testCode={test.testCode}
        category={sample.category}
        displayName={sample.displayName}
        onClose={onClose}
      />
    </FloatingDialog>
  );
}
