import { useEffect, useState } from "react";
import { Paper, Stack, TextField, Select, MenuItem, Button, Typography, Alert, Box, IconButton } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { ConfirmationDialog } from "../../../components/ConfirmationDialog";
import { ItemService, Item } from "../items/services/ItemService";
import { SpecificationService } from "./services/SpecificationService";

interface Spec { id: number; testCode: string; alertLimit: string; actionLimit: string; specLimit: string }

// Alert -> Action -> Specification limits per Item/TestCode - read by
// the backend calculation engines (Water/Product) for comparison.
export function SpecificationsPage() {
  const [items, setItems] = useState<Item[]>([]);
  const [itemId, setItemId] = useState("");
  const [specs, setSpecs] = useState<Spec[]>([]);
  const [testCode, setTestCode] = useState("");
  const [alertLimit, setAlertLimit] = useState("");
  const [actionLimit, setActionLimit] = useState("");
  const [specLimit, setSpecLimit] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<Spec | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => { ItemService.getAll().then(setItems); }, []);

  const selectedItem = items.find((i) => i.id === Number(itemId));

  const loadSpecs = (id: string) => {
    if (!id) { setSpecs([]); return; }
    SpecificationService.getForItem(Number(id)).then(setSpecs);
  };

  const resetForm = () => {
    setEditingId(null);
    setTestCode(""); setAlertLimit(""); setActionLimit(""); setSpecLimit("");
  };

  const startEdit = (s: Spec) => {
    setEditingId(s.id);
    setTestCode(s.testCode);
    setAlertLimit(s.alertLimit);
    setActionLimit(s.actionLimit);
    setSpecLimit(s.specLimit);
    setMessage(null);
  };

  const save = async () => {
    if (!itemId || !testCode) return;
    setMessage(null);
    try {
      if (editingId) {
        await SpecificationService.update(editingId, testCode, alertLimit, actionLimit, specLimit);
        setMessage({ text: "Specification updated.", ok: true });
      } else {
        await SpecificationService.create(Number(itemId), testCode, alertLimit, actionLimit, specLimit);
        setMessage({ text: "Specification saved.", ok: true });
      }
      resetForm();
      loadSpecs(itemId);
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save specification.", ok: false });
    }
  };

  const remove = async (s: Spec) => {
    setMessage(null);
    try {
      await SpecificationService.remove(s.id);
      setPendingDelete(null);
      loadSpecs(itemId);
    } catch (e: any) {
      setPendingDelete(null);
      setMessage({ text: e?.response?.data?.message ?? "Could not delete this specification.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Specifications" subtitle="Alert, Action, and Specification limits per item and test." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>Select Item</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Select
          size="small" displayEmpty value={itemId}
          onChange={(e) => { setItemId(e.target.value); resetForm(); loadSpecs(e.target.value); }}
          sx={{ minWidth: 260 }}
        >
          <MenuItem value=""><em>Select an item</em></MenuItem>
          {items.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code})</MenuItem>)}
        </Select>
      </Paper>

      {itemId && (
        <>
          <SectionTitle>{editingId ? "Edit Specification" : "Add Specification"}</SectionTitle>
          <Paper sx={{ p: 2.5, mb: 3 }}>
            {selectedItem && selectedItem.assignedTests.length === 0 && (
              <Alert severity="warning" sx={{ mb: 2 }}>
                This item has no assigned tests yet - assign tests to it under Laboratory Configuration &gt; Items before adding specifications.
              </Alert>
            )}
            <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
              <Select size="small" displayEmpty value={testCode} onChange={(e) => setTestCode(e.target.value)} disabled={!selectedItem || selectedItem.assignedTests.length === 0} sx={{ minWidth: 220 }}>
                <MenuItem value=""><em>Test Code</em></MenuItem>
                {selectedItem?.assignedTests.map((t) => <MenuItem key={t.testCode} value={t.testCode}>{t.testCode} — {t.displayName}</MenuItem>)}
              </Select>
              <TextField size="small" label="Alert Limit" value={alertLimit} onChange={(e) => setAlertLimit(e.target.value)} />
              <TextField size="small" label="Action Limit" value={actionLimit} onChange={(e) => setActionLimit(e.target.value)} />
              <TextField size="small" label="Spec Limit" value={specLimit} onChange={(e) => setSpecLimit(e.target.value)} />
              {editingId && <Button onClick={resetForm}>Cancel</Button>}
              <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Save"}</Button>
            </Stack>
          </Paper>

          <SectionTitle>Existing Specifications</SectionTitle>
          {specs.length === 0 ? (
            <Typography sx={{ color: "#9ca3af", fontSize: 13 }}>No specifications yet for this item.</Typography>
          ) : (
            <Stack spacing={1}>
              {specs.map((s) => (
                <Paper key={s.id} sx={{ p: 2, display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                  <Stack direction="row" spacing={3}>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Test</Typography><Typography sx={{ fontWeight: 600 }}>{s.testCode}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Alert</Typography><Typography>{s.alertLimit}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Action</Typography><Typography>{s.actionLimit}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Spec</Typography><Typography>{s.specLimit}</Typography></Box>
                  </Stack>
                  <Stack direction="row">
                    <IconButton size="small" onClick={() => startEdit(s)} title="Edit"><EditIcon fontSize="small" /></IconButton>
                    <IconButton size="small" color="error" onClick={() => setPendingDelete(s)} title="Delete"><DeleteIcon fontSize="small" /></IconButton>
                  </Stack>
                </Paper>
              ))}
            </Stack>
          )}
        </>
      )}

      <ConfirmationDialog
        open={pendingDelete != null}
        message={pendingDelete ? `Delete the ${pendingDelete.testCode} specification? This cannot be undone.` : ""}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && remove(pendingDelete)}
      />
    </>
  );
}
