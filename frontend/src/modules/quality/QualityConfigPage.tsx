import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  IconButton,
  MenuItem,
  Paper,
  Skeleton,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography
} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useHasPermission } from "../../contexts/AuthContext";
import { useTranslation } from "../../i18n";
import { PERMISSIONS } from "../../routes/routes";
import { getMySections, type LaboratorySection } from "../../services/laboratorySectionService";
import {
  qualityConfigService,
  type QualityChecklistItemDto,
  type QualityChecklistTemplateDto,
  type QualityDueRuleDto,
  type QualitySectionSettingDto
} from "../../services/qualityConfigService";
import {
  QUALITY_RECORD_TYPES,
  type QualityClassificationDto,
  type QualityRecordType
} from "../../services/qualityService";
import { serverMessage } from "../../utils/errorMessage";

type ConfigTab = "templates" | "classifications" | "dueRules" | "sectionSettings";

export function QualityConfigPage() {
  const { t } = useTranslation("qualityConfig");
  const canApprove = useHasPermission(PERMISSIONS.QUALITY_APPROVE);
  const canInvestigate = useHasPermission(PERMISSIONS.QUALITY_INVESTIGATE);

  const [tab, setTab] = useState<ConfigTab>("templates");
  const [sections, setSections] = useState<LaboratorySection[]>([]);

  // Templates state
  const [templateType, setTemplateType] = useState<QualityRecordType>("Deviation");
  const [templateSectionId, setTemplateSectionId] = useState<number | "">("");
  const [template, setTemplate] = useState<QualityChecklistTemplateDto | null>(null);
  const [templateItems, setTemplateItems] = useState<QualityChecklistItemDto[]>([]);
  const [templateLoading, setTemplateLoading] = useState(true);
  const [templateError, setTemplateError] = useState<string | null>(null);
  const [templateSaving, setTemplateSaving] = useState(false);

  // Classifications state
  const [classifications, setClassifications] = useState<QualityClassificationDto[]>([]);
  const [classLoading, setClassLoading] = useState(true);
  const [classError, setClassError] = useState<string | null>(null);
  const [newClassName, setNewClassName] = useState("");
  const [newClassOrder, setNewClassOrder] = useState(1);
  const [classSavingId, setClassSavingId] = useState<number | "new" | null>(null);

  // Due rules state
  const [dueRules, setDueRules] = useState<QualityDueRuleDto[]>([]);
  const [dueLoading, setDueLoading] = useState(true);
  const [dueError, setDueError] = useState<string | null>(null);
  const [dueType, setDueType] = useState<QualityRecordType>("Deviation");
  const [dueClassificationId, setDueClassificationId] = useState<number | "">("");
  const [dueWorkingDays, setDueWorkingDays] = useState<string>("");
  const [dueSaving, setDueSaving] = useState(false);

  // Section settings state
  const [selectedSectionId, setSelectedSectionId] = useState<number | "">("");
  const [sectionSetting, setSectionSetting] = useState<QualitySectionSettingDto | null>(null);
  const [gateRequired, setGateRequired] = useState(false);
  const [gateReason, setGateReason] = useState("");
  const [gateLoading, setGateLoading] = useState(false);
  const [gateError, setGateError] = useState<string | null>(null);
  const [gateSigOpen, setGateSigOpen] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    getMySections(controller.signal)
      .then((list) => {
        if (controller.signal.aborted) return;
        setSections(list);
        if (list.length > 0) {
          setSelectedSectionId((prev) => (prev === "" ? list[0].sectionId : prev));
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) setSections([]);
      });
    return () => controller.abort();
  }, []);

  const loadTemplate = useCallback(
    (signal?: AbortSignal) => {
      setTemplateLoading(true);
      setTemplateError(null);
      const sid = templateSectionId === "" ? null : templateSectionId;
      return qualityConfigService
        .getTemplate(templateType, sid, signal)
        .then((dto) => {
          if (signal?.aborted) return;
          setTemplate(dto);
          setTemplateItems(dto.items);
          setTemplateLoading(false);
        })
        .catch((e: unknown) => {
          if (signal?.aborted) return;
          setTemplateError(serverMessage(e) ?? t("config.templates.loadError"));
          setTemplateLoading(false);
        });
    },
    [templateType, templateSectionId, t]
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadTemplate(controller.signal);
    return () => controller.abort();
  }, [loadTemplate]);

  const loadClassifications = useCallback(
    (signal?: AbortSignal) => {
      setClassLoading(true);
      setClassError(null);
      return qualityConfigService
        .listClassifications(1, 100, signal)
        .then((items) => {
          if (signal?.aborted) return;
          setClassifications(items);
          setClassLoading(false);
        })
        .catch((e: unknown) => {
          if (signal?.aborted) return;
          setClassError(serverMessage(e) ?? t("config.classifications.loadError"));
          setClassLoading(false);
        });
    },
    [t]
  );

  const loadDueRules = useCallback(
    (signal?: AbortSignal) => {
      setDueLoading(true);
      setDueError(null);
      return qualityConfigService
        .listDueRules(1, 100, signal)
        .then((items) => {
          if (signal?.aborted) return;
          setDueRules(items);
          setDueLoading(false);
        })
        .catch((e: unknown) => {
          if (signal?.aborted) return;
          setDueError(serverMessage(e) ?? t("config.dueRules.loadError"));
          setDueLoading(false);
        });
    },
    [t]
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadClassifications(controller.signal);
    void loadDueRules(controller.signal);
    return () => controller.abort();
  }, [loadClassifications, loadDueRules]);

  const loadSectionSetting = useCallback(
    (sid: number, signal?: AbortSignal) => {
      setGateLoading(true);
      setGateError(null);
      return qualityConfigService
        .getSectionSetting(sid, signal)
        .then((dto) => {
          if (signal?.aborted) return;
          setSectionSetting(dto);
          setGateRequired(dto.changeControlRequired);
          setGateLoading(false);
        })
        .catch((e: unknown) => {
          if (signal?.aborted) return;
          setGateError(serverMessage(e) ?? t("config.sectionSettings.loadError"));
          setGateLoading(false);
        });
    },
    [t]
  );

  useEffect(() => {
    if (selectedSectionId === "") return;
    const controller = new AbortController();
    void loadSectionSetting(selectedSectionId, controller.signal);
    return () => controller.abort();
  }, [selectedSectionId, loadSectionSetting]);

  useEffect(() => {
    const cid = dueClassificationId === "" ? null : dueClassificationId;
    const match = dueRules.find((r) => r.type === dueType && r.classificationId === cid);
    setDueWorkingDays(match?.workingDays != null ? String(match.workingDays) : "");
  }, [dueType, dueClassificationId, dueRules]);

  // The server (QualityConfigService.SaveTemplateAsync) takes the site-wide template from Quality.Approve and a
  // section template from Quality.Investigate (within the sections the user may configure).
  const canEditTemplate = templateSectionId === "" ? canApprove : canInvestigate;

  const handleAddTemplateItem = () => {
    const nextOrder = templateItems.length + 1;
    setTemplateItems((prev) => [
      ...prev,
      {
        key: `item-${Date.now()}-${nextOrder}`,
        order: nextOrder,
        text: "",
        isActive: true
      }
    ]);
  };

  const handleSaveTemplate = async () => {
    if (!template) return;
    setTemplateSaving(true);
    setTemplateError(null);
    const sid = templateSectionId === "" ? null : templateSectionId;
    try {
      const normalized = templateItems.map((item, idx) => ({
        ...item,
        order: idx + 1,
        text: item.text.trim()
      }));
      const saved = await qualityConfigService.saveTemplate(
        templateType,
        sid,
        { items: normalized },
        template.version
      );
      setTemplate(saved);
      setTemplateItems(saved.items);
      toast.success(t("config.templates.saved"));
    } catch (e: unknown) {
      setTemplateError(serverMessage(e) ?? t("config.templates.saveError"));
    } finally {
      setTemplateSaving(false);
    }
  };

  const handleCreateClassification = async () => {
    if (!newClassName.trim()) return;
    setClassSavingId("new");
    setClassError(null);
    try {
      await qualityConfigService.createClassification(
        { name: newClassName.trim(), order: newClassOrder, isActive: true },
        0
      );
      setNewClassName("");
      toast.success(t("config.classifications.saved"));
      await loadClassifications();
    } catch (e: unknown) {
      setClassError(serverMessage(e) ?? t("config.classifications.saveError"));
    } finally {
      setClassSavingId(null);
    }
  };

  const handleSaveClassification = async (row: QualityClassificationDto) => {
    setClassSavingId(row.id);
    setClassError(null);
    try {
      await qualityConfigService.saveClassification(
        row.id,
        { name: row.name.trim(), order: row.order, isActive: row.isActive },
        row.version
      );
      toast.success(t("config.classifications.saved"));
      await loadClassifications();
    } catch (e: unknown) {
      setClassError(serverMessage(e) ?? t("config.classifications.saveError"));
    } finally {
      setClassSavingId(null);
    }
  };

  const handleSaveDueRule = async () => {
    setDueSaving(true);
    setDueError(null);
    const cid = dueClassificationId === "" ? null : dueClassificationId;
    const existing = dueRules.find((r) => r.type === dueType && r.classificationId === cid);
    const parsedDays = dueWorkingDays.trim() === "" ? null : Number(dueWorkingDays);
    try {
      await qualityConfigService.saveDueRule(
        dueType,
        { classificationId: cid, workingDays: parsedDays },
        existing?.version ?? 0
      );
      toast.success(t("config.dueRules.saved"));
      await loadDueRules();
    } catch (e: unknown) {
      setDueError(serverMessage(e) ?? t("config.dueRules.saveError"));
    } finally {
      setDueSaving(false);
    }
  };

  const handleConfirmSectionGate = async (password: string) => {
    if (selectedSectionId === "" || !sectionSetting) return;
    try {
      const updated = await qualityConfigService.setSectionSetting(
        selectedSectionId,
        {
          changeControlRequired: gateRequired,
          reason: gateReason.trim(),
          password
        },
        sectionSetting.version
      );
      setSectionSetting(updated);
      setGateRequired(updated.changeControlRequired);
      setGateReason("");
      setGateError(null);
      setGateSigOpen(false);
      toast.success(t("config.sectionSettings.saved"));
    } catch (e: unknown) {
      const status = (e as { response?: { status?: number } } | null)?.response?.status;
      if (status === 409) {
        setGateSigOpen(false);
        setGateError(serverMessage(e) ?? t("config.sectionSettings.saveError"));
        return;
      }
      throw e;
    }
  };

  return (
    <Box>
      <PageHeader title={t("config.title")} subtitle={t("config.subtitle")} />

      <Tabs
        value={tab}
        onChange={(_, v: ConfigTab) => setTab(v)}
        sx={{ borderBottom: 1, borderColor: "divider", mb: 3 }}
      >
        <Tab value="templates" label={t("config.tabs.templates")} />
        <Tab value="classifications" label={t("config.tabs.classifications")} />
        <Tab value="dueRules" label={t("config.tabs.dueRules")} />
        <Tab value="sectionSettings" label={t("config.tabs.sectionSettings")} />
      </Tabs>

      {tab === "templates" && (
        <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
          <Stack spacing={2}>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
              <TextField
                select
                size="small"
                label={t("config.templates.typeLabel")}
                value={templateType}
                onChange={(e) => setTemplateType(e.target.value as QualityRecordType)}
                sx={{ minWidth: 200 }}
              >
                {QUALITY_RECORD_TYPES.map((rt) => (
                  <MenuItem key={rt} value={rt}>
                    {t(`config.recordTypes.${rt}`)}
                  </MenuItem>
                ))}
              </TextField>

              <TextField
                select
                size="small"
                label={t("config.templates.scopeLabel")}
                value={templateSectionId}
                onChange={(e) =>
                  setTemplateSectionId(e.target.value === "" ? "" : Number(e.target.value))
                }
                sx={{ minWidth: 240 }}
              >
                <MenuItem value="">{t("config.templates.siteWide")}</MenuItem>
                {sections.map((s) => (
                  <MenuItem key={s.sectionId} value={s.sectionId}>
                    {s.sectionName}
                  </MenuItem>
                ))}
              </TextField>
            </Stack>

            {!canEditTemplate && (
              <Alert severity="info">
                {templateSectionId === ""
                  ? t("config.templates.readOnlySiteWide")
                  : t("config.templates.readOnlySection")}
              </Alert>
            )}
            {templateError && <Alert severity="error">{templateError}</Alert>}

            {templateLoading ? (
              <Skeleton variant="rounded" height={120} />
            ) : templateItems.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                {t("config.templates.empty")}
              </Typography>
            ) : (
              <Stack spacing={1.5}>
                {templateItems.map((item, idx) => (
                  <Stack
                    key={item.key}
                    direction={{ xs: "column", sm: "row" }}
                    spacing={1.5}
                    sx={{ alignItems: { sm: "center" } }}
                  >
                    <TextField
                      size="small"
                      fullWidth
                      label={t("config.templates.itemTextLabel")}
                      value={item.text}
                      disabled={!canEditTemplate}
                      onChange={(e) => {
                        const val = e.target.value;
                        setTemplateItems((prev) =>
                          prev.map((row, i) => (i === idx ? { ...row, text: val } : row))
                        );
                      }}
                    />
                    <FormControlLabel
                      control={
                        <Checkbox
                          checked={item.isActive}
                          disabled={!canEditTemplate}
                          onChange={(e) => {
                            const checked = e.target.checked;
                            setTemplateItems((prev) =>
                              prev.map((row, i) => (i === idx ? { ...row, isActive: checked } : row))
                            );
                          }}
                        />
                      }
                      label={t("config.templates.activeLabel")}
                    />
                    {canEditTemplate && (
                      <IconButton
                        aria-label={t("config.templates.removeItem")}
                        onClick={() =>
                          setTemplateItems((prev) => prev.filter((_, i) => i !== idx))
                        }
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    )}
                  </Stack>
                ))}
              </Stack>
            )}

            {canEditTemplate && (
              <Stack direction="row" spacing={1.5}>
                <Button
                  variant="outlined"
                  startIcon={<AddIcon />}
                  onClick={handleAddTemplateItem}
                  disabled={templateLoading || templateSaving}
                >
                  {t("config.templates.addItem")}
                </Button>
                <Button
                  variant="contained"
                  onClick={() => void handleSaveTemplate()}
                  disabled={templateLoading || templateSaving || !template}
                >
                  {templateSaving ? t("config.templates.saving") : t("config.templates.save")}
                </Button>
              </Stack>
            )}
          </Stack>
        </Paper>
      )}

      {tab === "classifications" && (
        <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
          <Stack spacing={2}>
            {!canApprove && <Alert severity="info">{t("config.classifications.readOnly")}</Alert>}
            {classError && <Alert severity="error">{classError}</Alert>}

            {classLoading ? (
              <Skeleton variant="rounded" height={120} />
            ) : (
              <Stack spacing={1.5}>
                {classifications.map((row) => (
                  <Stack
                    key={row.id}
                    direction={{ xs: "column", sm: "row" }}
                    spacing={1.5}
                    sx={{ alignItems: { sm: "center" } }}
                  >
                    <TextField
                      size="small"
                      label={t("config.classifications.nameLabel")}
                      value={row.name}
                      disabled={!canApprove}
                      onChange={(e) => {
                        const val = e.target.value;
                        setClassifications((prev) =>
                          prev.map((c) => (c.id === row.id ? { ...c, name: val } : c))
                        );
                      }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label={t("config.classifications.orderLabel")}
                      value={row.order}
                      disabled={!canApprove}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setClassifications((prev) =>
                          prev.map((c) => (c.id === row.id ? { ...c, order: val } : c))
                        );
                      }}
                      sx={{ width: 120 }}
                    />
                    <FormControlLabel
                      control={
                        <Checkbox
                          checked={row.isActive}
                          disabled={!canApprove}
                          onChange={(e) => {
                            const checked = e.target.checked;
                            setClassifications((prev) =>
                              prev.map((c) => (c.id === row.id ? { ...c, isActive: checked } : c))
                            );
                          }}
                        />
                      }
                      label={t("config.classifications.activeLabel")}
                    />
                    {canApprove && (
                      <Button
                        variant="outlined"
                        size="small"
                        disabled={classSavingId === row.id || !row.name.trim()}
                        onClick={() => void handleSaveClassification(row)}
                      >
                        {t("config.classifications.save")}
                      </Button>
                    )}
                  </Stack>
                ))}
              </Stack>
            )}

            {canApprove && (
              <Stack
                direction={{ xs: "column", sm: "row" }}
                spacing={1.5}
                sx={{ pt: 1, borderTop: 1, borderColor: "divider", alignItems: { sm: "center" } }}
              >
                <TextField
                  size="small"
                  label={t("config.classifications.add")}
                  value={newClassName}
                  onChange={(e) => setNewClassName(e.target.value)}
                />
                <TextField
                  size="small"
                  type="number"
                  label={t("config.classifications.orderLabel")}
                  value={newClassOrder}
                  onChange={(e) => setNewClassOrder(Number(e.target.value))}
                  sx={{ width: 120 }}
                />
                <Button
                  variant="contained"
                  disabled={!newClassName.trim() || classSavingId === "new"}
                  onClick={() => void handleCreateClassification()}
                >
                  {t("config.classifications.create")}
                </Button>
              </Stack>
            )}
          </Stack>
        </Paper>
      )}

      {tab === "dueRules" && (
        <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
          <Stack spacing={2}>
            {!canApprove && <Alert severity="info">{t("config.dueRules.readOnly")}</Alert>}
            {dueError && <Alert severity="error">{dueError}</Alert>}

            {dueLoading ? (
              <Skeleton variant="rounded" height={100} />
            ) : (
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "center" } }}>
                <TextField
                  select
                  size="small"
                  label={t("config.dueRules.typeLabel")}
                  value={dueType}
                  onChange={(e) => setDueType(e.target.value as QualityRecordType)}
                  sx={{ minWidth: 180 }}
                >
                  {QUALITY_RECORD_TYPES.map((rt) => (
                    <MenuItem key={rt} value={rt}>
                      {t(`config.recordTypes.${rt}`)}
                    </MenuItem>
                  ))}
                </TextField>

                <TextField
                  select
                  size="small"
                  label={t("config.dueRules.classificationLabel")}
                  value={dueClassificationId}
                  onChange={(e) =>
                    setDueClassificationId(e.target.value === "" ? "" : Number(e.target.value))
                  }
                  sx={{ minWidth: 220 }}
                >
                  <MenuItem value="">{t("config.dueRules.allClassifications")}</MenuItem>
                  {classifications.map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.name}
                    </MenuItem>
                  ))}
                </TextField>

                <TextField
                  size="small"
                  type="number"
                  label={t("config.dueRules.workingDaysLabel")}
                  value={dueWorkingDays}
                  disabled={!canApprove}
                  onChange={(e) => setDueWorkingDays(e.target.value)}
                  sx={{ width: 150 }}
                />

                {canApprove && (
                  <Button
                    variant="contained"
                    disabled={dueSaving}
                    onClick={() => void handleSaveDueRule()}
                  >
                    {t("config.dueRules.save")}
                  </Button>
                )}
              </Stack>
            )}
          </Stack>
        </Paper>
      )}

      {tab === "sectionSettings" && (
        <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
          <Stack spacing={2}>
            {!canApprove && <Alert severity="info">{t("config.sectionSettings.readOnly")}</Alert>}

            <TextField
              select
              size="small"
              label={t("config.sectionSettings.sectionLabel")}
              value={selectedSectionId}
              onChange={(e) => setSelectedSectionId(Number(e.target.value))}
              sx={{ maxWidth: 300 }}
            >
              {sections.map((s) => (
                <MenuItem key={s.sectionId} value={s.sectionId}>
                  {s.sectionName}
                </MenuItem>
              ))}
            </TextField>

            {gateError && (
              <Alert
                severity="error"
                action={
                  selectedSectionId !== "" ? (
                    <Button
                      size="small"
                      color="inherit"
                      onClick={() => void loadSectionSetting(selectedSectionId)}
                    >
                      {t("config.sectionSettings.reload")}
                    </Button>
                  ) : undefined
                }
              >
                {gateError}
              </Alert>
            )}

            {gateLoading ? (
              <Skeleton variant="rounded" height={96} />
            ) : (
              <>
                <FormControlLabel
                  control={
                    <Switch
                      checked={gateRequired}
                      disabled={!canApprove}
                      onChange={(e) => setGateRequired(e.target.checked)}
                    />
                  }
                  label={t("config.sectionSettings.gateLabel")}
                />

                {canApprove && (
                  <>
                    <TextField
                      size="small"
                      label={t("config.sectionSettings.reasonLabel")}
                      value={gateReason}
                      onChange={(e) => setGateReason(e.target.value)}
                      multiline
                      minRows={2}
                      required
                      fullWidth
                      slotProps={{ htmlInput: { maxLength: 4000 } }}
                    />
                    <Box>
                      <Button
                        variant="contained"
                        disabled={!gateReason.trim() || !sectionSetting}
                        onClick={() => setGateSigOpen(true)}
                      >
                        {t("config.sectionSettings.save")}
                      </Button>
                    </Box>
                  </>
                )}
              </>
            )}
          </Stack>
        </Paper>
      )}

      <SignatureDialog
        open={gateSigOpen}
        meaningStatement={t("config.sectionSettings.meaningStatement", { lng: "en" })}
        meaningStatementTranslation={t("config.sectionSettings.meaningTranslation")}
        onCancel={() => setGateSigOpen(false)}
        onConfirm={handleConfirmSectionGate}
      />
    </Box>
  );
}
