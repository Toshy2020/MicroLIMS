import { useState } from "react";
import { FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, TextField, Typography } from "@mui/material";
import { i18n, useTranslation } from "../../../i18n";
import { FormDialog } from "../../../components/lab/FormDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { getErrorMessage } from "../../../utils/errorMessage";
import { qualityService, type QualityExtensionDto } from "../../../services/qualityService";

const TEXT_MAX = 4000;

export type ExtensionDialogMode =
  // Ask for a later due date, of the whole record (actionId null) or of one action.
  | { kind: "request"; actionId: number | null; actionDescription: string | null; currentDueDate: string | null }
  // Approve or refuse a pending request: a signed decision (Quality.Approve).
  | { kind: "decide"; extension: QualityExtensionDto };

interface ExtensionDialogProps {
  // null keeps the dialog closed.
  mode: ExtensionDialogMode | null;
  recordId: number;
  recordNumber: string;
  recordVersion: number;
  onClose: () => void;
  onDone: () => Promise<void>;
}

export function ExtensionDialog({ mode, recordId, recordNumber, recordVersion, onClose, onDone }: ExtensionDialogProps) {
  if (!mode) return null;
  return mode.kind === "request" ? (
    <RequestDialog mode={mode} recordId={recordId} recordVersion={recordVersion} onClose={onClose} onDone={onDone} />
  ) : (
    <DecideDialog
      extension={mode.extension}
      recordNumber={recordNumber}
      recordVersion={recordVersion}
      onClose={onClose}
      onDone={onDone}
    />
  );
}

interface RequestDialogProps {
  mode: Extract<ExtensionDialogMode, { kind: "request" }>;
  recordId: number;
  recordVersion: number;
  onClose: () => void;
  onDone: () => Promise<void>;
}

function RequestDialog({ mode, recordId, recordVersion, onClose, onDone }: RequestDialogProps) {
  const { t } = useTranslation("quality");
  const [date, setDate] = useState("");
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await qualityService.requestExtension(
        recordId,
        { actionId: mode.actionId, requestedDueDate: date, reason: reason.trim() },
        recordVersion
      );
      onClose();
      await onDone();
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("extensionDialog.requestFailed")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open
      title={t("extensionDialog.requestTitle")}
      onClose={onClose}
      onSubmit={submit}
      submitLabel={t("extensionDialog.submitRequest")}
      submitting={submitting}
      submitDisabled={!date || !reason.trim()}
      error={error}
    >
      {mode.actionDescription && (
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {t("extensionDialog.forAction", { description: mode.actionDescription })}
        </Typography>
      )}
      <Typography variant="body2" color="text.secondary">
        {mode.currentDueDate ? t("extensionDialog.currentDue", { date: mode.currentDueDate }) : t("extensionDialog.noCurrentDue")}
      </Typography>
      <TextField
        required
        type="date"
        label={t("extensionDialog.newDate")}
        value={date}
        onChange={(e) => setDate(e.target.value)}
        slotProps={{ inputLabel: { shrink: true } }}
      />
      <TextField
        required
        multiline
        minRows={3}
        label={t("extensionDialog.reason")}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
      />
    </FormDialog>
  );
}

interface DecideDialogProps {
  extension: QualityExtensionDto;
  recordNumber: string;
  recordVersion: number;
  onClose: () => void;
  onDone: () => Promise<void>;
}

// The decision is an electronic signature: the person chooses and comments here, then signs with their own
// password. The server builds the signed content (meaning, time, signer) and binds it to the record; this
// dialog only states the meaning the signer is about to give.
function DecideDialog({ extension, recordNumber, recordVersion, onClose, onDone }: DecideDialogProps) {
  const { t } = useTranslation("quality");
  const [decision, setDecision] = useState<"approve" | "refuse" | "">("");
  const [comment, setComment] = useState("");
  const [signing, setSigning] = useState(false);

  const sign = async (password: string) => {
    await qualityService.decideExtension(
      extension.id,
      { approve: decision === "approve", comment: comment.trim() || null, password },
      recordVersion
    );
    setSigning(false);
    onClose();
    await onDone();
  };

  return (
    <>
      <FormDialog
        open
        title={t("extensionDialog.decideTitle")}
        onClose={onClose}
        onSubmit={() => setSigning(true)}
        submitLabel={t("extensionDialog.signAndDecide")}
        submitDisabled={decision === ""}
      >
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          {recordNumber}
        </Typography>
        <Typography variant="body2">{t("extensionDialog.requestedBy", { name: extension.requestedByName })}</Typography>
        <Typography variant="body2">{t("extensionDialog.requestedDate", { date: extension.requestedDueDate })}</Typography>
        <Typography variant="body2">{t("extensionDialog.requestedReason", { reason: extension.reason })}</Typography>
        <FormControl component="fieldset">
          <FormLabel component="legend">{t("extensionDialog.decision")}</FormLabel>
          <RadioGroup value={decision} onChange={(e) => setDecision(e.target.value === "approve" ? "approve" : "refuse")}>
            <FormControlLabel value="approve" control={<Radio />} label={t("extensionDialog.approve")} />
            <FormControlLabel value="refuse" control={<Radio />} label={t("extensionDialog.refuse")} />
          </RadioGroup>
        </FormControl>
        <TextField
          multiline
          minRows={2}
          label={t("extensionDialog.comment")}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
      </FormDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={i18n.getFixedT("en", "quality")("extensionDialog.signatureStatement")}
        meaningStatementTranslation={t("extensionDialog.signatureStatement")}
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </>
  );
}
