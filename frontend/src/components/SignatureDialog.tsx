import { useState } from "react";
import { Box, Typography, TextField, Button, Alert, Stack } from "@mui/material";
import { FloatingDialog } from "./FloatingDialog";
import { useAuth } from "../contexts/AuthContext";

interface SignatureDialogProps {
  open: boolean;
  meaningStatement: string; // plain-language statement, e.g. "I am approving this result."
  showComment?: boolean;
  comment?: string;
  onCommentChange?: (value: string) => void;
  onCancel: () => void;
  onConfirm: (password: string) => Promise<void>;
}

// 21 CFR Part 11 signing UI: the signer's own name/role (read-only),
// the meaning of the signature in plain language, a password field
// (re-verified server-side - being logged in is not sufficient), and an
// optional comment. On failure the server's message is shown and the
// password is cleared, but the dialog stays open.
export function SignatureDialog({ open, meaningStatement, showComment = false, comment, onCommentChange, onCancel, onConfirm }: SignatureDialogProps) {
  const { fullName, username, role } = useAuth();
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleConfirm = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await onConfirm(password);
      setPassword("");
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Signature failed.");
      setPassword("");
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = () => {
    setPassword("");
    setError(null);
    onCancel();
  };

  return (
    <FloatingDialog
      open={open}
      title="Electronic Signature"
      onClose={handleCancel}
      actions={
        <>
          <Button onClick={handleCancel}>Cancel</Button>
          <Button variant="contained" onClick={handleConfirm} disabled={!password || submitting}>
            {submitting ? "Signing..." : "Sign"}
          </Button>
        </>
      }
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Box>
          <Typography sx={{ fontWeight: 700 }}>{fullName ?? username}</Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{role}</Typography>
        </Box>
        <Alert severity="info">{meaningStatement}</Alert>
        {showComment && (
          <TextField label="Comment (optional)" multiline rows={2} value={comment ?? ""} onChange={(e) => onCommentChange?.(e.target.value)} />
        )}
        <TextField
          label="Password" type="password" value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoFocus required
          onKeyDown={(e) => { if (e.key === "Enter" && password) handleConfirm(); }}
        />
      </Stack>
    </FloatingDialog>
  );
}
