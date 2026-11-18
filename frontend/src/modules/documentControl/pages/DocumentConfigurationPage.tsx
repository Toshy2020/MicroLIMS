import { monospaceFontFamily } from "../../../theme/palette";
import { compactChipSx } from "../documentControlStyles";
import { useState, useEffect } from "react";
import {
  Box,
  Typography,
  Paper,
  Tabs,
  Tab,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Switch,
  Chip,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Alert,
  CircularProgress,
  IconButton,
  Tooltip,
  FormControl,
  FormControlLabel,
  FormHelperText,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Link as RouterLink } from "react-router-dom";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { useTranslation } from "../../../i18n";

import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import { documentControlService } from "../services/documentControlService";
import type {
  DocumentTypeDto,
  DocumentDepartmentDto,
  DocumentNumberingConfigDto,
  ConfigurationSettingDto
} from "../types/documentControlTypes";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export function DocumentConfigurationPage() {
  const { t } = useTranslation("documentControl");
  const theme = useTheme();
  const { permissions } = useAuth();
  // Configuration is the Documents.ConfigManage permission (the System Administrator holds it by default), not a role.
  const canConfigure = permissions.includes(PERMISSIONS.DOCUMENTS_CONFIG_MANAGE);

  const [currentTab, setCurrentTab] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Config data
  const [types, setTypes] = useState<DocumentTypeDto[]>([]);
  const [departments, setDepartments] = useState<DocumentDepartmentDto[]>([]);
  const [numbering, setNumbering] = useState<DocumentNumberingConfigDto | null>(null);
  const [settings, setSettings] = useState<ConfigurationSettingDto[]>([]);

  // Dialog states
  const [typeDialogOpen, setTypeDialogOpen] = useState(false);
  const [editingType, setEditingType] = useState<DocumentTypeDto | null>(null);
  const [typeForm, setTypeForm] = useState({ code: "", name: "", cycle: 24, isActive: true, requiresQaApprover: false });

  const [numberingForm, setNumberingForm] = useState({ prefix: "DOC-", format: "0000000", isEnabled: true });
  const [savingNumbering, setSavingNumbering] = useState(false);

  const [settingDialogOpen, setSettingDialogOpen] = useState(false);
  const [editingSetting, setEditingSetting] = useState<ConfigurationSettingDto | null>(null);
  const [settingValue, setSettingValue] = useState("");

  const loadAll = async () => {
    setLoading(true);
    setError(null);
    try {
      const [tRes, dRes, nRes, sRes] = await Promise.all([
        documentControlService.getTypes(true),
        documentControlService.getDepartments(true),
        documentControlService.getNumberingConfig(),
        documentControlService.getSettings()
      ]);
      setTypes(tRes);
      setDepartments(dRes);
      setNumbering(nRes);
      setNumberingForm({
        prefix: nRes.prefix,
        format: nRes.numberFormat,
        isEnabled: nRes.isEnabled
      });
      setSettings(sRes);
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, t("config.loadError")));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    // Without the permission the server would only answer 403.
    if (canConfigure) loadAll();
  }, [canConfigure]);

  if (!canConfigure) {
    return (
      <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
        <Alert severity="error">
          <Trans
            i18nKey="config.accessDenied"
            ns="documentControl"
            components={{ strong: <strong /> }}
          />
        </Alert>
      </Box>
    );
  }

  // Type actions
  const handleSaveType = async () => {
    try {
      if (editingType) {
        await documentControlService.updateType(editingType.id, {
          name: typeForm.name,
          defaultReviewCycleMonths: Number(typeForm.cycle),
          isActive: typeForm.isActive,
          requiresQaApprover: typeForm.requiresQaApprover
        }, editingType.version);
      } else {
        await documentControlService.createType({
          code: typeForm.code,
          name: typeForm.name,
          defaultReviewCycleMonths: Number(typeForm.cycle),
          requiresQaApprover: typeForm.requiresQaApprover
        });
      }
      setTypeDialogOpen(false);
      loadAll();
      toast.success(t("config.types.savedToast"));
    } catch (err: unknown) {
      toast.error(t("config.types.saveFailedToast", { error: serverMessage(err) ?? getErrorMessage(err, t("common.unknownError")) }));
    }
  };

  // Numbering actions
  const handleSaveNumbering = async () => {
    setSavingNumbering(true);
    try {
      const updated = await documentControlService.updateNumberingConfig({
        prefix: numberingForm.prefix,
        numberFormat: numberingForm.format,
        isEnabled: numberingForm.isEnabled
      });
      setNumbering(updated);
      toast.success(t("config.numbering.updatedToast"));
    } catch (err: unknown) {
      toast.error(t("config.numbering.updateFailedToast", { error: serverMessage(err) ?? getErrorMessage(err, t("common.unknownError")) }));
    } finally {
      setSavingNumbering(false);
    }
  };

  // Setting actions
  const handleSaveSetting = async () => {
    if (!editingSetting) return;
    try {
      await documentControlService.updateSetting(editingSetting.settingKey, settingValue);
      setSettingDialogOpen(false);
      loadAll();
      toast.success(t("config.settings.updatedToast"));
    } catch (err: unknown) {
      toast.error(t("config.settings.updateFailedToast", { error: serverMessage(err) ?? getErrorMessage(err, t("common.unknownError")) }));
    }
  };

  return (
    <Box sx={{ pb: 4 }}>
      <PageHeader
        title={t("config.title")}
        subtitle={t("config.subtitle")}
      />

      {error && <Alert severity="error" sx={{ mb: 3 }}>{error}</Alert>}

      <Paper sx={{ mb: 3 }}>
        <Tabs
          value={currentTab}
          onChange={(_, val) => setCurrentTab(val)}
          indicatorColor="primary"
          textColor="primary"
          sx={{ borderBottom: 1, borderColor: "divider", px: 2 }}
        >
          <Tab label={t("config.tabs.documentTypes", { count: types.length })} />
          <Tab label={t("config.tabs.departmentsAndSections", { count: departments.length })} />
          <Tab label={t("config.tabs.numberingConfiguration")} />
          <Tab label={t("config.tabs.moduleSettings", { count: settings.length })} />
        </Tabs>

        {loading ? (
          <Box sx={{ p: 6, display: "flex", justifyContent: "center" }}>
            <CircularProgress />
          </Box>
        ) : (
          <>
            {/* Tab 0: Document Types */}
            {currentTab === 0 && (
              <Box sx={{ p: 3 }}>
                <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                    {t("config.types.title")}
                  </Typography>
                  <Button
                    size="small"
                    variant="contained"
                    startIcon={<AddIcon />}
                    onClick={() => {
                      setEditingType(null);
                      setTypeForm({ code: "", name: "", cycle: 24, isActive: true, requiresQaApprover: false });
                      setTypeDialogOpen(true);
                    }}
                  >
                    {t("config.types.addButton")}
                  </Button>
                </Box>

                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.types.columns.code")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.types.columns.name")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.types.columns.defaultReviewCycle")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.types.columns.qaApprover")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.types.columns.status")}</TableCell>
                        <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>{t("config.types.columns.actions")}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {types.map((docType) => (
                        <TableRow key={docType.id} hover sx={{ opacity: docType.isActive ? 1 : 0.6 }}>
                          <TableCell sx={{ fontWeight: 700 }}>{docType.code}</TableCell>
                          <TableCell>{docType.name}</TableCell>
                          <TableCell>{t("common.months", { count: docType.defaultReviewCycleMonths })}</TableCell>
                          <TableCell>
                            {docType.requiresQaApprover ? (
                              <Chip label={t("config.types.required")} size="small" color="primary" variant="outlined" sx={compactChipSx} />
                            ) : (
                              <Typography variant="body2" color="text.secondary">
                                {t("config.types.notRequired")}
                              </Typography>
                            )}
                          </TableCell>
                          <TableCell>
                            {docType.isActive ? (
                              <Chip label={t("common.active")} size="small" color="success" sx={compactChipSx} />
                            ) : (
                              <Chip label={t("common.deactivated")} size="small" color="default" sx={compactChipSx} />
                            )}
                          </TableCell>
                          <TableCell align="right">
                            <Tooltip title={t("config.types.editTooltip")}>
                              <IconButton aria-label={t("config.types.editAria", { code: docType.code })}
                                size="small"
                                onClick={() => {
                                  setEditingType(docType);
                                  setTypeForm({
                                    code: docType.code,
                                    name: docType.name,
                                    cycle: docType.defaultReviewCycleMonths,
                                    isActive: docType.isActive,
                                    requiresQaApprover: Boolean(docType.requiresQaApprover)
                                  });
                                  setTypeDialogOpen(true);
                                }}
                              >
                                <EditOutlinedIcon fontSize="small" />
                              </IconButton>
                            </Tooltip>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Box>
            )}

            {/* Tab 1: Departments & Sections - edited in Administration > Organization (one editor, with reasons and deactivation checks) */}
            {currentTab === 1 && (
              <Box sx={{ p: 3, maxWidth: 650 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
                  {t("config.departments.title")}
                </Typography>
                <Alert severity="info" sx={{ mb: 2 }}>
                  {t("config.departments.info", { count: departments.length })}
                </Alert>
                <Button component={RouterLink} to="/admin/organization" variant="contained">
                  {t("config.departments.openOrganization")}
                </Button>
              </Box>
            )}

            {/* Tab 2: Numbering Configuration */}
            {currentTab === 2 && numbering && (
              <Box sx={{ p: 3, maxWidth: 650 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
                  {t("config.numbering.title")}
                </Typography>
                <Alert severity="info" sx={{ mb: 3 }}>
                  <Trans
                    i18nKey="config.numbering.info"
                    ns="documentControl"
                    components={{ strong: <strong />, code: <code /> }}
                  />
                </Alert>

                <Paper variant="outlined" sx={{ p: 2.5, mb: 3 }}>
                  <Typography
                    variant="caption"
                    sx={{
                      color: "text.secondary",
                      fontWeight: 700,
                      textTransform: "uppercase"
                    }}>
                    {t("config.numbering.sampleTitle")}
                  </Typography>
                  <Typography variant="h5" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700, color: "primary.main", my: 1 }}>
                    {numbering.sampleNextId}
                  </Typography>
                  <Typography variant="caption" sx={{
                    color: "text.secondary"
                  }}>
                    {t("config.numbering.lastModifiedBy", { user: numbering.modifiedByUserName, date: formatDateTimeWithTimeZone(numbering.modifiedAt) })}
                  </Typography>
                </Paper>

                <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
                  <TextField
                    label={t("config.numbering.prefixLabel")}
                    value={numberingForm.prefix}
                    onChange={(e) => setNumberingForm({ ...numberingForm, prefix: e.target.value })}
                    helperText={t("config.numbering.prefixHelper")}
                  />

                  <TextField
                    label={t("config.numbering.formatLabel")}
                    value={numberingForm.format}
                    onChange={(e) => setNumberingForm({ ...numberingForm, format: e.target.value })}
                    helperText={t("config.numbering.formatHelper")}
                  />

                  <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", p: 1.5, bgcolor: (t) => t.palette.mode === "dark" ? "action.hover" : "grey.50", borderRadius: 1 }}>
                    <Box>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>{t("config.numbering.enableTitle")}</Typography>
                      <Typography variant="caption" sx={{
                        color: "text.secondary"
                      }}>{t("config.numbering.enableSubtitle")}</Typography>
                    </Box>
                    <Switch
                      checked={numberingForm.isEnabled}
                      onChange={(e) => setNumberingForm({ ...numberingForm, isEnabled: e.target.checked })}
                    />
                  </Box>

                  <Button
                    variant="contained"
                    onClick={handleSaveNumbering}
                    disabled={savingNumbering || !numberingForm.prefix.trim() || !numberingForm.format.trim()}
                  >
                    {savingNumbering ? t("config.numbering.saving") : t("config.numbering.save")}
                  </Button>
                </Box>
              </Box>
            )}

            {/* Tab 3: Module Settings */}
            {currentTab === 3 && (
              <Box sx={{ p: 3 }}>
                <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                  {t("config.settings.title")}
                </Typography>

                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead sx={tableHeadSx(theme)}>
                      <TableRow>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.settings.columns.group")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.settings.columns.settingKey")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.settings.columns.value")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.settings.columns.dataType")}</TableCell>
                        <TableCell sx={{ fontWeight: 700 }}>{t("config.settings.columns.modifiedBy")}</TableCell>
                        <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>{t("config.settings.columns.action")}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {settings.map((s) => (
                        <TableRow key={s.id} hover>
                          <TableCell sx={{ fontWeight: 600 }}>{s.settingGroup}</TableCell>
                          <TableCell sx={{ fontFamily: monospaceFontFamily }}>{s.settingKey}</TableCell>
                          <TableCell>
                            <Chip label={s.settingValue} size="small" variant="outlined" />
                          </TableCell>
                          <TableCell>{s.dataType}</TableCell>
                          <TableCell>
                            <Typography variant="caption" sx={{
                              display: "block"
                            }}>{s.modifiedByUserName || t("common.system")}</Typography>
                            <Typography variant="caption" sx={{
                              color: "text.secondary"
                            }}>{formatDateTimeWithTimeZone(s.modifiedAt)}</Typography>
                          </TableCell>
                          <TableCell align="right">
                            <IconButton aria-label={t("config.settings.editAria", { key: s.settingKey })}
                              size="small"
                              onClick={() => {
                                setEditingSetting(s);
                                setSettingValue(s.settingValue);
                                setSettingDialogOpen(true);
                              }}
                            >
                              <EditOutlinedIcon fontSize="small" />
                            </IconButton>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </Box>
            )}
          </>
        )}
      </Paper>

      {/* Document Type Dialog */}
      <Dialog open={typeDialogOpen} onClose={() => setTypeDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{editingType ? t("config.types.dialog.editTitle") : t("config.types.dialog.addTitle")}</DialogTitle>
        <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {!editingType && (
            <TextField
              label={t("config.types.dialog.codeLabel")}
              value={typeForm.code}
              onChange={(e) => setTypeForm({ ...typeForm, code: e.target.value })}
              placeholder={t("config.types.dialog.codePlaceholder")}
            />
          )}
          <TextField
            label={t("config.types.dialog.nameLabel")}
            value={typeForm.name}
            onChange={(e) => setTypeForm({ ...typeForm, name: e.target.value })}
            placeholder={t("config.types.dialog.namePlaceholder")}
          />
          <TextField
            label={t("config.types.dialog.cycleLabel")}
            type="number"
            value={typeForm.cycle}
            onChange={(e) => setTypeForm({ ...typeForm, cycle: Number(e.target.value) })}
          />
          <FormControl component="fieldset" variant="standard">
            <FormControlLabel
              control={
                <Switch
                  checked={typeForm.requiresQaApprover}
                  onChange={(e) => setTypeForm({ ...typeForm, requiresQaApprover: e.target.checked })}
                />
              }
              label={t("config.types.dialog.requiresQaLabel")}
            />
            <FormHelperText>
              {t("config.types.dialog.requiresQaHelper")}
            </FormHelperText>
          </FormControl>
          {editingType && (
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <Typography variant="body2">{t("config.types.dialog.activeStatus")}</Typography>
              <Switch
                checked={typeForm.isActive}
                onChange={(e) => setTypeForm({ ...typeForm, isActive: e.target.checked })}
              />
            </Box>
          )}
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setTypeDialogOpen(false)} color="inherit">{t("common.cancel")}</Button>
          <Button onClick={handleSaveType} variant="contained">{t("common.save")}</Button>
        </DialogActions>
      </Dialog>

      {/* Setting Dialog */}
      <Dialog open={settingDialogOpen} onClose={() => setSettingDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t("config.settings.dialogTitle")}</DialogTitle>
        <DialogContent dividers sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <Typography variant="caption" sx={{
            color: "text.secondary"
          }}>
            <Trans
              i18nKey="config.settings.keyLine"
              ns="documentControl"
              values={{ key: editingSetting?.settingKey }}
              components={{ strong: <strong /> }}
            />
          </Typography>
          <TextField
            label={t("config.settings.valueLabel")}
            value={settingValue}
            onChange={(e) => setSettingValue(e.target.value)}
            fullWidth
          />
        </DialogContent>
        <DialogActions sx={{ px: 3, py: 2 }}>
          <Button onClick={() => setSettingDialogOpen(false)} color="inherit">{t("common.cancel")}</Button>
          <Button onClick={handleSaveSetting} variant="contained">{t("config.settings.saveButton")}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
