import { useCallback, useEffect, useRef, useState } from "react";
import {
  Box, TextField, Button, Typography, Alert, Link, Stack, useTheme,
  IconButton, InputAdornment, CircularProgress, Checkbox, FormControlLabel
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import { useNavigate, Link as RouterLink } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { completeSignInSession } from "../modules/authentication/completeSignIn";
import { authenticationService, type LoginResult } from "../modules/authentication/services/authenticationService";
import { mfaService } from "../modules/authentication/services/mfaService";
import { MfaCodeField } from "../modules/authentication/components/MfaCodeField";
import { RecoveryCodesPanel } from "../modules/authentication/components/RecoveryCodesPanel";
import { monospaceFontFamily } from "../theme/palette";
import { LoginBrandArt } from "./LoginBrandArt";

const DEVICE_TOKEN_KEY = "microlims_device_token";
const MFA_CHALLENGE_TTL_MS = 5 * 60 * 1000;
const MAX_CHALLENGE_ATTEMPTS = 5;

type LoginStep = "password" | "mfa" | "recoveryCodes" | "recoveryWarning";

interface ApiErrorShape {
  response?: {
    status?: number;
    data?: { message?: string };
  };
}

function getApiErrorInfo(err: unknown): { status?: number; message?: string; hasResponse: boolean } {
  const typed = err as ApiErrorShape | null | undefined;
  return {
    status: typed?.response?.status,
    message: typed?.response?.data?.message,
    hasResponse: Boolean(typed?.response)
  };
}

function readStoredDeviceToken(): string | null {
  try {
    return localStorage.getItem(DEVICE_TOKEN_KEY);
  } catch {
    return null;
  }
}

function writeStoredDeviceToken(token: string): void {
  try {
    localStorage.setItem(DEVICE_TOKEN_KEY, token);
  } catch {
    // Storage may be unavailable in restricted contexts; sign-in still proceeds.
  }
}

function clearStoredDeviceToken(): void {
  try {
    localStorage.removeItem(DEVICE_TOKEN_KEY);
  } catch {
    // Ignore storage access errors.
  }
}

const visuallyHiddenSx = {
  position: "absolute",
  width: 1,
  height: 1,
  overflow: "hidden",
  clipPath: "inset(50%)",
  whiteSpace: "nowrap"
} as const;

export interface LoginPageProps {
  /** Clock function used for client-side MFA challenge expiry tracking; defaults to `Date.now`. */
  now?: () => number;
}

export function LoginPage({ now = Date.now }: LoginPageProps) {
  const theme = useTheme();
  const { login } = useAuth();
  const navigate = useNavigate();

  const [step, setStep] = useState<LoginStep>("password");
  const [stepAnnouncement, setStepAnnouncement] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [forgotMode, setForgotMode] = useState(false);
  const [resetUsername, setResetUsername] = useState("");
  const [resetMessage, setResetMessage] = useState<string | null>(null);
  const [resetSubmitting, setResetSubmitting] = useState(false);

  // Kept only in component state — never written to storage or query strings.
  const [mfaToken, setMfaToken] = useState<string | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [recoveryCode, setRecoveryCode] = useState("");
  const [useRecoveryCode, setUseRecoveryCode] = useState(false);
  const [trustDevice, setTrustDevice] = useState(false);
  const [trustedDeviceHours, setTrustedDeviceHours] = useState(0);
  const [mfaError, setMfaError] = useState<string | null>(null);
  const [failedAttempts, setFailedAttempts] = useState(0);
  const [challengeStartedAt, setChallengeStartedAt] = useState<number | null>(null);

  const [pendingLoginResult, setPendingLoginResult] = useState<LoginResult | null>(null);

  const codeInputRef = useRef<HTMLInputElement>(null);
  const recoveryInputRef = useRef<HTMLInputElement>(null);
  const refocusMfaInputRef = useRef(false);

  // The tab otherwise keeps the title of the page the user signed out from.
  useEffect(() => {
    document.title = "Sign in · MicroLIMS";
  }, []);

  useEffect(() => {
    if (step !== "mfa") return;
    let active = true;
    mfaService
      .getOptions()
      .then((opts) => {
        if (active) {
          setTrustedDeviceHours(opts.trustedDeviceHours > 0 ? opts.trustedDeviceHours : 0);
        }
      })
      .catch(() => {
        if (active) setTrustedDeviceHours(0);
      });
    return () => {
      active = false;
    };
  }, [step]);

  const clearMfaState = useCallback(() => {
    setMfaToken(null);
    setMfaCode("");
    setRecoveryCode("");
    setUseRecoveryCode(false);
    setTrustDevice(false);
    setTrustedDeviceHours(0);
    setMfaError(null);
    setFailedAttempts(0);
    setChallengeStartedAt(null);
    setPendingLoginResult(null);
  }, []);

  const resetToPasswordStep = useCallback(
    (message: string | null) => {
      clearMfaState();
      setPassword("");
      setError(message);
      setStep("password");
      setStepAnnouncement(message ?? "Returned to sign in.");
    },
    [clearMfaState]
  );

  useEffect(() => {
    if (step !== "mfa" || challengeStartedAt === null) return;
    const remainingMs = Math.max(0, MFA_CHALLENGE_TTL_MS - (now() - challengeStartedAt));
    const timerId = window.setTimeout(() => {
      resetToPasswordStep("Your sign-in took too long. Enter your password again.");
    }, remainingMs);
    return () => {
      window.clearTimeout(timerId);
    };
  }, [step, challengeStartedAt, now, resetToPasswordStep]);

  useEffect(() => {
    if (step === "mfa" && !isSubmitting && refocusMfaInputRef.current) {
      refocusMfaInputRef.current = false;
      (useRecoveryCode ? recoveryInputRef.current : codeInputRef.current)?.focus();
    }
  }, [step, isSubmitting, useRecoveryCode, mfaError]);

  const completeSignIn = async (result: LoginResult) => {
    await completeSignInSession(result, login, username);
    navigate("/dashboard");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError(null);
    const storedDeviceToken = readStoredDeviceToken();
    try {
      const result = storedDeviceToken
        ? await authenticationService.login(username, password, storedDeviceToken)
        : await authenticationService.login(username, password);

      if (result.mfaEnrolmentRequired) {
        if (storedDeviceToken) clearStoredDeviceToken();
        clearMfaState();
        navigate("/mfa-enrol", { state: { mfaToken: result.mfaToken } });
        return;
      }

      if (result.mfaRequired) {
        if (storedDeviceToken) clearStoredDeviceToken();
        clearMfaState();
        setMfaToken(result.mfaToken ?? null);
        setChallengeStartedAt(now());
        setStep("mfa");
        setStepAnnouncement("Two-factor authentication required. Enter the 6-digit code.");
        return;
      }

      await completeSignIn(result);
    } catch (err: unknown) {
      const { status, message, hasResponse } = getApiErrorInfo(err);
      if (status === 401) {
        setError("Invalid username or password.");
      } else if (status !== undefined && status >= 500) {
        setError("Laboratory authentication service error. Please try again later.");
      } else if (!hasResponse) {
        setError("Unable to reach the laboratory server. Check network connection.");
      } else {
        setError(message || "Login failed. Please check credentials.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleMfaSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting || !mfaToken) return;

    if (challengeStartedAt !== null && now() - challengeStartedAt >= MFA_CHALLENGE_TTL_MS) {
      resetToPasswordStep("Your sign-in took too long. Enter your password again.");
      return;
    }

    setIsSubmitting(true);
    setMfaError(null);
    try {
      const payload = useRecoveryCode
        ? { mfaToken, recoveryCode: recoveryCode.trim(), trustDevice }
        : { mfaToken, code: mfaCode, trustDevice };

      const result = await mfaService.verify(payload);
      if (trustDevice && result.deviceToken) {
        writeStoredDeviceToken(result.deviceToken);
      }

      setMfaToken(null);

      if (result.recoveryCodes && result.recoveryCodes.length > 0) {
        setPendingLoginResult(result);
        setStep("recoveryCodes");
        setStepAnnouncement("Hardware token confirmed. Save your recovery codes before continuing.");
        return;
      }

      if (result.recoveryCodesRemaining != null) {
        setPendingLoginResult(result);
        setStep("recoveryWarning");
        setStepAnnouncement(
          `Recovery code accepted. ${result.recoveryCodesRemaining} recovery codes remaining.`
        );
        return;
      }

      await completeSignIn(result);
    } catch (err: unknown) {
      const { status, message, hasResponse } = getApiErrorInfo(err);
      if (status === 401) {
        const nextAttempts = failedAttempts + 1;
        setFailedAttempts(nextAttempts);
        if (nextAttempts >= MAX_CHALLENGE_ATTEMPTS) {
          resetToPasswordStep("Too many incorrect codes. Sign in again.");
          return;
        }
        refocusMfaInputRef.current = true;
        setMfaError(message || "The code is not correct.");
      } else if (status !== undefined && status >= 500) {
        refocusMfaInputRef.current = true;
        setMfaError("Laboratory authentication service error. Please try again later.");
      } else if (!hasResponse) {
        refocusMfaInputRef.current = true;
        setMfaError("Unable to reach the laboratory server. Check network connection.");
      } else {
        refocusMfaInputRef.current = true;
        setMfaError(message || "The code is not correct.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handlePendingContinue = async () => {
    if (!pendingLoginResult || isSubmitting) return;
    setIsSubmitting(true);
    try {
      await completeSignIn(pendingLoginResult);
    } catch {
      resetToPasswordStep("Unable to complete sign-in. Sign in again.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleForgotSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setResetSubmitting(true);
    try {
      await authenticationService.requestPasswordReset(resetUsername);
    } catch {
      // Swallow
    } finally {
      setResetSubmitting(false);
      setResetMessage("If that account exists, a reset link has been generated.");
    }
  };

  const chrome = theme.custom.chrome;
  const trustLabel = `Trust this computer for ${trustedDeviceHours} ${trustedDeviceHours === 1 ? "hour" : "hours"}`;

  return (
    // Split screen: a brand panel (40%) saying what the system is, and the
    // sign-in form (60%). On a phone the panel shrinks to a short band above
    // the form and drops its drawings.
    <Box
      component="main"
      sx={{
        minHeight: "calc(100vh - var(--system-banner-height, 0px))",
        display: "grid",
        gridTemplateColumns: { xs: "1fr", md: "40% 60%" },
        gridTemplateRows: { xs: "auto 1fr", md: "1fr" },
        bgcolor: "background.paper"
      }}
    >
      <Box
        component="section"
        aria-label="About MicroLIMS"
        sx={{
          position: "relative",
          overflow: "hidden",
          background: chrome.brandBandBg,
          color: "common.white",
          display: "flex",
          flexDirection: "column",
          px: { xs: 2.5, md: 7 },
          pt: { xs: 2.5, md: 6 },
          pb: { xs: 2.25, md: 0 }
        }}
      >
        <Box
          aria-hidden="true"
          sx={{
            position: "absolute", width: 520, height: 520, right: -220, top: -160, borderRadius: "50%",
            background: `radial-gradient(circle, ${chrome.brandBandGlow} 0%, transparent 70%)`
          }}
        />
        <Typography sx={{ position: "relative", fontSize: { xs: 26, md: 30 }, fontWeight: 700, letterSpacing: 0.5, lineHeight: 1.2 }}>
          Micro<Box component="span" sx={{ fontWeight: 300, color: chrome.brandBandAccent }}>LIMS</Box>
        </Typography>
        <Box sx={{ position: "relative", my: { xs: 0, md: "auto" }, mt: { xs: 1.5 }, pt: { md: 4 } }}>
          <Typography component="h2" sx={{ fontSize: { xs: 18, md: 30 }, fontWeight: 700, lineHeight: 1.2, maxWidth: 420 }}>
            Pharmaceutical Quality Control
          </Typography>
          <Typography sx={{ display: { xs: "none", md: "block" }, mt: 1, fontSize: 16, color: chrome.brandBandMuted }}>
            Laboratory Information Management System
          </Typography>
          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1, mt: { xs: 1.5, md: 2.75 } }}>
            {["FDA 21 CFR Part 11", "EU GMP Annex 11"].map((label) => (
              <Box
                key={label}
                component="span"
                sx={{
                  fontSize: 12, fontWeight: 600, px: 1.25, py: 0.5, borderRadius: 5,
                  bgcolor: chrome.brandBandChipBg, border: `1px solid ${chrome.brandBandChipBorder}`
                }}
              >
                {label}
              </Box>
            ))}
          </Box>
        </Box>
        {/* Room for the drawings, so the headline can never sit on them. The
            art keeps the panel's aspect: 290/546 of the panel's 40vw width. */}
        <Box sx={{ display: { xs: "none", md: "block" }, flexShrink: 0, height: "21.3vw", position: "relative" }}>
          <Typography sx={{ position: "absolute", left: 0, bottom: 32, fontSize: 12, color: chrome.brandBandMuted }}>
            v0.1
          </Typography>
        </Box>
        <LoginBrandArt
          sx={{
            display: { xs: "none", md: "block" },
            position: "absolute", left: 0, right: 0, bottom: 0, width: "100%", height: "auto",
            color: chrome.brandBandArt, pointerEvents: "none"
          }}
        />
      </Box>

      <Box sx={{ display: "flex", alignItems: { xs: "flex-start", md: "center" }, justifyContent: "center", px: { xs: 2.5, md: 3 }, py: { xs: 3.5, md: 5 } }}>
        <Box sx={{ width: "100%", maxWidth: 400 }}>
          <Box role="status" aria-live="polite" sx={visuallyHiddenSx}>
            {stepAnnouncement}
          </Box>

          {forgotMode ? (
            <Box component="form" onSubmit={handleForgotSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
              <Box>
                <Typography component="h1" sx={{ fontSize: 24, fontWeight: 700 }}>Need help signing in?</Typography>
                <Typography sx={{ mt: 0.5, fontSize: 14, color: "text.secondary" }}>
                  Enter your username and we'll generate a password reset request.
                </Typography>
              </Box>
              {resetMessage ? (
                <Alert severity="info">{resetMessage}</Alert>
              ) : (
                <>
                  <TextField label="Username" value={resetUsername} onChange={(e) => setResetUsername(e.target.value)} autoFocus required />
                  <Button type="submit" variant="contained" size="large" disabled={resetSubmitting}>
                    {resetSubmitting ? "Submitting..." : "Send Reset Link"}
                  </Button>
                </>
              )}
              <Stack spacing={1} sx={{ textAlign: "center" }}>
                <Link
                  component="button"
                  type="button"
                  underline="hover"
                  sx={{ fontSize: 14 }}
                  onClick={() => { setForgotMode(false); setResetMessage(null); setResetUsername(""); }}
                >
                  Back to sign in
                </Link>
                <Link component={RouterLink} to="/admin-recovery" underline="hover" sx={{ fontSize: 14 }}>
                  Have a recovery code from your administrator?
                </Link>
              </Stack>
            </Box>
          ) : step === "mfa" ? (
            <Box component="form" onSubmit={handleMfaSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
              <Box sx={{ mb: 1 }}>
                <Typography component="h1" sx={{ fontSize: 24, fontWeight: 700 }}>
                  {useRecoveryCode ? "Enter a recovery code" : "Enter the 6-digit code"}
                </Typography>
                <Typography sx={{ mt: 0.5, fontSize: 14, color: "text.secondary" }}>
                  {useRecoveryCode
                    ? "Enter one of your single-use recovery codes."
                    : "Enter the code from your authenticator app or hardware token."}
                </Typography>
              </Box>
              {mfaError && <Alert severity="error" role="alert">{mfaError}</Alert>}
              {useRecoveryCode ? (
                <TextField
                  label="Recovery code"
                  value={recoveryCode}
                  onChange={(e) => setRecoveryCode(e.target.value)}
                  disabled={isSubmitting}
                  inputRef={recoveryInputRef}
                  autoFocus
                  fullWidth
                  slotProps={{
                    inputLabel: { required: false },
                    htmlInput: { required: true, autoComplete: "off" },
                    input: { sx: { fontFamily: monospaceFontFamily } }
                  }}
                />
              ) : (
                <MfaCodeField
                  value={mfaCode}
                  onChange={setMfaCode}
                  disabled={isSubmitting}
                  inputRef={codeInputRef}
                  autoFocus
                />
              )}
              {trustedDeviceHours > 0 && (
                <FormControlLabel
                  control={
                    <Checkbox
                      checked={trustDevice}
                      onChange={(e) => setTrustDevice(e.target.checked)}
                      disabled={isSubmitting}
                    />
                  }
                  label={trustLabel}
                />
              )}
              <Button
                type="submit"
                variant="contained"
                size="large"
                disabled={isSubmitting}
                startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : undefined}
                sx={{ mt: 0.5 }}
              >
                {isSubmitting ? "Verifying…" : "Verify"}
              </Button>
              <Stack spacing={1} sx={{ textAlign: "center" }}>
                <Link
                  component="button"
                  type="button"
                  underline="hover"
                  sx={{ fontSize: 14 }}
                  onClick={() => {
                    setUseRecoveryCode((prev) => !prev);
                    setMfaError(null);
                    setMfaCode("");
                    setRecoveryCode("");
                    refocusMfaInputRef.current = true;
                  }}
                >
                  {useRecoveryCode ? "Use an authenticator code instead" : "Use a recovery code instead"}
                </Link>
                <Link
                  component="button"
                  type="button"
                  underline="hover"
                  sx={{ fontSize: 14 }}
                  onClick={() => resetToPasswordStep(null)}
                >
                  Use a different account
                </Link>
              </Stack>
            </Box>
          ) : step === "recoveryCodes" && pendingLoginResult?.recoveryCodes ? (
            <RecoveryCodesPanel
              codes={pendingLoginResult.recoveryCodes}
              onDone={() => void handlePendingContinue()}
              submitting={isSubmitting}
              introText="These codes are shown once. Store them safely in case you lose your token."
            />
          ) : step === "recoveryWarning" && pendingLoginResult ? (
            <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
              <Typography component="h1" sx={{ fontSize: 24, fontWeight: 700 }}>
                Recovery code used
              </Typography>
              <Alert severity="warning" role="alert">
                {pendingLoginResult.recoveryCodesRemaining === 0
                  ? "You have 0 recovery codes remaining. Generate new recovery codes in Profile → Security."
                  : `You have ${pendingLoginResult.recoveryCodesRemaining} ${
                      pendingLoginResult.recoveryCodesRemaining === 1 ? "recovery code" : "recovery codes"
                    } remaining. You can generate a new set in Profile → Security.`}
              </Alert>
              <Button
                type="button"
                variant="contained"
                size="large"
                disabled={isSubmitting}
                onClick={handlePendingContinue}
              >
                {isSubmitting ? "Signing in…" : "Continue"}
              </Button>
            </Box>
          ) : (
            <Box component="form" onSubmit={handleSubmit} sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
              <Box sx={{ mb: 1 }}>
                <Typography component="h1" sx={{ fontSize: 24, fontWeight: 700 }}>Sign in</Typography>
                <Typography sx={{ mt: 0.5, fontSize: 14, color: "text.secondary" }}>Use your laboratory account.</Typography>
              </Box>
              {error && <Alert severity="error" role="alert">{error}</Alert>}
              <TextField
                label="Username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoComplete="username"
                autoFocus
                required
              />
              <TextField
                label="Password"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
                slotProps={{
                  input: {
                    endAdornment: (
                      <InputAdornment position="end">
                        <IconButton
                          aria-label={showPassword ? "Hide password" : "Show password"}
                          onClick={() => setShowPassword((prev) => !prev)}
                          edge="end"
                          size="small"
                        >
                          {showPassword ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                        </IconButton>
                      </InputAdornment>
                    )
                  }
                }}
              />
              <Button
                type="submit"
                variant="contained"
                size="large"
                disabled={isSubmitting}
                startIcon={isSubmitting ? <CircularProgress size={18} color="inherit" /> : undefined}
                sx={{ mt: 0.5 }}
              >
                {isSubmitting ? "Signing in…" : "Sign In"}
              </Button>
              {/* One way into recovery: the help screen offers both a reset
                  request and the administrator recovery-code route. */}
              <Box sx={{ textAlign: "center" }}>
                <Link component="button" type="button" underline="hover" sx={{ fontSize: 14 }} onClick={() => setForgotMode(true)}>
                  Need help signing in?
                </Link>
              </Box>
            </Box>
          )}
          {/* Every attempt, successful or not, is written to LoginHistory
              (AuthenticationService.RecordLoginAsync). */}
          <Typography
            sx={{ mt: 4.5, pt: 2, borderTop: 1, borderColor: "divider", textAlign: "center", fontSize: 12, color: "text.secondary" }}
          >
            Authorised users only. Every sign-in attempt is recorded.
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}
