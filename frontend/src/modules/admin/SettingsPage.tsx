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
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { serverMessage } from "../../utils/errorMessage";
import { i18n, useTranslation } from "../../i18n";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "admin", ...options });

const withUnit = (value: string, unit: string | null) => (unit ? `${value} ${unit}` : value);

// A list setting is stored comma-separated; show it readably and edit it one per line.
const displayValue = (s: SystemSetting, nobody: string) =>
  s.valueType === "EmailList" ? (s.value ? s.value.split(",").join(", ") : nobody) : withUnit(s.value, s.unit);

// The admin panel controls the system, never GxP data. Every change needs a
// reason; security keys also need the administrator's password (electronic
// signature). The server validates and applies the rules; this page only
// collects the value, the reason and the signature.
export function SettingsPage() {
  const { t } = useTranslation("admin");
  const { t: tc } = useTranslation("common");
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
      .catch(() => setError(tl("settings.loadError")));
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
      toast.success(t("settings.updated", { key: editing.key }));
      closeEdit();
      load();
    } catch (e: unknown) {
      const message: string = serverMessage(e) ?? t("settings.saveError");
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
      <PageHeader title={t("settings.title")} subtitle={t("settings.subtitle")} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!settings && !error && <LoadingSpinner />}
      {settings?.some((s) => s.key === "System.MaintenanceMode") && <MaintenanceSection settings={settings} onChanged={load} />}

      {categories.map((category) => (
        <Paper key={category} sx={{ mb: 2, p: 2 }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6" component="h2">{t(`settings.categories.${category}`, { defaultValue: category })}</Typography>
            {category === "Alerts" && (
              <Button variant="outlined" size="small" onClick={() => setTestAlertOpen(true)}>{t("settings.sendTestAlert")}</Button>
            )}
          </Stack>
          <TableContainer>
            <Table size="small" aria-label={category}>
              <TableHead>
                <TableRow>
                  <TableCell>{t("settings.setting")}</TableCell>
                  <TableCell>{t("common.value")}</TableCell>
                  <TableCell>{t("settings.source")}</TableCell>
                  <TableCell align="right">{t("common.actions")}</TableCell>
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
                      {displayValue(s, t("settings.nobody"))}
                      {s.requiresSignature && <Chip size="small" label={t("settings.signatureChip")} sx={{ ml: 1 }} />}
                    </TableCell>
                    <TableCell>
                      {s.source}{s.version > 0 ? ` (v${s.version})` : ""}
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title={t("settings.change")}>
                        <IconButton size="small" aria-label={t("settings.editAria", { key: s.key })} onClick={() => openEdit(s)}><EditIcon fontSize="small" /></IconButton>
                      </Tooltip>
                      <Tooltip title={t("settings.history")}>
                        <IconButton size="small" aria-label={t("settings.historyAria", { key: s.key })} onClick={() => openHistory(s)}><HistoryIcon fontSize="small" /></IconButton>
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
          <Typography variant="h6" component="h2" sx={{ mb: 1 }}>{t("settings.notChanged")}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            {t("settings.notChangedNote")}
          </Typography>
          <TableContainer>
            <Table size="small" aria-label={t("settings.notChanged")}>
              <TableHead>
                <TableRow>
                  <TableCell>{t("settings.setting")}</TableCell>
                  <TableCell>{t("common.value")}</TableCell>
                  <TableCell>{t("settings.sourceAndHow")}</TableCell>
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
                        {f.note}{f.editLocation ? t("settings.editUnder", { location: f.editLocation }) : ""}
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
        title={t("settings.changeTitle", { key: editing?.key ?? t("settings.changeTitleFallback") })}
        actions={
          <>
            <Button onClick={closeEdit}>{tc("actions.cancel")}</Button>
            <Button variant="contained" onClick={onSaveClick} disabled={(editing?.valueType !== "EmailList" && !draft.trim()) || !reason.trim()}>
              {editing?.requiresSignature ? t("settings.continueToSign") : t("common.save")}
            </Button>
          </>
        }
      >
        {editing && (
          <Stack spacing={2} sx={{ pt: 1 }}>
            <Typography variant="body2">{editing.description}</Typography>
            <Typography variant="caption" color="text.secondary">
              {editing.valueType === "EmailList"
                ? t("settings.emailListLimit", { max: editing.max })
                : <>{t("settings.defaultValue", { value: withUnit(editing.defaultValue, editing.unit) })}
                  {editing.min !== null && editing.max !== null ? t("settings.allowed", { min: editing.min, max: editing.max }) : "."}</>}
            </Typography>
            {editing.valueType === "Boolean" ? (
              <Select size="small" value={draft} onChange={(e) => setDraft(String(e.target.value))} inputProps={{ "aria-label": t("common.value") }}>
                <MenuItem value="true">true</MenuItem>
                <MenuItem value="false">false</MenuItem>
              </Select>
            ) : editing.allowedValues ? (
              <Select size="small" value={draft} onChange={(e) => setDraft(String(e.target.value))} inputProps={{ "aria-label": t("common.value") }}>
                {editing.allowedValues.map((v) => <MenuItem key={v} value={v}>{v}</MenuItem>)}
              </Select>
            ) : editing.valueType === "EmailList" ? (
              <TextField
                label={t("common.value")} size="small" multiline minRows={3} value={draft} onChange={(e) => setDraft(e.target.value)}
                helperText={t("settings.emailListHelper")}
              />
            ) : (
              <TextField
                label={t("common.value")} size="small" value={draft} onChange={(e) => setDraft(e.target.value)}
                type={editing.valueType === "Integer" ? "number" : "text"}
              />
            )}
            <TextField
              label={t("common.reasonForChange")} size="small" required multiline minRows={2} value={reason}
              onChange={(e) => setReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }}
            />
            {editError && <Alert severity="error">{editError}</Alert>}
          </Stack>
        )}
      </FloatingDialog>

      <TestActionDialog
        open={testAlertOpen}
        title={t("settings.sendTestAlert")}
        description={t("settings.testAlertDescription")}
        confirmLabel={t("settings.sendTestAlert")}
        onClose={() => setTestAlertOpen(false)}
        onSubmit={async (reason) => <TestSendOutcome result={await IntegrationsService.sendTestAlert(reason)} />}
      />

      <SignatureDialog
        open={signing}
        meaningStatement={i18n.getFixedT("en", "admin")("settings.signatureMeaning", { key: editing?.key ?? "", oldValue: editing?.value ?? "", newValue: draft, reason: reason.trim() })}
        meaningStatementTranslation={t("settings.signatureMeaning", { key: editing?.key ?? "", oldValue: editing?.value ?? "", newValue: draft, reason: reason.trim() })}
        onCancel={() => setSigning(false)}
        onConfirm={save}
      />

      <Drawer anchor="right" open={Boolean(historyFor)} onClose={() => setHistoryFor(null)}>
        <Box sx={{ width: { xs: "100vw", sm: 480 }, p: 2 }} role="region" aria-label={t("settings.historyRegion")}>
          <Typography variant="h6" component="h2">{t("settings.history")}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{historyFor?.key}</Typography>
          {!history && <LoadingSpinner />}
          {history && history.length === 0 && <Typography variant="body2">{t("settings.noHistory")}</Typography>}
          {history?.map((h) => (
            <Paper key={h.version} variant="outlined" sx={{ p: 1.5, mb: 1 }}>
              <Typography variant="subtitle2">
                v{h.version}: {h.previousValue} {"->"} {h.value}
              </Typography>
              <Typography variant="caption" color="text.secondary" component="span" sx={{ display: "block" }}>
                {t("settings.changedBy", { name: h.changedByName ?? t("settings.userFallback", { id: h.changedByUserId }), date: formatDateTimeWithTimeZone(h.changedAt) })}
                {h.electronicSignatureId ? t("settings.eSigned") : ""}
              </Typography>
              <Typography variant="body2">{t("settings.reasonLine", { reason: h.reason })}</Typography>
            </Paper>
          ))}
        </Box>
      </Drawer>
    </>
  );
}
