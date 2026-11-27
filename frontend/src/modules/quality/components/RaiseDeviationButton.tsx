import { useState } from "react";
import { Alert, Box, Button, Stack, TextField, Typography } from "@mui/material";
import ReportProblemOutlinedIcon from "@mui/icons-material/ReportProblemOutlined";
import { toast } from "sonner";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { useOptionalAuth } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import {
  qualityEntryService,
  type DeviationDto
} from "../../../services/qualityEntryService";
import type { QualityLinkTargetType } from "../../../services/qualityService";
import { serverMessage } from "../../../utils/errorMessage";

export type DeviationSourceKind = "oosRequest" | "monitoringDeviation" | "auditFinding";

export interface RaiseDeviationButtonProps {
  targetType?: QualityLinkTargetType;
  targetId?: number;
  sectionId?: number | null;
  sourceKind?: DeviationSourceKind;
  sourceId?: number;
  sourceVersion?: number;
  initialTitle?: string;
  initialDescription?: string;
  label?: string;
  // Names the button for assistive technology when the visible label alone does not say which record it acts on.
  ariaLabel?: string;
  size?: "small" | "medium";
  variant?: "text" | "outlined" | "contained";
  onRaised?: (deviation: DeviationDto) => void;
}

function defaultOccurredAtInput(): string {
  return new Date().toISOString().slice(0, 16);
}

function toUtcIso(localOrUtcInput: string): string {
  const trimmed = localOrUtcInput.trim();
  if (!trimmed) return new Date().toISOString();
  const hasZone = trimmed.endsWith("Z") || /[+-]\d{2}:\d{2}$/.test(trimmed);
  const parsed = new Date(hasZone ? trimmed : `${trimmed}Z`);
  return Number.isNaN(parsed.getTime()) ? new Date().toISOString() : parsed.toISOString();
}

