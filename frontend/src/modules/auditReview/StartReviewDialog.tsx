import { useState } from "react";
import { MenuItem, TextField } from "@mui/material";
import { FormDialog } from "../../components/lab";
import type {
  AuditReviewScopeKind,
  StartAuditTrailReviewRequest
} from "../../services/auditTrailReviewService";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";

interface StartReviewDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (request: StartAuditTrailReviewRequest) => Promise<void>;
}

const MAX_TITLE_LENGTH = 200;
const MAX_ENTITY_NAME_LENGTH = 150;
const MAX_RECORD_ID_LENGTH = 100;

export function StartReviewDialog({ open, onClose, onSubmit }: StartReviewDialogProps) {
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
      errors.title = "Title is required.";
    } else if (trimmedTitle.length > MAX_TITLE_LENGTH) {
      errors.title = `Title must be ${MAX_TITLE_LENGTH} characters or fewer.`;
    }

    const fromDate = periodFrom ? new Date(periodFrom) : null;
    const toDate = periodTo ? new Date(periodTo) : null;
    if (!fromDate || Number.isNaN(fromDate.getTime())) {
      errors.periodFrom = "Period start is required.";
    }
    if (!toDate || Number.isNaN(toDate.getTime())) {
      errors.periodTo = "Period end is required.";
    } else if (toDate.getTime() > Date.now()) {
      errors.periodTo = "Review period end cannot be in the future.";
    }
    if (
      fromDate &&
      toDate &&
      !Number.isNaN(fromDate.getTime()) &&
      !Number.isNaN(toDate.getTime()) &&
      fromDate.getTime() >= toDate.getTime()
    ) {
      errors.periodFrom = "Review period start must be earlier than period end.";
    }

    const trimmedEntity = entityName.trim();
    const trimmedRecordId = recordId.trim();
    if (scopeKind === "EntityName" || scopeKind === "Record") {
      if (!trimmedEntity) {
        errors.entityName = "Record type is required.";
      } else if (trimmedEntity.length > MAX_ENTITY_NAME_LENGTH) {
        errors.entityName = `Record type must be ${MAX_ENTITY_NAME_LENGTH} characters or fewer.`;
      }
    }
    if (scopeKind === "Record") {
      if (!trimmedRecordId) {
        errors.recordId = "Record ID is required.";
      } else if (trimmedRecordId.length > MAX_RECORD_ID_LENGTH) {
        errors.recordId = `Record ID must be ${MAX_RECORD_ID_LENGTH} characters or fewer.`;
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
      setSubmitError(serverMessage(err) ?? getErrorMessage(err, "Could not start the audit trail review."));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title="Start audit trail review"
      onClose={handleClose}
      onSubmit={handleSubmit}
      submitLabel="Start review"
      submitting={submitting}
      error={submitError}
    >
      <TextField
        label="Title"
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
        label="Period from"
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
        label="Period to"
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
        label="Scope"
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
        <MenuItem value="All">All</MenuItem>
        <MenuItem value="EntityName">Record type</MenuItem>
        <MenuItem value="Record">Record</MenuItem>
      </TextField>

      {(scopeKind === "EntityName" || scopeKind === "Record") && (
        <TextField
          label="Record type"
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
          label="Record ID"
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
