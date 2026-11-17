import { useEffect, useMemo, useRef, useState } from "react";
import { Outlet, useNavigate, useLocation, Navigate, Link as RouterLink } from "react-router-dom";
import { Box, Dialog, DialogTitle, DialogContent, DialogContentText, DialogActions, Button, Typography, useMediaQuery, useTheme } from "@mui/material";
import WarningAmberRoundedIcon from "@mui/icons-material/WarningAmberRounded";
import { Header } from "../components/Header";
import { Sidebar } from "../components/Sidebar";
import { AppBreadcrumbs } from "../components/AppBreadcrumbs";
import { BreadcrumbProvider } from "../components/breadcrumbContext";
import { AppErrorBoundary } from "../components/AppErrorBoundary";
import { ConnectionBanner } from "../components/bench/ConnectionBanner";
import { DraftsMenu } from "../components/bench/DraftsMenu";
import { useAuth } from "../contexts/AuthContext";
import { useBenchMode } from "../theme/BenchModeContext";
import { useIdleTimeout } from "../hooks/useIdleTimeout";
import { useMyLabs } from "../hooks/useMyLabs";
import { useDraftOverview } from "../hooks/useDraftOverview";
import { useConnectionStatus } from "../hooks/useConnectionStatus";
import { useMenuGroups } from "../hooks/useMenuGroups";
import { mfaService, type MfaStatus } from "../modules/authentication/services/mfaService";
import { formatDateTimeWithTimeZone } from "../utils/formatDate";
import { HelpPanelProvider, useHelpPanel } from "../modules/help/HelpPanelContext";
import { MenuGroupsProvider } from "../modules/navigation/MenuGroupsContext";
import { HelpPanelSlot } from "../modules/help/HelpPanelSlot";
import { Tour } from "../modules/help/tour/Tour";
import { TOUR_STEPS, tourStateKey, type RoleType } from "../modules/help/tour/tourSteps";
import { WhatsNewDialog } from "../modules/help/whatsNew/WhatsNewDialog";
import {
  isNewerVersion,
  itemsForRole,
  RELEASE_NOTES,
  WHATS_NEW_STATE_KEY,
  type ReleaseNote
} from "../modules/help/whatsNew/releaseNotes";
import { useUiState } from "../hooks/useUiState";
import { toast } from "sonner";
import { accountPageTrail, findNavTrail } from "../routes/navigation";
import { useTranslation } from "../i18n";

const MAIN_CONTENT_ID = "main-content";

// "30 seconds" / "ثانية واحدة": the unit and its plural come from the language, with Latin digits.
function formatSeconds(seconds: number, language: string): string {
  const tag = language === "ar" ? "ar-u-nu-latn" : "en-GB";
  return new Intl.NumberFormat(tag, { style: "unit", unit: "second", unitDisplay: "long" }).format(seconds);
}

const CHANGE_PASSWORD_PATH = "/change-password";
const SET_SIGNING_PASSWORD_PATH = "/set-signing-password";
const MFA_ENROL_PATH = "/mfa-enrol";

function isRoleType(value: string | null): value is RoleType {
  return value !== null && Object.prototype.hasOwnProperty.call(TOUR_STEPS, value);
}

function isForcedEnrolmentPending(
  mfaStatus: MfaStatus | null,
  signingPasswordRequired: boolean,
  pathname: string
): boolean {
  const forcedMfaPending = Boolean(mfaStatus?.required && !mfaStatus.enrolled && !mfaStatus.enrolmentDueAt);
  return (
    signingPasswordRequired ||
    forcedMfaPending ||
    pathname === CHANGE_PASSWORD_PATH ||
    pathname === SET_SIGNING_PASSWORD_PATH ||
    pathname === MFA_ENROL_PATH
  );
}

