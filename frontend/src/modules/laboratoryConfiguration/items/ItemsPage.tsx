import { useState } from "react";
import { Box, Paper, TextField, Select, MenuItem, Button, Stack, Typography, Alert, IconButton } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { ItemTable } from "./ItemTable";
import { ItemService } from "./services/ItemService";

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
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [sopNumber, setSopNumber] = useState("");
  const [category, setCategory] = useState("FinishedProduct");
  const [testCodes, setTestCodes] = useState<string[]>([""]);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const updateTestCode = (i: number, value: string) => setTestCodes((t) => t.map((v, idx) => (idx === i ? value : v)));
  const addTestCode = () => setTestCodes((t) => [...t, ""]);
  const removeTestCode = (i: number) => setTestCodes((t) => (t.length > 1 ? t.filter((_, idx) => idx !== i) : t));

  const createItem = async () => {
    setMessage(null);
    const codes = testCodes.filter(Boolean);
    if (!name || !code || codes.length === 0) {
      setMessage({ text: "Name, Code, and at least one assigned test are required.", ok: false });
      return;
    }
    try {
      await ItemService.create({ name, code, category, sopNumber, assignedTests: codes.map((tc) => ({ testCode: tc, displayName: tc })) });
      setMessage({ text: `Item "${name}" created.`, ok: true });
      setName(""); setCode(""); setSopNumber(""); setTestCodes([""]);
      setRefreshKey((k) => k + 1);
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not create item.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Items" subtitle="Configure which tests are auto-assigned when a sample is received." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>New Item</SectionTitle>
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
            <Stack spacing={1}>
              {testCodes.map((tc, i) => (
                <Stack direction="row" spacing={1} key={i} alignItems="center">
                  <TextField size="small" placeholder="e.g. TAMC, TYMC, PATHOGEN_ECOLI" value={tc} onChange={(e) => updateTestCode(i, e.target.value)} sx={{ minWidth: 260 }} />
                  <IconButton size="small" onClick={() => removeTestCode(i)}><CloseIcon fontSize="small" /></IconButton>
                </Stack>
              ))}
            </Stack>
            <Button size="small" onClick={addTestCode} sx={{ mt: 1 }}>+ Add Test</Button>
          </Box>

          <Box><Button variant="contained" onClick={createItem}>Save Item</Button></Box>
        </Stack>
      </Paper>

      <SectionTitle>Configured Items</SectionTitle>
      <ItemTable key={refreshKey} />
    </>
  );
}
