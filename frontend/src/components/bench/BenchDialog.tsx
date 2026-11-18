import { useEffect, useState } from "react";
import type { ComponentProps, ReactNode } from "react";
import { FloatingDialog } from "../FloatingDialog";
import { ConfirmationDialog } from "../ConfirmationDialog";
import { useTranslation } from "../../i18n";
import { useWakeLock } from "../../hooks/useWakeLock";
import { incrementOpenBenchDialogs } from "../../pwa/benchDialogState";
import { BenchActionBar } from "./BenchActionBar";
import { RegistryBenchKeypad } from "./BenchKeypad";

interface BenchDialogProps {
  open: boolean;
  title: ReactNode;
  onClose: () => void;
  children: ReactNode;
  onSave: () => void;
  onSaveAndNext?: () => void;
  saving: boolean;
  saveLabel?: string;
  saveDisabledReason?: string;
  /** True when the form holds entries that closing would lose. */
  dirty: boolean;
  maxWidth?: ComponentProps<typeof FloatingDialog>["maxWidth"];
}

// A floating dialog that is full-screen on a bench tablet, with the shared
// action bar and keypad, and that keeps the screen awake while open. Closing
// a form with entries asks first.
export function BenchDialog({ open, title, onClose, children, onSave, onSaveAndNext, saving, saveLabel, saveDisabledReason, dirty, maxWidth }: BenchDialogProps) {
  const { t } = useTranslation();
  const [confirmingDiscard, setConfirmingDiscard] = useState(false);
  useWakeLock(open);

  useEffect(() => {
    if (!open) return undefined;
    return incrementOpenBenchDialogs();
  }, [open]);

  const requestClose = () => {
    if (saving) return;
    if (dirty) setConfirmingDiscard(true);
    else onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open}
        title={title}
        onClose={requestClose}
        maxWidth={maxWidth}
        benchFullScreen
        actions={
          <BenchActionBar
            onCancel={requestClose}
            onSave={onSave}
            onSaveAndNext={onSaveAndNext}
            saving={saving}
            saveLabel={saveLabel}
            saveDisabledReason={saveDisabledReason}
            keypad={<RegistryBenchKeypad />}
          />
        }
      >
        {children}
      </FloatingDialog>
      <ConfirmationDialog
        open={confirmingDiscard}
        destructive
        title={t("components.benchDialog.discardTitle")}
        message={t("components.benchDialog.discardMessage")}
        confirmText={t("components.benchDialog.discardConfirm")}
        onCancel={() => setConfirmingDiscard(false)}
        onConfirm={() => {
          setConfirmingDiscard(false);
          onClose();
        }}
      />
    </>
  );
}
