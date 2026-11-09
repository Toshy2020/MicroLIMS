import { useState } from "react";
import { Box, Typography, TextField, Button, Alert, Stack, Divider } from "@mui/material";
import { FloatingDialog } from "./FloatingDialog";
import { useAuth } from "../contexts/AuthContext";
import { formatRoleName } from "../utils/userDisplay";

// A second person's credentials, for changes two people must sign.
export interface SecondSignature {
  username: string;
  password: string;
}

interface SignatureDialogProps {
  open: boolean;
  meaningStatement: string; // plain-language statement, e.g. "I am approving this result."
  showComment?: boolean;
  commentLabel?: string;
  comment?: string;
  onCommentChange?: (value: string) => void;
  // Whether Sign stays disabled until the comment is filled (default: not required).
  commentRequired?: boolean;
  // Changes that need a second, different person (for example skipping a monitoring
  // occurrence) ask for their username and password as well.
  requireSecondSigner?: boolean;
  secondSignerHint?: string;
  onCancel: () => void;
  onConfirm: (password: string, second?: SecondSignature) => Promise<void>;
}

/**
 * @component SignatureDialog
 * @status Stable
 * @purpose 21 CFR Part 11 signing UI: the signer's own name/role (read-only via `formatRoleName`),
 * the meaning of the signature in plain language, a password field
 * (re-verified server-side - being logged in is not sufficient), and an
 * optional comment. On failure the server's message is shown and the
 * password is cleared, but the dialog stays open.
 * @tokens Respects active light/dark theme natively (`background.paper`, `action.hover`, `divider`).
 * @a11y Focuses password input on open, announces validation errors via `<Alert>`, and supports `Escape` dismissal.
 */
export function SignatureDialog({
  open, meaningStatement, showComment = false, commentLabel = "Comment (optional)", comment, onCommentChange, commentRequired = false,
  requireSecondSigner = false, secondSignerHint, onCancel, onConfirm
}: SignatureDialogProps) {
  const { fullName, username, role, roleName } = useAuth();
  const [password, setPassword] = useState("");
  const [secondUsername, setSecondUsername] = useState("");
  const [secondPassword, setSecondPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const complete = !!password && (!commentRequired || !!comment?.trim()) && (!requireSecondSigner || (!!secondUsername.trim() && !!secondPassword));

  const handleConfirm = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await onConfirm(password, requireSecondSigner ? { username: secondUsername.trim(), password: secondPassword } : undefined);
      setPassword("");
      setSecondPassword("");
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Signature failed.");
      setPassword("");
      setSecondPassword("");
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = () => {
    setPassword("");
    setSecondUsername("");
    setSecondPassword("");
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
          <Button variant="contained" onClick={handleConfirm} disabled={!complete || submitting}>
            {submitting ? "Signing..." : "Sign"}
          </Button>
        </>
      }
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Box>
          <Typography sx={{ fontWeight: 700 }}>{fullName ?? username}</Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{formatRoleName(role, roleName)}</Typography>
        </Box>
        <Alert severity="info">{meaningStatement}</Alert>
        {showComment && (
          <TextField label={commentLabel} multiline rows={2} value={comment ?? ""} onChange={(e) => onCommentChange?.(e.target.value)} />
        )}
        <TextField
          label="Password" type="password" value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoFocus required
          onKeyDown={(e) => { if (e.key === "Enter" && complete && !submitting) handleConfirm(); }}
        />
        {requireSecondSigner && (
          <>
            <Divider />
            <Box>
              <Typography sx={{ fontWeight: 700 }}>Second signature</Typography>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {secondSignerHint ?? "A different person who is allowed to approve this signs as well."}
              </Typography>
            </Box>
            <TextField
              label="Second signer username" value={secondUsername} autoComplete="off" required
              onChange={(e) => setSecondUsername(e.target.value)}
            />
            <TextField
              label="Second signer password" type="password" value={secondPassword} autoComplete="off" required
              onChange={(e) => setSecondPassword(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter" && complete && !submitting) handleConfirm(); }}
            />
          </>
        )}
      </Stack>
    </FloatingDialog>
  );
}
