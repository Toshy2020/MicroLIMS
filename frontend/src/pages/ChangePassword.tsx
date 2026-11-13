import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Box, TextField, Button, Alert, Paper, CircularProgress } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { PASSWORD_POLICY_RULES, PasswordPolicyChecklist } from "../modules/authentication/components/PasswordPolicyChecklist";
import { useAuth } from "../contexts/AuthContext";
import { getErrorMessage, serverMessage } from "../utils/errorMessage";

export function ChangePasswordPage() {
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
          ? "New signing password does not meet the policy requirements below."
          : "New password does not meet the policy requirements below."
      );
      return;
    }
    if (newPassword !== confirmPassword) {
      setError(
        isDirectoryUser
          ? "New signing password and confirmation do not match."
          : "New password and confirmation do not match."
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
          getErrorMessage(err, isDirectoryUser ? "Could not change signing password." : "Could not change password.")
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader
        title={isDirectoryUser ? "Change Signing Password" : "Change Password"}
        subtitle={
          isDirectoryUser
            ? "Update your MicroLIMS signing password. It is used only for electronic signatures and must not be your company password."
            : "Update your laboratory credentials in compliance with periodic security policy."
        }
      />
      <Paper sx={{ p: 3, maxWidth: 480 }}>
        {success ? (
          <Alert severity="success">
            {isDirectoryUser
              ? "Signing password changed successfully. Returning to your profile..."
              : "Password changed successfully. Returning to your profile..."}
          </Alert>
        ) : (
          <Box component="form" onSubmit={handleSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={isDirectoryUser ? "Current Signing Password" : "Current Password"}
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              autoFocus
              required
            />
            <TextField
              label={isDirectoryUser ? "New Signing Password" : "New Password"}
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              onBlur={() => setNewPasswordBlurred(true)}
            />
            <TextField
              label={isDirectoryUser ? "Confirm New Signing Password" : "Confirm New Password"}
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              error={confirmPassword.length > 0 && confirmPassword !== newPassword}
              helperText={confirmPassword.length > 0 && confirmPassword !== newPassword ? "Passwords do not match." : undefined}
            />

            <PasswordPolicyChecklist
              password={newPassword}
              blurred={newPasswordBlurred}
              heading={isDirectoryUser ? "Signing password must contain:" : "Password must contain:"}
            />

            <Button
              type="submit"
              variant="contained"
              size="large"
              disabled={submitting}
              startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : undefined}
            >
              {submitting ? "Updating Credentials..." : isDirectoryUser ? "Change Signing Password" : "Change Password"}
            </Button>
          </Box>
        )}
      </Paper>
    </>
  );
}
