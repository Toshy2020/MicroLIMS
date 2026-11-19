import { useCallback, useEffect, useId, useState, type FormEvent } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography
} from "@mui/material";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import { toast } from "sonner";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { i18n, useTranslation } from "../../../i18n";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { DialogBodySkeleton } from "../../../components/DialogBodySkeleton";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SectionTitle } from "../../../components/SectionTitle";
import { EmptyState } from "../../../components/lab/EmptyState";
import { FormDialog } from "../../../components/lab/FormDialog";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import { describeUserAgent } from "../../../utils/describeUserAgent";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { mfaService, type MfaStatus, type TrustedDeviceInfo } from "../services/mfaService";
import { AuthenticatedEnrolmentFlow } from "./EnrolmentSteps";
import { MfaCodeField } from "./MfaCodeField";
import { RecoveryCodesPanel } from "./RecoveryCodesPanel";

function useFactorTypeLabel() {
  const { t } = useTranslation("account");
  return (type: string | null | undefined): string =>
    type === "HardwareToken" ? t("security.hardwareToken") : t("security.authenticatorApp");
}

function SecurityField({ label, value }: { label: string; value: string }) {
  return (
    <Box sx={{ mb: 1.5, "&:last-of-type": { mb: 0 } }}>
      <Typography component="dt" variant="caption" sx={{ color: "text.secondary" }}>
        {label}
      </Typography>
      <Typography component="dd" sx={{ m: 0, fontWeight: 600 }}>
        {value}
      </Typography>
    </Box>
  );
}

interface RemoveFactorDialogProps {
  open: boolean;
  onClose: () => void;
  onRemoved: () => void;
}

