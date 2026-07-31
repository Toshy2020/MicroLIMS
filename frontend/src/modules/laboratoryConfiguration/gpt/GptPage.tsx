import { useEffect, useState } from "react";
import { Box, Paper, Typography, TextField, Select, MenuItem, Button, Stack, Alert, Table, TableHead, TableRow, TableCell, TableBody } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { GptService } from "./services/GptService";

// General Agar (Recovery%), General Broth (Turbid/Clear), Selective
// (Inhibition + Indication) - mirrors GptWorkflowEngine's 3 mechanics exactly.
export function GptPage() {
  const [lots, setLots] = useState<any[]>([]);
  const [selectedId, setSelectedId] = useState<number | "">("");
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const [agar, setAgar] = useState<Record<string, any>>({ negativeControlGrowth: false });
  const [broth, setBroth] = useState("Turbid");
  const [inhib, setInhib] = useState<Record<string, any>>({ passed: true });
  const [indic, setIndic] = useState<Record<string, any>>({ passed: true });

  const load = () => GptService.getAllMedia().then(setLots);
  useEffect(() => { load(); }, []);

  const selected = lots.find((l) => l.id === selectedId);

  const advance = async (mediaId: number) => {
    setMessage(null);
    try { await GptService.advanceStage(mediaId); load(); }
    catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Could not advance.", ok: false }); }
  };

  const submitAgar = async () => {
    try {
      await GptService.generalAgar({ mediaId: selectedId, ...agar, cryovialId: Number(agar.cryovialId), oldMediaResult: Number(agar.oldMediaResult), newMediaResult: Number(agar.newMediaResult) });
      setMessage({ text: "Result recorded.", ok: true }); load();
    } catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Failed.", ok: false }); }
  };
  const submitBroth = async () => {
    try { await GptService.generalBroth({ mediaId: selectedId, turbidResult: broth }); setMessage({ text: "Result recorded.", ok: true }); load(); }
    catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Failed.", ok: false }); }
  };
  const submitSelective = async (panel: "Inhibition" | "Indication") => {
    const state = panel === "Inhibition" ? inhib : indic;
    try {
      await GptService.selective({ mediaId: selectedId, panel, ...state, cryovialId: Number(state.cryovialId) });
      setMessage({ text: `${panel} result recorded.`, ok: true }); load();
    } catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "Failed.", ok: false }); }
  };

  return (
    <>
      <PageHeader title="Growth Promotion Test (GPT)" subtitle="Media preparation → Sterility → Recovery → Release." />
      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}

      <SectionTitle>Select Media Lot</SectionTitle>
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Select displayEmpty fullWidth value={selectedId} onChange={(e) => setSelectedId(Number(e.target.value))}>
          <MenuItem value=""><em>Select a lot</em></MenuItem>
          {lots.map((l) => <MenuItem key={l.id} value={l.id}>{l.lotNumber} — {l.mediaType?.class} — {l.gptStage}</MenuItem>)}
        </Select>
        {selected && (
          <Box sx={{ mt: 1.5, display: "flex", alignItems: "center", gap: 1.5 }}>
            <StatusBadge status={selected.gptStage} />
            {selected.gptStage !== "Release" && selected.gptStage !== "Rejected" && (
              <Button size="small" variant="outlined" onClick={() => advance(selected.id)}>Advance Stage</Button>
            )}
          </Box>
        )}
      </Paper>

      {selected?.gptStage === "Recovery" && selected.mediaType?.class === "GeneralAgar" && (
        <Paper sx={{ p: 2.5, mb: 3, maxWidth: 640 }}>
          <Typography sx={{ fontWeight: 700, mb: 1 }}>General Agar Challenge</Typography>
          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
            <TextField label="Organism" value={agar.organismName ?? ""} onChange={(e) => setAgar({ ...agar, organismName: e.target.value })} />
            <TextField label="Cryovial ID" value={agar.cryovialId ?? ""} onChange={(e) => setAgar({ ...agar, cryovialId: e.target.value })} />
            <TextField label="ATCC" value={agar.atcc ?? ""} onChange={(e) => setAgar({ ...agar, atcc: e.target.value })} />
            <TextField label="Initial Inoculum" value={agar.initialInoculum ?? ""} onChange={(e) => setAgar({ ...agar, initialInoculum: e.target.value })} />
            <TextField label="Old Media Result" value={agar.oldMediaResult ?? ""} onChange={(e) => setAgar({ ...agar, oldMediaResult: e.target.value })} />
            <TextField label="New Media Result" value={agar.newMediaResult ?? ""} onChange={(e) => setAgar({ ...agar, newMediaResult: e.target.value })} />
          </Box>
          <Select sx={{ mt: 2 }} value={agar.negativeControlGrowth ? "yes" : "no"} onChange={(e) => setAgar({ ...agar, negativeControlGrowth: e.target.value === "yes" })}>
            <MenuItem value="no">Negative Control: No Growth</MenuItem>
            <MenuItem value="yes">Negative Control: Growth</MenuItem>
          </Select>
          <Box sx={{ mt: 2 }}><Button variant="contained" onClick={submitAgar}>Record Result</Button></Box>
        </Paper>
      )}

      {selected?.gptStage === "Recovery" && selected.mediaType?.class === "GeneralBroth" && (
        <Paper sx={{ p: 2.5, mb: 3, maxWidth: 400 }}>
          <Typography sx={{ fontWeight: 700, mb: 1 }}>General Broth Result</Typography>
          <Select fullWidth value={broth} onChange={(e) => setBroth(e.target.value)}>
            <MenuItem value="Turbid">Turbid (pass)</MenuItem>
            <MenuItem value="Clear">Clear (fail)</MenuItem>
          </Select>
          <Box sx={{ mt: 2 }}><Button variant="contained" onClick={submitBroth}>Record Result</Button></Box>
        </Paper>
      )}

      {selected?.gptStage === "Recovery" && (selected.mediaType?.class === "SelectiveAgar" || selected.mediaType?.class === "SelectiveBroth") && (
        <Paper sx={{ p: 2.5, mb: 3 }}>
          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 3 }}>
            <Box>
              <Typography sx={{ fontWeight: 700, color: "#7b2d8e", mb: 1 }}>Inhibition Test</Typography>
              <Stack spacing={1.5}>
                <TextField label="Organism" value={inhib.organismName ?? ""} onChange={(e) => setInhib({ ...inhib, organismName: e.target.value })} />
                <TextField label="Cryovial ID" value={inhib.cryovialId ?? ""} onChange={(e) => setInhib({ ...inhib, cryovialId: e.target.value })} />
                <TextField label="Observation" multiline rows={2} value={inhib.observationText ?? ""} onChange={(e) => setInhib({ ...inhib, observationText: e.target.value })} />
                <Select value={inhib.passed ? "pass" : "fail"} onChange={(e) => setInhib({ ...inhib, passed: e.target.value === "pass" })}>
                  <MenuItem value="pass">Pass</MenuItem><MenuItem value="fail">Fail</MenuItem>
                </Select>
                <Button variant="outlined" onClick={() => submitSelective("Inhibition")}>Record Inhibition</Button>
              </Stack>
            </Box>
            <Box>
              <Typography sx={{ fontWeight: 700, color: "#7b2d8e", mb: 1 }}>Indication Test</Typography>
              <Stack spacing={1.5}>
                <TextField label="Organism" value={indic.organismName ?? ""} onChange={(e) => setIndic({ ...indic, organismName: e.target.value })} />
                <TextField label="Cryovial ID" value={indic.cryovialId ?? ""} onChange={(e) => setIndic({ ...indic, cryovialId: e.target.value })} />
                <TextField label="Observation" multiline rows={2} value={indic.observationText ?? ""} onChange={(e) => setIndic({ ...indic, observationText: e.target.value })} />
                <Select value={indic.passed ? "pass" : "fail"} onChange={(e) => setIndic({ ...indic, passed: e.target.value === "pass" })}>
                  <MenuItem value="pass">Pass</MenuItem><MenuItem value="fail">Fail</MenuItem>
                </Select>
                <Button variant="outlined" onClick={() => submitSelective("Indication")}>Record Indication</Button>
              </Stack>
            </Box>
          </Box>
        </Paper>
      )}

      <SectionTitle>All Media Lots</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Table>
          <TableHead><TableRow><TableCell>Lot</TableCell><TableCell>Class</TableCell><TableCell>Stage</TableCell></TableRow></TableHead>
          <TableBody>
            {lots.map((l) => (
              <TableRow key={l.id}><TableCell>{l.lotNumber}</TableCell><TableCell>{l.mediaType?.class}</TableCell><TableCell><StatusBadge status={l.gptStage} /></TableCell></TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  );
}
