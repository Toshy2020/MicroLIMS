import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, MenuItem, Paper, Select, Skeleton, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TextField, Typography
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { EmptyState } from "../../components/lab/EmptyState";
import { PartialOutageBanner } from "../../components/lab/PartialOutageBanner";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { ToneBadge } from "../../components/StatusBadge";
import { tableHeadSx } from "../../theme";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { formatRoleName } from "../../utils/userDisplay";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import type { TFunction } from "i18next";
import { SettingsService, type SystemSetting } from "./services/SettingsService";
import { SsoAdminService, type SsoAdminOverview } from "./services/SsoAdminService";
import { ImportDirectoryLinksDialog } from "./ImportDirectoryLinksDialog";
import { i18n, useTranslation } from "../../i18n";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "admin", ...options });

export const SSO_MODE_SETTING_KEY = "security.sso.mode";
const FALLBACK_MODES = ["OpenIdConnect", "ActiveDirectory"];

const showValue = (v: string | boolean | number | null, t: TFunction<"admin">): string => (v === null ? t("companySignIn.notSet") : typeof v === "boolean" ? (v ? t("companySignIn.yes") : t("companySignIn.no")) : String(v));

// Read-only status of company (single) sign-in. The server decides the blockers
// and enforces the switch-on rules; this page shows them and collects the
// reason (and the signature when the setting needs one).
export function CompanySignInPage() {
  const { t } = useTranslation("admin");
  const { t: tc } = useTranslation("common");
  const tEn = i18n.getFixedT("en", "admin");
  const theme = useTheme();
  const [overview, setOverview] = useState<SsoAdminOverview | null>(null);
  const [setting, setSetting] = useState<SystemSetting | null>(null);
  const [settingFailed, setSettingFailed] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [testing, setTesting] = useState(false);
  const [testAlert, setTestAlert] = useState<{ severity: "success" | "error"; message: string } | null>(null);
  const [targetMode, setTargetMode] = useState("");
  const [switchOpen, setSwitchOpen] = useState(false);
  const [signing, setSigning] = useState(false);
  const [switchReason, setSwitchReason] = useState("");
  const [switchError, setSwitchError] = useState<string | null>(null);
  const [importOpen, setImportOpen] = useState(false);

  const load = useCallback((signal?: AbortSignal) => {
    setLoading(true);
    setLoadError(null);
    setSettingFailed(false);
    SettingsService.getAll()
      .then((o) => { if (!signal?.aborted) setSetting(o.settings.find((s) => s.key === SSO_MODE_SETTING_KEY) ?? null); })
      .catch(() => { if (!signal?.aborted) { setSetting(null); setSettingFailed(true); } });
    SsoAdminService.getOverview(signal)
      .then((o) => { if (!signal?.aborted) setOverview(o); })
      .catch((err: unknown) => {
        if (!signal?.aborted) setLoadError(serverMessage(err) ?? getErrorMessage(err, tl("companySignIn.loadError")));
      })
      .finally(() => { if (!signal?.aborted) setLoading(false); });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const modes = (setting?.allowedValues ?? FALLBACK_MODES).filter((m) => m !== "Off");
  const selectedMode = targetMode && modes.includes(targetMode) ? targetMode : (modes[0] ?? "");

  const testConnection = async () => {
    setTesting(true);
    setTestAlert(null);
    try {
      const res = await SsoAdminService.testConnection();
      setTestAlert({ severity: "success", message: t("companySignIn.testPassed", { time: formatDateTimeWithTimeZone(res.testedAt) }) });
      setOverview((o) => (o ? { ...o, lastTestPassedAt: res.testedAt } : o));
    } catch (err: unknown) {
      setTestAlert({ severity: "error", message: serverMessage(err) ?? getErrorMessage(err, t("companySignIn.testFailed")) });
    } finally {
      setTesting(false);
    }
  };

  const save = async (password?: string) => {
    if (!setting || !overview || overview.blockers.length > 0) return;
    try {
      await SettingsService.update(SSO_MODE_SETTING_KEY, selectedMode, switchReason.trim(), password);
      toast.success(t("companySignIn.switchedOn"));
      setSwitchOpen(false);
      setSigning(false);
      setSwitchReason("");
      load();
    } catch (err: unknown) {
      const message = serverMessage(err) ?? getErrorMessage(err, t("companySignIn.switchError"));
      // A refused password stays in the signature dialog.
      if (signing && /password|signature/i.test(message)) throw err;
      setSigning(false);
      setSwitchError(message);
    }
  };

  const onConfirmSwitch = () => {
    setSwitchError(null);
    if (setting?.requiresSignature) setSigning(true);
    else void save();
  };

  const header = <PageHeader title={t("companySignIn.title")} subtitle={t("companySignIn.subtitle")} />;

  if (loading && !overview) {
    return (
      <>
        {header}
        <Stack spacing={2} role="status" aria-label={t("companySignIn.loadingStatus")}>
          <Skeleton variant="rectangular" height={120} />
          <Skeleton variant="rectangular" height={180} />
        </Stack>
      </>
    );
  }

  if (!overview) {
    return (
      <>
        {header}
        <Alert severity="error" action={<Button color="inherit" size="small" onClick={() => load()}>{tc("actions.retry")}</Button>}>
          {loadError ?? t("companySignIn.loadError")}
        </Alert>
      </>
    );
  }

  const blockers = overview.blockers;
  const isOff = overview.mode === "Off";
  // The reason the switch-on control is unavailable, shown next to it.
  const gate = blockers.length > 0
    ? t("companySignIn.gateBlockers")
    : settingFailed || !setting
      ? t("companySignIn.gateSetting")
      : null;
  const configEntries = Object.entries(overview.configuration);

  return (
    <>
      {header}
      <Stack spacing={3}>
        <PartialOutageBanner sources={settingFailed ? [t("companySignIn.settingSource")] : []} onRetry={() => load()} />

        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Box sx={{ display: "flex", flexWrap: "wrap", justifyContent: "space-between", alignItems: "center", gap: 2 }}>
              <Box>
                <Typography variant="subtitle1" component="h2" sx={{ fontWeight: 700 }}>{t("companySignIn.mode", { mode: overview.mode })}</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
                  {t("companySignIn.lastTest", { time: overview.lastTestPassedAt ? formatDateTimeWithTimeZone(overview.lastTestPassedAt) : t("companySignIn.neverTested") })}
                </Typography>
              </Box>
              <Button variant="outlined" onClick={testConnection} disabled={testing}>
                {testing ? t("companySignIn.testing") : t("companySignIn.testConnection")}
              </Button>
            </Box>

            {testAlert && <Alert severity={testAlert.severity}>{testAlert.message}</Alert>}

            <Box>
              <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>{t("companySignIn.blockers")}</Typography>
              {blockers.length > 0 ? (
                <Stack component="ul" spacing={0.5} aria-label={t("companySignIn.blockers")} sx={{ pl: 2.5, m: 0 }}>
                  {blockers.map((b) => <Typography key={b} component="li" variant="body2">{b}</Typography>)}
                </Stack>
              ) : (
                <ToneBadge tone="notDetected" label={t("companySignIn.noBlockers")} />
              )}
            </Box>

            {isOff ? (
              <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 2 }}>
                <Select
                  size="small" value={selectedMode} onChange={(e) => setTargetMode(String(e.target.value))}
                  inputProps={{ "aria-label": t("companySignIn.modeToSwitchOn") }} sx={{ minWidth: 200 }}
                >
                  {modes.map((m) => <MenuItem key={m} value={m}>{m}</MenuItem>)}
                </Select>
                <Button
                  variant="contained" disabled={Boolean(gate) || !selectedMode}
                  onClick={() => { setSwitchError(null); setSwitchOpen(true); }}
                >
                  {t("companySignIn.switchOn")}
                </Button>
                {gate && <Typography variant="body2" color="text.secondary">{gate}</Typography>}
              </Box>
            ) : (
              <Typography variant="body2" color="text.secondary">{t("companySignIn.isOn", { mode: overview.mode })}</Typography>
            )}
          </Stack>
        </Paper>

        <Box>
          <SectionTitle>{t("companySignIn.configuration")}</SectionTitle>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            {t("companySignIn.configNote")}
          </Typography>
          <TableContainer component={Paper}>
            <Table size="small" aria-label={t("companySignIn.configTable")}>
              <TableHead>
                <TableRow sx={tableHeadSx(theme)}><TableCell>{t("companySignIn.setting")}</TableCell><TableCell>{t("common.value")}</TableCell></TableRow>
              </TableHead>
              <TableBody>
                {configEntries.length === 0 ? (
                  <TableRow><TableCell colSpan={2}>{t("companySignIn.noConfig")}</TableCell></TableRow>
                ) : configEntries.map(([key, value]) => (
                  <TableRow key={key}>
                    <TableCell sx={{ fontWeight: 600 }}>{key}</TableCell>
                    <TableCell>{showValue(value, t)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Box>

        <Box>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <SectionTitle>{t("companySignIn.unlinked")}</SectionTitle>
            <Button variant="outlined" size="small" onClick={() => setImportOpen(true)}>{t("companySignIn.importLinks")}</Button>
          </Box>
          {overview.unlinkedUsers.length === 0 ? (
            <EmptyState title={t("companySignIn.noUnlinked")} description={t("companySignIn.noUnlinkedDescription")} />
          ) : (
            <TableContainer component={Paper}>
              <Table size="small" aria-label={t("companySignIn.unlinkedTable")}>
                <TableHead>
                  <TableRow sx={tableHeadSx(theme)}><TableCell>{t("companySignIn.username")}</TableCell><TableCell>{t("companySignIn.fullName")}</TableCell><TableCell>{t("common.role")}</TableCell></TableRow>
                </TableHead>
                <TableBody>
                  {overview.unlinkedUsers.map((u) => (
                    <TableRow key={u.id}>
                      <TableCell>{u.username}</TableCell>
                      <TableCell>{u.fullName}</TableCell>
                      <TableCell>{formatRoleName(u.role)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Box>
      </Stack>

      <FloatingDialog
        open={switchOpen}
        onClose={() => { setSwitchOpen(false); setSigning(false); }}
        maxWidth="xs"
        title={t("companySignIn.dialogTitle")}
        actions={
          <>
            <Button onClick={() => setSwitchOpen(false)} autoFocus>{tc("actions.cancel")}</Button>
            <Button variant="contained" onClick={onConfirmSwitch} disabled={!switchReason.trim()}>
              {setting?.requiresSignature ? t("settings.continueToSign") : t("companySignIn.switchOnShort")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">{t("companySignIn.dialogIntro", { key: SSO_MODE_SETTING_KEY, mode: selectedMode })}</Typography>
          {switchError && <Alert severity="error">{switchError}</Alert>}
          <TextField
            label={t("common.reasonForChange")} size="small" required multiline minRows={2} value={switchReason}
            onChange={(e) => setSwitchReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={tEn("companySignIn.meaning", { key: SSO_MODE_SETTING_KEY, oldValue: overview.mode, newValue: selectedMode, reason: switchReason.trim() })}
        meaningStatementTranslation={t("companySignIn.meaning", { key: SSO_MODE_SETTING_KEY, oldValue: overview.mode, newValue: selectedMode, reason: switchReason.trim() })}
        onCancel={() => setSigning(false)}
        onConfirm={save}
      />

      <ImportDirectoryLinksDialog open={importOpen} onClose={() => setImportOpen(false)} onImported={() => load()} />
    </>
  );
}
