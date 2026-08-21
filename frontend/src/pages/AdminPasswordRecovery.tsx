import { useState } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { Paper, TextField, Button, Stack, Typography, Alert, Box, Link } from "@mui/material";
import LockResetIcon from "@mui/icons-material/LockReset";
import { authenticationService } from "../modules/authentication/services/authenticationService";

export function AdminPasswordRecovery() {
  const navigate = useNavigate();
  const [username, setUsername] = useState("");
  const [recoveryCode, setRecoveryCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!username || !recoveryCode || !newPassword || !confirmPassword) {
      setError("All fields are required.");
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
    <Box sx={{ minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center", bgcolor: "grey.100", p: 2 }}>
      <Paper sx={{ p: 4, maxWidth: 450, width: "100%", borderRadius: 2 }}>
        <Stack spacing={3}>
          <Stack spacing={1} alignItems="center" textAlign="center">
            <LockResetIcon color="primary" sx={{ fontSize: 48 }} />
            <Typography variant="h5" fontWeight={700}>
              Password Recovery
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Administrator-Assisted Account Access Recovery
            </Typography>
          </Stack>

          {error && <Alert severity="error">{error}</Alert>}
          {success ? (
            <Stack spacing={2} textAlign="center">
              <Alert severity="success">
                Your password has been successfully reset! You can now log in using your new password.
              </Alert>
              <Button variant="contained" onClick={() => navigate("/login")}>
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
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  fullWidth
                  required
                  helperText="Min 8 chars (upper, lower, digit, symbol)"
                />
                <TextField
                  label="Confirm New Password"
                  size="small"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  fullWidth
                  required
                />
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

          <Box textAlign="center" sx={{ mt: 1 }}>
            <Link component={RouterLink} to="/login" variant="body2" underline="hover">
              Return to Login
            </Link>
          </Box>
        </Stack>
      </Paper>
    </Box>
  );
}
