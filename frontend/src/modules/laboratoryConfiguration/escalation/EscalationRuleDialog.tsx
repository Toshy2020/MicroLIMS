import { type FormEvent, useEffect, useState } from "react";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  InputAdornment,
  Link as MuiLink,
  MenuItem,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { APP_ROUTES } from "../../../routes/routes";
import { getErrorMessage } from "../../../utils/errorMessage";
import { errorsByField } from "../../../utils/fieldErrors";
import { formatRoleName } from "../../../utils/userDisplay";
import {
  type EscalationNotifyRole,
  type EscalationRuleDto,
  type EscalationTrigger,
  NOTIFY_ROLE_OPTIONS,
  TRIGGER_OPTIONS,
  escalationRulesService
} from "./services/escalationRulesService";

export interface EscalationRuleDialogProps {
  open: boolean;
  mode: "create" | "replace";
  sectionId: number;
  sectionName: string;
  existingRule?: EscalationRuleDto | null;
  onClose: () => void;
  onSaved: () => void;
}

const MIN_HOURS = 1;
const MAX_HOURS = 999;

export function EscalationRuleDialog({
  open,
  mode,
  sectionId,
  sectionName,
  existingRule = null,
  onClose,
  onSaved
}: EscalationRuleDialogProps) {
  const { t } = useTranslation("labConfig");
  const [trigger, setTrigger] = useState<EscalationTrigger>("ReviewWaiting");
  const [thresholdInput, setThresholdInput] = useState("8");
  const [notifyRole, setNotifyRole] = useState<EscalationNotifyRole>("SectionHead");
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!open) return;
    if (mode === "replace" && existingRule) {
      setTrigger(existingRule.trigger);
      setThresholdInput(String(existingRule.thresholdWorkingHours));
      setNotifyRole(existingRule.notifyRoleType);
    } else {
      setTrigger("ReviewWaiting");
      setThresholdInput("8");
      setNotifyRole("SectionHead");
    }
    setFormError(null);
    setFieldErrors({});
  }, [open, mode, existingRule]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (saving) return;

    const trimmed = thresholdInput.trim();
    const parsedHours = trimmed.length > 0 ? Number(trimmed) : Number.NaN;
    const nextFieldErrors: Record<string, string> = {};

    if (!Number.isFinite(parsedHours) || parsedHours < MIN_HOURS || parsedHours > MAX_HOURS) {
      nextFieldErrors.thresholdWorkingHours = t("escalation.ruleDialog.thresholdRange");
    }

    if (Object.keys(nextFieldErrors).length > 0) {
      setFieldErrors(nextFieldErrors);
      setFormError(null);
      return;
    }

    setSaving(true);
    setFormError(null);
    setFieldErrors({});

    try {
      if (mode === "replace" && existingRule) {
        await escalationRulesService.replace(existingRule.id, {
          thresholdWorkingHours: parsedHours,
          notifyRoleType: notifyRole
        });
        toast.success(t("escalation.ruleDialog.replaced"));
      } else {
        await escalationRulesService.create({
          sectionId,
          trigger,
          thresholdWorkingHours: parsedHours,
          notifyRoleType: notifyRole
        });
        toast.success(t("escalation.ruleDialog.created"));
      }
      onSaved();
    } catch (err: unknown) {
      const byField = errorsByField(err);
      setFieldErrors(byField);
      const message = getErrorMessage(err, t("escalation.ruleDialog.saveError"));
      if (Object.keys(byField).length === 0) {
        setFormError(message);
      }
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const title = mode === "replace" ? t("escalation.ruleDialog.titleReplace") : t("escalation.ruleDialog.titleAdd");

  return (
    <Dialog
      open={open}
      onClose={saving ? undefined : onClose}
      maxWidth="sm"
      fullWidth
    >
      <DialogTitle>{title}</DialogTitle>
      <form onSubmit={handleSubmit} noValidate>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            <Typography variant="body2" color="text.secondary">
              <Trans
                i18nKey="escalation.ruleDialog.sectionLine"
                ns="labConfig"
                values={{ name: sectionName }}
                components={{ bold: <strong /> }}
              />
            </Typography>

            {mode === "replace" && (
              <Alert severity="info">
                {t("escalation.ruleDialog.replaceNote")}
              </Alert>
            )}

            {formError && (
              <Alert severity="error" role="alert">
                {formError}
              </Alert>
            )}

            <TextField
              select
              label={t("escalation.ruleDialog.trigger")}
              value={trigger}
              disabled={mode === "replace" || saving}
              error={Boolean(fieldErrors.trigger)}
              helperText={fieldErrors.trigger}
              onChange={(e) => {
                setTrigger(e.target.value as EscalationTrigger);
                setFieldErrors((prev) => ({ ...prev, trigger: "" }));
              }}
              fullWidth
            >
              {TRIGGER_OPTIONS.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {t(`escalation.triggers.${opt.value}`, { defaultValue: opt.label })}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              label={t("escalation.ruleDialog.threshold")}
              type="number"
              value={thresholdInput}
              disabled={saving}
              error={Boolean(fieldErrors.thresholdWorkingHours)}
              helperText={
                fieldErrors.thresholdWorkingHours || (
                  <>
                    <Trans
                      i18nKey="escalation.ruleDialog.thresholdHelper"
                      ns="labConfig"
                      components={{ lnk: <MuiLink component={RouterLink} to={APP_ROUTES.WORKING_HOURS} underline="hover">{null}</MuiLink> }}
                    />
                  </>
                )
              }
              onChange={(e) => {
                setThresholdInput(e.target.value);
                setFieldErrors((prev) => ({ ...prev, thresholdWorkingHours: "" }));
              }}
              slotProps={{
                htmlInput: { min: MIN_HOURS, max: MAX_HOURS, step: "any" },
                input: {
                  endAdornment: <InputAdornment position="end">{t("escalation.ruleDialog.workingHours")}</InputAdornment>
                }
              }}
              fullWidth
            />

            <TextField
              select
              label={t("escalation.ruleDialog.notifyRole")}
              value={notifyRole}
              disabled={saving}
              error={Boolean(fieldErrors.notifyRole || fieldErrors.notifyRoleType)}
              helperText={fieldErrors.notifyRole || fieldErrors.notifyRoleType}
              onChange={(e) => {
                setNotifyRole(e.target.value as EscalationNotifyRole);
                setFieldErrors((prev) => ({ ...prev, notifyRole: "", notifyRoleType: "" }));
              }}
              fullWidth
            >
              {NOTIFY_ROLE_OPTIONS.map((roleValue) => (
                <MenuItem key={roleValue} value={roleValue}>
                  {formatRoleName(roleValue)}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={saving}>
            {t("escalation.ruleDialog.cancel")}
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={saving}
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
          >
            {saving ? t("escalation.ruleDialog.saving") : mode === "replace" ? t("escalation.ruleDialog.replaceRule") : t("escalation.ruleDialog.createRule")}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
