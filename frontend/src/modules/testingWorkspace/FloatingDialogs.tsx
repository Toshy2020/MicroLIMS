import { lazy, Suspense, useEffect } from "react";
import { Box, Typography } from "@mui/material";
import { FloatingDialog } from "../../components/FloatingDialog";
import { DialogBodySkeleton } from "../../components/DialogBodySkeleton";
import { SampleCard, TestOrderSummary } from "./types/workspaceTypes";

// The workflow dialog pulls in every step panel, so it is split out of the
// workspace page's bundle. The chunk is fetched once the browser is idle
// after the page mounts, so opening a test normally finds it already loaded.
const loadTestWorkflowDialog = () => import("./TestWorkflowDialog");
const TestWorkflowDialog = lazy(() => loadTestWorkflowDialog().then((m) => ({ default: m.TestWorkflowDialog })));

function usePrefetchTestWorkflowDialog() {
  useEffect(() => {
    const prefetch = () => { loadTestWorkflowDialog().catch(() => undefined); };
    if (typeof window.requestIdleCallback === "function") {
      const id = window.requestIdleCallback(prefetch, { timeout: 3000 });
      return () => window.cancelIdleCallback(id);
    }
    const id = window.setTimeout(prefetch, 1500);
    return () => window.clearTimeout(id);
  }, []);
}

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
  usePrefetchTestWorkflowDialog();
  if (!test || !sample) return null;

  // Sample reference, sample name and test order on a second line, so the
  // analyst can check the tube in hand matches before recording anything.
  const subtitle = [sample.referenceNumber, sample.displayName, `TO #${test.testOrderId}`].filter(Boolean).join(" · ");
  const title = (
    <Box sx={{ minWidth: 0 }}>
      <Box component="span" sx={{ display: "block" }}>{test.testCode} Workflow</Box>
      <Typography
        variant="caption"
        title={subtitle}
        sx={{ display: "block", color: "text.secondary", fontWeight: 400, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
      >
        {subtitle}
      </Typography>
    </Box>
  );

  return (
    <FloatingDialog open={open} title={title} onClose={onClose}>
      <Suspense fallback={<DialogBodySkeleton blocks={3} blockHeight={48} />}>
      <TestWorkflowDialog
        testOrderId={test.testOrderId}
        testCode={test.testCode}
        category={sample.category}
        displayName={sample.displayName}
        itemId={sample.itemId ?? undefined}
        sampleId={sample.sampleId}
        onClose={onClose}
      />
      </Suspense>
    </FloatingDialog>
  );
}
