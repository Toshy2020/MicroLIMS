import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Box, TextField, Button, Typography, Alert, Paper, List, ListItem, ListItemIcon, ListItemText } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import RadioButtonUncheckedIcon from "@mui/icons-material/RadioButtonUnchecked";
import { PageHeader } from "../components/PageHeader";
import { SectionTitle } from "../components/SectionTitle";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { useAuth } from "../contexts/AuthContext";

// Mirrors the server-side PasswordPolicy (MicroLIMS.Shared.Validation) -
// the server still validates authoritatively regardless of this.
const POLICY_RULES: { label: string; test: (pw: string) => boolean }[] = [
  { label: "At least 8 characters", test: (pw) => pw.length >= 8 },
  { label: "At least one uppercase letter", test: (pw) => /[A-Z]/.test(pw) },
  { label: "At least one lowercase letter", test: (pw) => /[a-z]/.test(pw) },
  { label: "At least one digit", test: (pw) => /[0-9]/.test(pw) },
  { label: "At least one special (non-alphanumeric) character", test: (pw) => /[^a-zA-Z0-9]/.test(pw) }
];

export function ChangePasswordPage() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const { refresh } = useAuth();
  const navigate = useNavigate();

  const failedRules = POLICY_RULES.filter((rule) => !rule.test(newPassword));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (failedRules.length > 0) {
      setError("New password does not meet the policy requirements below.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New password and confirmation do not match.");
      return;
    }

    setSubmitting(true);
    try {
      await authenticationService.changePassword(currentPassword, newPassword);
      await refresh();
      setSuccess(true);
      setTimeout(() => navigate("/profile"), 1500);
    } catch (err: any) {
      setError(err.response?.data?.message ?? "Could not change password.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHeader title="Change Password" />
      <SectionTitle>Update Your Password</SectionTitle>
      <Paper sx={{ p: 3, maxWidth: 480 }}>
        {success ? (
          <Alert severity="success">Password changed successfully. Returning to your profile...</Alert>
        ) : (
          <Box component="form" onSubmit={handleSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label="Current Password" type="password" value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)} autoFocus required
            />
            <TextField
              label="New Password" type="password" value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)} required
            />
            <TextField
              label="Confirm New Password" type="password" value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)} required
              error={confirmPassword.length > 0 && confirmPassword !== newPassword}
              helperText={confirmPassword.length > 0 && confirmPassword !== newPassword ? "Passwords do not match." : " "}
            />

            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>Password must contain:</Typography>
              <List dense disablePadding>
                {POLICY_RULES.map((rule) => {
                  const met = rule.test(newPassword);
                  return (
                    <ListItem key={rule.label} disableGutters disablePadding>
                      <ListItemIcon sx={{ minWidth: 28 }}>
                        {met ? <CheckCircleIcon fontSize="small" color="success" /> : <RadioButtonUncheckedIcon fontSize="small" color="disabled" />}
                      </ListItemIcon>
                      <ListItemText primaryTypographyProps={{ fontSize: 13, color: met ? "text.primary" : "text.secondary" }} primary={rule.label} />
                    </ListItem>
                  );
                })}
              </List>
            </Box>

            <Button type="submit" variant="contained" size="large" disabled={submitting}>
              {submitting ? "Changing..." : "Change Password"}
            </Button>
          </Box>
        )}
      </Paper>
    </>
  );
}
