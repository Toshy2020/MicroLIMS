import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography
} from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { tableHeadSx } from "../../../theme";
import { serverMessage } from "../../../utils/errorMessage";
import { BiologicalWasteService } from "./services/BiologicalWasteService";
import { RecordDestructionDialog } from "./components/RecordDestructionDialog";
import type { BiologicalWasteDestruction, BiologicalWasteEligible } from "./types/biologicalWasteTypes";

const dt = (v: string) => new Date(v).toLocaleString();

const ITEM_TYPE_LABEL: Record<string, string> = {
  ThawedVial: "Thawed cryovial vial",
  UnthawedCryovials: "Unthawed vials (withdrawn batch)",
  Other: "Other waste"
};

// Biological waste destruction records: one signed record per autoclave
// cycle, covering cryovial vials due for destruction and any other waste
// destroyed in the same cycle.
export function BiologicalWasteDestructionPage() {
  const [records, setRecords] = useState<BiologicalWasteDestruction[]>([]);
  const [eligible, setEligible] = useState<BiologicalWasteEligible | null>(null);
  const [selected, setSelected] = useState<BiologicalWasteDestruction | null>(null);
  const [recording, setRecording] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => {
    BiologicalWasteService.getAll().then(setRecords)
      .catch((e) => setMessage({ text: serverMessage(e) ?? "Could not load destruction records.", ok: false }));
    BiologicalWasteService.getEligible().then(setEligible).catch(() => setEligible(null));
  };
  useEffect(load, []);

  const pendingVials = (eligible?.thawedVials.length ?? 0) + (eligible?.withdrawnBatches.reduce((n, b) => n + b.vialsRemaining, 0) ?? 0);

  return (
    <>
      <PageHeader
        title="Biological Waste Destruction"
        subtitle="Autoclave destruction of thawed cryovial vials, withdrawn cryovial batches and other waste in the same cycle."
      >
        <Button variant="contained" color="error" onClick={() => { setMessage(null); setRecording(true); }}>
          Record destruction cycle
        </Button>
      </PageHeader>

      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}
      {pendingVials > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {pendingVials} cryovial vial{pendingVials === 1 ? " is" : "s are"} awaiting destruction.
        </Alert>
      )}

      <SectionTitle>Destruction records</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>Reference</TableCell><TableCell>Cycle started</TableCell><TableCell>Autoclave</TableCell>
                <TableCell>Program</TableCell><TableCell>Cycle no.</TableCell><TableCell>Cryovial items</TableCell>
                <TableCell>Other items</TableCell><TableCell>Signed by</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {records.length === 0 && (
                <TableRow><TableCell colSpan={8} sx={{ color: "text.secondary" }}>No destruction cycles recorded yet.</TableCell></TableRow>
              )}
              {records.map((r) => (
                <TableRow key={r.id} hover sx={{ cursor: "pointer" }} onClick={() => setSelected(r)}>
                  <TableCell sx={{ fontFamily: "monospace", fontWeight: 700 }}>{r.reference}</TableCell>
                  <TableCell>{dt(r.cycleStartedAt)}</TableCell>
                  <TableCell>{r.autoclaveName}</TableCell>
                  <TableCell>{r.programCode}: {r.programName}</TableCell>
                  <TableCell>{r.cycleNumber}</TableCell>
                  <TableCell>{r.cryovialItemCount}</TableCell>
                  <TableCell>{r.otherItemCount}</TableCell>
                  <TableCell>{r.signedByName}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      <FloatingDialog
        open={!!selected}
        onClose={() => setSelected(null)}
        maxWidth="md"
        title={selected ? `${selected.reference}: Biological Waste Destruction` : ""}
        actions={<Button onClick={() => setSelected(null)}>Close</Button>}
      >
        {selected && (
          <>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 1, mb: 2 }}>
              <Typography variant="body2">Autoclave: <strong>{selected.autoclaveName} ({selected.autoclaveCode})</strong></Typography>
              <Typography variant="body2">Program: <strong>{selected.programCode}: {selected.programName}</strong></Typography>
              <Typography variant="body2">Parameters: <strong>{selected.temperature}°C, {selected.cycleTimeMinutes} min ({selected.loadType})</strong></Typography>
              <Typography variant="body2">Cycle: <strong>{selected.cycleNumber}, started {dt(selected.cycleStartedAt)}</strong></Typography>
              <Typography variant="body2">Signed: <strong>{selected.signedByName} ({selected.signedByRole}), {dt(selected.signedAt)}</strong></Typography>
              {selected.notes && <Typography variant="body2">Notes: <strong>{selected.notes}</strong></Typography>}
            </Box>
            <Table size="small">
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell>Type</TableCell><TableCell>Description</TableCell><TableCell>Quantity</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {selected.items.map((i) => (
                  <TableRow key={i.id}>
                    <TableCell>{ITEM_TYPE_LABEL[i.itemType] ?? i.itemType}</TableCell>
                    <TableCell>{i.description}</TableCell>
                    <TableCell>{i.quantity} {i.unit}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </>
        )}
      </FloatingDialog>

      <RecordDestructionDialog
        open={recording}
        onClose={() => setRecording(false)}
        onRecorded={(reference) => {
          setRecording(false);
          setMessage({ text: `Destruction cycle ${reference} recorded and signed.`, ok: true });
          load();
        }}
      />
    </>
  );
}