function FirstLoginTour({ mfaStatus }: { mfaStatus: MfaStatus | null }) {
  const { t } = useTranslation();
  const { role, mustChangePassword, signingPasswordRequired } = useAuth();
  const { tourReplaySeq } = useHelpPanel();
  const location = useLocation();
  const validRole = isRoleType(role) ? role : null;
  const tourKey = validRole ? tourStateKey(validRole) : "tour.none.v1";
  const tourState = useUiState(tourKey);

  const [dismissedKey, setDismissedKey] = useState<string | null>(null);
  const [handledReplaySeq, setHandledReplaySeq] = useState(0);

  const forcedEnrolmentPending = isForcedEnrolmentPending(mfaStatus, signingPasswordRequired, location.pathname);

  const isReplayActive = tourReplaySeq > handledReplaySeq;
  const shouldAutoStart =
    validRole !== null &&
    !mustChangePassword &&
    !forcedEnrolmentPending &&
    tourState.loaded &&
    tourState.value === null &&
    dismissedKey !== tourKey;

  if (!validRole || (!shouldAutoStart && !isReplayActive)) {
    return null;
  }

  return (
    <Tour
      key={isReplayActive ? `replay-${tourReplaySeq}` : `auto-${tourKey}`}
      steps={TOUR_STEPS[validRole]}
      onFinish={(result) => {
        setDismissedKey(tourKey);
        if (isReplayActive) {
          setHandledReplaySeq(tourReplaySeq);
        }
        tourState.set(result).catch(() => {
          toast.error(t("layout.tourSaveFailed"));
        });
      }}
    />
  );
}

function WhatsNewPrompt({
  mfaStatus,
  mfaLoaded,
  notes = RELEASE_NOTES
}: {
  mfaStatus: MfaStatus | null;
  mfaLoaded: boolean;
  notes?: readonly ReleaseNote[];
}) {
  const { t } = useTranslation();
  const { role, mustChangePassword, signingPasswordRequired } = useAuth();
  const { tourReplaySeq } = useHelpPanel();
  const location = useLocation();

  const validRole = isRoleType(role) ? role : null;
  const tourKey = validRole ? tourStateKey(validRole) : "tour.none.v1";
  const tourState = useUiState(tourKey);
  const whatsNewState = useUiState(WHATS_NEW_STATE_KEY);

  const [dismissedVersion, setDismissedVersion] = useState<string | null>(null);
  const [tourBlockedSession, setTourBlockedSession] = useState(false);
  const autoMarkedVersionRef = useRef<string | null>(null);

  const forcedEnrolmentPending = isForcedEnrolmentPending(mfaStatus, signingPasswordRequired, location.pathname);
  const blocked =
    validRole === null ||
    mustChangePassword ||
    forcedEnrolmentPending ||
    !mfaLoaded ||
    !tourState.loaded ||
    !whatsNewState.loaded;

  // The first-login tour takes precedence: if the tour key is absent (tour due
  // to run) or a replay tour was triggered, What's New waits for a later login
  // and stores nothing in this session.
  const tourDueNow = !blocked && (tourState.value === null || tourReplaySeq > 0);
  useEffect(() => {
    if (tourDueNow && !tourBlockedSession) {
      setTourBlockedSession(true);
    }
  }, [tourDueNow, tourBlockedSession]);

  const tourBlocking = tourBlockedSession || tourDueNow;
  const latestNote = notes[0];
  const isNewer = latestNote !== undefined && isNewerVersion(__APP_VERSION__, whatsNewState.value);
  const visibleItems = useMemo(
    () => (latestNote && validRole ? itemsForRole(latestNote, validRole) : []),
    [latestNote, validRole]
  );

  const shouldStoreSilently = !blocked && !tourBlocking && isNewer && visibleItems.length === 0;
  useEffect(() => {
    if (!shouldStoreSilently) return;
    if (autoMarkedVersionRef.current === __APP_VERSION__) return;
    autoMarkedVersionRef.current = __APP_VERSION__;
    whatsNewState.set(__APP_VERSION__).catch(() => {
      // Best-effort silent acknowledgement when this role has no items in the release note.
    });
  }, [shouldStoreSilently, whatsNewState]);

  const shouldShow =
    !blocked &&
    !tourBlocking &&
    isNewer &&
    visibleItems.length > 0 &&
    dismissedVersion !== __APP_VERSION__;

  if (!shouldShow || !validRole) {
    return null;
  }

  const handleClose = () => {
    setDismissedVersion(__APP_VERSION__);
    whatsNewState.set(__APP_VERSION__).catch(() => {
      toast.error(t("layout.releaseNoteSaveFailed"));
    });
  };

  return <WhatsNewDialog open onClose={handleClose} role={validRole} mode="latest" notes={notes} />;
}

