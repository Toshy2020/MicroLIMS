import { useState } from "react";
import { Alert, Button } from "@mui/material";
import { useTranslation } from "../i18n";
import { useRecordChanged, type RecordChangedState } from "../hooks/useRecordChanged";
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
  const change = useRecordChanged(type, id);
  return <RecordChangedNotice change={change} dirty={dirty} onReload={onReload} />;
}

// A screen can keep its subscription when the notice moves into a modal.
export function RecordChangedNotice({ change, dirty, onReload }: {
  change: RecordChangedState;
  dirty: boolean;
  onReload: () => void;
}) {
  const { t } = useTranslation();
  const { changed, at, dismiss } = change;
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
            {t("components.recordChangedBanner.reload")}
          </Button>
        }
      >
        {t("components.recordChangedBanner.message", { time: formatDateTimeWithTimeZone(at) })}
      </Alert>
      <ConfirmationDialog
        open={confirming}
        destructive
        title={t("components.recordChangedBanner.confirmTitle")}
        message={t("components.recordChangedBanner.confirmMessage")}
        confirmText={t("components.recordChangedBanner.confirmButton")}
        onCancel={() => setConfirming(false)}
        onConfirm={reload}
      />
    </>
  );
}
