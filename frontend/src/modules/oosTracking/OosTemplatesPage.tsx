import { useCallback, useEffect, useRef, useState, type KeyboardEvent } from "react";
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
  TablePagination,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import AddOutlinedIcon from "@mui/icons-material/AddOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { useOptionalAuth } from "../../contexts/AuthContext";
import { useTranslation } from "../../i18n";
import { PERMISSIONS } from "../../routes/routes";
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

interface LocalChecklistItem extends OosTemplateItem {
  clientId: string;
}

export function OosTemplatesPage() {
  const { t } = useTranslation("oosTracking");
  const auth = useOptionalAuth();
  const canManageCategories = Boolean(
    auth &&
      auth.permissions.includes(PERMISSIONS.OOS_MANAGE) &&
      auth.permissions.includes(PERMISSIONS.MASTER_DATA_MANAGE)
  );

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
  const [items, setItems] = useState<LocalChecklistItem[]>([]);
  const [moveAnnouncement, setMoveAnnouncement] = useState("");
  const nextClientIdRef = useRef(0);
  const makeClientId = useCallback(() => `row-${++nextClientIdRef.current}`, []);
  const controlRefs = useRef<Map<string, HTMLButtonElement | null>>(new Map());
  const pendingFocusRef = useRef<{ clientId: string; direction: "up" | "down" } | null>(null);

  const [newItemPhase, setNewItemPhase] = useState<number>(1);
  const [newItemText, setNewItemText] = useState("");

  // Categories state
  const [categories, setCategories] = useState<OosCategoryDto[]>([]);
  const [categoriesTotalCount, setCategoriesTotalCount] = useState(0);
  const [categoriesPage, setCategoriesPage] = useState(1);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [newCatName, setNewCatName] = useState("");
  const [newCatReason, setNewCatReason] = useState("");
  const [addCatError, setAddCatError] = useState<string | null>(null);
  const [addingCat, setAddingCat] = useState(false);

  const [editingCat, setEditingCat] = useState<OosCategoryDto | null>(null);
  const [editCatName, setEditCatName] = useState("");
  const [editCatActive, setEditCatActive] = useState(true);
  const [editCatReason, setEditCatReason] = useState("");
  const [editCatError, setEditCatError] = useState<string | null>(null);
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

  const applyTemplateDto = useCallback(
    (dto: OosTemplateDto) => {
      setTemplate(dto);
      setPhaseOneDays(dto.phaseOneDueWorkingDays != null ? String(dto.phaseOneDueWorkingDays) : "");
      setTotalDays(dto.totalDueWorkingDays != null ? String(dto.totalDueWorkingDays) : "");
      setAlertOpensExcursion(dto.alertOpensExcursionReview);
      setItems(dto.items.map((it) => ({ ...it, clientId: it.key ?? makeClientId() })));
    },
    [makeClientId]
  );

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
    (page = categoriesPage, signal?: AbortSignal) => {
      setCategoriesLoading(true);
      setCategoriesError(null);
      oosInvestigationService
        .listCategories(page, 100, signal)
        .then((res) => {
          if (!signal?.aborted) {
            setCategories(res.items);
            setCategoriesTotalCount(res.totalCount);
          }
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
    [categoriesPage, t]
  );

  useEffect(() => {
    if (tab !== "categories") return;
    const controller = new AbortController();
    loadCategories(categoriesPage, controller.signal);
    return () => controller.abort();
  }, [tab, categoriesPage, loadCategories]);

  useEffect(() => {
    const pending = pendingFocusRef.current;
    if (!pending) return;
    pendingFocusRef.current = null;
    controlRefs.current.get(`${pending.clientId}:${pending.direction}`)?.focus();
  }, [items]);

  const moveItem = (index: number, delta: -1 | 1, focusDirection?: "up" | "down") => {
    const target = index + delta;
    if (target < 0 || target >= items.length) return;
    const next = [...items];
    const [moved] = next.splice(index, 1);
    next.splice(target, 0, moved);
    if (focusDirection) {
      pendingFocusRef.current = { clientId: moved.clientId, direction: focusDirection };
    }
    setItems(next.map((it, idx) => ({ ...it, order: idx + 1 })));
    setMoveAnnouncement(
      t("templates.itemMoved", {
        position: target + 1,
        defaultValue: "Item moved to position {{position}}"
      })
    );
  };

  const handleItemKeyDown = (index: number, e: KeyboardEvent<HTMLElement>) => {
    if (e.altKey && e.key === "ArrowUp") {
      e.preventDefault();
      moveItem(index, -1, "up");
    } else if (e.altKey && e.key === "ArrowDown") {
      e.preventDefault();
      moveItem(index, 1, "down");
    }
  };

  const handleAddItem = () => {
    const trimmed = newItemText.trim();
    if (!trimmed) return;
    setItems((prev) => [
      ...prev,
      {
        clientId: makeClientId(),
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
          items: items.map(({ clientId: _clientId, ...it }, idx) => ({ ...it, order: idx + 1 }))
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
    if (!canManageCategories) return;
    const name = newCatName.trim();
    const reason = newCatReason.trim();
    if (!name || !reason) return;
    setAddingCat(true);
    setAddCatError(null);
    try {
      await oosInvestigationService.addCategory({ name, isActive: true, reason });
      setNewCatName("");
      setNewCatReason("");
      toast.success(t("templates.categories.added", { defaultValue: "Cause category added." }));
      loadCategories(categoriesPage);
    } catch (e) {
      const msg = serverMessage(e) ?? t("templates.errors.saveCategory", { defaultValue: "Failed to save category." });
      setAddCatError(msg);
      toast.error(msg);
    } finally {
      setAddingCat(false);
    }
  };

  const openEditCategory = (cat: OosCategoryDto) => {
    if (!canManageCategories || cat.isNoCauseFound) return;
    setEditingCat(cat);
    setEditCatName(cat.name);
    setEditCatActive(cat.isActive);
    setEditCatReason("");
    setEditCatError(null);
  };

  const handleSaveCategory = async () => {
    if (!editingCat || !canManageCategories) return;
    const name = editCatName.trim();
    const reason = editCatReason.trim();
    if (!name || !reason) return;
    setSavingCat(true);
    setEditCatError(null);
    try {
      await oosInvestigationService.saveCategory(
        editingCat.id,
        { name, isActive: editCatActive, reason },
        editingCat.version
      );
      setEditingCat(null);
      toast.success(t("templates.categories.updated", { defaultValue: "Cause category updated." }));
      loadCategories(categoriesPage);
    } catch (e) {
      const msg = serverMessage(e) ?? t("templates.errors.saveCategory", { defaultValue: "Failed to save category." });
      setEditCatError(msg);
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

                  <Box>
                    <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                      {t("templates.itemsHeading", { defaultValue: "Checklist Items" })}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {t("templates.reorderHint", {
                        defaultValue: "Use the arrow buttons or press Alt+Up / Alt+Down to reorder items."
                      })}
                    </Typography>
                  </Box>

                  <Box role="status" aria-live="polite" sx={visuallyHidden}>
                    {moveAnnouncement}
                  </Box>

                  {items.length === 0 ? (
                    <Alert severity="info">
                      {t("templates.emptyItems", {
                        defaultValue: "No checklist items configured yet. Add items below."
                      })}
                    </Alert>
                  ) : (
                    <Stack spacing={1}>
                      {items.map((item, idx) => {
                        const itemLabel =
                          item.text.trim() || t("templates.untitledItem", { defaultValue: "Untitled" });
                        return (
                          <Box
                            key={item.clientId}
                            aria-keyshortcuts="Alt+ArrowUp Alt+ArrowDown"
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
                              <InputLabel id={`item-phase-${item.clientId}`}>
                                {t("templates.phase", { defaultValue: "Phase" })}
                              </InputLabel>
                              <Select
                                labelId={`item-phase-${item.clientId}`}
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
                                  slotProps={{
                                    input: {
                                      "aria-label": t("templates.activeItem", {
                                        item: itemLabel,
                                        defaultValue: "Active: {{item}}"
                                      })
                                    }
                                  }}
                                />
                              }
                              label={t("templates.active", { defaultValue: "Active" })}
                            />

                            <IconButton
                              ref={(el) => {
                                const k = `${item.clientId}:up`;
                                if (el) controlRefs.current.set(k, el);
                                else controlRefs.current.delete(k);
                              }}
                              size="small"
                              aria-label={t("templates.moveUpItem", {
                                item: itemLabel,
                                defaultValue: "Move {{item}} up"
                              })}
                              aria-disabled={idx === 0}
                              aria-keyshortcuts="Alt+ArrowUp"
                              sx={{ opacity: idx === 0 ? 0.38 : 1 }}
                              onClick={() => moveItem(idx, -1, "up")}
                            >
                              <ArrowUpwardIcon fontSize="small" />
                            </IconButton>
                            <IconButton
                              ref={(el) => {
                                const k = `${item.clientId}:down`;
                                if (el) controlRefs.current.set(k, el);
                                else controlRefs.current.delete(k);
                              }}
                              size="small"
                              aria-label={t("templates.moveDownItem", {
                                item: itemLabel,
                                defaultValue: "Move {{item}} down"
                              })}
                              aria-disabled={idx === items.length - 1}
                              aria-keyshortcuts="Alt+ArrowDown"
                              sx={{ opacity: idx === items.length - 1 ? 0.38 : 1 }}
                              onClick={() => moveItem(idx, 1, "down")}
                            >
                              <ArrowDownwardIcon fontSize="small" />
                            </IconButton>
                          </Box>
                        );
                      })}
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
          {!canManageCategories && (
            <Alert severity="info">
              {t("templates.categories.permissionRequired", {
                defaultValue: "Adding or editing cause categories requires MasterData.Manage permission."
              })}
            </Alert>
          )}
          {addCatError && (
            <Alert severity="error" role="alert">
              {addCatError}
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
                disabled={!canManageCategories}
                onChange={(e) => setNewCatName(e.target.value)}
              />
              <TextField
                fullWidth
                size="small"
                label={t("templates.categories.reason", { defaultValue: "Reason for change" })}
                value={newCatReason}
                disabled={!canManageCategories}
                onChange={(e) => setNewCatReason(e.target.value)}
              />
              <Button
                variant="contained"
                startIcon={<AddOutlinedIcon />}
                disabled={!canManageCategories || addingCat || !newCatName.trim() || !newCatReason.trim()}
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
            <Paper sx={{ border: "1px solid", borderColor: "divider" }}>
              <TableContainer>
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
                            aria-label={t("templates.categories.editItem", {
                              name: cat.name,
                              defaultValue: "Edit {{name}}"
                            })}
                            disabled={!canManageCategories || cat.isNoCauseFound}
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
              {categoriesTotalCount > 100 && (
                <TablePagination
                  component="div"
                  count={categoriesTotalCount}
                  page={categoriesPage - 1}
                  rowsPerPage={100}
                  rowsPerPageOptions={[100]}
                  onPageChange={(_, nextPage) => setCategoriesPage(nextPage + 1)}
                />
              )}
            </Paper>
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
            {editCatError && (
              <Alert severity="error" role="alert">
                {editCatError}
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
          <Button onClick={() => setEditingCat(null)}>{t("actions.cancel", { ns: "common" })}</Button>
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
