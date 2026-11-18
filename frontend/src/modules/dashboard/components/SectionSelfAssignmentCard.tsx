import { useEffect, useId, useState } from "react";
import axios from "axios";
import {
  Alert,
  Box,
  Button,
  Paper,
  Skeleton,
  Stack,
  Switch,
  Typography,
  useTheme
} from "@mui/material";
import TuneOutlinedIcon from "@mui/icons-material/TuneOutlined";
import { toast } from "sonner";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import {
  sectionSelfAssignmentService,
  type SectionSelfAssignmentDto
} from "../../../services/sectionSelfAssignmentService";
import { ChangeReasonDialog } from "../../../components/config/ChangeReasonDialog";
import { EmptyState } from "../../../components/lab";
import { getErrorMessage } from "../../../utils/errorMessage";
import { getFieldErrors } from "../../../utils/fieldErrors";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { useTranslation } from "../../../i18n";

interface PendingChange {
  section: SectionSelfAssignmentDto;
  nextEnabled: boolean;
}

// Kept as a key so the translated text is looked up at render, not stored in the effect.
const LOAD_FALLBACK_KEY = "selfAssign.fallbackLoad";

function isConflictError(err: unknown): boolean {
  if (axios.isAxiosError(err)) return err.response?.status === 409;
  const candidate = err as { response?: { status?: number } } | null;
  return candidate?.response?.status === 409;
}

function extractReasonError(err: unknown, fallback: string): string {
  const fieldErrors = getFieldErrors(err);
  const reasonError = fieldErrors.find((f) => f.field.toLowerCase() === "reason");
  if (reasonError?.message.trim()) return reasonError.message;
  const firstFieldError = fieldErrors.find((f) => f.message.trim());
  if (firstFieldError?.message.trim()) return firstFieldError.message;
  return getErrorMessage(err, fallback);
}

function formatLastChanged(section: SectionSelfAssignmentDto, t: (key: string, options?: Record<string, string>) => string): string {
  if (!section.changedAt) {
    return t("selfAssign.lastChanged.never");
  }
  const timestamp = formatDateTimeWithTimeZone(section.changedAt);
  if (!section.changedByName) {
    return t("selfAssign.lastChanged.carried", { timestamp });
  }
  return t("selfAssign.lastChanged.by", { name: section.changedByName, timestamp });
}

