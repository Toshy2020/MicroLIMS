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

interface PendingChange {
  section: SectionSelfAssignmentDto;
  nextEnabled: boolean;
}

function isConflictError(err: unknown): boolean {
  if (axios.isAxiosError(err)) return err.response?.status === 409;
  const candidate = err as { response?: { status?: number } } | null;
  return candidate?.response?.status === 409;
}

function extractReasonError(err: unknown): string {
  const fieldErrors = getFieldErrors(err);
  const reasonError = fieldErrors.find((f) => f.field.toLowerCase() === "reason");
  if (reasonError?.message.trim()) return reasonError.message;
  const firstFieldError = fieldErrors.find((f) => f.message.trim());
  if (firstFieldError?.message.trim()) return firstFieldError.message;
  return getErrorMessage(err, "Could not update the self-assignment setting.");
}

function formatLastChanged(section: SectionSelfAssignmentDto): string {
  if (!section.changedAt) {
    return "Default setting (never changed)";
  }
  const timestamp = formatDateTimeWithTimeZone(section.changedAt);
  if (!section.changedByName) {
    return `Carried over from laboratory-wide setting · ${timestamp}`;
  }
  return `Last changed by ${section.changedByName} · ${timestamp}`;
}

export function SectionSelfAssignmentCard() {
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
        setLoadError(getErrorMessage(err, "Could not load section self-assignment settings."));
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
        `Taking tests from My Work turned ${updated.enabled ? "on" : "off"} for ${updated.sectionName}.`
      );
    } catch (err: unknown) {
      if (isConflictError(err)) {
        const message = "Changed by someone else. The latest settings have been reloaded.";
        setPendingChange(null);
        setReasonError(null);
        setConflictMessage(message);
        toast.error(message);
        reload();
      } else {
        setReasonError(extractReasonError(err));
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
          Taking tests from My Work
        </Typography>
      </Box>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 2 }}>
        Control whether analysts in your sections can take unassigned tests directly from My Work.
        Changes require an audit reason (10–500 characters).
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
              Retry
            </Button>
          }
        >
          {loadError}
        </Alert>
      ) : sections !== null && sections.length === 0 ? (
        <EmptyState
          title="No sections assigned"
          description="You are not currently assigned to any laboratory section."
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
                      {section.enabled ? "On" : "Off"}
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
                    {formatLastChanged(section)}
                  </Typography>
                </Box>

                <Switch
                  checked={section.enabled}
                  disabled={saving}
                  onChange={(_, checked) => openChangeDialog(section, checked)}
                  slotProps={{
                    input: {
                      "aria-label": `Taking tests from My Work for ${section.sectionName}`
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
            ? `Turn ${pendingChange.nextEnabled ? "on" : "off"} taking tests from My Work`
            : "Change reason"
        }
        message={
          pendingChange
            ? pendingChange.nextEnabled
              ? `Allow analysts in ${pendingChange.section.sectionName} to take unassigned tests directly from My Work. Enter a reason (10–500 characters) for the audit trail.`
              : `Pause analyst self-assignment from My Work in ${pendingChange.section.sectionName}. Analysts must be assigned tests by a Section Head. Enter a reason (10–500 characters) for the audit trail.`
            : undefined
        }
        confirmText={pendingChange?.nextEnabled ? "Turn on" : "Turn off"}
        loading={saving}
        error={reasonError}
        onConfirm={handleConfirmChange}
        onCancel={closeChangeDialog}
      />
    </Paper>
  );
}
