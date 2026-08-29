import { useEffect, useState } from "react";
import { Paper, TextField, Button, Stack, Select, MenuItem } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { DataTable } from "../../../components/DataTable";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { apiClient } from "../../../services/apiClient";

export function DiluentsPage() {
  const [diluents, setDiluents] = useState<any[]>([]);
  const [neutralizers, setNeutralizers] = useState<any[]>([]);
  const [materials, setMaterials] = useState<any[]>([]);
  const [name, setName] = useState("");
  const [tracked, setTracked] = useState("No");
  const [materialId, setMaterialId] = useState("");
  const [neutName, setNeutName] = useState("");

  const load = () => {
    masterDataOptions.getDiluentTypes().then(setDiluents);
    masterDataOptions.getNeutralizers().then(setNeutralizers);
    masterDataOptions.getMaterials("DehydratedMedia").then(setMaterials);
  };
  useEffect(() => { load(); }, []);

  const addDiluent = async () => {
    await apiClient.post("/masterdata/diluent-types", { name, requiresBatchTracking: tracked === "Yes", materialId: materialId || null });
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
            <Select size="small" displayEmpty value={materialId} onChange={(e) => setMaterialId(e.target.value)}>
              <MenuItem value=""><em>Material</em></MenuItem>
              {materials.map((m) => <MenuItem key={m.id} value={m.id}>{m.materialName}</MenuItem>)}
            </Select>
          )}
          <Button variant="outlined" onClick={addDiluent}>Add</Button>
        </Stack>
        <DataTable
          columns={[
            { key: "name", label: "Name" },
            { key: "requiresBatchTracking", label: "Batch Tracked?", render: (d) => <StatusBadge status={d.requiresBatchTracking ? "Yes" : "No"} /> }
          ]}
          rows={diluents}
          getRowId={(d) => d.id}
        />
      </Paper>

      <SectionTitle>Neutralizers</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Stack direction="row" spacing={1.5} sx={{ mb: 2 }}>
          <TextField size="small" placeholder="e.g. Lecithin" value={neutName} onChange={(e) => setNeutName(e.target.value)} sx={{ maxWidth: 280 }} />
          <Button variant="outlined" onClick={addNeutralizer}>Add</Button>
        </Stack>
        <DataTable columns={[{ key: "name", label: "Name" }]} rows={neutralizers} getRowId={(n) => n.id} />
      </Paper>
    </>
  );
}