export function SectionSelfAssignmentCard() {
  const { t } = useTranslation("dashboard");
  const theme = useTheme();
  const headingId = useId();
  const canManage = useHasPermission(PERMISSIONS.SAMPLES_ASSIGN_ANALYST);

  const [sections, setSections] = useState<SectionSelfAssignmentDto[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [conflictMessage, setConflictMessage] = useState<string | null>(null);
  const [pendingChange, setPendingChange] = useState<PendingChange | null>(null);
  const [saving, setSaving] = useState(false);
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const reload = () => setReloadKey((k) => k + 1);

  useEffect(() => {
    if (!canManage) return;

    const controller = new AbortController();
    setLoading(true);
    setLoadError(null);

    sectionSelfAssignmentService
      .getAll(controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted) {
          setSections(rows);
        }
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted || axios.isCancel(err)) return;
        setLoadError(getErrorMessage(err, LOAD_FALLBACK_KEY));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [canManage, reloadKey]);

  if (!canManage) {
    return null;
  }

  const openChangeDialog = (section: SectionSelfAssignmentDto, nextEnabled: boolean) => {
    if (saving) return;
    setReasonError(null);
    setPendingChange({ section, nextEnabled });
  };

  const closeChangeDialog = () => {
    if (saving) return;
    setPendingChange(null);
    setReasonError(null);
  };

  const handleConfirmChange = async (reason: string) => {
    if (!pendingChange) return;
    const { section, nextEnabled } = pendingChange;

    setSaving(true);
    setReasonError(null);

    try {
      const updated = await sectionSelfAssignmentService.update(section.sectionId, {
        enabled: nextEnabled,
        reason,
        version: section.version
      });
      setSections((prev) =>
        prev ? prev.map((item) => (item.sectionId === updated.sectionId ? updated : item)) : [updated]
      );
      setConflictMessage(null);
      setPendingChange(null);
      toast.success(
        t("selfAssign.toast.turned", {
          state: updated.enabled ? t("selfAssign.toast.stateOn") : t("selfAssign.toast.stateOff"),
          section: updated.sectionName
        })
      );
    } catch (err: unknown) {
      if (isConflictError(err)) {
        const message = t("selfAssign.conflict");
        setPendingChange(null);
        setReasonError(null);
        setConflictMessage(message);
        toast.error(message);
        reload();
      } else {
        setReasonError(extractReasonError(err, t("selfAssign.fallbackSave")));
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <Paper component="section" aria-labelledby={headingId} sx={{ p: 2 }}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.75 }}>
        <TuneOutlinedIcon sx={{ color: theme.palette.primary.main }} aria-hidden />
        <Typography
          id={headingId}
          component="h2"
          sx={{ fontSize: 15, fontWeight: 700, color: theme.palette.primary.main }}
        >
          {t("selfAssign.heading")}
        </Typography>
      </Box>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
        {t("selfAssign.intro")}
      </Typography>

      {conflictMessage && (
        <Alert severity="warning" onClose={() => setConflictMessage(null)} sx={{ mb: 1.5 }}>
          {conflictMessage}
        </Alert>
      )}

      {loading && sections === null ? (
        <Stack spacing={1} data-testid="section-self-assignment-loading" aria-busy="true">
          <Skeleton variant="rounded" height={56} />
          <Skeleton variant="rounded" height={56} />
        </Stack>
      ) : loadError && sections === null ? (
        <Alert
          severity="error"
          action={
            <Button
              type="button"
              color="inherit"
              size="small"
              onClick={reload}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {t("selfAssign.retry")}
            </Button>
          }
        >
          {loadError === LOAD_FALLBACK_KEY ? t(LOAD_FALLBACK_KEY) : loadError}
        </Alert>
      ) : sections !== null && sections.length === 0 ? (
        <EmptyState
          title={t("selfAssign.emptyTitle")}
          description={t("selfAssign.emptyBody")}
        />
      ) : (
        <Stack component="ul" spacing={1} sx={{ listStyle: "none", m: 0, p: 0 }}>
          {(sections ?? []).map((section) => {
            const tone = theme.custom.status[section.enabled ? "notDetected" : "pending"];
            return (
              <Box
                component="li"
                key={section.sectionId}
                data-testid={`section-self-assignment-row-${section.sectionId}`}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  gap: 2,
                  px: 1.5,
                  py: 1.25,
                  border: "1px solid",
                  borderColor: "divider",
                  borderRadius: 1.5,
                  bgcolor: "background.default"
                }}
              >
                <Box sx={{ minWidth: 0 }}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                    <Typography sx={{ fontSize: 13.5, fontWeight: 700, color: "text.primary" }}>
                      {section.sectionName}
                    </Typography>
                    <Box
                      component="span"
                      sx={{
                        display: "inline-block",
                        px: 1,
                        py: 0.125,
                        borderRadius: 5,
                        fontSize: 12,
                        fontWeight: 600,
                        lineHeight: 1.5,
                        color: tone.text,
                        bgcolor: tone.bg,
                        border: `1px solid ${tone.border}`
                      }}
                    >
                      {section.enabled ? t("selfAssign.chipOn") : t("selfAssign.chipOff")}
                    </Box>
                  </Box>
                  <Typography
                    variant="caption"
                    sx={{
                      display: "block",
                      color: "text.secondary",
                      mt: 0.25,
                      fontVariantNumeric: "tabular-nums"
                    }}
                  >
                    {formatLastChanged(section, t)}
                  </Typography>
                </Box>

                <Switch
                  checked={section.enabled}
                  disabled={saving}
                  onChange={(_, checked) => openChangeDialog(section, checked)}
                  slotProps={{
                    input: {
                      "aria-label": t("selfAssign.switchLabel", { name: section.sectionName })
                    }
                  }}
                />
              </Box>
            );
          })}
        </Stack>
      )}

      <ChangeReasonDialog
        open={pendingChange !== null}
        title={
          pendingChange
            ? pendingChange.nextEnabled
              ? t("selfAssign.dialog.titleOn")
              : t("selfAssign.dialog.titleOff")
            : t("selfAssign.dialog.titleFallback")
        }
        message={
          pendingChange
            ? pendingChange.nextEnabled
              ? t("selfAssign.dialog.messageOn", { section: pendingChange.section.sectionName })
              : t("selfAssign.dialog.messageOff", { section: pendingChange.section.sectionName })
            : undefined
        }
        confirmText={pendingChange?.nextEnabled ? t("selfAssign.dialog.confirmOn") : t("selfAssign.dialog.confirmOff")}
        loading={saving}
        error={reasonError}
        onConfirm={handleConfirmChange}
        onCancel={closeChangeDialog}
      />
    </Paper>
  );
}
