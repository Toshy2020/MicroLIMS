import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import LayersOutlinedIcon from "@mui/icons-material/LayersOutlined";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { useTranslation } from "../../../i18n";
import {
  masterDataOptions,
  ProductionStageOption,
  ProductionStageRole
} from "../../../services/masterDataOptions";

const PRODUCTION_STAGE_ROLES: ProductionStageRole[] = [
  "Bulk",
  "InProcess",
  "Finished",
  "Stability",
  "Other"
];

export function ProductionStagesCard() {
  const { t } = useTranslation("masterData");
  const theme = useTheme();
  const [list, setList] = useState<ProductionStageOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [name, setName] = useState("");
  const [role, setRole] = useState<ProductionStageRole | "">("");
  const [editing, setEditing] = useState<ProductionStageOption | null>(null);
  const [pendingDelete, setPendingDelete] = useState<ProductionStageOption | null>(null);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const reload = () =>
    masterDataOptions
      .getProductionStages()
      .then(setList)
      .catch(() =>
        setMessage({
          text: t("receivingConfig.productionStages.loadError"),
          ok: false
        })
      )
      .finally(() => setLoading(false));

  useEffect(() => {
    reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (message?.ok) {
      const timer = setTimeout(() => setMessage(null), 3000);
      return () => clearTimeout(timer);
    }
  }, [message]);

  const startEdit = (o: ProductionStageOption) => {
    setEditing(o);
    setName(o.name);
    setRole(o.role);
    setMessage(null);
    inputRef.current?.focus();
  };

  const cancelEdit = () => {
    setEditing(null);
    setName("");
    setRole("");
  };

  const save = async () => {
    const trimmed = name.trim();
    if (!trimmed || !role || saving) return;
    setSaving(true);
    setMessage(null);
    try {
      if (editing) {
        await masterDataOptions.updateProductionStage(editing.id, {
          name: trimmed,
          role
        }, editing.version);
        setMessage({ text: t("receivingConfig.renamedMsg", { name: trimmed }), ok: true });
      } else {
        await masterDataOptions.createProductionStage({
          name: trimmed,
          role
        });
        setMessage({ text: t("receivingConfig.addedMsg", { name: trimmed }), ok: true });
      }
      cancelEdit();
      await reload();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setMessage({
        text:
          err?.response?.data?.message ??
          (editing ? t("receivingConfig.renameError") : t("receivingConfig.addError")),
        ok: false
      });
    } finally {
      setSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    setPendingDelete(null);
    setMessage(null);
    try {
      await masterDataOptions.deleteProductionStage(target.id);
      if (editing?.id === target.id) cancelEdit();
      setMessage({ text: t("receivingConfig.deletedMsg", { name: target.name }), ok: true });
      await reload();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setMessage({
        text: err?.response?.data?.message ?? t("receivingConfig.deleteError"),
        ok: false
      });
    }
  };

  return (
    <Paper
      variant="outlined"
      sx={{
        display: "flex",
        flexDirection: "column",
        borderRadius: 1.5,
        overflow: "hidden"
      }}
    >
      {/* Card header - icon, title, live count */}
      <Box
        sx={{
          px: 2,
          py: 1.5,
          borderBottom: "1px solid",
          borderColor: "divider",
          bgcolor: "background.default",
          display: "flex",
          alignItems: "center",
          gap: 1
        }}
      >
        <Box sx={{ display: "flex", color: theme.palette.primary.main }}>
          <LayersOutlinedIcon sx={{ fontSize: 19 }} />
        </Box>
        <Typography
          sx={{
            fontSize: 15,
            fontWeight: 600,
            color: theme.palette.primary.main,
            flex: 1
          }}
        >
          {t("receivingConfig.productionStages.title")}
        </Typography>
        <Chip
          size="small"
          label={loading ? t("common.dash") : list.length}
          sx={{ height: 20, fontSize: 12, fontWeight: 600 }}
        />
      </Box>

      <Box
        sx={{
          p: 2,
          display: "flex",
          flexDirection: "column",
          gap: 1.5,
          flex: 1
        }}
      >
        {/* Fixed two-line box so the form + list below it line up across all
            three cards regardless of caption length */}
        <Typography
          variant="caption"
          sx={{ color: "text.secondary", lineHeight: 1.5, minHeight: 36 }}
        >
          {t("receivingConfig.productionStages.subtitle")}
        </Typography>

        {/* Add / rename form with role dropdown */}
        <Stack spacing={1}>
          <Box
            sx={{
              display: "flex",
              gap: 1,
              alignItems: "flex-start",
              flexWrap: "wrap"
            }}
          >
            <TextField
              inputRef={inputRef}
              size="small"
              sx={{ flex: "1 1 140px", minWidth: 130 }}
              label={editing ? t("receivingConfig.renameLabel", { name: editing.name }) : t("receivingConfig.productionStages.inputLabel")}
              placeholder={t("receivingConfig.productionStages.placeholder")}
              value={name}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.preventDefault();
                  save();
                }
                if (e.key === "Escape" && editing) cancelEdit();
              }}
            />
            <FormControl size="small" sx={{ width: 120, flexShrink: 0 }}>
              <InputLabel id="stage-role-select-label">{t("receivingConfig.productionStages.roleLabel")}</InputLabel>
              <Select<ProductionStageRole | "">
                labelId="stage-role-select-label"
                label={t("receivingConfig.productionStages.roleLabel")}
                value={role}
                onChange={(e) =>
                  setRole(e.target.value as ProductionStageRole)
                }
                inputProps={{ "aria-label": t("receivingConfig.productionStages.roleLabel") }}
              >
                {PRODUCTION_STAGE_ROLES.map((r) => (
                  <MenuItem key={r} value={r}>
                    {t(`receivingConfig.productionStages.roles.${r}`)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Box
              sx={{
                display: "flex",
                gap: 1,
                alignItems: "center",
                mt: 0.25,
                ml: { xs: "auto", sm: 0 }
              }}
            >
              {editing && (
                <Button
                  size="small"
                  onClick={cancelEdit}
                  disabled={saving}
                  sx={{ minWidth: 60 }}
                >
                  {t("common.cancel")}
                </Button>
              )}
              <Button
                size="small"
                variant="contained"
                startIcon={editing ? undefined : <AddIcon />}
                onClick={save}
                disabled={!name.trim() || !role || saving}
                sx={{ minWidth: 68, whiteSpace: "nowrap" }}
              >
                {editing ? t("common.save") : t("receivingConfig.add")}
              </Button>
            </Box>
          </Box>
        </Stack>

        <Collapse in={message != null} unmountOnExit>
          <Alert
            severity={message?.ok ? "success" : "error"}
            onClose={() => setMessage(null)}
            sx={{ py: 0.25, fontSize: 13 }}
          >
            {message?.text}
          </Alert>
        </Collapse>

        {/* The list itself - compact rows, actions always visible */}
        <Box
          sx={{
            border: "1px solid",
            borderColor: "divider",
            borderRadius: 1,
            overflow: "hidden"
          }}
        >
          {loading ? (
            <Typography sx={{ p: 2, fontSize: 13, color: "text.secondary" }}>
              {t("receivingConfig.loading")}
            </Typography>
          ) : list.length === 0 ? (
            <Typography
              sx={{
                p: 2,
                fontSize: 13,
                color: "text.secondary",
                fontStyle: "italic"
              }}
            >
              {t("receivingConfig.emptyList")}
            </Typography>
          ) : (
            list.map((o, idx) => {
              const isEditing = editing?.id === o.id;
              return (
                <Box
                  key={o.id}
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                    pl: isEditing ? 1.25 : 1.75,
                    pr: 0.75,
                    py: 0.5,
                    minHeight: 40,
                    borderTop: idx === 0 ? "none" : "1px solid",
                    borderColor: "divider",
                    borderLeft: isEditing
                      ? `3px solid ${theme.palette.primary.main}`
                      : "none",
                    bgcolor: isEditing ? "action.selected" : "transparent",
                    transition: theme.transitions.create(
                      ["background-color"],
                      { duration: 150 }
                    ),
                    "&:hover": {
                      bgcolor: isEditing ? "action.selected" : "action.hover"
                    }
                  }}
                >
                  <Typography
                    sx={{
                      fontSize: 13,
                      fontWeight: isEditing ? 600 : 400,
                      flex: 1,
                      wordBreak: "break-word"
                    }}
                  >
                    {o.name}
                  </Typography>
                  <Chip
                    size="small"
                    label={t(`receivingConfig.productionStages.roles.${o.role}`, { defaultValue: o.role })}
                    variant="outlined"
                    sx={{ height: 20, fontSize: 12 }}
                  />
                  <Tooltip title={t("receivingConfig.renameTooltip")}>
                    <IconButton
                      size="small"
                      aria-label={t("receivingConfig.renameAria", { name: o.name })}
                      onClick={() => startEdit(o)}
                    >
                      <EditOutlinedIcon sx={{ fontSize: 17 }} />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title={t("receivingConfig.deleteTooltip")}>
                    <IconButton
                      size="small"
                      aria-label={t("receivingConfig.deleteAria", { name: o.name })}
                      onClick={() => setPendingDelete(o)}
                      sx={{
                        color: "text.secondary",
                        "&:hover": { color: theme.palette.error.main }
                      }}
                    >
                      <DeleteOutlineIcon sx={{ fontSize: 17 }} />
                    </IconButton>
                  </Tooltip>
                </Box>
              );
            })
          )}
        </Box>
      </Box>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={
          pendingDelete
            ? t("receivingConfig.productionStages.deleteConfirm", { name: pendingDelete.name })
            : ""
        }
        onCancel={() => setPendingDelete(null)}
        onConfirm={confirmDelete}
      />
    </Paper>
  );
}
