import React, { useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Typography,
  Box,
  Alert,
  CircularProgress,
  Paper,
  Divider,
  InputAdornment,
  IconButton
} from "@mui/material";
import LockIcon from "@mui/icons-material/Lock";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import VerifiedUserIcon from "@mui/icons-material/VerifiedUser";

interface ElectronicSignatureDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (password: string) => Promise<void>;
  documentCode: string;
  documentTitle: string;
  revisionNumber: string;
  signerFullName: string;
  signerUsername: string;
  signerRole?: string;
  meaning?: string;
}

export const ElectronicSignatureDialog: React.FC<ElectronicSignatureDialogProps> = ({
  open,
  onClose,
  onConfirm,
  documentCode,
  documentTitle,
  revisionNumber,
  signerFullName,
  signerUsername,
  signerRole = "Designated Approver",
  meaning = "Approved"
}) => {
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [signing, setSigning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSign = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!password) {
      setError("Password re-authentication is required to apply electronic signature.");
      return;
    }

    try {
      setSigning(true);
      setError(null);
      await onConfirm(password);
      setPassword("");
      onClose();
    } catch (err: any) {
      const msg =
        err.response?.data?.message ||
        err.response?.data?.errors?.[0] ||
        err.message ||
        "Electronic signature authentication failed.";
      setError(msg);
    } finally {
      setSigning(false);
    }
  };

  const handleClose = () => {
    if (!signing) {
      setPassword("");
      setError(null);
      onClose();
    }
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ pb: 1, borderBottom: "1px solid", borderColor: "divider" }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <VerifiedUserIcon color="primary" />
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            21 CFR Part 11 Electronic Signature
          </Typography>
        </Box>
      </DialogTitle>

      <form onSubmit={handleSign}>
        <DialogContent sx={{ pt: 2.5 }}>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          {/* Legal / Regulatory Notice */}
          <Alert
            severity="warning"
            icon={<LockIcon fontSize="inherit" />}
            sx={{
              mb: 2.5,
              bgcolor: (theme) =>
                theme.palette.mode === "dark" ? "#332714" : "#FFF9E6",
              border: "1px solid",
              borderColor: (theme) =>
                theme.palette.mode === "dark" ? "#664D28" : "#FFE299"
            }}
          >
            <Typography variant="caption" sx={{ fontWeight: 600, display: "block" }}>
              21 CFR Part 11 Statutory Notice:
            </Typography>
            <Typography variant="caption">
              By executing this digital signature ceremony with your authenticated credentials,
              you certify under 21 CFR Part 11 that you have reviewed and approve this controlled
              document revision. This electronic signature carries the full legal weight and binding
              effect of a handwritten signature.
            </Typography>
          </Alert>

          {/* Signature Manifest Card */}
          <Paper variant="outlined" sx={{ p: 2, mb: 2.5, bgcolor: (t) => t.palette.mode === "dark" ? "#1A2027" : "#F8FAFC" }}>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.5, display: "block", mb: 1 }}>
              Signature Attribution Record
            </Typography>
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
              <Box>
                <Typography variant="caption" color="text.secondary">Document Code:</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{documentCode}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Revision Number:</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>Rev {revisionNumber}</Typography>
              </Box>
              <Box sx={{ gridColumn: "span 2" }}>
                <Typography variant="caption" color="text.secondary">Document Title:</Typography>
                <Typography variant="body2" sx={{ fontWeight: 500 }}>{documentTitle}</Typography>
              </Box>
            </Box>
            <Divider sx={{ my: 1.5 }} />
            <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 1 }}>
              <Box>
                <Typography variant="caption" color="text.secondary">Signer Name:</Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{signerFullName}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Username:</Typography>
                <Typography variant="body2">{signerUsername}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Signer Role:</Typography>
                <Typography variant="body2">{signerRole}</Typography>
              </Box>
              <Box>
                <Typography variant="caption" color="text.secondary">Signature Meaning:</Typography>
                <Typography variant="body2" sx={{ fontWeight: 700, color: "success.main" }}>
                  {meaning}
                </Typography>
              </Box>
            </Box>
          </Paper>

          {/* Password Re-Entry Field */}
          <TextField
            label="Re-enter Your Password to Authenticate"
            type={showPassword ? "text" : "password"}
            required
            fullWidth
            autoFocus
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            disabled={signing}
            placeholder="Account password"
            InputProps={{
              endAdornment: (
                <InputAdornment position="end">
                  <IconButton
                    aria-label="toggle password visibility"
                    onClick={() => setShowPassword(!showPassword)}
                    edge="end"
                  >
                    {showPassword ? <VisibilityOff /> : <Visibility />}
                  </IconButton>
                </InputAdornment>
              )
            }}
            helperText="Per 21 CFR §11.200, individual password confirmation is required at the time of each signing ceremony."
          />
        </DialogContent>

        <DialogActions sx={{ px: 3, py: 2, borderTop: "1px solid", borderColor: "divider" }}>
          <Button onClick={handleClose} disabled={signing}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            color="success"
            disabled={signing || !password.trim()}
            startIcon={signing ? <CircularProgress size={16} color="inherit" /> : <LockIcon />}
          >
            {signing ? "Authenticating & Signing..." : "Sign & Approve"}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
};