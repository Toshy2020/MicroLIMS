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

function factorTypeLabel(type: string | null | undefined): string {
  if (type === "HardwareToken") return "Hardware token";
  return "Authenticator app";
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
      toast.success("Second factor removed.");
      onRemoved();
    } catch (err: unknown) {
      const msg = serverMessage(err) ?? getErrorMessage(err, "Could not remove second factor.");
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
        Remove second factor
      </DialogTitle>
      <DialogContent>
        <Typography id={messageId} variant="body2" sx={{ mb: 2 }}>
          Enter your password and current 6-digit code to remove two-factor authentication from your account.
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
            label="Password"
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
          Cancel
        </Button>
        <Button
          type="submit"
          form={formId}
          variant="contained"
          color="error"
          disabled={submitting || !password || code.length < 6}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          Remove second factor
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/**
 * Self-service MFA status, setup, recovery-code regeneration, removal, and trusted computers.
 */
export function ProfileSecuritySection() {
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
          setLoadError(serverMessage(err) ?? getErrorMessage(err, "Could not load security settings."));
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
      const msg = serverMessage(err) ?? getErrorMessage(err, "Could not regenerate recovery codes.");
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
      toast.success("Trusted computer removed.");
      reloadStatus();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, "Could not remove trusted computer."));
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
      <SectionTitle>Security</SectionTitle>

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
              Retry
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
                label="Second factor"
                value={status.enrolled ? factorTypeLabel(status.type) : "Not enrolled"}
              />
              {status.enrolled && status.confirmedAt && (
                <SecurityField
                  label="Confirmed"
                  value={formatDateTimeWithTimeZone(status.confirmedAt)}
                />
              )}
              {status.enrolled && (
                <SecurityField
                  label="Recovery codes"
                  value={`${status.recoveryCodesRemaining} remaining`}
                />
              )}
            </Box>

            <Stack direction="row" spacing={1.5} useFlexGap sx={{ mt: 2, alignItems: "center", flexWrap: "wrap" }}>
              {!status.enrolled ? (
                <Button variant="contained" onClick={() => setSetupOpen(true)}>
                  Set up
                </Button>
              ) : (
                <>
                  <Button variant="outlined" onClick={() => setRegenOpen(true)}>
                    Regenerate recovery codes
                  </Button>
                  <Button
                    variant="outlined"
                    color="error"
                    disabled={status.required}
                    onClick={() => setRemoveOpen(true)}
                  >
                    Remove
                  </Button>
                  {status.required && (
                    <Typography variant="body2" color="text.secondary">
                      Your role requires a second factor
                    </Typography>
                  )}
                </>
              )}
            </Stack>
          </Paper>

          <Paper sx={{ p: 2.5 }}>
            <Typography variant="subtitle1" sx={{ mb: 1.5 }}>
              Trusted computers
            </Typography>

            {status.trustedDevices.length === 0 ? (
              <EmptyState
                title="No trusted computers"
                description="Computers you choose to trust during sign-in will appear here."
              />
            ) : (
              <Stack spacing={1.5}>
                {status.trustedDevices.map((device) => {
                  const browserLabel = describeUserAgent(device.userAgent);
                  const ipLabel = device.createdIp || "Unknown IP";
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
                        <Tooltip title={device.userAgent || "Unknown browser"}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }} noWrap>
                            {browserLabel}
                          </Typography>
                        </Tooltip>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          IP: {ipLabel}
                        </Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          Trusted: {formatDateTimeWithTimeZone(device.createdAt)} · Expires:{" "}
                          {formatDateTimeWithTimeZone(device.expiresAt)}
                        </Typography>
                      </Box>
                      <Button
                        variant="outlined"
                        size="small"
                        color="error"
                        aria-label={`Forget ${browserLabel} (${ipLabel})`}
                        onClick={() => setForgetTarget(device)}
                      >
                        Forget
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
        title="Set up second factor"
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
          title="Regenerate recovery codes"
          maxWidth="xs"
        >
          <Box sx={{ pt: 1 }}>
            <RecoveryCodesPanel
              codes={regeneratedCodes}
              introText="Your previous recovery codes are no longer valid. Save these new codes before continuing."
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
          title="Regenerate recovery codes"
          submitLabel="Regenerate"
          submitting={regenSubmitting}
          submitDisabled={!regenPassword || regenCode.length < 6}
          error={regenError}
          maxWidth="xs"
        >
          <Typography variant="body2" color="text.secondary">
            Generating a new set invalidates any unused recovery codes.
          </Typography>
          <TextField
            label="Password"
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
        title="Forget trusted computer"
        confirmText="Forget"
        loading={forgetLoading}
        message={
          forgetTarget
            ? `Next time you sign in from ${describeUserAgent(forgetTarget.userAgent)} (${forgetTarget.createdIp || "Unknown IP"}), a 6-digit code will be required.`
            : ""
        }
        onCancel={() => setForgetTarget(null)}
        onConfirm={() => void handleConfirmForget()}
      />
    </Box>
  );
}
