import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, Drawer, IconButton, MenuItem, Paper, Select, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Tooltip, Typography
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import HistoryIcon from "@mui/icons-material/History";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import { MaintenanceSection } from "./MaintenanceSection";
import { TestActionDialog } from "./TestActionDialog";
import { TestSendOutcome } from "./TestSendOutcome";
import { IntegrationsService } from "./services/IntegrationsService";
import { SettingsService, SystemSetting, SystemSettingFixedItem, SystemSettingVersion } from "./services/SettingsService";

const CATEGORY_TITLES: Record<string, string> = {
  Security: "Security (passwords, lockout, sessions, e-signature)",
  Retention: "Retention and clean-up",
  Monitoring: "Monitoring thresholds",
  Alerts: "Alerts"
};

const withUnit = (value: string, unit: string | null) => (unit ? `${value} ${unit}` : value);

// A list setting is stored comma-separated; show it readably and edit it one per line.
const displayValue = (s: SystemSetting) =>
  s.valueType === "EmailList" ? (s.value ? s.value.split(",").join(", ") : "Nobody") : withUnit(s.value, s.unit);

// The admin panel controls the system, never GxP data. Every change needs a
// reason; security keys also need the administrator's password (electronic
// signature). The server validates and applies the rules; this page only
// collects the value, the reason and the signature.
export function SettingsPage() {
  const [settings, setSettings] = useState<SystemSetting[] | null>(null);
  const [fixedItems, setFixedItems] = useState<SystemSettingFixedItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState<SystemSetting | null>(null);
  const [draft, setDraft] = useState("");
  const [reason, setReason] = useState("");
  const [editError, setEditError] = useState<string | null>(null);
  const [signing, setSigning] = useState(false);
  const [testAlertOpen, setTestAlertOpen] = useState(false);

  const [historyFor, setHistoryFor] = useState<SystemSetting | null>(null);
  const [history, setHistory] = useState<SystemSettingVersion[] | null>(null);

  const load = useCallback(() => {
    SettingsService.getAll()
      .then((o) => { setSettings(o.settings); setFixedItems(o.fixedItems); })
      .catch(() => setError("Could not load the settings. Reload the page to try again."));
  }, []);
  useEffect(load, [load]);

  const openEdit = (s: SystemSetting) => {
    setEditing(s); setDraft(s.valueType === "EmailList" ? s.value.split(",").join("\n") : s.value); setReason(""); setEditError(null);
  };
  const closeEdit = () => { setEditing(null); setSigning(false); };

  const save = async (password?: string) => {
    if (!editing) return;
    try {
      await SettingsService.update(editing.key, draft, reason.trim(), password);
      toast.success(`${editing.key} updated. It applies without a restart.`);
      closeEdit();
      load();
    } catch (e: any) {
      const message: string = e?.response?.data?.message ?? "Could not save the setting.";
      // A refused password stays in the signature dialog; anything else returns to the form.
      if (signing && /password|signature/i.test(message)) throw e;
      setSigning(false);
      setEditError(message);
    }
  };

  const onSaveClick = () => {
    setEditError(null);
    if (editing?.requiresSignature) setSigning(true);
    else void save();
  };

  const openHistory = (s: SystemSetting) => {
    setHistoryFor(s); setHistory(null);
    SettingsService.history(s.key).then(setHistory).catch(() => setHistory([]));
  };

  // The System category (maintenance mode, announcement) has its own section.
  const categories = settings ? Array.from(new Set(settings.map((s) => s.category))).filter((c) => c !== "System") : [];

  return (
    <>
      <PageHeader title="Settings" subtitle="System operating settings. Changes need a reason; security settings also need your electronic signature." />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!settings && !error && <LoadingSpinner />}
      {settings?.some((s) => s.key === "System.MaintenanceMode") && <MaintenanceSection settings={settings} onChanged={load} />}

      {categories.map((category) => (
        <Paper key={category} sx={{ mb: 2, p: 2 }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6" component="h2">{CATEGORY_TITLES[category] ?? category}</Typography>
            {category === "Alerts" && (
              <Button variant="outlined" size="small" onClick={() => setTestAlertOpen(true)}>Send test alert</Button>
            )}
          </Stack>
          <TableContainer>
            <Table size="small" aria-label={category}>
              <TableHead>
                <TableRow>
                  <TableCell>Setting</TableCell>
                  <TableCell>Value</TableCell>
                  <TableCell>Source</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {settings!.filter((s) => s.category === category).map((s) => (
                  <TableRow key={s.key}>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{s.key}</Typography>
                      <Typography variant="caption" color="text.secondary">{s.description}</Typography>
                    </TableCell>
                    <TableCell>
                      {displayValue(s)}
                      {s.requiresSignature && <Chip size="small" label="Signature" sx={{ ml: 1 }} />}
                    </TableCell>
                    <TableCell>
                      {s.source}{s.version > 0 ? ` (v${s.version})` : ""}
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Change">
                        <IconButton size="small" aria-label={`Edit ${s.key}`} onClick={() => openEdit(s)}><EditIcon fontSize="small" /></IconButton>
                      </Tooltip>
                      <Tooltip title="History">
                        <IconButton size="small" aria-label={`History of ${s.key}`} onClick={() => openHistory(s)}><HistoryIcon fontSize="small" /></IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      ))}

      {fixedItems.length > 0 && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Typography variant="h6" component="h2" sx={{ mb: 1 }}>Not changed here</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Shown for information. Secrets are never stored or displayed.
          </Typography>
          <TableContainer>
            <Table size="small" aria-label="Not changed here">
              <TableHead>
                <TableRow>
                  <TableCell>Setting</TableCell>
                  <TableCell>Value</TableCell>
                  <TableCell>Source and how to change it</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {fixedItems.map((f) => (
                  <TableRow key={f.key}>
                    <TableCell>{f.label}</TableCell>
                    <TableCell>{f.value}</TableCell>
                    <TableCell>
                      {f.source}
                      <Typography variant="caption" color="text.secondary" component="span" sx={{ display: "block" }}>
                        {f.note}{f.editLocation ? ` Edit it under ${f.editLocation}.` : ""}
                      </Typography>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}

      <FloatingDialog
        open={Boolean(editing)}
        onClose={closeEdit}
        maxWidth="xs"
        title={`Change ${editing?.key ?? "setting"}`}
        actions={
          <>
            <Button onClick={closeEdit}>Cancel</Button>
            <Button variant="contained" onClick={onSaveClick} disabled={(editing?.valueType !== "EmailList" && !draft.trim()) || !reason.trim()}>
              {editing?.requiresSignature ? "Continue to sign" : "Save"}
            </Button>
          </>
        }
      >
        {editing && (
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Typography variant="body2">{editing.description}</Typography>
            <Typography variant="caption" color="text.secondary">
              {editing.valueType === "EmailList"
                ? `Up to ${editing.max} addresses; empty means nobody.`
                : <>Default {withUnit(editing.defaultValue, editing.unit)}
                  {editing.min !== null && editing.max !== null ? `. Allowed ${editing.min} to ${editing.max}.` : "."}</>}
            </Typography>
            {editing.valueType === "Boolean" ? (
              <Select size="small" value={draft} onChange={(e) => setDraft(String(e.target.value))} inputProps={{ "aria-label": "Value" }}>
                <MenuItem value="true">true</MenuItem>
                <MenuItem value="false">false</MenuItem>
              </Select>
            ) : editing.allowedValues ? (
              <Select size="small" value={draft} onChange={(e) => setDraft(String(e.target.value))} inputProps={{ "aria-label": "Value" }}>
                {editing.allowedValues.map((v) => <MenuItem key={v} value={v}>{v}</MenuItem>)}
              </Select>
            ) : editing.valueType === "EmailList" ? (
              <TextField
                label="Value" size="small" multiline minRows={3} value={draft} onChange={(e) => setDraft(e.target.value)}
                helperText="One address per line, or separated by commas."
              />
            ) : (
              <TextField
                label="Value" size="small" value={draft} onChange={(e) => setDraft(e.target.value)}
                type={editing.valueType === "Integer" ? "number" : "text"}
              />
            )}
            <TextField
              label="Reason for change" size="small" required multiline minRows={2} value={reason}
              onChange={(e) => setReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
            />
            {editError && <Alert severity="error">{editError}</Alert>}
          </Stack>
        )}
      </FloatingDialog>

      <TestActionDialog
        open={testAlertOpen}
        title="Send test alert"
        description="Sends a clearly labelled test message to the configured critical-alert recipients."
        confirmLabel="Send test alert"
        onClose={() => setTestAlertOpen(false)}
        onSubmit={async (reason) => <TestSendOutcome result={await IntegrationsService.sendTestAlert(reason)} />}
      />

      <SignatureDialog
        open={signing}
        meaningStatement={`I am changing the system setting ${editing?.key ?? ""} from ${editing?.value ?? ""} to ${draft}. Reason: ${reason.trim()}`}
        onCancel={() => setSigning(false)}
        onConfirm={save}
      />

      <Drawer anchor="right" open={Boolean(historyFor)} onClose={() => setHistoryFor(null)}>
        <Box sx={{ width: { xs: "100vw", sm: 480 }, p: 2 }} role="region" aria-label="Setting history">
          <Typography variant="h6" component="h2">History</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{historyFor?.key}</Typography>
          {!history && <LoadingSpinner />}
          {history && history.length === 0 && <Typography variant="body2">No changes recorded. The value has never been changed here.</Typography>}
          {history?.map((h) => (
            <Paper key={h.version} variant="outlined" sx={{ p: 1.5, mb: 1 }}>
              <Typography variant="subtitle2">
                v{h.version}: {h.previousValue} {"->"} {h.value}
              </Typography>
              <Typography variant="caption" color="text.secondary" component="span" sx={{ display: "block" }}>
                {h.changedByName ?? `User ${h.changedByUserId}`} on {new Date(h.changedAt).toLocaleString()}
                {h.electronicSignatureId ? " (e-signed)" : ""}
              </Typography>
              <Typography variant="body2">Reason: {h.reason}</Typography>
            </Paper>
          ))}
        </Box>
      </Drawer>
    </>
  );
}
