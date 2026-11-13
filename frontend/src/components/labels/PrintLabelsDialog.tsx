import { useEffect, useState } from "react";
import { Alert, Box, Link, Stack, TextField, Typography } from "@mui/material";
import axios from "axios";
import { toast } from "sonner";
import {
  labelService,
  type LabelEntityType,
  type LabelPrintStatus,
  type LabelTarget,
  type PrintLabelItem
} from "../../services/labelService";
import { monospaceFontFamily } from "../../theme/palette";
import { getErrorMessage } from "../../utils/errorMessage";
import { DialogBodySkeleton } from "../DialogBodySkeleton";
import { LoadErrorAlert } from "../LoadErrorAlert";
import { FormDialog } from "../lab";

export interface PrintLabelsDialogProps {
  open: boolean;
  targets: LabelTarget[];
  onClose: () => void;
}

const MIN_COPIES = 1;
const MAX_COPIES = 50;
const MIN_REPRINT_REASON_LENGTH = 10;

function targetKey(entityType: LabelEntityType, entityId: number): string {
  return `${entityType}:${entityId}`;
}

function parseCopies(raw: string): number | null {
  const trimmed = raw.trim();
  if (!trimmed) return null;
  const num = Number(trimmed);
  if (!Number.isInteger(num) || num < MIN_COPIES || num > MAX_COPIES) {
    return null;
  }
  return num;
}

function extractPrintErrorMessage(err: unknown): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as { message?: unknown; errors?: unknown } | undefined;
    if (typeof data?.message === "string" && data.message.trim()) {
      return data.message;
    }
    if (Array.isArray(data?.errors) && typeof data.errors[0] === "string" && data.errors[0].trim()) {
      return data.errors[0];
    }
  }
  return getErrorMessage(err, "Could not generate label PDF.");
}

