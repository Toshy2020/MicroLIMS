import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { Alert, Box, Button, CircularProgress, Paper, TextField } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { PASSWORD_POLICY_RULES, PasswordPolicyChecklist } from "../modules/authentication/components/PasswordPolicyChecklist";
import { useAuth } from "../contexts/AuthContext";
import { getErrorMessage, serverMessage } from "../utils/errorMessage";
import { useTranslation } from "../i18n";

export function SetSigningPasswordPage() {
  const { t } = useTranslation("account");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [newPasswordBlurred, setNewPasswordBlurred] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const { clearSigningPasswordRequired } = useAuth();
  const navigate = useNavigate();

  const failedRules = PASSWORD_POLICY_RULES.filter((rule) => !rule.test(newPassword));
  const mismatch = confirmPassword.length > 0 && confirmPassword !== newPassword;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (failedRules.length > 0) {
      setError(t("setSigningPassword.policyNotMet"));
      return;
    }
    if (newPassword !== confirmPassword) {
      setError(t("setSigningPassword.mismatch"));
      return;
    }

    setSubmitting(true);
    try {
      await authenticationService.setSigningPassword(newPassword);
      clearSigningPasswordRequired();
      toast.success(t("setSigningPassword.saved"));
      navigate("/");
    } catch (err: unknown) {
      const message = serverMessage(err) ?? getErrorMessage(err, t("setSigningPassword.saveFailed"));
      setError(message);
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        title={t("setSigningPassword.title")}
        subtitle={t("setSigningPassword.subtitle")}
      />
      <Paper sx={{ p: 3, maxWidth: 480 }}>
        <Box component="form" onSubmit={handleSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {error && (
            <Alert severity="error" role="alert">
              {error}
            </Alert>
          )}
          <TextField
            label={t("setSigningPassword.newPassword")}
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            onBlur={() => setNewPasswordBlurred(true)}
            autoFocus
            required
          />
          <TextField
            label={t("setSigningPassword.confirmPassword")}
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
            error={mismatch}
            helperText={mismatch ? t("setSigningPassword.passwordsDoNotMatch") : undefined}
          />

          <PasswordPolicyChecklist
            password={newPassword}
            blurred={newPasswordBlurred}
            heading={t("setSigningPassword.policyHeading")}
          />

          <Button
            type="submit"
            variant="contained"
            size="large"
            disabled={submitting}
            startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : undefined}
          >
            {submitting ? t("setSigningPassword.saving") : t("setSigningPassword.save")}
          </Button>
        </Box>
      </Paper>
    </>
  );
}
