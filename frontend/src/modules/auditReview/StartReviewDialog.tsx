import { useState } from "react";
import { MenuItem, TextField } from "@mui/material";
import { FormDialog } from "../../components/lab";
import type {
  AuditReviewScopeKind,
  StartAuditTrailReviewRequest
} from "../../services/auditTrailReviewService";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import { useTranslation } from "../../i18n";

interface StartReviewDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (request: StartAuditTrailReviewRequest) => Promise<void>;
}

const MAX_TITLE_LENGTH = 200;
const MAX_ENTITY_NAME_LENGTH = 150;
const MAX_RECORD_ID_LENGTH = 100;

export function StartReviewDialog({ open, onClose, onSubmit }: StartReviewDialogProps) {
  const { t } = useTranslation("auditReview");
  const [title, setTitle] = useState("");
  const [periodFrom, setPeriodFrom] = useState("");
  const [periodTo, setPeriodTo] = useState("");
  const [scopeKind, setScopeKind] = useState<AuditReviewScopeKind>("All");
  const [entityName, setEntityName] = useState("");
  const [recordId, setRecordId] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const resetForm = () => {
    setTitle("");
    setPeriodFrom("");
    setPeriodTo("");
    setScopeKind("All");
    setEntityName("");
    setRecordId("");
    setFieldErrors({});
    setSubmitError(null);
  };

  const handleClose = () => {
    if (submitting) return;
    resetForm();
    onClose();
  };

  const validate = (): StartAuditTrailReviewRequest | null => {
    const errors: Record<string, string> = {};
    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      errors.title = t("start.titleRequired");
    } else if (trimmedTitle.length > MAX_TITLE_LENGTH) {
      errors.title = t("start.titleTooLong", { max: MAX_TITLE_LENGTH });
    }

    const fromDate = periodFrom ? new Date(periodFrom) : null;
    const toDate = periodTo ? new Date(periodTo) : null;
    if (!fromDate || Number.isNaN(fromDate.getTime())) {
      errors.periodFrom = t("start.fromRequired");
    }
    if (!toDate || Number.isNaN(toDate.getTime())) {
      errors.periodTo = t("start.toRequired");
    } else if (toDate.getTime() > Date.now()) {
      errors.periodTo = t("start.toFuture");
    }
    if (
      fromDate &&
      toDate &&
      !Number.isNaN(fromDate.getTime()) &&
      !Number.isNaN(toDate.getTime()) &&
      fromDate.getTime() >= toDate.getTime()
    ) {
      errors.periodFrom = t("start.fromBeforeTo");
    }

    const trimmedEntity = entityName.trim();
    const trimmedRecordId = recordId.trim();
    if (scopeKind === "EntityName" || scopeKind === "Record") {
      if (!trimmedEntity) {
        errors.entityName = t("start.entityRequired");
      } else if (trimmedEntity.length > MAX_ENTITY_NAME_LENGTH) {
        errors.entityName = t("start.entityTooLong", { max: MAX_ENTITY_NAME_LENGTH });
      }
    }
    if (scopeKind === "Record") {
      if (!trimmedRecordId) {
        errors.recordId = t("start.recordIdRequired");
      } else if (trimmedRecordId.length > MAX_RECORD_ID_LENGTH) {
        errors.recordId = t("start.recordIdTooLong", { max: MAX_RECORD_ID_LENGTH });
      }
    }

    setFieldErrors(errors);
    if (Object.keys(errors).length > 0 || !fromDate || !toDate) return null;

    return {
      title: trimmedTitle,
      periodFromUtc: fromDate.toISOString(),
      periodToUtc: toDate.toISOString(),
      scopeKind,
      entityName: scopeKind === "All" ? null : trimmedEntity,
      recordId: scopeKind === "Record" ? trimmedRecordId : null
    };
  };

  const handleSubmit = async () => {
    setSubmitError(null);
    const request = validate();
    if (!request) return;

    setSubmitting(true);
    try {
      await onSubmit(request);
      resetForm();
    } catch (err: unknown) {
      setSubmitError(serverMessage(err) ?? getErrorMessage(err, t("start.error")));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title={t("start.title")}
      onClose={handleClose}
      onSubmit={handleSubmit}
      submitLabel={t("list.startReview")}
      submitting={submitting}
      error={submitError}
    >
      <TextField
        label={t("start.titleLabel")}
        size="small"
        required
        fullWidth
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        error={Boolean(fieldErrors.title)}
        helperText={fieldErrors.title ?? `${title.trim().length}/${MAX_TITLE_LENGTH}`}
        slotProps={{ htmlInput: { maxLength: MAX_TITLE_LENGTH } }}
      />

      <TextField
        label={t("start.periodFrom")}
        type="datetime-local"
        size="small"
        required
        fullWidth
        value={periodFrom}
        onChange={(e) => setPeriodFrom(e.target.value)}
        error={Boolean(fieldErrors.periodFrom)}
        helperText={fieldErrors.periodFrom}
        slotProps={{ inputLabel: { shrink: true } }}
      />

      <TextField
        label={t("start.periodTo")}
        type="datetime-local"
        size="small"
        required
        fullWidth
        value={periodTo}
        onChange={(e) => setPeriodTo(e.target.value)}
        error={Boolean(fieldErrors.periodTo)}
        helperText={fieldErrors.periodTo}
        slotProps={{ inputLabel: { shrink: true } }}
      />

      <TextField
        select
        label={t("start.scope")}
        size="small"
        required
        fullWidth
        value={scopeKind}
        onChange={(e) => {
          const next = e.target.value as AuditReviewScopeKind;
          setScopeKind(next);
          if (next === "All") {
            setEntityName("");
            setRecordId("");
          } else if (next === "EntityName") {
            setRecordId("");
          }
          setFieldErrors((prev) => ({ ...prev, entityName: "", recordId: "" }));
        }}
      >
        <MenuItem value="All">{t("start.scopeAll")}</MenuItem>
        <MenuItem value="EntityName">{t("start.scopeEntity")}</MenuItem>
        <MenuItem value="Record">{t("start.scopeRecord")}</MenuItem>
      </TextField>

      {(scopeKind === "EntityName" || scopeKind === "Record") && (
        <TextField
          label={t("start.scopeEntity")}
          size="small"
          required
          fullWidth
          placeholder="e.g. Sample, TestOrder, User"
          value={entityName}
          onChange={(e) => setEntityName(e.target.value)}
          error={Boolean(fieldErrors.entityName)}
          helperText={fieldErrors.entityName}
          slotProps={{ htmlInput: { maxLength: MAX_ENTITY_NAME_LENGTH } }}
        />
      )}

      {scopeKind === "Record" && (
        <TextField
          label={t("start.recordId")}
          size="small"
          required
          fullWidth
          value={recordId}
          onChange={(e) => setRecordId(e.target.value)}
          error={Boolean(fieldErrors.recordId)}
          helperText={fieldErrors.recordId}
          slotProps={{ htmlInput: { maxLength: MAX_RECORD_ID_LENGTH } }}
        />
      )}
    </FormDialog>
  );
}