export function PrintLabelsDialog({ open, targets, onClose }: PrintLabelsDialogProps) {
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [statuses, setStatuses] = useState<LabelPrintStatus[]>([]);
  const [copiesByKey, setCopiesByKey] = useState<Record<string, string>>({});
  const [rowErrors, setRowErrors] = useState<Record<string, string>>({});
  const [setAllValue, setSetAllValue] = useState("");
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [blockedPdfUrl, setBlockedPdfUrl] = useState<string | null>(null);
  const [retryNonce, setRetryNonce] = useState(0);

  const targetsKey = targets.map((t) => targetKey(t.entityType, t.entityId)).join("|");

  // Revoke any fallback blob URL when replaced, closed, or unmounted.
  useEffect(() => {
    return () => {
      if (blockedPdfUrl) {
        URL.revokeObjectURL(blockedPdfUrl);
      }
    };
  }, [blockedPdfUrl]);

  useEffect(() => {
    if (!open) {
      setLoading(false);
      setLoadError(null);
      setStatuses([]);
      setCopiesByKey({});
      setRowErrors({});
      setSetAllValue("");
      setReason("");
      setReasonError(null);
      setSubmitError(null);
      setSubmitting(false);
      setBlockedPdfUrl(null);
      return;
    }

    if (!targetsKey) {
      setLoading(false);
      setStatuses([]);
      return;
    }

    const controller = new AbortController();
    const requestItems = targetsKey.split("|").map((entry) => {
      const [entityType, rawId] = entry.split(":");
      return { entityType: entityType as LabelEntityType, entityId: Number(rawId) };
    });

    setLoading(true);
    setLoadError(null);
    setSubmitError(null);
    setRowErrors({});
    setReasonError(null);
    setBlockedPdfUrl(null);

    labelService
      .getStatus(requestItems, controller.signal)
      .then((result) => {
        if (controller.signal.aborted) return;
        setStatuses(result);
        const initialCopies: Record<string, string> = {};
        for (const item of result) {
          initialCopies[targetKey(item.entityType, item.entityId)] = String(item.defaultCopies);
        }
        setCopiesByKey(initialCopies);
        setLoading(false);
      })
      .catch((err) => {
        if (controller.signal.aborted) return;
        setLoadError(getErrorMessage(err, "Could not load label print status."));
        setLoading(false);
      });

    return () => {
      controller.abort();
    };
  }, [open, targetsKey, retryNonce]);

  const statusByKey = new Map<string, LabelPrintStatus>();
  for (const status of statuses) {
    statusByKey.set(targetKey(status.entityType, status.entityId), status);
  }

  const printableTargets = targets.filter((target) => {
    const status = statusByKey.get(targetKey(target.entityType, target.entityId));
    return status?.canPrint === true;
  });

  const hasReprint = statuses.some((s) => s.printCount > 0);
  const title = hasReprint ? "Reprint labels" : "Print labels";
  const submitLabel = hasReprint ? "Reprint" : "Print";
  const submitDisabled = loading || Boolean(loadError) || printableTargets.length === 0 || Boolean(blockedPdfUrl);

  const handleSetAllChange = (value: string) => {
    setSetAllValue(value);
    setSubmitError(null);
    setCopiesByKey((prev) => {
      const next = { ...prev };
      for (const target of printableTargets) {
        next[targetKey(target.entityType, target.entityId)] = value;
      }
      return next;
    });
    setRowErrors((prev) => {
      const next = { ...prev };
      for (const target of printableTargets) {
        delete next[targetKey(target.entityType, target.entityId)];
      }
      return next;
    });
  };

  const handleCopiesChange = (key: string, value: string) => {
    setCopiesByKey((prev) => ({ ...prev, [key]: value }));
    setSubmitError(null);
    setRowErrors((prev) => {
      if (!prev[key]) return prev;
      const next = { ...prev };
      delete next[key];
      return next;
    });
  };

  const handleSubmit = async () => {
    if (submitDisabled || submitting) return;

    const nextRowErrors: Record<string, string> = {};
    const itemsToPrint: PrintLabelItem[] = [];

    for (const target of printableTargets) {
      const key = targetKey(target.entityType, target.entityId);
      const parsed = parseCopies(copiesByKey[key] ?? "");
      if (parsed === null) {
        nextRowErrors[key] = `Enter a whole number between ${MIN_COPIES} and ${MAX_COPIES}.`;
      } else {
        itemsToPrint.push({
          entityType: target.entityType,
          entityId: target.entityId,
          copies: parsed
        });
      }
    }

    let nextReasonError: string | null = null;
    const trimmedReason = reason.trim();
    if (hasReprint && trimmedReason.length < MIN_REPRINT_REASON_LENGTH) {
      nextReasonError = `Reason must be at least ${MIN_REPRINT_REASON_LENGTH} characters.`;
    }

    if (Object.keys(nextRowErrors).length > 0 || nextReasonError) {
      setRowErrors(nextRowErrors);
      setReasonError(nextReasonError);
      return;
    }

    setSubmitting(true);
    setSubmitError(null);

    try {
      const pdfBlob = await labelService.print(itemsToPrint, hasReprint ? trimmedReason : undefined);
      const url = URL.createObjectURL(pdfBlob);
      const popup = window.open(url);
      toast.success("Labels ready to print");

      if (popup) {
        let revoked = false;
        const revoke = () => {
          if (revoked) return;
          revoked = true;
          URL.revokeObjectURL(url);
        };
        popup.addEventListener?.("load", revoke, { once: true });
        window.setTimeout(revoke, 60_000);
        onClose();
      } else {
        setBlockedPdfUrl(url);
      }
    } catch (err) {
      setSubmitError(extractPrintErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      title={title}
      onClose={onClose}
      onSubmit={handleSubmit}
      submitLabel={submitLabel}
      submitting={submitting}
      submitDisabled={submitDisabled}
      error={submitError}
    >
      {loading && <DialogBodySkeleton blocks={Math.max(2, targets.length)} />}

      {!loading && loadError && (
        <LoadErrorAlert message={loadError} onRetry={() => setRetryNonce((n) => n + 1)} />
      )}

      {!loading && !loadError && (
        <Stack spacing={2}>
          {blockedPdfUrl && (
            <Alert severity="info">
              Pop-up was blocked by your browser.{" "}
              <Link href={blockedPdfUrl} target="_blank" rel="noopener noreferrer" underline="always">
                Open label PDF
              </Link>
            </Alert>
          )}

          {targets.length >= 2 && (
            <TextField
              label="Set all to"
              type="number"
              size="small"
              value={setAllValue}
              onChange={(e) => handleSetAllChange(e.target.value)}
              slotProps={{
                htmlInput: {
                  min: MIN_COPIES,
                  max: MAX_COPIES,
                  step: 1,
                  style: { fontVariantNumeric: "tabular-nums" }
                }
              }}
              sx={{ maxWidth: 200 }}
            />
          )}

          <Stack spacing={1.5}>
            {targets.map((target) => {
              const key = targetKey(target.entityType, target.entityId);
              const status = statusByKey.get(key);
              const canPrint = status?.canPrint === true;
              const rowError = rowErrors[key];

              return (
                <Box
                  key={key}
                  sx={{
                    display: "flex",
                    alignItems: "flex-start",
                    justifyContent: "space-between",
                    gap: 2,
                    p: 1.5,
                    borderRadius: 1,
                    border: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper"
                  }}
                >
                  <Box sx={{ minWidth: 0, flex: 1 }}>
                    <Typography
                      variant="body2"
                      sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}
                    >
                      {target.code || "—"}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {target.displayName}
                    </Typography>
                    {!canPrint && status && (
                      <Typography
                        variant="body2"
                        sx={(theme) => ({
                          mt: 0.5,
                          color: theme.custom.status.detected.text
                        })}
                      >
                        {status.blockedReason ?? "This record cannot be printed."}
                      </Typography>
                    )}
                  </Box>

                  {canPrint && (
                    <TextField
                      type="number"
                      size="small"
                      value={copiesByKey[key] ?? ""}
                      onChange={(e) => handleCopiesChange(key, e.target.value)}
                      error={Boolean(rowError)}
                      helperText={rowError}
                      slotProps={{
                        htmlInput: {
                          "aria-label": `Number of labels for ${target.code}`,
                          min: MIN_COPIES,
                          max: MAX_COPIES,
                          step: 1,
                          style: { fontVariantNumeric: "tabular-nums" }
                        }
                      }}
                      sx={{ width: 150, flexShrink: 0 }}
                    />
                  )}
                </Box>
              );
            })}
          </Stack>

          {hasReprint && (
            <TextField
              label="Reason for reprint"
              required
              multiline
              minRows={2}
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                setReasonError(null);
                setSubmitError(null);
              }}
              error={Boolean(reasonError)}
              helperText={reasonError ?? "At least 10 characters"}
            />
          )}
        </Stack>
      )}
    </FormDialog>
  );
}
