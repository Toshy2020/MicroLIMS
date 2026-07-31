import { useEffect, useState } from "react";
import { Paper, TextField, Button, Table, TableBody, TableRow, TableCell, Stack } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { apiClient } from "../../../services/apiClient";

export function CauseOfTestingPage() {
  const [list, setList] = useState<any[]>([]);
  const [name, setName] = useState("");
  const load = () => masterDataOptions.getCausesOfTesting().then(setList);
  useEffect(() => { load(); }, []);

  const add = async () => {
    if (!name) return;
    await apiClient.post("/masterdata/causes-of-testing", JSON.stringify(name), { headers: { "Content-Type": "application/json" } });
    setName(""); load();
  };

  return (
    <>
      <PageHeader title="Cause of Testing" subtitle="Shared list across all six receiving categories." />
      <SectionTitle>Cause of Testing</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Stack direction="row" spacing={1.5} sx={{ mb: 2 }}>
          <TextField size="small" placeholder="e.g. Investigation" value={name} onChange={(e) => setName(e.target.value)} sx={{ maxWidth: 280 }} />
          <Button variant="outlined" onClick={add}>Add</Button>
        </Stack>
        <Table><TableBody>{list.map((c) => <TableRow key={c.id}><TableCell>{c.name}</TableCell></TableRow>)}</TableBody></Table>
      </Paper>
    </>
  );
}
