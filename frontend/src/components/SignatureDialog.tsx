import { useState } from "react";
import { Box, Typography, TextField, Button, Alert, Stack, Divider } from "@mui/material";
import { FloatingDialog } from "./FloatingDialog";
import { useAuth } from "../contexts/AuthContext";
import { formatRoleName } from "../utils/userDisplay";
import { serverMessage } from "../utils/errorMessage";
import { useTranslation } from "../i18n";

// A second person's credentials, for changes two people must sign.
export interface SecondSignature {
  username: string;
  password: string;
}

interface SignatureDialogProps {
  open: boolean;
  meaningStatement: string; // plain-language statement, e.g. "I am approving this result."
  // The same statement in Arabic, shown below the English one when the interface is Arabic.
  // Only the English statement is recorded and printed; the translation helps the signer read it.
  meaningStatementTranslation?: string;
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
  open, meaningStatement, meaningStatementTranslation, showComment = false, commentLabel, comment, onCommentChange, commentRequired = false,
  requireSecondSigner = false, secondSignerHint, onCancel, onConfirm
}: SignatureDialogProps) {
  const { t, i18n } = useTranslation();
  const isArabic = i18n.language === "ar";
  const { fullName, username, role, roleName, signInMethod } = useAuth();
  const isDirectoryUser = signInMethod === "Directory";
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
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("signature.failed"));
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
      title={t("signature.title")}
      onClose={handleCancel}
      actions={
        <>
          <Button onClick={handleCancel}>{t("actions.cancel")}</Button>
          <Button variant="contained" onClick={handleConfirm} disabled={!complete || submitting}>
            {submitting ? t("signature.signing") : t("actions.sign")}
          </Button>
        </>
      }
    >
      <Stack spacing={2}>
        {error && <Alert severity="error">{error}</Alert>}
        <Box>
          <Typography sx={{ fontWeight: 700 }}>{fullName ?? username}</Typography>
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{formatRoleName(role, roleName, t)}</Typography>
        </Box>
        {/* The recorded statement is English: it stays first and left-to-right in both languages. */}
        <Alert severity="info">
          {isArabic ? <Box component="span" lang="en" dir="ltr" sx={{ display: "block" }}>{meaningStatement}</Box> : meaningStatement}
        </Alert>
        {isArabic && meaningStatementTranslation && (
          <Box component="section" aria-label={t("signature.arabicTranslation")} sx={{ px: 2, py: 1, border: 1, borderColor: "divider", borderRadius: 1 }}>
            <Typography variant="body2">{meaningStatementTranslation}</Typography>
          </Box>
        )}
        {isArabic && (
          <Typography variant="caption" role="note" sx={{ color: "text.secondary" }}>
            {t("signature.englishRecorded")}
          </Typography>
        )}
        {showComment && (
          <TextField label={commentLabel ?? t("signature.commentOptional")} multiline rows={2} value={comment ?? ""} onChange={(e) => onCommentChange?.(e.target.value)} />
        )}
        <TextField
          label={isDirectoryUser ? t("signature.signingPassword") : t("signature.password")}
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          helperText={
            isDirectoryUser ? t("signature.signingPasswordHelp") : undefined
          }
          autoFocus
          required
          onKeyDown={(e) => { if (e.key === "Enter" && complete && !submitting) handleConfirm(); }}
        />
        {requireSecondSigner && (
          <>
            <Divider />
            <Box>
              <Typography sx={{ fontWeight: 700 }}>{t("signature.second")}</Typography>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                {secondSignerHint ?? t("signature.secondHint")}
              </Typography>
            </Box>
            <TextField
              label={t("signature.secondUsername")} value={secondUsername} autoComplete="off" required
              onChange={(e) => setSecondUsername(e.target.value)}
            />
            <TextField
              label={t("signature.secondPassword")} type="password" value={secondPassword} autoComplete="off" required
              onChange={(e) => setSecondPassword(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter" && complete && !submitting) handleConfirm(); }}
            />
          </>
        )}
      </Stack>
    </FloatingDialog>
  );
}
