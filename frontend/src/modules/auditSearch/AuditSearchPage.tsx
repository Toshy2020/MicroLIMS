import { useState } from "react";
import { Paper, Box, TextField, Button, Table, TableHead, TableRow, TableCell, TableBody, Typography } from "@mui/material";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { AuditSearchService } from "./services/AuditSearchService";

const FIELDS = [
  ["fromDate", "From Date", "date"], ["toDate", "To Date", "date"],
  ["batchNumber", "Batch Number", "text"], ["controlNumber", "Control Number", "text"],
  ["sampleReferenceNumber", "Sample Reference", "text"], ["mediaLotNumber", "Media Lot Number", "text"],
  ["referenceStrainCode", "Reference Strain Code", "text"], ["cryovialCode", "Cryovial Code", "text"],
  ["entityName", "Entity", "text"], ["action", "Action", "text"]
] as const;

export function AuditSearchPage() {
  const [form, setForm] = useState<Record<string, string>>({});
  const [results, setResults] = useState<any[] | null>(null);

  const search = async () => {
    const payload: Record<string, any> = {};
    Object.entries(form).forEach(([k, v]) => { if (v) payload[k] = v; });
    const res = await AuditSearchService.search(payload);
    setResults(res);
  };

  return (
    <>
      <PageHeader title="Audit Search" subtitle="Search by any combination of date, batch, control, media, RS, cryovial, sample, or user." />

      <SectionTitle>Search Audit Trail</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 2 }}>
          {FIELDS.map(([key, label, type]) => (
            <TextField
              key={key} label={label} type={type} InputLabelProps={type === "date" ? { shrink: true } : undefined}
              value={form[key] ?? ""} onChange={(e) => setForm({ ...form, [key]: e.target.value })}
            />
          ))}
        </Box>
        <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 2 }}>
          <Button variant="contained" onClick={search}>Search</Button>
        </Box>
      </Paper>

      <SectionTitle>Results</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        {!results ? (
          <Typography color="text.secondary">Run a search to see results.</Typography>
        ) : results.length === 0 ? (
          <Typography color="text.secondary">No matching audit entries.</Typography>
        ) : (
          <Table>
            <TableHead><TableRow>
              <TableCell>Time</TableCell><TableCell>Entity</TableCell><TableCell>Action</TableCell>
              <TableCell>Sample Ref</TableCell><TableCell>Batch</TableCell><TableCell>User</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {results.map((r) => (
                <TableRow key={r.id}>
                  <TableCell>{new Date(r.timestamp).toLocaleString()}</TableCell>
                  <TableCell>{r.entityName}</TableCell><TableCell>{r.action}</TableCell>
                  <TableCell>{r.sampleReferenceNumber ?? "—"}</TableCell>
                  <TableCell>{r.batchNumber ?? "—"}</TableCell>
                  <TableCell>{r.userId}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Paper>
    </>
  );
}