function RemoveFactorDialog({ open, onClose, onRemoved }: RemoveFactorDialogProps) {
  const { t } = useTranslation("account");
  const formId = useId();
  const messageId = useId();
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) {
      setPassword("");
      setCode("");
      setError(null);
      setSubmitting(false);
    }
  }, [open]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await mfaService.removeFactor(password, code);
      toast.success(t("security.removed"));
      onRemoved();
    } catch (err: unknown) {
      const msg = serverMessage(err) ?? getErrorMessage(err, t("security.removeFailed"));
      setError(msg);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={submitting ? undefined : onClose}
      maxWidth="xs"
      fullWidth
      role="alertdialog"
      aria-describedby={messageId}
    >
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        <WarningAmberRoundedIcon color="error" aria-hidden />
        {t("security.removeTitle")}
      </DialogTitle>
      <DialogContent>
        <Typography id={messageId} variant="body2" sx={{ mb: 2 }}>
          {t("security.removeMessage")}
        </Typography>
        <Box
          component="form"
          id={formId}
          onSubmit={handleSubmit}
          noValidate
          sx={{ display: "flex", flexDirection: "column", gap: 2 }}
        >
          {error && <Alert severity="error" role="alert">{error}</Alert>}
          <TextField
            label={t("security.password")}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            disabled={submitting}
            fullWidth
            slotProps={{
              inputLabel: { required: false },
              htmlInput: { required: true }
            }}
          />
          <MfaCodeField value={code} onChange={setCode} disabled={submitting} />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting} autoFocus>
          {t("security.cancel")}
        </Button>
        <Button
          type="submit"
          form={formId}
          variant="contained"
          color="error"
          disabled={submitting || !password || code.length < 6}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {t("security.removeTitle")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/**
 * Self-service MFA status, setup, recovery-code regeneration, removal, and trusted computers.
 */
export function ProfileSecuritySection() {
  const { t } = useTranslation("account");
  const factorTypeLabel = useFactorTypeLabel();
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  const [setupOpen, setSetupOpen] = useState(false);

  const [regenOpen, setRegenOpen] = useState(false);
  const [regenPassword, setRegenPassword] = useState("");
  const [regenCode, setRegenCode] = useState("");
  const [regenError, setRegenError] = useState<string | null>(null);
  const [regenSubmitting, setRegenSubmitting] = useState(false);
  const [regeneratedCodes, setRegeneratedCodes] = useState<string[] | null>(null);

  const [removeOpen, setRemoveOpen] = useState(false);

  const [forgetTarget, setForgetTarget] = useState<TrustedDeviceInfo | null>(null);
  const [forgetLoading, setForgetLoading] = useState(false);

  const reloadStatus = useCallback(() => {
    setReloadNonce((n) => n + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setLoadError(null);

    mfaService
      .getStatus(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) {
          setStatus(data);
          setLoading(false);
        }
      })
      .catch((err: unknown) => {
        if (!controller.signal.aborted) {
          setLoadError(serverMessage(err) ?? getErrorMessage(err, i18n.t("security.loadFailed", { ns: "account" })));
          setLoading(false);
        }
      });

    return () => {
      controller.abort();
    };
  }, [reloadNonce]);

  const closeRegenDialog = () => {
    setRegenOpen(false);
    setRegenPassword("");
    setRegenCode("");
    setRegenError(null);
    setRegenSubmitting(false);
    setRegeneratedCodes(null);
  };

  const handleRegenSubmit = async () => {
    setRegenSubmitting(true);
    setRegenError(null);
    try {
      const res = await mfaService.regenerateRecoveryCodes(regenPassword, regenCode);
      setRegenPassword("");
      setRegenCode("");
      setRegeneratedCodes(res.recoveryCodes);
    } catch (err: unknown) {
      const msg = serverMessage(err) ?? getErrorMessage(err, t("security.regenerateFailed"));
      setRegenError(msg);
      toast.error(msg);
    } finally {
      setRegenSubmitting(false);
    }
  };

  const handleConfirmForget = async () => {
    if (!forgetTarget || forgetLoading) return;
    setForgetLoading(true);
    try {
      await mfaService.forgetTrustedDevice(forgetTarget.id);
      setForgetTarget(null);
      toast.success(t("security.trustedRemoved"));
      reloadStatus();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, t("security.trustedRemoveFailed")));
    } finally {
      setForgetLoading(false);
    }
  };

  const startAuthenticatedEnrolment = useCallback((password: string) => {
    return mfaService.startEnrolment(password);
  }, []);

  const confirmAuthenticatedEnrolment = useCallback((code: string) => {
    return mfaService.confirmEnrolment(code);
  }, []);

  return (
    <Box sx={{ mt: 4 }}>
      <SectionTitle>{t("security.title")}</SectionTitle>

      {loading && !status ? (
        <Paper sx={{ p: 2.5, maxWidth: 680 }}>
          <DialogBodySkeleton blocks={4} blockHeight={28} />
        </Paper>
      ) : loadError ? (
        <Alert
          severity="error"
          sx={{ maxWidth: 680 }}
          action={
            <Button color="inherit" size="small" onClick={reloadStatus}>
              {t("security.retry")}
            </Button>
          }
        >
          {loadError}
        </Alert>
      ) : status ? (
        <Stack spacing={3} sx={{ maxWidth: 680 }}>
          <Paper sx={{ p: 2.5 }}>
            <Box component="dl" sx={{ m: 0 }}>
              <SecurityField
                label={t("security.secondFactor")}
                value={status.enrolled ? factorTypeLabel(status.type) : t("security.notEnrolled")}
              />
              {status.enrolled && status.confirmedAt && (
                <SecurityField
                  label={t("security.confirmed")}
                  value={formatDateTimeWithTimeZone(status.confirmedAt)}
                />
              )}
              {status.enrolled && (
                <SecurityField
                  label={t("security.recoveryCodes")}
                  value={t("security.remaining", { count: status.recoveryCodesRemaining })}
                />
              )}
            </Box>

            <Stack direction="row" spacing={1.5} useFlexGap sx={{ mt: 2, alignItems: "center", flexWrap: "wrap" }}>
              {!status.enrolled ? (
                <Button variant="contained" onClick={() => setSetupOpen(true)}>
                  {t("security.setUp")}
                </Button>
              ) : (
                <>
                  <Button variant="outlined" onClick={() => setRegenOpen(true)}>
                    {t("security.regenerateCodes")}
                  </Button>
                  <Button
                    variant="outlined"
                    color="error"
                    disabled={status.required}
                    onClick={() => setRemoveOpen(true)}
                  >
                    {t("security.remove")}
                  </Button>
                  {status.required && (
                    <Typography variant="body2" color="text.secondary">
                      {t("security.roleRequires")}
                    </Typography>
                  )}
                </>
              )}
            </Stack>
          </Paper>

          <Paper sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" sx={{ mb: 1.5 }}>
              {t("security.trustedComputers")}
            </Typography>

            {status.trustedDevices.length === 0 ? (
              <EmptyState
                title={t("security.noTrustedTitle")}
                description={t("security.noTrustedDescription")}
              />
            ) : (
              <Stack spacing={1.5}>
                {status.trustedDevices.map((device) => {
                  const browserLabel = describeUserAgent(device.userAgent);
                  const ipLabel = device.createdIp || t("security.unknownIp");
                  return (
                    <Box
                      key={device.id}
                      sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: 2,
                        p: 1.5,
                        borderRadius: 1,
                        border: 1,
                        borderColor: "divider"
                      }}
                    >
                      <Box sx={{ minWidth: 0 }}>
                        <Tooltip title={device.userAgent || t("security.unknownBrowser")}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                            {browserLabel}
                          </Typography>
                        </Tooltip>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          <Trans i18nKey="security.ipLine" ns="account" values={{ ip: ipLabel }} components={{ ltr: <Ltr>{null}</Ltr> }} />
                        </Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          <Trans
                            i18nKey="security.trustedLine"
                            ns="account"
                            values={{
                              created: formatDateTimeWithTimeZone(device.createdAt),
                              expires: formatDateTimeWithTimeZone(device.expiresAt)
                            }}
                            components={{ ltr: <Ltr>{null}</Ltr> }}
                          />
                        </Typography>
                      </Box>
                      <Button
                        variant="outlined"
                        size="small"
                        color="error"
                        aria-label={t("security.forgetAria", { browser: browserLabel, ip: ipLabel })}
                        onClick={() => setForgetTarget(device)}
                      >
                        {t("security.forget")}
                      </Button>
                    </Box>
                  );
                })}
              </Stack>
            )}
          </Paper>
        </Stack>
      ) : null}

      <FloatingDialog
        open={setupOpen}
        onClose={() => setSetupOpen(false)}
        title={t("security.setupTitle")}
        maxWidth="xs"
      >
        <Box sx={{ pt: 1 }}>
          <AuthenticatedEnrolmentFlow
            startWithPassword={startAuthenticatedEnrolment}
            confirm={confirmAuthenticatedEnrolment}
            onDone={() => {
              setSetupOpen(false);
              reloadStatus();
            }}
          />
        </Box>
      </FloatingDialog>

      {regeneratedCodes ? (
        <FloatingDialog
          open={regenOpen}
          onClose={closeRegenDialog}
          title={t("security.regenerateTitle")}
          maxWidth="xs"
        >
          <Box sx={{ pt: 1 }}>
            <RecoveryCodesPanel
              codes={regeneratedCodes}
              introText={t("security.regeneratedIntro")}
              onDone={() => {
                closeRegenDialog();
                reloadStatus();
              }}
            />
          </Box>
        </FloatingDialog>
      ) : (
        <FormDialog
          open={regenOpen}
          onClose={closeRegenDialog}
          onSubmit={handleRegenSubmit}
          title={t("security.regenerateTitle")}
          submitLabel={t("security.regenerate")}
          submitting={regenSubmitting}
          submitDisabled={!regenPassword || regenCode.length < 6}
          error={regenError}
          maxWidth="xs"
        >
          <Typography variant="body2" color="text.secondary">
            {t("security.regenerateWarning")}
          </Typography>
          <TextField
            label={t("security.password")}
            type="password"
            value={regenPassword}
            onChange={(e) => setRegenPassword(e.target.value)}
            autoComplete="current-password"
            autoFocus
            disabled={regenSubmitting}
            fullWidth
            slotProps={{
              inputLabel: { required: false },
              htmlInput: { required: true }
            }}
          />
          <MfaCodeField value={regenCode} onChange={setRegenCode} disabled={regenSubmitting} />
        </FormDialog>
      )}

      <RemoveFactorDialog
        open={removeOpen}
        onClose={() => setRemoveOpen(false)}
        onRemoved={() => {
          setRemoveOpen(false);
          reloadStatus();
        }}
      />

      <ConfirmationDialog
        open={Boolean(forgetTarget)}
        destructive
        title={t("security.forgetTitle")}
        confirmText={t("security.forget")}
        loading={forgetLoading}
        message={
          forgetTarget
            ? t("security.forgetMessage", {
                browser: describeUserAgent(forgetTarget.userAgent),
                ip: forgetTarget.createdIp || t("security.unknownIp")
              })
            : ""
        }
        onCancel={() => setForgetTarget(null)}
        onConfirm={() => void handleConfirmForget()}
      />
    </Box>
  );
}
