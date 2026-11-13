import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { Alert, Box, Button, CircularProgress, Paper, TextField } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { PASSWORD_POLICY_RULES, PasswordPolicyChecklist } from "../modules/authentication/components/PasswordPolicyChecklist";
import { useAuth } from "../contexts/AuthContext";
import { getErrorMessage, serverMessage } from "../utils/errorMessage";

export function SetSigningPasswordPage() {
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
      setError("Signing password does not meet the policy requirements below.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("Signing password and confirmation do not match.");
      return;
    }

    setSubmitting(true);
    try {
      await authenticationService.setSigningPassword(newPassword);
      clearSigningPasswordRequired();
      toast.success("Signing password saved.");
      navigate("/");
    } catch (err: unknown) {
      const message = serverMessage(err) ?? getErrorMessage(err, "Could not save signing password.");
      setError(message);
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        title="Choose your signing password"
        subtitle="Your signing password is used only for electronic signatures in MicroLIMS and must not be your company password."
      />
      <Paper sx={{ p: 3, maxWidth: 480 }}>
        <Box component="form" onSubmit={handleSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {error && (
            <Alert severity="error" role="alert">
              {error}
            </Alert>
          )}
          <TextField
            label="New Signing Password"
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            onBlur={() => setNewPasswordBlurred(true)}
            autoFocus
            required
          />
          <TextField
            label="Confirm Signing Password"
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
            error={mismatch}
            helperText={mismatch ? "Signing passwords do not match." : undefined}
          />

          <PasswordPolicyChecklist
            password={newPassword}
            blurred={newPasswordBlurred}
            heading="Signing password must contain:"
          />

          <Button
            type="submit"
            variant="contained"
            size="large"
            disabled={submitting}
            startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : undefined}
          >
            {submitting ? "Saving..." : "Save Signing Password"}
          </Button>
        </Box>
      </Paper>
    </>
  );
}
