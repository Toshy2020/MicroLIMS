import { useState } from "react";
import { Alert, Box, Button } from "@mui/material";
import { ConfirmationDialog } from "../ConfirmationDialog";
import { useTranslation } from "../../i18n";
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
  const { t } = useTranslation();
  const [confirming, setConfirming] = useState(false);
  if (!startedAt) return null;

  return (
    <>
      <Alert
        severity="info"
        role="region"
        aria-label={t("components.draftRestoreNotice.regionAria")}
        action={
          <Box sx={{ display: "flex", gap: 1 }}>
            <Button color="inherit" size="small" onClick={onRestore}>{t("components.draftRestoreNotice.restore")}</Button>
            <Button color="inherit" size="small" onClick={() => setConfirming(true)}>{t("components.draftRestoreNotice.discard")}</Button>
          </Box>
        }
        sx={{ mb: 2 }}
      >
        {t("components.draftRestoreNotice.message", { date: formatDateTimeWithTimeZone(startedAt) })}
      </Alert>
      <ConfirmationDialog
        open={confirming}
        destructive
        title={t("components.draftRestoreNotice.confirmTitle")}
        message={t("components.draftRestoreNotice.confirmMessage")}
        confirmText={t("components.draftRestoreNotice.discard")}
        onCancel={() => setConfirming(false)}
        onConfirm={() => {
          setConfirming(false);
          onDiscard();
        }}
      />
    </>
  );
}
