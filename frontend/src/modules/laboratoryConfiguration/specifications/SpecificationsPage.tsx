import { useEffect, useState } from "react";
import { Paper, Stack, TextField, Select, MenuItem, Button, Typography, Alert, Box } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
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
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  useEffect(() => { ItemService.getAll().then(setItems); }, []);

  const loadSpecs = (id: string) => {
    if (!id) { setSpecs([]); return; }
    SpecificationService.getForItem(Number(id)).then(setSpecs);
  };

  const create = async () => {
    if (!itemId || !testCode) return;
    setMessage(null);
    try {
      await SpecificationService.create(Number(itemId), testCode, alertLimit, actionLimit, specLimit);
      setMessage({ text: "Specification saved.", ok: true });
      setTestCode(""); setAlertLimit(""); setActionLimit(""); setSpecLimit("");
      loadSpecs(itemId);
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not save specification.", ok: false });
    }
  };

  return (
    <>
      <PageHeader title="Specifications" subtitle="Alert, Action, and Specification limits per item and test." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>Select Item</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Select size="small" displayEmpty value={itemId} onChange={(e) => { setItemId(e.target.value); loadSpecs(e.target.value); }} sx={{ minWidth: 260 }}>
          <MenuItem value=""><em>Select an item</em></MenuItem>
          {items.map((i) => <MenuItem key={i.id} value={i.id}>{i.name} ({i.code})</MenuItem>)}
        </Select>
      </Paper>

      {itemId && (
        <>
          <SectionTitle>Add Specification</SectionTitle>
          <Paper sx={{ p: 2.5, mb: 3 }}>
            <Stack direction="row" spacing={2} flexWrap="wrap" alignItems="center">
              <TextField size="small" label="Test Code" value={testCode} onChange={(e) => setTestCode(e.target.value)} />
              <TextField size="small" label="Alert Limit" value={alertLimit} onChange={(e) => setAlertLimit(e.target.value)} />
              <TextField size="small" label="Action Limit" value={actionLimit} onChange={(e) => setActionLimit(e.target.value)} />
              <TextField size="small" label="Spec Limit" value={specLimit} onChange={(e) => setSpecLimit(e.target.value)} />
              <Button variant="contained" onClick={create}>Save</Button>
            </Stack>
          </Paper>

          <SectionTitle>Existing Specifications</SectionTitle>
          {specs.length === 0 ? (
            <Typography sx={{ color: "#9ca3af", fontSize: 13 }}>No specifications yet for this item.</Typography>
          ) : (
            <Stack spacing={1}>
              {specs.map((s) => (
                <Paper key={s.id} sx={{ p: 2 }}>
                  <Stack direction="row" spacing={3}>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Test</Typography><Typography sx={{ fontWeight: 600 }}>{s.testCode}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Alert</Typography><Typography>{s.alertLimit}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Action</Typography><Typography>{s.actionLimit}</Typography></Box>
                    <Box><Typography sx={{ fontSize: 11, color: "#9ca3af" }}>Spec</Typography><Typography>{s.specLimit}</Typography></Box>
                  </Stack>
                </Paper>
              ))}
            </Stack>
          )}
        </>
      )}
    </>
  );
}
