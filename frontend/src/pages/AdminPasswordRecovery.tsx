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

const POLICY_RULES: { label: string; test: (pw: string) => boolean }[] = [
  { label: "At least 8 characters", test: (pw) => pw.length >= 8 },
  { label: "At least one uppercase letter", test: (pw) => /[A-Z]/.test(pw) },
  { label: "At least one lowercase letter", test: (pw) => /[a-z]/.test(pw) },
  { label: "At least one digit", test: (pw) => /[0-9]/.test(pw) },
  { label: "At least one special (non-alphanumeric) character", test: (pw) => /[^a-zA-Z0-9]/.test(pw) }
];

export function AdminPasswordRecovery() {
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
      setError("All fields are required.");
      return;
    }

    if (failedRules.length > 0) {
      setNewPasswordBlurred(true);
      setError("New password does not meet the policy requirements below.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("New password and confirm password do not match.");
      return;
    }

    setLoading(true);
    try {
      await authenticationService.confirmAdminPasswordRecovery(username, recoveryCode, newPassword);
      setSuccess(true);
    } catch (err: any) {
      setError(err?.response?.data?.message ?? "Could not perform password recovery. Please verify your recovery code.");
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
              Password Recovery
            </Typography>
            <Typography variant="body2" sx={{
              color: "text.secondary"
            }}>
              Administrator-Assisted Account Access Recovery
            </Typography>
          </Stack>

          {error && <Alert severity="error">{error}</Alert>}
          {success ? (
            <Stack spacing={2} sx={{
              textAlign: "center"
            }}>
              <Alert severity="success">
                Your password has been successfully reset! You can now log in using your new password.
              </Alert>
              <Button component={RouterLink} to="/login" variant="contained">
                Go to Login Page
              </Button>
            </Stack>
          ) : (
            <form onSubmit={handleSubmit}>
              <Stack spacing={2}>
                <TextField
                  label="Username"
                  size="small"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  fullWidth
                  required
                />
                <TextField
                  label="One-Time Recovery Code"
                  size="small"
                  placeholder="XXXX-XXXX-XXXX"
                  value={recoveryCode}
                  onChange={(e) => setRecoveryCode(e.target.value)}
                  fullWidth
                  required
                  helperText="Format: 12 alphanumeric characters provided by your admin"
                />
                <TextField
                  label="New Password"
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
                            aria-label={showNewPassword ? "Hide new password" : "Show new password"}
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
                  label="Confirm New Password"
                  size="small"
                  type={showConfirmPassword ? "text" : "password"}
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  fullWidth
                  required
                  error={confirmPassword.length > 0 && confirmPassword !== newPassword}
                  helperText={confirmPassword.length > 0 && confirmPassword !== newPassword ? "Passwords do not match." : undefined}
                  slotProps={{
                    input: {
                      endAdornment: (
                        <InputAdornment position="end">
                          <IconButton
                            size="small"
                            aria-label={showConfirmPassword ? "Hide confirm password" : "Show confirm password"}
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
                  <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>Password must contain:</Typography>
                  <List dense disablePadding aria-label="Password policy requirements" aria-live="polite">
                    {POLICY_RULES.map((rule) => {
                      const met = rule.test(newPassword);
                      const state = met ? "met" : newPasswordBlurred && newPassword ? "failed" : "pending";
                      const statusDesc = state === "met" ? "Satisfied" : state === "failed" ? "Requirement not met" : "Not met yet";
                      return (
                        <ListItem key={rule.label} disableGutters disablePadding>
                          <ListItemIcon sx={{ minWidth: 28 }}>
                            {state === "met" && <CheckCircleIcon fontSize="small" color="success" titleAccess="Met" />}
                            {state === "failed" && <CancelIcon fontSize="small" color="error" titleAccess="Not met" />}
                            {state === "pending" && <RemoveIcon fontSize="small" sx={{ color: "text.disabled" }} titleAccess="Not met yet" />}
                          </ListItemIcon>
                          <ListItemText
                            primary={
                              <>
                                {rule.label}
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
                  {loading ? "Resetting Password..." : "Submit Password Recovery"}
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
              Return to Login
            </Link>
          </Box>
        </Stack>
      </Paper>
    </Box>
  );
}
