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
          text: "Could not load production stages. Refresh to try again.",
          ok: false
        })
      )
      .finally(() => setLoading(false));

  useEffect(() => {
    reload();
  }, []);

  useEffect(() => {
    if (message?.ok) {
      const t = setTimeout(() => setMessage(null), 3000);
      return () => clearTimeout(t);
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
        });
        setMessage({ text: `Renamed to "${trimmed}".`, ok: true });
      } else {
        await masterDataOptions.createProductionStage({
          name: trimmed,
          role
        });
        setMessage({ text: `"${trimmed}" added.`, ok: true });
      }
      cancelEdit();
      await reload();
    } catch (e: any) {
      setMessage({
        text:
          e?.response?.data?.message ??
          `Could not ${editing ? "rename" : "add"} this entry. Check the name and try again.`,
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
      setMessage({ text: `"${target.name}" deleted.`, ok: true });
      await reload();
    } catch (e: any) {
      setMessage({
        text: e?.response?.data?.message ?? "Could not delete this entry.",
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
          Production Stages
        </Typography>
        <Chip
          size="small"
          label={loading ? "—" : list.length}
          sx={{ height: 20, fontSize: 11, fontWeight: 600 }}
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
          The stage dropdown on Finished Product receiving. Not shown for other
          categories.
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
              label={editing ? `Rename "${editing.name}"` : "Production stage"}
              placeholder="e.g. Coating"
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
              <InputLabel id="stage-role-select-label">Role</InputLabel>
              <Select<ProductionStageRole | "">
                labelId="stage-role-select-label"
                label="Role"
                value={role}
                onChange={(e) =>
                  setRole(e.target.value as ProductionStageRole)
                }
              >
                {PRODUCTION_STAGE_ROLES.map((r) => (
                  <MenuItem key={r} value={r}>
                    {r}
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
                  Cancel
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
                {editing ? "Save" : "Add"}
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
              Loading…
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
              None configured yet — add the first one above.
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
                    label={o.role}
                    variant="outlined"
                    sx={{ height: 20, fontSize: 11 }}
                  />
                  <Tooltip title="Rename">
                    <IconButton
                      size="small"
                      aria-label={`Rename ${o.name}`}
                      onClick={() => startEdit(o)}
                    >
                      <EditOutlinedIcon sx={{ fontSize: 17 }} />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton
                      size="small"
                      aria-label={`Delete ${o.name}`}
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
            ? `Delete "${pendingDelete.name}" from Production Stages? Samples already recorded with this stage keep it — only the dropdown changes.`
            : ""
        }
        onCancel={() => setPendingDelete(null)}
        onConfirm={confirmDelete}
      />
    </Paper>
  );
}
