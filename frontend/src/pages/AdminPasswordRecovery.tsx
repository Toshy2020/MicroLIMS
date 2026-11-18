import { useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Paper, TextField, Button, Stack, Typography, Alert, Box, Link,
  List, ListItem, ListItemIcon, ListItemText, IconButton, InputAdornment
} from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import LockResetIcon from "@mui/icons-material/LockReset";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import RemoveIcon from "@mui/icons-material/Remove";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { useTranslation } from "../i18n";
import { serverMessage } from "../utils/errorMessage";

const RECOVERY_CODE_PLACEHOLDER = "XXXX-XXXX-XXXX";

const POLICY_RULES: { key: "minLength" | "uppercase" | "lowercase" | "digit" | "special"; test: (pw: string) => boolean }[] = [
  { key: "minLength", test: (pw) => pw.length >= 8 },
  { key: "uppercase", test: (pw) => /[A-Z]/.test(pw) },
  { key: "lowercase", test: (pw) => /[a-z]/.test(pw) },
  { key: "digit", test: (pw) => /[0-9]/.test(pw) },
  { key: "special", test: (pw) => /[^a-zA-Z0-9]/.test(pw) }
];

function ruleState(met: boolean, blurred: boolean, pw: string): "met" | "failed" | "pending" {
  if (met) return "met";
  return blurred && pw ? "failed" : "pending";
}

export function AdminPasswordRecovery() {
  const { t } = useTranslation("account");
  const [username, setUsername] = useState("");
  const [recoveryCode, setRecoveryCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [newPasswordBlurred, setNewPasswordBlurred] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const failedRules = POLICY_RULES.filter((rule) => !rule.test(newPassword));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!username || !recoveryCode || !newPassword || !confirmPassword) {
      setError(t("adminRecovery.allFieldsRequired"));
      return;
    }

    if (failedRules.length > 0) {
      setNewPasswordBlurred(true);
      setError(t("adminRecovery.policyNotMet"));
      return;
    }

    if (newPassword !== confirmPassword) {
      setError(t("adminRecovery.mismatch"));
      return;
    }

    setLoading(true);
    try {
      await authenticationService.confirmAdminPasswordRecovery(username, recoveryCode, newPassword);
      setSuccess(true);
    } catch (err: unknown) {
      setError(serverMessage(err) ?? t("adminRecovery.recoveryFailed"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", bgcolor: "background.default", p: 2 }}>
      <Paper sx={{ p: 4, maxWidth: 460, width: "100%", borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
        <Stack spacing={3}>
          <Stack
            spacing={1}
            sx={{
              alignItems: "center",
              textAlign: "center"
            }}>
            <LockResetIcon color="primary" sx={{ fontSize: 48 }} />
            <Typography variant="h5" sx={{
              fontWeight: 700
            }}>
              {t("adminRecovery.title")}
            </Typography>
            <Typography variant="body2" sx={{
              color: "text.secondary"
            }}>
              {t("adminRecovery.subtitle")}
            </Typography>
          </Stack>

          {error && <Alert severity="error">{error}</Alert>}
          {success ? (
            <Stack spacing={2} sx={{
              textAlign: "center"
            }}>
              <Alert severity="success">
                {t("adminRecovery.successMessage")}
              </Alert>
              <Button component={RouterLink} to="/login" variant="contained">
                {t("adminRecovery.goToLogin")}
              </Button>
            </Stack>
          ) : (
            <form onSubmit={handleSubmit}>
              <Stack spacing={2}>
                <TextField
                  label={t("adminRecovery.username")}
                  size="small"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  fullWidth
                  required
                />
                <TextField
                  label={t("adminRecovery.recoveryCodeLabel")}
                  size="small"
                  placeholder={RECOVERY_CODE_PLACEHOLDER}
                  value={recoveryCode}
                  onChange={(e) => setRecoveryCode(e.target.value)}
                  fullWidth
                  required
                  helperText={t("adminRecovery.recoveryCodeHelper")}
                />
                <TextField
                  label={t("adminRecovery.newPassword")}
                  size="small"
                  type={showNewPassword ? "text" : "password"}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  onBlur={() => setNewPasswordBlurred(true)}
                  fullWidth
                  required
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            size="small"
                            aria-label={showNewPassword ? t("adminRecovery.hideNewPassword") : t("adminRecovery.showNewPassword")}
                            onClick={() => setShowNewPassword((prev) => !prev)}
                            edge="end"
                          >
                            {showNewPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                          </IconButton>
                        </InputAdornment>
                      )
                    }
                  }}
                />
                <TextField
                  label={t("adminRecovery.confirmNewPassword")}
                  size="small"
                  type={showConfirmPassword ? "text" : "password"}
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  fullWidth
                  required
                  error={confirmPassword.length > 0 && confirmPassword !== newPassword}
                  helperText={confirmPassword.length > 0 && confirmPassword !== newPassword ? t("adminRecovery.passwordsDoNotMatch") : undefined}
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            size="small"
                            aria-label={showConfirmPassword ? t("adminRecovery.hideConfirmPassword") : t("adminRecovery.showConfirmPassword")}
                            onClick={() => setShowConfirmPassword((prev) => !prev)}
                            edge="end"
                          >
                            {showConfirmPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                          </IconButton>
                        </InputAdornment>
                      )
                    }
                  }}
                />

                <Box>
                  <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>{t("adminRecovery.policyHeading")}</Typography>
                  <List dense disablePadding aria-label={t("adminRecovery.policyAria")} aria-live="polite">
                    {POLICY_RULES.map((rule) => {
                      const met = rule.test(newPassword);
                      const state = ruleState(met, newPasswordBlurred, newPassword);
                      const statusDesc = state === "met" ? t("adminRecovery.statusMet") : state === "failed" ? t("adminRecovery.statusFailed") : t("adminRecovery.statusPending");
                      return (
                        <ListItem key={rule.key} disableGutters disablePadding>
                          <ListItemIcon sx={{ minWidth: 28 }}>
                            {state === "met" && <CheckCircleIcon fontSize="small" color="success" titleAccess={t("adminRecovery.iconMet")} />}
                            {state === "failed" && <CancelIcon fontSize="small" color="error" titleAccess={t("adminRecovery.iconFailed")} />}
                            {state === "pending" && <RemoveIcon fontSize="small" sx={{ color: "text.disabled" }} titleAccess={t("adminRecovery.iconPending")} />}
                          </ListItemIcon>
                          <ListItemText
                            primary={
                              <>
                                {t(`adminRecovery.rules.${rule.key}`)}
                                <Box component="span" sx={visuallyHidden}>: {statusDesc}</Box>
                              </>
                            }
                            slotProps={{
                              primary: { sx: { fontSize: 13, color: state === "failed" ? "error.main" : met ? "text.primary" : "text.secondary" } }
                            }}
                          />
                        </ListItem>
                      );
                    })}
                  </List>
                </Box>

                <Button
                  type="submit"
                  variant="contained"
                  color="primary"
                  size="large"
                  disabled={loading}
                  fullWidth
                >
                  {loading ? t("adminRecovery.resetting") : t("adminRecovery.submit")}
                </Button>
              </Stack>
            </form>
          )}

          <Box
            sx={{
              textAlign: "center",
              mt: 1
            }}>
            <Link component={RouterLink} to="/login" variant="body2" underline="hover">
              {t("adminRecovery.returnToLogin")}
            </Link>
          </Box>
        </Stack>
      </Paper>
    </Box>
  );
}
