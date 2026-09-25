import { useCallback, useEffect, useState } from "react";
import { Box, Button, Snackbar, Alert } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";

import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { AuditHistoryDialog } from "../../components/AuditHistoryDialog";

import { SampleRecord } from "./types/receivingTypes";
import { ReceiveService } from "./services/ReceiveService";
import { SampleRegisterTable } from "./components/SampleRegisterTable";
import { NewSampleDialog } from "./dialogs/NewSampleDialog";
import { EditSampleDetailsDialog } from "./dialogs/EditSampleDetailsDialog";
import { VoidSampleConfirmationDialog } from "./dialogs/VoidSampleConfirmationDialog";
import { AddLaboratoryDialog } from "./dialogs/AddLaboratoryDialog";
import { SampleSummaryDialog } from "../testingWorkspace/SampleSummaryDialog";

// The main Receiving area only offers these three item-based categories
// (design.md §3.1) - Water/EM/After Cleaning are received from inside
// their owning lab's workspace instead.
const RECEIVING_CATEGORY_NAMES = ["FinishedProduct", "RawMaterial", "PackagingMaterial"];

// Receiving desk (Samples.Receive): register of FP/RM/PM samples, a
// "Receive" wizard with a required target-lab choice, and a signed
// "Add Laboratory" action for samples already on the register.
export function ReceivingPage() {
  const [samples, setSamples] = useState<SampleRecord[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [newSampleOpen, setNewSampleOpen] = useState(false);
  const [editSample, setEditSample] = useState<SampleRecord | null>(null);
  const [voidingSample, setVoidingSample] = useState<SampleRecord | null>(null);
  const [addingLabSample, setAddingLabSample] = useState<SampleRecord | null>(null);
  const [summarySampleId, setSummarySampleId] = useState<number | null>(null);
  const [auditSampleId, setAuditSampleId] = useState<number | null>(null);
  const [notification, setNotification] = useState<{ text: string; severity: "success" | "error" } | null>(null);

  const loadRecords = useCallback(async () => {
    setLoading(true);
    try {
      // One paged call per category (the workspace list API filters to a
      // single category at a time) merged into one register.
      const pages = await Promise.all(
        RECEIVING_CATEGORY_NAMES.map((category) => ReceiveService.getRecordsPaged({ category, pageSize: 200 }))
      );
      const merged = pages.flatMap((p) => p.items);
      merged.sort((a, b) => new Date(b.receivedAt).getTime() - new Date(a.receivedAt).getTime());
      setSamples(merged);
    } catch {
      setNotification({ text: "Failed to load the sample register.", severity: "error" });
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadRecords();
  }, [loadRecords]);

  const handleReceiveSuccess = (count: number) => {
    setNotification({
      text: `Successfully received ${count} sample${count > 1 ? "s" : ""}. Test orders generated automatically.`,
      severity: "success"
    });
    loadRecords();
  };

  if (samples === null) {
    return <LoadingSpinner />;
  }

  return (
    <Box>
      <PageHeader
        title="Receive Sample"
        subtitle="Register incoming Finished Product, Raw Material, and Packaging Material samples, and choose which laboratories they go to."
      >
        <Box sx={{ display: "flex", gap: 1.5 }}>
          <Button
            variant="outlined"
            onClick={() => loadRecords()}
            disabled={loading}
            startIcon={<RefreshIcon />}
            sx={{ fontWeight: 600 }}
          >
            Refresh
          </Button>
          <Button
            variant="contained"
            onClick={() => setNewSampleOpen(true)}
            startIcon={<AddIcon />}
            sx={{ fontWeight: 700 }}
          >
            Receive
          </Button>
        </Box>
      </PageHeader>

      <SampleRegisterTable
        samples={samples}
        onTestClick={(_test, sample) => setSummarySampleId(sample.sampleId)}
        onViewSummary={(sample) => setSummarySampleId(sample.sampleId)}
        onEdit={(sample) => setEditSample(sample)}
        onViewReport={(sample) => window.open(`/samples/${sample.sampleId}/report`, "_blank")}
        onViewAuditHistory={(sample) => setAuditSampleId(sample.sampleId)}
        onPrepareSample={(sample) => setSummarySampleId(sample.sampleId)}
        onVoid={(sample) => setVoidingSample(sample)}
        onAddLaboratory={(sample) => setAddingLabSample(sample)}
      />

      <NewSampleDialog
        open={newSampleOpen}
        onClose={() => setNewSampleOpen(false)}
        onSuccess={handleReceiveSuccess}
        allowedCategories={["product", "rm", "pm"]}
        labMode={{ kind: "choose" }}
      />

      <EditSampleDetailsDialog
        open={Boolean(editSample)}
        sample={editSample}
        onClose={() => setEditSample(null)}
        onSuccess={() => {
          setEditSample(null);
          setNotification({ text: "Sample details corrected and signed.", severity: "success" });
          loadRecords();
        }}
      />

      <VoidSampleConfirmationDialog
        open={Boolean(voidingSample)}
        sample={voidingSample}
        onClose={() => setVoidingSample(null)}
        onSuccess={() => {
          setVoidingSample(null);
          setNotification({ text: "Sample voided.", severity: "success" });
          loadRecords();
        }}
      />

      <AddLaboratoryDialog
        open={Boolean(addingLabSample)}
        sample={addingLabSample}
        onClose={() => setAddingLabSample(null)}
        onSuccess={() => {
          setAddingLabSample(null);
          setNotification({ text: "Laboratory added to the sample.", severity: "success" });
          loadRecords();
        }}
      />

      <SampleSummaryDialog
        open={Boolean(summarySampleId)}
        sampleId={summarySampleId}
        onClose={() => {
          setSummarySampleId(null);
          loadRecords();
        }}
      />

      <AuditHistoryDialog
        open={Boolean(auditSampleId)}
        entityName="Sample"
        entityId={auditSampleId}
        onClose={() => setAuditSampleId(null)}
      />

      <Snackbar
        open={Boolean(notification)}
        autoHideDuration={5000}
        onClose={() => setNotification(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      >
        {notification ? (
          <Alert severity={notification.severity} onClose={() => setNotification(null)} sx={{ borderRadius: 1.5 }}>
            {notification.text}
          </Alert>
        ) : undefined}
      </Snackbar>
    </Box>
  );
}
