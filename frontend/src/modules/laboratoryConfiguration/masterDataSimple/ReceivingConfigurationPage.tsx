import { useEffect, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Collapse,
  IconButton,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import PersonOutlineIcon from "@mui/icons-material/PersonOutlined";
import AssignmentOutlinedIcon from "@mui/icons-material/AssignmentOutlined";
import LayersOutlinedIcon from "@mui/icons-material/LayersOutlined";
import { PageHeader } from "../../../components/PageHeader";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { masterDataOptions } from "../../../services/masterDataOptions";

interface NamedOption {
  id: number;
  name: string;
}

interface NameListCardProps {
  title: string;
  usedFor: string;
  // What actually happens to existing data on delete - differs per list:
  // Cause of Testing is a real FK (delete is blocked once used), the other
  // two are only picklists behind a free-text/plain-string column.
  deleteNote: string;
  inputLabel: string;
  addPlaceholder: string;
  icon: React.ReactNode;
  load: () => Promise<NamedOption[]>;
  create: (name: string) => Promise<NamedOption>;
  update: (id: number, name: string) => Promise<NamedOption>;
  remove: (id: number) => Promise<unknown>;
}

// One reference list (Samplers / Cause of Testing / Production Stages).
// All three behave identically, so the add/edit/delete behavior lives here
// once rather than being repeated three times on the page.
function NameListCard({
  title,
  usedFor,
  deleteNote,
  inputLabel,
  addPlaceholder,
  icon,
  load,
  create,
  update,
  remove
}: NameListCardProps) {
  const theme = useTheme();
  const [list, setList] = useState<NamedOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [name, setName] = useState("");
  const [editing, setEditing] = useState<NamedOption | null>(null);
  const [pendingDelete, setPendingDelete] = useState<NamedOption | null>(null);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const reload = () =>
    load()
      .then(setList)
      .catch(() => setMessage({ text: `Could not load ${title.toLowerCase()}. Refresh to try again.`, ok: false }))
      .finally(() => setLoading(false));

  useEffect(() => { reload(); }, []);

  // Success is transient confirmation; errors stay until dismissed or
  // superseded so the analyst can act on them.
  useEffect(() => {
    if (message?.ok) {
      const t = setTimeout(() => setMessage(null), 3000);
      return () => clearTimeout(t);
    }
  }, [message]);

  const startEdit = (o: NamedOption) => {
    setEditing(o);
    setName(o.name);
    setMessage(null);
    inputRef.current?.focus();
  };

  const cancelEdit = () => { setEditing(null); setName(""); };

  const save = async () => {
    const trimmed = name.trim();
    if (!trimmed || saving) return;
    setSaving(true);
    setMessage(null);
    try {
      if (editing) {
        await update(editing.id, trimmed);
        setMessage({ text: `Renamed to "${trimmed}".`, ok: true });
      } else {
        await create(trimmed);
        setMessage({ text: `"${trimmed}" added.`, ok: true });
      }
      cancelEdit();
      await reload();
    } catch (e: any) {
      setMessage({
        text: e?.response?.data?.message ?? `Could not ${editing ? "rename" : "add"} this entry. Check the name and try again.`,
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
      await remove(target.id);
      if (editing?.id === target.id) cancelEdit();
      setMessage({ text: `"${target.name}" deleted.`, ok: true });
      await reload();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this entry.", ok: false });
    }
  };

  return (
    <Paper
      variant="outlined"
      sx={{ display: "flex", flexDirection: "column", borderRadius: 1.5, overflow: "hidden" }}
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
        <Box sx={{ display: "flex", color: theme.palette.primary.main }}>{icon}</Box>
        <Typography sx={{ fontSize: 15, fontWeight: 600, color: theme.palette.primary.main, flex: 1 }}>
          {title}
        </Typography>
        <Chip
          size="small"
          label={loading ? "—" : list.length}
          sx={{ height: 20, fontSize: 11, fontWeight: 600 }}
        />
      </Box>

      <Box sx={{ p: 2, display: "flex", flexDirection: "column", gap: 1.5, flex: 1 }}>
        {/* Fixed two-line box so the form + list below it line up across all
            three cards regardless of caption length */}
        <Typography variant="caption" sx={{ color: "text.secondary", lineHeight: 1.5, minHeight: 36 }}>
          {usedFor}
        </Typography>

        {/* Add / rename form - one visible label, Enter submits */}
        <Stack direction="row" spacing={1} sx={{
          alignItems: "flex-start"
        }}>
          <TextField
            inputRef={inputRef}
            size="small"
            fullWidth
            label={editing ? `Rename "${editing.name}"` : inputLabel}
            placeholder={addPlaceholder}
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") { e.preventDefault(); save(); }
              if (e.key === "Escape" && editing) cancelEdit();
            }}
          />
          {editing && (
            <Button size="small" onClick={cancelEdit} disabled={saving} sx={{ mt: 0.25, minWidth: 64 }}>
              Cancel
            </Button>
          )}
          <Button
            size="small"
            variant="contained"
            startIcon={editing ? undefined : <AddIcon />}
            onClick={save}
            disabled={!name.trim() || saving}
            sx={{ mt: 0.25, minWidth: 72, whiteSpace: "nowrap" }}
          >
            {editing ? "Save" : "Add"}
          </Button>
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

        {/* The list itself - compact rows, actions always visible (never
            hover-only, which is unusable on touch) */}
        <Box sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, overflow: "hidden" }}>
          {loading ? (
            <Typography sx={{ p: 2, fontSize: 13, color: "text.secondary" }}>Loading…</Typography>
          ) : list.length === 0 ? (
            <Typography sx={{ p: 2, fontSize: 13, color: "text.secondary", fontStyle: "italic" }}>
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
                    borderLeft: isEditing ? `3px solid ${theme.palette.primary.main}` : "none",
                    bgcolor: isEditing ? "action.selected" : "transparent",
                    transition: theme.transitions.create(["background-color"], { duration: 150 }),
                    "&:hover": { bgcolor: isEditing ? "action.selected" : "action.hover" }
                  }}
                >
                  <Typography sx={{ fontSize: 13, fontWeight: isEditing ? 600 : 400, flex: 1, wordBreak: "break-word" }}>
                    {o.name}
                  </Typography>
                  <Tooltip title="Rename">
                    <IconButton size="small" aria-label={`Rename ${o.name}`} onClick={() => startEdit(o)}>
                      <EditOutlinedIcon sx={{ fontSize: 17 }} />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton
                      size="small"
                      aria-label={`Delete ${o.name}`}
                      onClick={() => setPendingDelete(o)}
                      sx={{ color: "text.secondary", "&:hover": { color: theme.palette.error.main } }}
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
        message={pendingDelete ? `Delete "${pendingDelete.name}" from ${title}? ${deleteNote}` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={confirmDelete}
      />
    </Paper>
  );
}

