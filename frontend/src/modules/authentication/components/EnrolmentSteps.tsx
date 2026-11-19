import { useEffect, useRef, useState, type FormEvent } from "react";
import { Alert, Box, Button, Stack, TextField, Typography } from "@mui/material";
import { i18n, useTranslation } from "../../../i18n";
import { monospaceFontFamily } from "../../../theme/palette";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import type { EnrolmentStart } from "../services/mfaService";
import { MfaCodeField } from "./MfaCodeField";
import { RecoveryCodesPanel } from "./RecoveryCodesPanel";

export interface EnrolmentStepsProps {
  start: () => Promise<EnrolmentStart>;
  confirm: (code: string) => Promise<{ recoveryCodes: string[] }>;
  onDone: () => void;
}

function formatManualKey(key: string): string {
  const cleaned = key.replace(/\s+/g, "");
  return cleaned.match(/.{1,4}/g)?.join(" ") ?? cleaned;
}

/**
 * Shared authenticator enrolment steps: fetch QR/manual key -> verify 6-digit code -> acknowledge recovery codes.
 */
export function EnrolmentSteps({ start, confirm, onDone }: EnrolmentStepsProps) {
  const { t } = useTranslation("account");
  const [enrolmentStart, setEnrolmentStart] = useState<EnrolmentStart | null>(null);
  const [loadingStart, setLoadingStart] = useState(true);
  const [startError, setStartError] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  const [code, setCode] = useState("");
  const [codeError, setCodeError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [copiedKey, setCopiedKey] = useState(false);
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);

  const codeInputRef = useRef<HTMLInputElement>(null);
  const refocusCodeRef = useRef(false);

  useEffect(() => {
    let active = true;
    setLoadingStart(true);
    setStartError(null);
    start()
      .then((data) => {
        if (!active) return;
        setEnrolmentStart(data);
        setLoadingStart(false);
      })
      .catch((err: unknown) => {
        if (!active) return;
        setStartError(serverMessage(err) ?? getErrorMessage(err, i18n.t("enrolment.startFailed", { ns: "account" })));
        setLoadingStart(false);
      });

    return () => {
      active = false;
      setEnrolmentStart(null);
      setRecoveryCodes(null);
    };
  }, [start, reloadNonce]);

  useEffect(() => {
    if (!submitting && refocusCodeRef.current) {
      refocusCodeRef.current = false;
      codeInputRef.current?.focus();
    }
  }, [submitting, codeError]);

  const handleCopyKey = async () => {
    if (!enrolmentStart) return;
    try {
      await navigator.clipboard?.writeText(enrolmentStart.manualKey.replace(/\s+/g, ""));
      setCopiedKey(true);
    } catch {
      // Clipboard API may be unavailable in restricted contexts.
    }
  };

  const handleConfirm = async (e: FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    setSubmitting(true);
    setCodeError(null);
    try {
      const result = await confirm(code);
      setEnrolmentStart(null);
      setCode("");
      setRecoveryCodes(result.recoveryCodes);
    } catch (err: unknown) {
      refocusCodeRef.current = true;
      setCodeError(serverMessage(err) ?? getErrorMessage(err, t("enrolment.codeIncorrect")));
    } finally {
      setSubmitting(false);
    }
  };

  if (recoveryCodes) {
    return (
      <RecoveryCodesPanel
        codes={recoveryCodes}
        onDone={() => {
          setRecoveryCodes(null);
          onDone();
        }}
      />
    );
  }

  if (loadingStart) {
    return (
      <Typography variant="body2" color="text.secondary">
        {t("enrolment.preparing")}
      </Typography>
    );
  }

  if (startError || !enrolmentStart) {
    return (
      <Stack spacing={2}>
        <Alert severity="error" role="alert">
          {startError ?? t("enrolment.startFailed")}
        </Alert>
        <Button type="button" variant="outlined" onClick={() => setReloadNonce((n) => n + 1)}>
          {t("enrolment.tryAgain")}
        </Button>
      </Stack>
    );
  }

  const qrSrc = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(enrolmentStart.qrSvg)}`;

  return (
    <Box component="form" onSubmit={handleConfirm} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
      <Typography variant="body2" color="text.secondary">
        {t("enrolment.scanInstructions")}
      </Typography>

      <Box sx={{ display: "flex", justifyContent: "center", p: 1.5, borderRadius: 1, border: 1, borderColor: "divider", bgcolor: "background.default" }}>
        <Box
          component="img"
          src={qrSrc}
          alt={t("enrolment.qrAlt")}
          sx={{ width: 180, height: 180, display: "block" }}
        />
      </Box>

      <Box sx={{ p: 1.5, borderRadius: 1, border: 1, borderColor: "divider", bgcolor: "background.default" }}>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
          {t("enrolment.setupKey")}
        </Typography>
        <Stack direction="row" spacing={1} sx={{ mt: 0.5, alignItems: "center", justifyContent: "space-between" }}>
          <Typography
            sx={{
              fontFamily: monospaceFontFamily,
              fontSize: 14,
              fontWeight: 600,
              letterSpacing: 1,
              userSelect: "all",
              wordBreak: "break-word"
            }}
          >
            {formatManualKey(enrolmentStart.manualKey)}
          </Typography>
          <Button type="button" variant="outlined" size="small" onClick={handleCopyKey}>
            {copiedKey ? t("enrolment.copied") : t("enrolment.copyKey")}
          </Button>
        </Stack>
      </Box>

      {codeError && (
        <Alert severity="error" role="alert" icon={false}>
          {codeError}
        </Alert>
      )}

      <MfaCodeField
        value={code}
        onChange={setCode}
        disabled={submitting}
        inputRef={codeInputRef}
        autoFocus
      />

      <Button type="submit" variant="contained" size="large" disabled={submitting}>
        {submitting ? t("enrolment.confirming") : t("enrolment.confirm")}
      </Button>
    </Box>
  );
}

export interface AuthenticatedEnrolmentFlowProps {
  confirm: (code: string) => Promise<{ recoveryCodes: string[] }>;
  startWithPassword: (password: string) => Promise<EnrolmentStart>;
  onDone: () => void;
}

/**
 * Wraps `EnrolmentSteps` with the initial password check required for signed-in users.
 */
export function AuthenticatedEnrolmentFlow({
  confirm,
  startWithPassword,
  onDone
}: AuthenticatedEnrolmentFlowProps) {
  const { t } = useTranslation("account");
  const [password, setPassword] = useState("");
  const [submittedPassword, setSubmittedPassword] = useState<string | null>(null);
  const [initialStart, setInitialStart] = useState<(() => Promise<EnrolmentStart>) | null>(null);

  useEffect(() => {
    return () => {
      setPassword("");
      setSubmittedPassword(null);
      setInitialStart(null);
    };
  }, []);

  if (!submittedPassword || !initialStart) {
    return (
      <Box
        component="form"
        onSubmit={(e) => {
          e.preventDefault();
          const captured = password;
          setPassword("");
          setSubmittedPassword(captured);
          setInitialStart(() => () => startWithPassword(captured));
        }}
        sx={{ display: "flex", flexDirection: "column", gap: 2 }}
      >
        <Typography variant="body2" color="text.secondary">
          {t("enrolment.enterPassword")}
        </Typography>
        <TextField
          label={t("enrolment.password")}
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoComplete="current-password"
          autoFocus
          fullWidth
          slotProps={{
            inputLabel: { required: false },
            htmlInput: { required: true }
          }}
        />
        <Button type="submit" variant="contained" size="large" disabled={!password}>
          {t("enrolment.continue")}
        </Button>
      </Box>
    );
  }

  return <EnrolmentSteps start={initialStart} confirm={confirm} onDone={onDone} />;
}