export function MainLayout({ whatsNewNotes }: { whatsNewNotes?: readonly ReleaseNote[] } = {}) {
  const { t, i18n } = useTranslation();
  const { logout, mustChangePassword, signingPasswordRequired } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  // One menu, one lab lookup and one "where am I" answer for the sidebar,
  // the breadcrumb and the tab title.
  const { codes: labCodes } = useMyLabs();
  const groups = useMenuGroups();
  const activeTrail = useMemo(
    () => findNavTrail(groups, location.pathname, location.search) ?? accountPageTrail(location.pathname),
    [groups, location.pathname, location.search]
  );

  const [mobileOpen, setMobileOpen] = useState(false);
  // Bench mode starts with the sidebar closed (icon rail) to leave the width to the entry form.
  const { benchMode } = useBenchMode();
  // Unsaved on-tablet entries and whether the server can be reached; checked
  // every 15 s while on a bench tablet or while drafts exist.
  const draftOverview = useDraftOverview();
  const { online } = useConnectionStatus(benchMode || draftOverview.drafts.length > 0);
  const [collapsed, setCollapsed] = useState(() => benchMode || localStorage.getItem("microlims_sidebar_collapsed") === "true");
  const [mfaStatus, setMfaStatus] = useState<MfaStatus | null>(null);
  const [mfaLoaded, setMfaLoaded] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    mfaService
      .getStatus(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) {
          setMfaStatus(data);
          setMfaLoaded(true);
        }
      })
      .catch(() => {
        // Grace banner is best-effort; never block layout on status failure.
        if (!controller.signal.aborted) {
          setMfaLoaded(true);
        }
      });
    return () => {
      controller.abort();
    };
  }, []);

  const handleToggleCollapse = () => {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem("microlims_sidebar_collapsed", String(next));
      return next;
    });
  };

  // On desktop the hamburger toggles the persistent icon-only rail; on
  // mobile there's no rail mode (see Sidebar's effectiveCollapsed), so it
  // opens/closes the temporary drawer instead.
  const handleToggleSidebar = () => {
    if (isMobile) {
      setMobileOpen((prev) => !prev);
    } else {
      handleToggleCollapse();
    }
  };

  const handleIdleTimeout = () => {
    logout();
    navigate("/login");
  };

  const { showWarning, secondsRemaining, stayLoggedIn } = useIdleTimeout(handleIdleTimeout);

  // A seeded/admin-set password can't be kept forever - block navigation
  // anywhere else until the forced change is done.
  if (mustChangePassword && location.pathname !== CHANGE_PASSWORD_PATH) {
    return <Navigate to={CHANGE_PASSWORD_PATH} replace />;
  }
  if (signingPasswordRequired && location.pathname !== SET_SIGNING_PASSWORD_PATH) {
    return <Navigate to={SET_SIGNING_PASSWORD_PATH} replace />;
  }

  return (
    <HelpPanelProvider whatsNewNotes={whatsNewNotes}>
    <MenuGroupsProvider groups={groups}>
    <BreadcrumbProvider>
    <Box
      sx={{
        // dvh where supported: on mobile browsers 100vh includes the area
        // under the address bar, which cut off the bottom of every page.
        // Less the global status banner (announcement / maintenance notice), when shown.
        height: "calc(100vh - var(--system-banner-height, 0px))",
        maxHeight: "calc(100vh - var(--system-banner-height, 0px))",
        "@supports (height: 100dvh)": {
          height: "calc(100dvh - var(--system-banner-height, 0px))",
          maxHeight: "calc(100dvh - var(--system-banner-height, 0px))"
        },
        bgcolor: "background.default",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden"
      }}
    >
      {/* Keyboard users land here first and can jump past the navigation. */}
      <Box
        component="a"
        href={`#${MAIN_CONTENT_ID}`}
        className="no-print"
        sx={{
          position: "absolute",
          insetInlineStart: 8,
          top: -48,
          zIndex: 2000,
          px: 2,
          py: 1,
          borderRadius: 1,
          bgcolor: "background.paper",
          color: "primary.main",
          fontWeight: 600,
          fontSize: 14,
          boxShadow: 3,
          "&:focus": { top: 8 }
        }}
      >
        {t("layout.skipToMain")}
      </Box>

      <Header
        onToggleSidebar={handleToggleSidebar}
        sidebarCollapsed={collapsed}
        labCodes={labCodes}
        unsavedDraftCount={draftOverview.drafts.length}
        draftsMenu={<DraftsMenu overview={draftOverview} />}
      />
      <ConnectionBanner online={online} unsavedCount={draftOverview.drafts.length} />

      <Box
        component="div"
        sx={{
          display: "flex",
          flex: 1,
          minHeight: 0,
          overflow: "hidden",
          position: "relative"
        }}
      >
        <Sidebar
          mobileOpen={mobileOpen}
          onMobileClose={() => setMobileOpen(false)}
          collapsed={collapsed}
          onToggleCollapse={handleToggleCollapse}
          groups={groups}
          activeTrail={activeTrail}
        />

        <Box
          component="main"
          id={MAIN_CONTENT_ID}
          tabIndex={-1}
          sx={{
            outline: "none",
            flexGrow: 1,
            height: "100%",
            overflowY: "auto",
            overflowX: "hidden",
            p: { xs: 2, sm: 3 },
            width: "100%",
            minWidth: 0,
            bgcolor: "background.default"
          }}
        >
          <Box sx={{ maxWidth: 1600, mx: "auto" }}>
            {mfaStatus?.required && !mfaStatus.enrolled && mfaStatus.enrolmentDueAt && (
              <Box
                role="status"
                sx={{
                  mb: 2,
                  px: 2,
                  py: 1.25,
                  borderRadius: 1,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  flexWrap: "wrap",
                  gap: 1.5,
                  bgcolor: theme.custom.status.inconclusive.bg,
                  color: theme.custom.status.inconclusive.text,
                  border: `1px solid ${theme.custom.status.inconclusive.border}`
                }}
              >
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  <WarningAmberRoundedIcon fontSize="small" aria-hidden />
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {t("layout.mfaRequiredBy", { date: formatDateTimeWithTimeZone(mfaStatus.enrolmentDueAt) })}
                  </Typography>
                </Box>
                <Button component={RouterLink} to="/mfa-enrol" size="small" variant="outlined" color="inherit">
                  {t("layout.mfaSetUp")}
                </Button>
              </Box>
            )}
            <AppBreadcrumbs trail={activeTrail} groups={groups} />
            <AppErrorBoundary inline key={location.pathname}>
              <Outlet />
            </AppErrorBoundary>
          </Box>
        </Box>
        <HelpPanelSlot />
        <FirstLoginTour mfaStatus={mfaStatus} />
        <WhatsNewPrompt mfaStatus={mfaStatus} mfaLoaded={mfaLoaded} notes={whatsNewNotes} />
      </Box>

      {/* GMP session-timeout control - not dismissable via escape/backdrop, no "stay logged out" bypass.
          onClose ignores every close reason (escapeKeyDown, backdropClick), which is what keeps it open;
          MUI v9 removed disableEscapeKeyDown in favour of exactly this. */}
      <Dialog open={showWarning} onClose={() => {}}>
        <DialogTitle>{t("layout.timeoutTitle")}</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {t("layout.timeoutBody", { duration: formatSeconds(secondsRemaining, i18n.language) })}
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button variant="contained" onClick={stayLoggedIn} autoFocus>{t("layout.stayLoggedIn")}</Button>
        </DialogActions>
      </Dialog>
    </Box>
    </BreadcrumbProvider>
    </MenuGroupsProvider>
    </HelpPanelProvider>
  );
}
