import { useState } from "react";
import { Alert, Box, Button } from "@mui/material";
import { ConfirmationDialog } from "../ConfirmationDialog";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

interface DraftRestoreNoticeProps {
  // From useBenchDraft().restoredStartedAt; null means there is nothing to offer.
  startedAt: string | null;
  onRestore: () => void;
  onDiscard: () => void;
}

// Offers an unsaved entry found on this tablet. Nothing is applied or saved until
// the analyst chooses Restore and then presses Save on the form.
export function DraftRestoreNotice({ startedAt, onRestore, onDiscard }: DraftRestoreNoticeProps) {
  const [confirming, setConfirming] = useState(false);
  if (!startedAt) return null;

  return (
    <>
      <Alert
        severity="info"
        role="region"
        aria-label="Unsaved entry found"
        action={
          <Box sx={{ display: "flex", gap: 1 }}>
            <Button color="inherit" size="small" onClick={onRestore}>Restore entry</Button>
            <Button color="inherit" size="small" onClick={() => setConfirming(true)}>Discard</Button>
          </Box>
        }
        sx={{ mb: 2 }}
      >
        An unsaved entry from {formatDateTimeWithTimeZone(startedAt)} (tablet clock) was found on this tablet. It has not been saved to MicroLIMS.
      </Alert>
      <ConfirmationDialog
        open={confirming}
        destructive
        title="Discard unsaved entry"
        message="Discard this unsaved entry? It exists only on this tablet and cannot be recovered."
        confirmText="Discard"
        onCancel={() => setConfirming(false)}
        onConfirm={() => {
          setConfirming(false);
          onDiscard();
        }}
      />
    </>
  );
}
