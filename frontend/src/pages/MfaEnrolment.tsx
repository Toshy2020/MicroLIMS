import { useCallback, useEffect, useState } from "react";
import { Box, Paper, Typography } from "@mui/material";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { toast } from "sonner";
import { getErrorMessage } from "../utils/errorMessage";
import { completeSignInSession } from "../modules/authentication/completeSignIn";
import { mfaService } from "../modules/authentication/services/mfaService";
import type { LoginResult } from "../modules/authentication/services/authenticationService";
import { AuthenticatedEnrolmentFlow, EnrolmentSteps } from "../modules/authentication/components/EnrolmentSteps";

interface MfaEnrolmentLocationState {
  mfaToken?: string;
}

/**
 * Hosts both forced login-time enrolment (by `mfaToken` in router state) and
 * voluntary enrolment for already signed-in users.
 */
export function MfaEnrolmentPage() {
  const { token, login } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const stateToken = (location.state as MfaEnrolmentLocationState | null)?.mfaToken ?? null;
  const [mfaToken, setMfaToken] = useState<string | null>(stateToken);
  const [pendingLogin, setPendingLogin] = useState<LoginResult | null>(null);

  useEffect(() => {
    document.title = "Set up second factor · MicroLIMS";
    return () => {
      setMfaToken(null);
      setPendingLogin(null);
    };
  }, []);

  const startTokenEnrolment = useCallback(() => {
    if (!mfaToken) return Promise.reject(new Error("Missing enrolment challenge."));
    return mfaService.startEnrolmentWithToken(mfaToken);
  }, [mfaToken]);

  const confirmTokenEnrolment = useCallback(
    async (code: string) => {
      if (!mfaToken) throw new Error("Missing enrolment challenge.");
      const res = await mfaService.confirmEnrolmentWithToken(mfaToken, code);
      setPendingLogin(res.login);
      return { recoveryCodes: res.recoveryCodes };
    },
    [mfaToken]
  );

  const handleForcedDone = useCallback(async () => {
    const loginPayload = pendingLogin;
    setPendingLogin(null);
    setMfaToken(null);
    if (!loginPayload) {
      navigate("/login", { replace: true });
      return;
    }
    try {
      await completeSignInSession(loginPayload, login);
      navigate("/dashboard", { replace: true });
    } catch (err: unknown) {
      // The second factor is set up; only the final sign-in step failed.
      toast.error(getErrorMessage(err, "Your second factor is set up, but sign-in could not be completed. Sign in again."));
      navigate("/login", { replace: true });
    }
  }, [login, navigate, pendingLogin]);

  const startAuthenticatedEnrolment = useCallback((password: string) => {
    return mfaService.startEnrolment(password);
  }, []);

  const confirmAuthenticatedEnrolment = useCallback((code: string) => {
    return mfaService.confirmEnrolment(code);
  }, []);

  const handleAuthenticatedDone = useCallback(() => {
    navigate("/profile", { replace: true });
  }, [navigate]);

  if (!token && !mfaToken && !pendingLogin) {
    return <Navigate to="/login" replace />;
  }

  return (
    <Box
      component="main"
      sx={{
        minHeight: "calc(100vh - var(--system-banner-height, 0px))",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        p: { xs: 2, sm: 3 },
        bgcolor: "background.default"
      }}
    >
      <Paper sx={{ width: "100%", maxWidth: 460, p: { xs: 2.5, sm: 3.5 } }}>
        <Typography component="h1" variant="h5" sx={{ mb: 2 }}>
          Set up second factor
        </Typography>
        {token ? (
          <AuthenticatedEnrolmentFlow
            startWithPassword={startAuthenticatedEnrolment}
            confirm={confirmAuthenticatedEnrolment}
            onDone={handleAuthenticatedDone}
          />
        ) : (
          <EnrolmentSteps
            start={startTokenEnrolment}
            confirm={confirmTokenEnrolment}
            onDone={() => void handleForcedDone()}
          />
        )}
      </Paper>
    </Box>
  );
}
