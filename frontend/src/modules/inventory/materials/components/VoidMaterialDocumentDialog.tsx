import { useState } from "react";
import { Button, TextField, Alert, LinearProgress } from "@mui/material";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialDocument } from "../types/materialTypes";

interface Props {
  open: boolean;
  document: MaterialDocument;
  materialId: number;
  onClose: () => void;
  onSuccess: () => void;
}

export function VoidMaterialDocumentDialog({ open, document, materialId, onClose, onSuccess }: Props) {
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    setReasonError(false);
    if (!reason.trim()) { setReasonError(true); return; }

    setSubmitting(true);
    setError(null);
    try {
      await MaterialService.voidDocument(document.id, materialId, reason.trim());
      onSuccess();
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Void operation failed. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = () => {
    if (submitting) return;
    setReason(""); setReasonError(false); setError(null);
    onClose();
  };

  return (
    <FloatingDialog
      open={open}
      title="Void Document"
      onClose={handleClose}
      actions={
        <>
          <Button onClick={handleClose} disabled={submitting}>Cancel</Button>
          <Button
            id="void-doc-submit"
            variant="contained"
            color="error"
            onClick={handleSubmit}
            disabled={submitting || !reason.trim()}
          >
            {submitting ? "Voiding…" : "Void Document"}
          </Button>
        </>
      }
    >
      {submitting && <LinearProgress sx={{ mb: 2 }} />}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Alert severity="warning" sx={{ mb: 2, fontSize: 12 }}>
        The document <strong>{document.originalFileName}</strong> will be marked <strong>Voided</strong>.
        The file and metadata are retained for audit purposes but this document will no longer
        satisfy any COA requirement.
      </Alert>

      <TextField
        id="void-reason"
        label="Reason for Voiding *"
        fullWidth
        size="small"
        multiline
        rows={2}
        value={reason}
        onChange={(e) => { setReason(e.target.value); setReasonError(false); }}
        error={reasonError}
        helperText={reasonError ? "A reason is required." : undefined}
      />
    </FloatingDialog>
  );
}
