import { useState } from "react";
import { Alert, Button, MenuItem, Paper, Select, Stack, TextField, Typography } from "@mui/material";
import { toast } from "sonner";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { AnnouncementAlert, MaintenanceNotice } from "../../components/SystemStatusBanner";
import { useSystemStatus } from "../../contexts/SystemStatusContext";
import { SettingsService, SystemSetting } from "./services/SettingsService";

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
      toast.success(maintenanceOn ? "Maintenance mode is off. Users can save changes again." : "Maintenance mode is on. Users cannot save changes.");
      closeToggle();
      setToggleReason("");
      onChanged();
      refresh();
    } catch (e) {
      const text = errorText(e, "Could not change maintenance mode.");
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
      toast.success("Saved. Users see the change within a minute.");
      setMessage(null); setBanner(null); setSeverity(null); setTextReason("");
      onChanged();
      refresh();
    } catch (e) {
      setTextError(errorText(e, "Could not save."));
      onChanged();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Paper sx={{ mb: 2, p: 2 }} component="section" aria-label="Maintenance and announcements">
      <Typography variant="h6" component="h2" sx={{ mb: 1 }}>Maintenance and announcements</Typography>

      <Stack spacing={1.5} sx={{ mb: 3 }}>
        <Alert severity={maintenanceOn ? "warning" : "success"}>
          {maintenanceOn
            ? "Maintenance mode is ON. Every change made through the application is refused with the maintenance message. Sign-in and this administration area stay available."
            : "Maintenance mode is off. Everything works normally."}
        </Alert>
        <div>
          <Button
            variant="contained" color={maintenanceOn ? "success" : "warning"}
            onClick={() => { setToggleOpen(true); setToggleReason(""); setToggleError(null); }}
          >
            {maintenanceOn ? "Turn maintenance mode off" : "Turn maintenance mode on"}
          </Button>
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
            Needs a reason and your electronic signature. Running background jobs that change laboratory records pause while it is on.
          </Typography>
        </div>
      </Stack>

      <Stack spacing={2}>
        <TextField
          label="Maintenance message" size="small" multiline minRows={2} value={draftMessage}
          onChange={(e) => setMessage(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
          helperText="Shown to users while maintenance mode is on, and returned with every refused change."
        />
        <TextField
          label="Announcement banner" size="small" multiline minRows={2} value={draftBanner}
          onChange={(e) => setBanner(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
          helperText="Shown on every page, including the sign-in page. Leave empty for no banner."
        />
        <Select size="small" value={draftSeverity} onChange={(e) => setSeverity(String(e.target.value))} inputProps={{ "aria-label": "Banner style" }} sx={{ maxWidth: 200 }}>
          <MenuItem value="info">info</MenuItem>
          <MenuItem value="warning">warning</MenuItem>
        </Select>

        <div aria-label="Preview">
          <Typography variant="caption" color="text.secondary">Preview</Typography>
          {draftBanner.trim()
            ? <AnnouncementAlert text={draftBanner.trim()} severity={draftSeverity === "warning" ? "warning" : "info"} />
            : <Typography variant="body2" color="text.secondary">No banner.</Typography>}
          <MaintenanceNotice message={draftMessage.trim()} />
        </div>

        <TextField
          label="Reason for change" size="small" required multiline minRows={2} value={textReason}
          onChange={(e) => setTextReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
        />
        {textError && <Alert severity="error">{textError}</Alert>}
        <div>
          <Button variant="contained" onClick={() => void saveText()} disabled={saving || !anyTextChange || !textReason.trim() || !draftMessage.trim()}>
            Save message and banner
          </Button>
        </div>
      </Stack>

      <FloatingDialog
        open={toggleOpen}
        onClose={closeToggle}
        maxWidth="xs"
        title={maintenanceOn ? "Turn maintenance mode off" : "Turn maintenance mode on"}
        actions={
          <>
            <Button onClick={closeToggle}>Cancel</Button>
            <Button variant="contained" onClick={() => { setToggleError(null); setSigning(true); }} disabled={!toggleReason.trim()}>
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">
            {maintenanceOn
              ? "Users will be able to save changes again."
              : "From now on every change made through the application is refused until you turn maintenance mode off."}
          </Typography>
          <TextField
            label="Reason for change" size="small" required multiline minRows={2} value={toggleReason}
            onChange={(e) => setToggleReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
          />
          {toggleError && <Alert severity="error">{toggleError}</Alert>}
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={`I am turning maintenance mode ${maintenanceOn ? "off" : "on"}. Reason: ${toggleReason.trim()}`}
        onCancel={() => setSigning(false)}
        onConfirm={confirmToggle}
      />
    </Paper>
  );
}
