import { useEffect, useState } from "react";
import { Paper, TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Stack, Select, MenuItem } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { masterDataOptions, mediaClassLabel } from "../../../services/masterDataOptions";
import { apiClient } from "../../../services/apiClient";

export function DiluentsPage() {
  const [diluents, setDiluents] = useState<any[]>([]);
  const [neutralizers, setNeutralizers] = useState<any[]>([]);
  const [mediaTypes, setMediaTypes] = useState<any[]>([]);
  const [name, setName] = useState("");
  const [tracked, setTracked] = useState("No");
  const [mediaTypeId, setMediaTypeId] = useState("");
  const [neutName, setNeutName] = useState("");

  const load = () => {
    masterDataOptions.getDiluentTypes().then(setDiluents);
    masterDataOptions.getNeutralizers().then(setNeutralizers);
    masterDataOptions.getMediaTypes().then(setMediaTypes);
  };
  useEffect(() => { load(); }, []);

  const addDiluent = async () => {
    await apiClient.post("/masterdata/diluent-types", { name, requiresBatchTracking: tracked === "Yes", mediaTypeId: mediaTypeId || null });
    setName(""); load();
  };
  const addNeutralizer = async () => {
    await apiClient.post("/masterdata/neutralizers", JSON.stringify(neutName), { headers: { "Content-Type": "application/json" } });
    setNeutName(""); load();
  };

  return (
    <>
      <PageHeader title="Diluents & Neutralizers" subtitle="Test Preparation reference lists." />
      <SectionTitle>Diluent Types</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack direction="row" spacing={1.5} sx={{ mb: 2 }}>
          <TextField size="small" placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} />
          <Select size="small" value={tracked} onChange={(e) => setTracked(e.target.value)}>
            <MenuItem value="No">No batch tracking</MenuItem><MenuItem value="Yes">Requires batch tracking</MenuItem>
          </Select>
          {tracked === "Yes" && (
            <Select size="small" displayEmpty value={mediaTypeId} onChange={(e) => setMediaTypeId(e.target.value)}>
              <MenuItem value=""><em>Media Type</em></MenuItem>
              {mediaTypes.map((m) => <MenuItem key={m.id} value={m.id}>{mediaClassLabel(m.class)}</MenuItem>)}
            </Select>
          )}
          <Button variant="outlined" onClick={addDiluent}>Add</Button>
        </Stack>
        <Table>
          <TableHead><TableRow><TableCell>Name</TableCell><TableCell>Batch Tracked?</TableCell></TableRow></TableHead>
          <TableBody>{diluents.map((d) => (
            <TableRow key={d.id}><TableCell>{d.name}</TableCell><TableCell><StatusBadge status={d.requiresBatchTracking ? "Yes" : "No"} /></TableCell></TableRow>
          ))}</TableBody>
        </Table>
      </Paper>

      <SectionTitle>Neutralizers</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Stack direction="row" spacing={1.5} sx={{ mb: 2 }}>
          <TextField size="small" placeholder="e.g. Lecithin" value={neutName} onChange={(e) => setNeutName(e.target.value)} sx={{ maxWidth: 280 }} />
          <Button variant="outlined" onClick={addNeutralizer}>Add</Button>
        </Stack>
        <Table><TableBody>{neutralizers.map((n) => <TableRow key={n.id}><TableCell>{n.name}</TableCell></TableRow>)}</TableBody></Table>
      </Paper>
    </>
  );
}
