import { useCallback, useId, useState } from "react";
import type { ReactNode } from "react";
import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import type { ButtonProps } from "@mui/material";
import { i18n, useTranslation } from "../../../i18n";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { getErrorMessage } from "../../../utils/errorMessage";
import { qualityService, type QualityRecordDetailDto, type QualityRecordType } from "../../../services/qualityService";
import type { Lookup } from "./lookups";

// The pieces every type's step forms share: the signature meaning, the error text beside an action, a button that
// says why it is locked, the select for server lists, and the signed / destructive step dialogs. Everything here
// is presentation: the server decides whether a step is allowed and answers with the reason when it is not.

export const TEXT_MAX = 4000;

// The SignatureMeaning values the quality steps sign with. The statement shown to the signer is the server's
// ReportVocabulary label for that value (see signatureMeanings in quality.json).
export type QualitySignatureMeaning =
  | "DeviationAssessed"
  | "DeviationRejected"
  | "QualityLabConclusion"
  | "QualityApproved"
  | "QualityClosed"
  | "QualityCancelled"
  | "CapaPlanApproved"
  | "CapaCompletionVerified"
  | "CapaEffectivenessChecked"
  | "ChangeImpactAssessed"
  | "ChangeApproved"
  | "ChangeRejected"
  | "ComplaintConcluded";

const FINAL_STATUSES: readonly string[] = ["Closed", "Rejected", "Cancelled"];

// A record that can still change: not cancelled and not in a final status.
export const isOpenRecord = (record: QualityRecordDetailDto): boolean =>
  !record.isCancelled && !FINAL_STATUSES.includes(record.status);

// The person raised or owns the record: the server refuses their approval or closure of it.
export const isRaiserOrOwner = (record: QualityRecordDetailDto, userId: number | null): boolean =>
  userId !== null && (record.raisedByUserId === userId || record.ownerUserId === userId);

