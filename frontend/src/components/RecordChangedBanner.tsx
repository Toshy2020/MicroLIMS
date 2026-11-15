import { useState } from "react";
import { Alert, Button } from "@mui/material";
import { useRecordChanged } from "../hooks/useRecordChanged";
import { formatDateTimeWithTimeZone } from "../utils/formatDate";
import { ConfirmationDialog } from "./ConfirmationDialog";

export interface RecordChangedBannerProps {
  type: string;
  id: number | null;
  // True when the screen holds unsaved edits that a reload would discard.
  dirty: boolean;
  onReload(): void;
}

/**
 * Tells the user that the record they have open was saved by someone else. It never reloads by
 * itself: the user chooses, and is asked first when they have unsaved edits.
 */
export function RecordChangedBanner({ type, id, dirty, onReload }: RecordChangedBannerProps) {
  const { changed, at, dismiss } = useRecordChanged(type, id);
  const [confirming, setConfirming] = useState(false);

  const reload = () => {
    setConfirming(false);
    dismiss();
    onReload();
  };

  if (!changed || !at) return null;

  return (
    <>
      <Alert
        severity="warning"
        role="status"
        aria-live="polite"
        sx={{ mb: 2 }}
        action={
          <Button
            type="button"
            color="inherit"
            size="small"
            onClick={() => (dirty ? setConfirming(true) : reload())}
          >
            Reload
          </Button>
        }
      >
        {`This record was changed by another user at ${formatDateTimeWithTimeZone(at)}. Reload to see the latest.`}
      </Alert>
      <ConfirmationDialog
        open={confirming}
        destructive
        title="Reload this record?"
        message="Discard your unsaved changes?"
        confirmText="Discard and reload"
        onCancel={() => setConfirming(false)}
        onConfirm={reload}
      />
    </>
  );
}
