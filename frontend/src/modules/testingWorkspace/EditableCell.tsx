import { useState } from "react";
import { Box, TextField, IconButton, Alert } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";

interface Props {
  value: string;
  editable: boolean;
  onSave: (newValue: string) => Promise<void>;
}

// Table-cell variant of EditableInfoRow - just the value + pencil, no
// label (the column header already provides that context).
export function EditableCell({ value, editable, onSave }: Props) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const startEdit = () => {
    setDraft(value);
    setError(null);
    setEditing(true);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await onSave(draft);
      setEditing(false);
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not save this change.");
    } finally {
      setSaving(false);
    }
  };

  if (editing) {
    return (
      <Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.25 }}>
          <TextField size="small" value={draft} onChange={(e) => setDraft(e.target.value)} disabled={saving} autoFocus sx={{ width: 130 }} />
          <IconButton size="small" onClick={save} disabled={saving} title="Save"><CheckIcon fontSize="small" color="success" /></IconButton>
          <IconButton size="small" onClick={() => setEditing(false)} disabled={saving} title="Cancel"><CloseIcon fontSize="small" /></IconButton>
        </Box>
        {error && <Alert severity="error" sx={{ mt: 0.5, py: 0, fontSize: 11 }}>{error}</Alert>}
      </Box>
    );
  }

  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
      <span>{value || "—"}</span>
      {editable && (
        <IconButton size="small" onClick={startEdit} title="Edit" sx={{ p: 0.25 }}>
          <EditIcon sx={{ fontSize: 13 }} />
        </IconButton>
      )}
    </Box>
  );
}