// Today's date in the browser's time zone as yyyy-MM-dd. Only used to explain why a date-gated action is
// locked; the server compares with the laboratory date.
export function todayIsoDate(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${now.getFullYear()}-${month}-${day}`;
}

// Runs one request of a step, then lets the page reload. A refusal is kept as text for the form to show.
export function useStepRunner(onChanged: () => Promise<void>) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = useCallback(
    async (action: () => Promise<unknown>, fallback: string): Promise<boolean> => {
      setBusy(true);
      setError(null);
      try {
        await action();
      } catch (e: unknown) {
        setError(getErrorMessage(e, fallback));
        setBusy(false);
        return false;
      }
      setBusy(false);
      await onChanged();
      return true;
    },
    [onChanged]
  );

  return { busy, error, setError, run };
}

// The server's refusal (or a readable failure) as text beside the action that caused it.
export function StepError({ message }: { message: string | null }) {
  if (!message) return null;
  return (
    <Alert severity="error" role="alert" sx={{ mt: 1 }}>
      {message}
    </Alert>
  );
}

interface GatedButtonProps extends Omit<ButtonProps, "disabled" | "children"> {
  label: string;
  // When set the button is locked and this is why; it is printed beside the button.
  lockedReason?: string | null;
  busy?: boolean;
}

// A button that, when it cannot be used, says why in text instead of just looking dead.
export function GatedButton({ label, lockedReason, busy = false, variant = "contained", ...rest }: GatedButtonProps) {
  const reasonId = useId();
  return (
    <Box sx={{ display: "inline-flex", flexDirection: "column", alignItems: "flex-start", gap: 0.5 }}>
      <Button
        variant={variant}
        disabled={Boolean(lockedReason) || busy}
        aria-describedby={lockedReason ? reasonId : undefined}
        {...rest}
      >
        {label}
      </Button>
      {lockedReason && (
        <Typography id={reasonId} variant="caption" color="text.secondary" sx={{ maxWidth: 420 }}>
          {lockedReason}
        </Typography>
      )}
    </Box>
  );
}

interface LookupSelectProps {
  label: string;
  value: number | "";
  onChange: (value: number | "") => void;
  lookup: Lookup;
  required?: boolean;
  disabled?: boolean;
}

// A select over a server list; says so when the list could not be loaded.
export function LookupSelect({ label, value, onChange, lookup, required = false, disabled = false }: LookupSelectProps) {
  const { t } = useTranslation("quality");
  const failed = lookup.status === "failed";
  return (
    <TextField
      select
      required={required}
      label={label}
      value={lookup.status === "ready" ? value : ""}
      onChange={(e) => onChange(e.target.value === "" ? "" : Number(e.target.value))}
      disabled={disabled || lookup.status === "loading"}
      error={failed}
      helperText={failed ? t("steps.common.lookupFailed") : lookup.status === "loading" ? t("steps.common.lookupLoading") : undefined}
      fullWidth
    >
      <MenuItem value="">{t("steps.common.choose")}</MenuItem>
      {lookup.options.map((option) => (
        <MenuItem key={option.id} value={option.id}>
          {option.label}
        </MenuItem>
      ))}
    </TextField>
  );
}

// The wording of a signature as the server's report vocabulary has it. Only the English statement is recorded
// and printed; the interface language only adds a reading aid for the signer.
export function signatureStatements(meaning: QualitySignatureMeaning, translate: (key: string) => string) {
  const key = `signatureMeanings.${meaning}`;
  return { english: i18n.getFixedT("en", "quality")(key), translated: translate(key) };
}

interface SignStepDialogProps {
  open: boolean;
  meaning: QualitySignatureMeaning;
  // When set the signer must also give a reason; it is sent with the signature.
  reasonLabel?: string;
  onCancel: () => void;
  // Must throw when the server refuses: the dialog then stays open and shows the server's message.
  onSign: (password: string, reason: string) => Promise<void>;
}

export function SignStepDialog({ open, meaning, reasonLabel, onCancel, onSign }: SignStepDialogProps) {
  const { t } = useTranslation("quality");
  const [reason, setReason] = useState("");
  const statements = signatureStatements(meaning, (key) => t(key));

  return (
    <SignatureDialog
      open={open}
      meaningStatement={statements.english}
      meaningStatementTranslation={statements.translated}
      showComment={Boolean(reasonLabel)}
      commentLabel={reasonLabel}
      comment={reason}
      onCommentChange={setReason}
      commentRequired={Boolean(reasonLabel)}
      onCancel={() => {
        setReason("");
        onCancel();
      }}
      onConfirm={async (password) => {
        await onSign(password, reason.trim());
        setReason("");
      }}
    />
  );
}

interface DestructiveSignActionProps {
  label: string;
  confirmTitle: string;
  confirmMessage: string;
  reasonLabel: string;
  meaning: QualitySignatureMeaning;
  // When set the action is locked and this is why.
  lockedReason?: string | null;
  onSign: (password: string, reason: string) => Promise<void>;
}

// A step that ends or voids the record (reject, cancel): a confirmation that starts on Cancel, then the signature
// with a required reason.
export function DestructiveSignAction({
  label,
  confirmTitle,
  confirmMessage,
  reasonLabel,
  meaning,
  lockedReason,
  onSign
}: DestructiveSignActionProps) {
  const [stage, setStage] = useState<"idle" | "confirm" | "sign">("idle");
  return (
    <>
      <GatedButton label={label} variant="outlined" color="error" lockedReason={lockedReason} onClick={() => setStage("confirm")} />
      <ConfirmationDialog
        open={stage === "confirm"}
        destructive
        title={confirmTitle}
        message={confirmMessage}
        confirmText={label}
        onCancel={() => setStage("idle")}
        onConfirm={() => setStage("sign")}
      />
      <SignStepDialog
        open={stage === "sign"}
        meaning={meaning}
        reasonLabel={reasonLabel}
        onCancel={() => setStage("idle")}
        onSign={async (password, reason) => {
          await onSign(password, reason);
          setStage("idle");
        }}
      />
    </>
  );
}

const cancelCall: Record<QualityRecordType, typeof qualityService.cancelDeviation> = {
  Deviation: qualityService.cancelDeviation,
  Capa: qualityService.cancelCapa,
  ChangeControl: qualityService.cancelChangeControl,
  Complaint: qualityService.cancelComplaint
};

interface CancelRecordActionProps {
  record: QualityRecordDetailDto;
  canApprove: boolean;
  onChanged: () => Promise<void>;
}

// Any open record can be cancelled by QA with a reason and a signature; it stays visible as cancelled.
export function CancelRecordAction({ record, canApprove, onChanged }: CancelRecordActionProps) {
  const { t } = useTranslation("quality");
  if (!canApprove || !isOpenRecord(record)) return null;
  return (
    <DestructiveSignAction
      label={t("steps.common.cancelRecord")}
      confirmTitle={t("steps.common.cancelConfirmTitle")}
      confirmMessage={t("steps.common.cancelConfirmMessage", { number: record.number })}
      reasonLabel={t("steps.common.cancelReason")}
      meaning="QualityCancelled"
      onSign={async (password, reason) => {
        await cancelCall[record.type](record.id, { reason, password }, record.version);
        await onChanged();
      }}
    />
  );
}

// The layout of a step form: a field column and the action row, with the refusal beside the actions.
export function StepForm({ children, actions, error }: { children?: ReactNode; actions: ReactNode; error: string | null }) {
  return (
    <Stack spacing={2}>
      {children}
      <Stack direction="row" spacing={1.5} sx={{ flexWrap: "wrap", rowGap: 1, alignItems: "flex-start" }}>
        {actions}
      </Stack>
      <StepError message={error} />
    </Stack>
  );
}