export function RaiseDeviationButton({
  targetType,
  targetId,
  sectionId = null,
  sourceKind,
  sourceId,
  sourceVersion,
  initialTitle = "",
  initialDescription = "",
  label,
  ariaLabel,
  size = "small",
  variant = "outlined",
  onRaised
}: RaiseDeviationButtonProps) {
  const { t } = useTranslation("qualityConfig");

  const auth = useOptionalAuth();
  const permissions = auth?.permissions ?? [];

  const requiredPermissions =
    sourceKind === "oosRequest"
      ? [PERMISSIONS.QUALITY_APPROVE]
      : sourceKind === "monitoringDeviation"
      ? [PERMISSIONS.QUALITY_APPROVE, PERMISSIONS.QUALITY_INVESTIGATE]
      : sourceKind === "auditFinding"
      ? [PERMISSIONS.QUALITY_APPROVE]
      : [PERMISSIONS.QUALITY_RAISE];

  const allowed = requiredPermissions.some((p) => permissions.includes(p));

  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState(initialTitle);
  const [description, setDescription] = useState(initialDescription);
  const [occurredAt, setOccurredAt] = useState(defaultOccurredAtInput);
  const [location, setLocation] = useState("");
  const [immediateAction, setImmediateAction] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!allowed) return null;

  const isFromSource = sourceKind !== undefined;

  const handleOpen = () => {
    setTitle(initialTitle);
    setDescription(initialDescription);
    setOccurredAt(defaultOccurredAtInput());
    setLocation("");
    setImmediateAction("");
    setError(null);
    setOpen(true);
  };

  const handleClose = () => {
    if (submitting) return;
    setOpen(false);
  };

  const canSubmit =
    title.trim().length > 0 &&
    immediateAction.trim().length > 0 &&
    (isFromSource || description.trim().length > 0) &&
    !submitting;

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setSubmitting(true);
    setError(null);
    try {
      let created: DeviationDto;
      if (sourceKind === "oosRequest" && sourceId != null && sourceVersion != null) {
        created = await qualityEntryService.raiseFromOosRequest(
          sourceId,
          {
            title: title.trim(),
            occurredAtUtc: toUtcIso(occurredAt),
            immediateAction: immediateAction.trim()
          },
          sourceVersion
        );
      } else if (sourceKind === "auditFinding" && sourceId != null && sourceVersion != null) {
        created = await qualityEntryService.raiseFromAuditFinding(
          sourceId,
          {
            title: title.trim(),
            occurredAtUtc: toUtcIso(occurredAt),
            immediateAction: immediateAction.trim()
          },
          sourceVersion
        );
      } else if (sourceKind === "monitoringDeviation" && sourceId != null && sourceVersion != null) {
        created = await qualityEntryService.escalateMonitoringDeviation(
          sourceId,
          {
            title: title.trim(),
            occurredAtUtc: toUtcIso(occurredAt),
            immediateAction: immediateAction.trim()
          },
          sourceVersion
        );
      } else {
        created = await qualityEntryService.raiseDeviation({
          title: title.trim(),
          description: description.trim(),
          occurredAtUtc: toUtcIso(occurredAt),
          location: location.trim() ? location.trim() : null,
          immediateAction: immediateAction.trim(),
          sectionId,
          links: targetType && targetId != null ? [{ targetType, targetId }] : null
        });
      }

      toast.success(t("raiseDeviation.success", { number: created.record.number }));
      setOpen(false);
      onRaised?.(created);
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("raiseDeviation.error"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button
        size={size}
        variant={variant}
        aria-label={ariaLabel}
        startIcon={<ReportProblemOutlinedIcon fontSize="small" />}
        onClick={handleOpen}
      >
        {label ?? t("raiseDeviation.button")}
      </Button>

      <FloatingDialog
        open={open}
        onClose={handleClose}
        title={t("raiseDeviation.dialogTitle")}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={handleClose} disabled={submitting}>
              {t("raiseDeviation.cancel")}
            </Button>
            <Button
              variant="contained"
              onClick={() => void handleSubmit()}
              disabled={!canSubmit}
            >
              {submitting ? t("raiseDeviation.submitting") : t("raiseDeviation.submit")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 0.5 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <TextField
            label={t("raiseDeviation.titleLabel")}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            required
            fullWidth
            size="small"
            slotProps={{ htmlInput: { maxLength: 200 } }}
          />

          {isFromSource ? (
            initialDescription ? (
              <Box
                sx={{
                  p: 1.5,
                  borderRadius: 1,
                  border: 1,
                  borderColor: "divider",
                  bgcolor: "background.default"
                }}
              >
                <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 0.5 }}>
                  {t("raiseDeviation.sourceContextLabel")}
                </Typography>
                <Typography variant="body2">{initialDescription}</Typography>
              </Box>
            ) : null
          ) : (
            <TextField
              label={t("raiseDeviation.descriptionLabel")}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              required
              fullWidth
              multiline
              minRows={3}
              size="small"
              slotProps={{ htmlInput: { maxLength: 4000 } }}
            />
          )}

          <TextField
            label={t("raiseDeviation.occurredAtLabel")}
            type="datetime-local"
            value={occurredAt}
            onChange={(e) => setOccurredAt(e.target.value)}
            required
            fullWidth
            size="small"
            slotProps={{ inputLabel: { shrink: true } }}
          />

          {!isFromSource && (
            <TextField
              label={t("raiseDeviation.locationLabel")}
              value={location}
              onChange={(e) => setLocation(e.target.value)}
              fullWidth
              size="small"
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
          )}

          <TextField
            label={t("raiseDeviation.immediateActionLabel")}
            value={immediateAction}
            onChange={(e) => setImmediateAction(e.target.value)}
            required
            fullWidth
            multiline
            minRows={2}
            size="small"
            slotProps={{ htmlInput: { maxLength: 4000 } }}
          />
        </Stack>
      </FloatingDialog>
    </>
  );
}