// Formerly the single-purpose "Cause of Testing" page - broadened to cover
// every reference list used on the receiving form, since Cause of Testing
// had no edit/delete and Samplers/Production Stages had no management UI at
// all (they were hardcoded arrays in masterDataOptions.ts).
export function ReceivingConfigurationPage() {
  return (
    <>
      <PageHeader
        title="Receiving Configuration"
        subtitle="Reference lists offered on the sample receiving form, shared across all six receiving categories."
      />

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "repeat(2, 1fr)", lg: "repeat(3, 1fr)" },
          gap: 2.5,
          alignItems: "start",
          mt: 2
        }}
      >
        <NameListCard
          title="Samplers"
          usedFor="Offered as suggestions in the “Sampled By” field. Analysts can still type a name that isn’t listed."
          deleteNote="Samples already recorded with this name keep it — only the suggestion list changes."
          inputLabel="Sampler name"
          addPlaceholder="e.g. Ahmed Reda"
          icon={<PersonOutlineIcon sx={{ fontSize: 19 }} />}
          load={masterDataOptions.getSamplers}
          create={masterDataOptions.createSampler}
          update={masterDataOptions.updateSampler}
          remove={masterDataOptions.deleteSampler}
        />

        <NameListCard
          title="Cause of Testing"
          usedFor="Required on every sample. A cause already used by a sample cannot be deleted."
          deleteNote="If any sample already uses it, the delete will be rejected and nothing changes."
          inputLabel="Cause of testing"
          addPlaceholder="e.g. Investigation"
          icon={<AssignmentOutlinedIcon sx={{ fontSize: 19 }} />}
          load={masterDataOptions.getCausesOfTesting}
          create={masterDataOptions.createCauseOfTesting}
          update={masterDataOptions.updateCauseOfTesting}
          remove={masterDataOptions.deleteCauseOfTesting}
        />

        <NameListCard
          title="Production Stages"
          usedFor="The stage dropdown on Finished Product receiving. Not shown for other categories."
          deleteNote="Samples already recorded with this stage keep it — only the dropdown changes."
          inputLabel="Production stage"
          addPlaceholder="e.g. Coating"
          icon={<LayersOutlinedIcon sx={{ fontSize: 19 }} />}
          load={masterDataOptions.getProductionStages}
          create={masterDataOptions.createProductionStage}
          update={masterDataOptions.updateProductionStage}
          remove={masterDataOptions.deleteProductionStage}
        />
      </Box>
    </>
  );
}
