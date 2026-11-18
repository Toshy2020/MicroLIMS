import { useState } from "react";
import { Alert, Button, MenuItem, Paper, Select, Stack, TextField, Typography } from "@mui/material";
import { toast } from "sonner";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { AnnouncementAlert, MaintenanceNotice } from "../../components/SystemStatusBanner";
import { useSystemStatus } from "../../contexts/SystemStatusContext";
import { SettingsService, SystemSetting } from "./services/SettingsService";
import { i18n, useTranslation } from "../../i18n";

export const MAINTENANCE_KEYS = {
  mode: "System.MaintenanceMode",
  message: "System.MaintenanceMessage",
  banner: "System.AnnouncementBanner",
  severity: "System.AnnouncementSeverity"
} as const;

const valueOf = (settings: SystemSetting[], key: string) => settings.find((s) => s.key === key)?.value ?? "";
const errorText = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback;

// Maintenance mode and the announcement banner. Turning maintenance on or off
// is GxP-relevant: it needs a reason and the administrator's electronic
// signature. The message and the banner need a reason only. The server applies
// the rules; this section collects the values, the reason and the signature.
export function MaintenanceSection({ settings, onChanged }: { settings: SystemSetting[]; onChanged: () => void }) {
  const { t } = useTranslation("admin");
  const { t: tc } = useTranslation("common");
  const tEn = i18n.getFixedT("en", "admin");
  const { refresh } = useSystemStatus();
  const maintenanceOn = valueOf(settings, MAINTENANCE_KEYS.mode) === "true";
  const storedMessage = valueOf(settings, MAINTENANCE_KEYS.message);
  const storedBanner = valueOf(settings, MAINTENANCE_KEYS.banner);
  const storedSeverity = valueOf(settings, MAINTENANCE_KEYS.severity) || "info";

  // Toggle: reason dialog, then signature.
  const [toggleOpen, setToggleOpen] = useState(false);
  const [toggleReason, setToggleReason] = useState("");
  const [toggleError, setToggleError] = useState<string | null>(null);
  const [signing, setSigning] = useState(false);

  // Message and banner editors.
  const [message, setMessage] = useState<string | null>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [severity, setSeverity] = useState<string | null>(null);
  const [textReason, setTextReason] = useState("");
  const [textError, setTextError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const draftMessage = message ?? storedMessage;
  const draftBanner = banner ?? storedBanner;
  const draftSeverity = severity ?? storedSeverity;
  const messageChanged = draftMessage.trim() !== storedMessage;
  const bannerChanged = draftBanner.trim() !== storedBanner;
  const severityChanged = draftSeverity !== storedSeverity;
  const anyTextChange = messageChanged || bannerChanged || severityChanged;

  const closeToggle = () => { setToggleOpen(false); setSigning(false); };

  const confirmToggle = async (password?: string) => {
    try {
      await SettingsService.update(MAINTENANCE_KEYS.mode, maintenanceOn ? "false" : "true", toggleReason.trim(), password);
      toast.success(maintenanceOn ? t("maintenance.off") : t("maintenance.on"));
      closeToggle();
      setToggleReason("");
      onChanged();
      refresh();
    } catch (e) {
      const text = errorText(e, t("maintenance.toggleError"));
      // A refused password stays in the signature dialog; anything else returns to the form.
      if (signing && /password|signature/i.test(text)) throw e;
      setSigning(false);
      setToggleError(text);
    }
  };

  const saveText = async () => {
    setSaving(true);
    setTextError(null);
    try {
      const reason = textReason.trim();
      if (messageChanged) await SettingsService.update(MAINTENANCE_KEYS.message, draftMessage.trim(), reason);
      if (bannerChanged) await SettingsService.update(MAINTENANCE_KEYS.banner, draftBanner.trim(), reason);
      if (severityChanged) await SettingsService.update(MAINTENANCE_KEYS.severity, draftSeverity, reason);
      toast.success(t("maintenance.saved"));
      setMessage(null); setBanner(null); setSeverity(null); setTextReason("");
      onChanged();
      refresh();
    } catch (e) {
      setTextError(errorText(e, t("maintenance.saveError")));
      onChanged();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Paper sx={{ mb: 2, p: 2 }} component="section" aria-label={t("maintenance.title")}>
      <Typography variant="h6" component="h2" sx={{ mb: 1 }}>{t("maintenance.title")}</Typography>

      <Stack spacing={1.5} sx={{ mb: 3 }}>
        <Alert severity={maintenanceOn ? "warning" : "success"}>
          {maintenanceOn
            ? t("maintenance.statusOn")
            : t("maintenance.statusOff")}
        </Alert>
        <div>
          <Button
            variant="contained" color={maintenanceOn ? "success" : "warning"}
            onClick={() => { setToggleOpen(true); setToggleReason(""); setToggleError(null); }}
          >
            {maintenanceOn ? t("maintenance.turnOff") : t("maintenance.turnOn")}
          </Button>
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
            {t("maintenance.needsSignature")}
          </Typography>
        </div>
      </Stack>

      <Stack spacing={2}>
        <TextField
          label={t("maintenance.messageLabel")} size="small" multiline minRows={2} value={draftMessage}
          onChange={(e) => setMessage(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
          helperText={t("maintenance.messageHelper")}
        />
        <TextField
          label={t("maintenance.bannerLabel")} size="small" multiline minRows={2} value={draftBanner}
          onChange={(e) => setBanner(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
          helperText={t("maintenance.bannerHelper")}
        />
        <Select size="small" value={draftSeverity} onChange={(e) => setSeverity(String(e.target.value))} inputProps={{ "aria-label": t("maintenance.bannerStyle") }} sx={{ maxWidth: 200 }}>
          <MenuItem value="info">info</MenuItem>
          <MenuItem value="warning">warning</MenuItem>
        </Select>

        <div aria-label={t("maintenance.preview")}>
          <Typography variant="caption" color="text.secondary">{t("maintenance.preview")}</Typography>
          {draftBanner.trim()
            ? <AnnouncementAlert text={draftBanner.trim()} severity={draftSeverity === "warning" ? "warning" : "info"} />
            : <Typography variant="body2" color="text.secondary">{t("maintenance.noBanner")}</Typography>}
          <MaintenanceNotice message={draftMessage.trim()} />
        </div>

        <TextField
          label={t("common.reasonForChange")} size="small" required multiline minRows={2} value={textReason}
          onChange={(e) => setTextReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
        />
        {textError && <Alert severity="error">{textError}</Alert>}
        <div>
          <Button variant="contained" onClick={() => void saveText()} disabled={saving || !anyTextChange || !textReason.trim() || !draftMessage.trim()}>
            {t("maintenance.saveText")}
          </Button>
        </div>
      </Stack>

      <FloatingDialog
        open={toggleOpen}
        onClose={closeToggle}
        maxWidth="xs"
        title={maintenanceOn ? t("maintenance.turnOff") : t("maintenance.turnOn")}
        actions={
          <>
            <Button onClick={closeToggle}>{tc("actions.cancel")}</Button>
            <Button variant="contained" onClick={() => { setToggleError(null); setSigning(true); }} disabled={!toggleReason.trim()}>
              {t("settings.continueToSign")}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">
            {maintenanceOn
              ? t("maintenance.dialogOn")
              : t("maintenance.dialogOff")}
          </Typography>
          <TextField
            label={t("common.reasonForChange")} size="small" required multiline minRows={2} value={toggleReason}
            onChange={(e) => setToggleReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
          />
          {toggleError && <Alert severity="error">{toggleError}</Alert>}
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={tEn("maintenance.meaning", { state: maintenanceOn ? tEn("maintenance.stateOff") : tEn("maintenance.stateOn"), reason: toggleReason.trim() })}
        meaningStatementTranslation={t("maintenance.meaning", { state: maintenanceOn ? t("maintenance.stateOff") : t("maintenance.stateOn"), reason: toggleReason.trim() })}
        onCancel={() => setSigning(false)}
        onConfirm={confirmToggle}
      />
    </Paper>
  );
}
