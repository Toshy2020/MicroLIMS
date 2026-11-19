import { useEffect, useRef, useState } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { Alert, Box, CircularProgress, Link, Stack, Typography } from "@mui/material";
import { useAuth } from "../contexts/AuthContext";
import { completeSignInSession } from "../modules/authentication/completeSignIn";
import { ssoService } from "../modules/authentication/services/ssoService";
import { useTranslation } from "../i18n";

export function SsoCompletePage() {
  const { t } = useTranslation("account");
  const { login } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const startedRef = useRef(false);
  const mountedRef = useRef(true);

  useEffect(() => {
    document.title = t("ssoComplete.documentTitle");
  }, [t]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    if (startedRef.current) return;
    startedRef.current = true;

    const rawHash = window.location.hash;
    if (rawHash) {
      window.history.replaceState(null, "", `${window.location.pathname}${window.location.search}`);
    }

    const params = new URLSearchParams(rawHash.startsWith("#") ? rawHash.slice(1) : rawHash);
    const errorParam = params.get("error");
    const code = params.get("code");

    if (errorParam || !code) {
      setError(t("ssoComplete.genericError"));
      return;
    }

    ssoService
      .exchange(code)
      .then(async (result) => {
        await completeSignInSession(result, login);
        if (!mountedRef.current) return;
        navigate(result.signingPasswordRequired ? "/set-signing-password" : "/", { replace: true });
      })
      .catch(() => {
        if (mountedRef.current) {
          setError(t("ssoComplete.genericError"));
        }
      });
  }, [login, navigate, t]);

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        px: 2.5,
        bgcolor: "background.default"
      }}
    >
      <Box sx={{ width: "100%", maxWidth: 420 }}>
        {error ? (
          <Stack spacing={2}>
            <Typography component="h1" sx={{ fontSize: 22, fontWeight: 700 }}>
              {t("ssoComplete.title")}
            </Typography>
            <Alert severity="error" role="alert">
              {error}
            </Alert>
            <Box>
              <Link component={RouterLink} to="/login" underline="hover" sx={{ fontSize: 14 }}>
                {t("ssoComplete.backToSignIn")}
              </Link>
            </Box>
          </Stack>
        ) : (
          <Stack spacing={2} sx={{ alignItems: "center" }} role="status" aria-live="polite">
            <CircularProgress size={28} />
            <Typography sx={{ fontSize: 14, color: "text.secondary" }}>
              {t("ssoComplete.completing")}
            </Typography>
          </Stack>
        )}
      </Box>
    </Box>
  );
}
