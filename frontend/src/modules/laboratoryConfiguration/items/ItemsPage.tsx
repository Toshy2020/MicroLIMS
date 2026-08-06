import { useEffect, useState } from "react";
import { Box, Paper, TextField, Select, MenuItem, Button, Stack, Typography, Alert } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { TestCodePickerMulti } from "../../../components/TestCodePickerMulti";
import { ItemTable } from "./ItemTable";
import { ItemService, Item } from "./services/ItemService";

const CATEGORIES = [
  { value: "FinishedProduct", label: "Product" },
  { value: "RawMaterial", label: "Raw Material" },
  { value: "PackagingMaterial", label: "Packaging Material" },
  { value: "Water", label: "Water" },
  { value: "EnvironmentalMonitoring", label: "Environmental Monitoring" },
  { value: "AfterCleaning", label: "After Cleaning" },
  { value: "GPT", label: "GPT" }
];

// Section Head owns this - the Master Configuration the Workflow Engine
// reads on every sample receipt (Frozen Principle #1). Category-specific
// setup (Specifications, Sampling Points, Rooms, Machine Parts) lives in
// its own page under Laboratory Configuration.
export function ItemsPage() {
  const [items, setItems] = useState<Item[]>([]);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [sopNumber, setSopNumber] = useState("");
  const [category, setCategory] = useState("FinishedProduct");
  const [testCodes, setTestCodes] = useState<string[]>([]);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => { ItemService.getAll().then(setItems); };
  useEffect(() => { load(); }, []);

  const startEdit = (item: Item) => {
    setEditingId(item.id);
    setName(item.name);
    setCode(item.code);
    setSopNumber(item.sopNumber);
    setCategory(item.category);
    setTestCodes(item.assignedTests.map((t) => t.testCode));
    setMessage(null);
  };

  const cancelEdit = () => {
    setEditingId(null);
    setName(""); setCode(""); setSopNumber(""); setCategory("FinishedProduct"); setTestCodes([]);
  };

  const save = async () => {
    setMessage(null);
    if (!name || !code || testCodes.length === 0) {
      setMessage({ text: "Name, Code, and at least one assigned test are required.", ok: false });
      return;
    }
    const payload = { name, code, category, sopNumber, assignedTests: testCodes.map((tc) => ({ testCode: tc, displayName: tc })) };
    try {
      if (editingId) {
        await ItemService.update(editingId, payload);
        setMessage({ text: `Item "${name}" updated.`, ok: true });
      } else {
        await ItemService.create(payload);
        setMessage({ text: `Item "${name}" created.`, ok: true });
      }
      cancelEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? `Could not ${editingId ? "update" : "create"} item.`, ok: false });
    }
  };

  const remove = async (item: Item) => {
    setMessage(null);
    try {
      await ItemService.remove(item.id);
      setMessage({ text: `Item "${item.name}" deleted.`, ok: true });
      if (editingId === item.id) cancelEdit();
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not delete item.", ok: false });
    }
  };

  const toggleFreeze = async (item: Item) => {
    setMessage(null);
    try {
      if (item.isActive) {
        await ItemService.freeze(item.id);
        setMessage({ text: `Item "${item.name}" frozen.`, ok: true });
      } else {
        await ItemService.unfreeze(item.id);
        setMessage({ text: `Item "${item.name}" unfrozen.`, ok: true });
      }
      load();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not update item status.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Items" subtitle="Configure which tests are auto-assigned when a sample is received." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>{editingId ? "Edit Item" : "New Item"}</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack spacing={2}>
          <Stack direction="row" spacing={2} flexWrap="wrap">
            <TextField size="small" label="Item Name" value={name} onChange={(e) => setName(e.target.value)} />
            <TextField size="small" label="Item Code" value={code} onChange={(e) => setCode(e.target.value)} />
            <TextField size="small" label="SOP Number" value={sopNumber} onChange={(e) => setSopNumber(e.target.value)} />
            <Select size="small" value={category} onChange={(e) => setCategory(e.target.value)} sx={{ minWidth: 220 }}>
              {CATEGORIES.map((c) => <MenuItem key={c.value} value={c.value}>{c.label}</MenuItem>)}
            </Select>
          </Stack>

          <Box>
            <Typography variant="body2" sx={{ mb: 1 }}>Assigned Tests (auto-created on sample receipt)</Typography>
            <TestCodePickerMulti value={testCodes} onChange={setTestCodes} label="Assigned Tests" sx={{ minWidth: 320 }} />
          </Box>

          <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1 }}>
            {editingId && <Button onClick={cancelEdit}>Cancel</Button>}
            <Button variant="contained" onClick={save}>{editingId ? "Save Changes" : "Save Item"}</Button>
          </Box>
        </Stack>
      </Paper>

      <SectionTitle>Configured Items</SectionTitle>
      <ItemTable items={items} onEdit={startEdit} onDelete={remove} onToggleFreeze={toggleFreeze} />
    </>
  );
}
