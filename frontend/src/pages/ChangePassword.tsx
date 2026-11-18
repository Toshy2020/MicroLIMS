import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Box, TextField, Button, Alert, Paper, CircularProgress } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { PASSWORD_POLICY_RULES, PasswordPolicyChecklist } from "../modules/authentication/components/PasswordPolicyChecklist";
import { useAuth } from "../contexts/AuthContext";
import { getErrorMessage, serverMessage } from "../utils/errorMessage";
import { useTranslation } from "../i18n";

export function ChangePasswordPage() {
  const { t } = useTranslation("account");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  // Unmet rules only turn red once the user has left the field - flagging
  // them while the first characters are still being typed reads as an error
  // the user has not made yet.
  const [newPasswordBlurred, setNewPasswordBlurred] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const { refresh, signInMethod } = useAuth();
  const navigate = useNavigate();

  const isDirectoryUser = signInMethod === "Directory";
  const failedRules = PASSWORD_POLICY_RULES.filter((rule) => !rule.test(newPassword));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (failedRules.length > 0) {
      setError(
        isDirectoryUser
          ? t("changePassword.signingPolicyNotMet")
          : t("changePassword.policyNotMet")
      );
      return;
    }
    if (newPassword !== confirmPassword) {
      setError(
        isDirectoryUser
          ? t("changePassword.signingMismatch")
          : t("changePassword.passwordMismatch")
      );
      return;
    }

    setSubmitting(true);
    try {
      await authenticationService.changePassword(currentPassword, newPassword);
      await refresh();
      setSuccess(true);
      setTimeout(() => navigate("/profile"), 1500);
    } catch (err: unknown) {
      setError(
        serverMessage(err) ??
          getErrorMessage(err, isDirectoryUser ? t("changePassword.signingChangeFailed") : t("changePassword.passwordChangeFailed"))
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        title={isDirectoryUser ? t("changePassword.signingTitle") : t("changePassword.title")}
        subtitle={
          isDirectoryUser
            ? t("changePassword.signingSubtitle")
            : t("changePassword.subtitle")
        }
      />
      <Paper sx={{ p: 3, maxWidth: 480 }}>
        {success ? (
          <Alert severity="success">
            {isDirectoryUser
              ? t("changePassword.signingSuccess")
              : t("changePassword.success")}
          </Alert>
        ) : (
          <Box component="form" onSubmit={handleSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={isDirectoryUser ? t("changePassword.currentSigningPassword") : t("changePassword.currentPassword")}
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              autoFocus
              required
            />
            <TextField
              label={isDirectoryUser ? t("changePassword.newSigningPassword") : t("changePassword.newPassword")}
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              onBlur={() => setNewPasswordBlurred(true)}
            />
            <TextField
              label={isDirectoryUser ? t("changePassword.confirmSigningPassword") : t("changePassword.confirmPassword")}
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              error={confirmPassword.length > 0 && confirmPassword !== newPassword}
              helperText={confirmPassword.length > 0 && confirmPassword !== newPassword ? t("changePassword.passwordsDoNotMatch") : undefined}
            />

            <PasswordPolicyChecklist
              password={newPassword}
              blurred={newPasswordBlurred}
              heading={isDirectoryUser ? t("changePassword.signingPolicyHeading") : t("changePassword.policyHeading")}
            />

            <Button
              type="submit"
              variant="contained"
              size="large"
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              {submitting
                ? t("changePassword.updating")
                : isDirectoryUser
                  ? t("changePassword.signingTitle")
                  : t("changePassword.title")}
            </Button>
          </Box>
        )}
      </Paper>
    </>
  );
}
