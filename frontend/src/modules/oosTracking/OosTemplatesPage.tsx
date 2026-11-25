import { useCallback, useEffect, useState, type KeyboardEvent } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  Tab,
  Tabs,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import AddOutlinedIcon from "@mui/icons-material/AddOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { useTranslation } from "../../i18n";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../services/laboratorySectionService";
import {
  oosInvestigationService,
  type OosCategoryDto,
  type OosInvestigationKind,
  type OosTemplateDto,
  type OosTemplateItem
} from "../../services/oosInvestigationService";
import { serverMessage } from "../../utils/errorMessage";

type TemplatesTab = "templates" | "categories";

export function OosTemplatesPage() {
  const { t } = useTranslation("oosTracking");

  const [tab, setTab] = useState<TemplatesTab>("templates");

  // Sections & template selection
  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [sectionsLoading, setSectionsLoading] = useState(true);
  const [sectionId, setSectionId] = useState<number | "">("");
  const [kind, setKind] = useState<OosInvestigationKind>("Oos");

  // Template editor state
  const [template, setTemplate] = useState<OosTemplateDto | null>(null);
  const [templateLoading, setTemplateLoading] = useState(false);
  const [templateError, setTemplateError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [savingTemplate, setSavingTemplate] = useState(false);

  const [phaseOneDays, setPhaseOneDays] = useState<string>("");
  const [totalDays, setTotalDays] = useState<string>("");
  const [alertOpensExcursion, setAlertOpensExcursion] = useState(false);
  const [items, setItems] = useState<OosTemplateItem[]>([]);

  const [newItemPhase, setNewItemPhase] = useState<number>(1);
  const [newItemText, setNewItemText] = useState("");

  // Categories state
  const [categories, setCategories] = useState<OosCategoryDto[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [newCatName, setNewCatName] = useState("");
  const [newCatReason, setNewCatReason] = useState("");
  const [catActionError, setCatActionError] = useState<string | null>(null);
  const [addingCat, setAddingCat] = useState(false);

  const [editingCat, setEditingCat] = useState<OosCategoryDto | null>(null);
  const [editCatName, setEditCatName] = useState("");
  const [editCatActive, setEditCatActive] = useState(true);
  const [editCatReason, setEditCatReason] = useState("");
  const [savingCat, setSavingCat] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setSectionsLoading(true);
    laboratorySectionService
      .getMySections(controller.signal)
      .then((loaded) => {
        if (controller.signal.aborted) return;
        setSections(loaded);
        if (loaded.length > 0) {
          setSectionId((prev) => (prev === "" ? loaded[0].sectionId : prev));
        }
      })
      .catch((e) => {
        if (!controller.signal.aborted) {
          setTemplateError(serverMessage(e) ?? t("templates.errors.loadSections", { defaultValue: "Failed to load sections." }));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setSectionsLoading(false);
      });
    return () => controller.abort();
  }, [t]);

  const applyTemplateDto = useCallback((dto: OosTemplateDto) => {
    setTemplate(dto);
    setPhaseOneDays(dto.phaseOneDueWorkingDays != null ? String(dto.phaseOneDueWorkingDays) : "");
    setTotalDays(dto.totalDueWorkingDays != null ? String(dto.totalDueWorkingDays) : "");
    setAlertOpensExcursion(dto.alertOpensExcursionReview);
    setItems(dto.items);
  }, []);

  useEffect(() => {
    if (sectionId === "") return;
    const controller = new AbortController();
    setTemplateLoading(true);
    setTemplateError(null);
    setSaveError(null);
    oosInvestigationService
      .getTemplate(sectionId, kind, controller.signal)
      .then((dto) => {
        if (!controller.signal.aborted) applyTemplateDto(dto);
      })
      .catch((e) => {
        if (!controller.signal.aborted) {
          setTemplateError(serverMessage(e) ?? t("templates.errors.loadTemplate", { defaultValue: "Failed to load template." }));
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setTemplateLoading(false);
      });
    return () => controller.abort();
  }, [sectionId, kind, applyTemplateDto, t]);

  const loadCategories = useCallback(
    (signal?: AbortSignal) => {
      setCategoriesLoading(true);
      setCategoriesError(null);
      oosInvestigationService
        .listCategories(1, 100, signal)
        .then((res) => {
          if (!signal?.aborted) setCategories(res.items);
        })
        .catch((e) => {
          if (!signal?.aborted) {
            setCategoriesError(
              serverMessage(e) ?? t("templates.errors.loadCategories", { defaultValue: "Failed to load cause categories." })
            );
          }
        })
        .finally(() => {
          if (!signal?.aborted) setCategoriesLoading(false);
        });
    },
    [t]
  );

  useEffect(() => {
    if (tab !== "categories") return;
    const controller = new AbortController();
    loadCategories(controller.signal);
    return () => controller.abort();
  }, [tab, loadCategories]);

  const moveItem = (index: number, delta: -1 | 1) => {
    const target = index + delta;
    if (target < 0 || target >= items.length) return;
    const next = [...items];
    const [moved] = next.splice(index, 1);
    next.splice(target, 0, moved);
    setItems(next.map((it, idx) => ({ ...it, order: idx + 1 })));
  };

  const handleItemKeyDown = (index: number, e: KeyboardEvent<HTMLElement>) => {
    if (e.altKey && e.key === "ArrowUp") {
      e.preventDefault();
      moveItem(index, -1);
    } else if (e.altKey && e.key === "ArrowDown") {
      e.preventDefault();
      moveItem(index, 1);
    }
  };

  const handleAddItem = () => {
    const trimmed = newItemText.trim();
    if (!trimmed) return;
    setItems((prev) => [
      ...prev,
      {
        key: null,
        phase: newItemPhase,
        order: prev.length + 1,
        text: trimmed,
        isActive: true
      }
    ]);
    setNewItemText("");
  };

  const handleSaveTemplate = async () => {
    if (sectionId === "" || !template) return;
    setSavingTemplate(true);
    setSaveError(null);
    try {
      const updated = await oosInvestigationService.saveTemplate(
        sectionId,
        kind,
        {
          phaseOneDueWorkingDays: phaseOneDays.trim() === "" ? null : Number(phaseOneDays),
          totalDueWorkingDays: totalDays.trim() === "" ? null : Number(totalDays),
          alertOpensExcursionReview: alertOpensExcursion,
          items: items.map((it, idx) => ({ ...it, order: idx + 1 }))
        },
        template.version
      );
      applyTemplateDto(updated);
      toast.success(t("templates.saved", { defaultValue: "Template saved." }));
    } catch (e) {
      const msg = serverMessage(e) ?? t("templates.errors.saveTemplate", { defaultValue: "Failed to save template." });
      setSaveError(msg);
      toast.error(msg);
    } finally {
      setSavingTemplate(false);
    }
  };

  const handleAddCategory = async () => {
    const name = newCatName.trim();
    const reason = newCatReason.trim();
    if (!name || !reason) return;
    setAddingCat(true);
    setCatActionError(null);
    try {
      await oosInvestigationService.addCategory({ name, isActive: true, reason });
      setNewCatName("");
      setNewCatReason("");
      toast.success(t("templates.categories.added", { defaultValue: "Cause category added." }));
      loadCategories();
    } catch (e) {
      const msg = serverMessage(e) ?? t("templates.errors.saveCategory", { defaultValue: "Failed to save category." });
      setCatActionError(msg);
      toast.error(msg);
    } finally {
      setAddingCat(false);
    }
  };

  const openEditCategory = (cat: OosCategoryDto) => {
    if (cat.isNoCauseFound) return;
    setEditingCat(cat);
    setEditCatName(cat.name);
    setEditCatActive(cat.isActive);
    setEditCatReason("");
    setCatActionError(null);
  };

  const handleSaveCategory = async () => {
    if (!editingCat) return;
    const name = editCatName.trim();
    const reason = editCatReason.trim();
    if (!name || !reason) return;
    setSavingCat(true);
    setCatActionError(null);
    try {
      await oosInvestigationService.saveCategory(
        editingCat.id,
        { name, isActive: editCatActive, reason },
        editingCat.version
      );
      setEditingCat(null);
      toast.success(t("templates.categories.updated", { defaultValue: "Cause category updated." }));
      loadCategories();
    } catch (e) {
      const msg = serverMessage(e) ?? t("templates.errors.saveCategory", { defaultValue: "Failed to save category." });
      setCatActionError(msg);
      toast.error(msg);
    } finally {
      setSavingCat(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title={t("templates.title", { defaultValue: "OOS & Excursion Templates" })}
        subtitle={t("templates.subtitle", {
          defaultValue: "Configure per-laboratory investigation checklists, working-day targets, and assignable cause categories."
        })}
      />

      <Tabs
        value={tab}
        onChange={(_, v: TemplatesTab) => setTab(v)}
        sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
      >
        <Tab
          value="templates"
          label={t("templates.tabs.checklist", { defaultValue: "Checklist Templates" })}
        />
        <Tab
          value="categories"
          label={t("templates.tabs.categories", { defaultValue: "Cause Categories" })}
        />
      </Tabs>

      {tab === "templates" && (
        <Stack spacing={2.5}>
          <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
              <FormControl size="small" sx={{ minWidth: 220 }}>
                <InputLabel id="oos-tpl-section-label">
                  {t("templates.section", { defaultValue: "Laboratory Section" })}
                </InputLabel>
                <Select
                  labelId="oos-tpl-section-label"
                  label={t("templates.section", { defaultValue: "Laboratory Section" })}
                  value={sectionId}
                  disabled={sectionsLoading}
                  onChange={(e) => setSectionId(Number(e.target.value))}
                >
                  {sections.map((s) => (
                    <MenuItem key={s.sectionId} value={s.sectionId}>
                      {s.sectionName}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <FormControl size="small" sx={{ minWidth: 180 }}>
                <InputLabel id="oos-tpl-kind-label">
                  {t("templates.kind", { defaultValue: "Investigation Kind" })}
                </InputLabel>
                <Select
                  labelId="oos-tpl-kind-label"
                  label={t("templates.kind", { defaultValue: "Investigation Kind" })}
                  value={kind}
                  onChange={(e) => setKind(e.target.value as OosInvestigationKind)}
                >
                  <MenuItem value="Oos">{t("kinds.Oos", { defaultValue: "OOS" })}</MenuItem>
                  <MenuItem value="Excursion">{t("kinds.Excursion", { defaultValue: "Excursion" })}</MenuItem>
                </Select>
              </FormControl>
            </Stack>
          </Paper>

          {templateError && (
            <Alert severity="error" role="alert">
              {templateError}
            </Alert>
          )}
          {saveError && (
            <Alert severity="error" role="alert">
              {saveError}
            </Alert>
          )}

          {templateLoading ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
              <CircularProgress />
            </Box>
          ) : (
            template && (
              <Paper sx={{ p: 2.5, border: "1px solid", borderColor: "divider" }}>
                <Stack spacing={2.5}>
                  <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
                    <TextField
                      size="small"
                      type="number"
                      label={t("templates.phaseOneDays", { defaultValue: "Phase I due (working days)" })}
                      value={phaseOneDays}
                      onChange={(e) => setPhaseOneDays(e.target.value)}
                      slotProps={{ htmlInput: { min: 1 } }}
                    />
                    <TextField
                      size="small"
                      type="number"
                      label={t("templates.totalDays", { defaultValue: "Total due (working days)" })}
                      value={totalDays}
                      onChange={(e) => setTotalDays(e.target.value)}
                      slotProps={{ htmlInput: { min: 1 } }}
                    />
                    <FormControlLabel
                      control={
                        <Switch
                          checked={alertOpensExcursion}
                          onChange={(e) => setAlertOpensExcursion(e.target.checked)}
                        />
                      }
                      label={t("templates.alertOpensExcursion", {
                        defaultValue: "Alert limit opens excursion review"
                      })}
                    />
                  </Stack>

                  <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                    {t("templates.itemsHeading", { defaultValue: "Checklist Items" })}
                  </Typography>

                  {items.length === 0 ? (
                    <Alert severity="info">
                      {t("templates.emptyItems", {
                        defaultValue: "No checklist items configured yet. Add items below."
                      })}
                    </Alert>
                  ) : (
                    <Stack spacing={1}>
                      {items.map((item, idx) => (
                        <Box
                          key={item.key ?? `new-${idx}`}
                          onKeyDown={(e) => handleItemKeyDown(idx, e)}
                          sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 1.5,
                            p: 1.5,
                            border: "1px solid",
                            borderColor: "divider",
                            borderRadius: 1,
                            opacity: item.isActive ? 1 : 0.65
                          }}
                        >
                          <FormControl size="small" sx={{ minWidth: 110 }}>
                            <InputLabel id={`item-phase-${idx}`}>
                              {t("templates.phase", { defaultValue: "Phase" })}
                            </InputLabel>
                            <Select
                              labelId={`item-phase-${idx}`}
                              label={t("templates.phase", { defaultValue: "Phase" })}
                              value={item.phase}
                              onChange={(e) => {
                                const val = Number(e.target.value);
                                setItems((prev) =>
                                  prev.map((p, i) => (i === idx ? { ...p, phase: val } : p))
                                );
                              }}
                            >
                              <MenuItem value={1}>{t("templates.phaseN", { phase: 1, defaultValue: "Phase 1" })}</MenuItem>
                              <MenuItem value={2}>{t("templates.phaseN", { phase: 2, defaultValue: "Phase 2" })}</MenuItem>
                            </Select>
                          </FormControl>

                          <TextField
                            fullWidth
                            size="small"
                            label={t("templates.itemText", { defaultValue: "Checklist item text" })}
                            value={item.text}
                            onChange={(e) => {
                              const val = e.target.value;
                              setItems((prev) =>
                                prev.map((p, i) => (i === idx ? { ...p, text: val } : p))
                              );
                            }}
                          />

                          <FormControlLabel
                            control={
                              <Switch
                                checked={item.isActive}
                                onChange={(e) => {
                                  const checked = e.target.checked;
                                  setItems((prev) =>
                                    prev.map((p, i) => (i === idx ? { ...p, isActive: checked } : p))
                                  );
                                }}
                                slotProps={{ input: { "aria-label": t("templates.active", { defaultValue: "Active" }) } }}
                              />
                            }
                            label={t("templates.active", { defaultValue: "Active" })}
                          />

                          <IconButton
                            size="small"
                            aria-label={t("templates.moveUp", { defaultValue: "Move item up" })}
                            disabled={idx === 0}
                            onClick={() => moveItem(idx, -1)}
                          >
                            <ArrowUpwardIcon fontSize="small" />
                          </IconButton>
                          <IconButton
                            size="small"
                            aria-label={t("templates.moveDown", { defaultValue: "Move item down" })}
                            disabled={idx === items.length - 1}
                            onClick={() => moveItem(idx, 1)}
                          >
                            <ArrowDownwardIcon fontSize="small" />
                          </IconButton>
                        </Box>
                      ))}
                    </Stack>
                  )}

                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: "center" }}>
                    <FormControl size="small" sx={{ minWidth: 110 }}>
                      <InputLabel id="new-item-phase-label">
                        {t("templates.phase", { defaultValue: "Phase" })}
                      </InputLabel>
                      <Select
                        labelId="new-item-phase-label"
                        label={t("templates.phase", { defaultValue: "Phase" })}
                        value={newItemPhase}
                        onChange={(e) => setNewItemPhase(Number(e.target.value))}
                      >
                        <MenuItem value={1}>{t("templates.phaseN", { phase: 1, defaultValue: "Phase 1" })}</MenuItem>
                        <MenuItem value={2}>{t("templates.phaseN", { phase: 2, defaultValue: "Phase 2" })}</MenuItem>
                      </Select>
                    </FormControl>
                    <TextField
                      fullWidth
                      size="small"
                      label={t("templates.newItemText", { defaultValue: "New checklist item text" })}
                      value={newItemText}
                      onChange={(e) => setNewItemText(e.target.value)}
                    />
                    <Button
                      variant="outlined"
                      startIcon={<AddOutlinedIcon />}
                      disabled={!newItemText.trim()}
                      onClick={handleAddItem}
                    >
                      {t("templates.addItem", { defaultValue: "Add item" })}
                    </Button>
                  </Stack>

                  <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
                    <Button
                      variant="contained"
                      startIcon={<SaveOutlinedIcon />}
                      disabled={savingTemplate}
                      onClick={() => void handleSaveTemplate()}
                    >
                      {t("templates.saveTemplate", { defaultValue: "Save Template" })}
                    </Button>
                  </Box>
                </Stack>
              </Paper>
            )
          )}
        </Stack>
      )}

      {tab === "categories" && (
        <Stack spacing={2.5}>
          {categoriesError && (
            <Alert severity="error" role="alert">
              {categoriesError}
            </Alert>
          )}
          {catActionError && (
            <Alert severity="error" role="alert">
              {catActionError}
            </Alert>
          )}

          <Paper sx={{ p: 2, border: "1px solid", borderColor: "divider" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5 }}>
              {t("templates.categories.addHeading", { defaultValue: "Add Cause Category" })}
            </Typography>
            <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ alignItems: "center" }}>
              <TextField
                size="small"
                label={t("templates.categories.name", { defaultValue: "Category name" })}
                value={newCatName}
                onChange={(e) => setNewCatName(e.target.value)}
              />
              <TextField
                fullWidth
                size="small"
                label={t("templates.categories.reason", { defaultValue: "Reason for change" })}
                value={newCatReason}
                onChange={(e) => setNewCatReason(e.target.value)}
              />
              <Button
                variant="contained"
                startIcon={<AddOutlinedIcon />}
                disabled={addingCat || !newCatName.trim() || !newCatReason.trim()}
                onClick={() => void handleAddCategory()}
              >
                {t("templates.categories.add", { defaultValue: "Add Category" })}
              </Button>
            </Stack>
          </Paper>

          {categoriesLoading ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
              <CircularProgress />
            </Box>
          ) : (
            <TableContainer component={Paper} sx={{ border: "1px solid", borderColor: "divider" }}>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>{t("templates.categories.name", { defaultValue: "Category name" })}</TableCell>
                    <TableCell>{t("templates.categories.status", { defaultValue: "Status" })}</TableCell>
                    <TableCell>{t("templates.categories.actions", { defaultValue: "Actions" })}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {categories.map((cat) => (
                    <TableRow key={cat.id}>
                      <TableCell>
                        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {cat.name}
                          </Typography>
                          {cat.isNoCauseFound && (
                            <Chip
                              size="small"
                              icon={<LockOutlinedIcon />}
                              label={t("templates.categories.locked", { defaultValue: "Locked" })}
                              variant="outlined"
                            />
                          )}
                        </Stack>
                      </TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          label={
                            cat.isActive
                              ? t("templates.active", { defaultValue: "Active" })
                              : t("templates.inactive", { defaultValue: "Inactive" })
                          }
                          color={cat.isActive ? "success" : "default"}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell>
                        <Button
                          size="small"
                          startIcon={<EditOutlinedIcon />}
                          disabled={cat.isNoCauseFound}
                          onClick={() => openEditCategory(cat)}
                        >
                          {t("templates.categories.edit", { defaultValue: "Edit" })}
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </Stack>
      )}

      <Dialog open={Boolean(editingCat)} onClose={() => setEditingCat(null)} maxWidth="sm" fullWidth>
        <DialogTitle component="div">
          <Typography component="span" variant="h6">
            {t("templates.categories.editHeading", { defaultValue: "Edit Cause Category" })}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {catActionError && (
              <Alert severity="error" role="alert">
                {catActionError}
              </Alert>
            )}
            <TextField
              fullWidth
              size="small"
              label={t("templates.categories.name", { defaultValue: "Category name" })}
              value={editCatName}
              onChange={(e) => setEditCatName(e.target.value)}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={editCatActive}
                  onChange={(e) => setEditCatActive(e.target.checked)}
                />
              }
              label={t("templates.active", { defaultValue: "Active" })}
            />
            <TextField
              fullWidth
              size="small"
              label={t("templates.categories.reason", { defaultValue: "Reason for change" })}
              value={editCatReason}
              onChange={(e) => setEditCatReason(e.target.value)}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditingCat(null)}>
            {t("common.cancel", { defaultValue: "Cancel" })}
          </Button>
          <Button
            variant="contained"
            disabled={savingCat || !editCatName.trim() || !editCatReason.trim()}
            onClick={() => void handleSaveCategory()}
          >
            {t("templates.categories.save", { defaultValue: "Save Category" })}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
