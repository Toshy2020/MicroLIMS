import { useEffect, useState } from "react";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
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
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

const dt = (v: string) => formatDateTimeWithTimeZone(v);

// Biological waste destruction records: one signed record per autoclave
// cycle, covering cryovial vials due for destruction and any other waste
// destroyed in the same cycle.
export function BiologicalWasteDestructionPage() {
  const { t } = useTranslation("labConfig");
  const [records, setRecords] = useState<BiologicalWasteDestruction[]>([]);
  const [eligible, setEligible] = useState<BiologicalWasteEligible | null>(null);
  const [selected, setSelected] = useState<BiologicalWasteDestruction | null>(null);
  const [recording, setRecording] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const load = () => {
    BiologicalWasteService.getAll().then(setRecords)
      .catch((e) => setMessage({ text: serverMessage(e) ?? t("biologicalWaste.page.loadError"), ok: false }));
    BiologicalWasteService.getEligible().then(setEligible).catch(() => setEligible(null));
  };
  useEffect(load, []);

  const pendingVials = (eligible?.thawedVials.length ?? 0) + (eligible?.withdrawnBatches.reduce((n, b) => n + b.vialsRemaining, 0) ?? 0);

  return (
    <>
      <PageHeader
        title={t("biologicalWaste.page.title")}
        subtitle={t("biologicalWaste.page.subtitle")}
      >
        <Button variant="contained" color="error" onClick={() => { setMessage(null); setRecording(true); }}>
          {t("biologicalWaste.page.recordCycle")}
        </Button>
      </PageHeader>

      {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}
      {pendingVials > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          {t("biologicalWaste.page.awaiting", { count: pendingVials })}
        </Alert>
      )}

      <SectionTitle>{t("biologicalWaste.page.recordsTitle")}</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell>{t("biologicalWaste.page.columns.reference")}</TableCell><TableCell>{t("biologicalWaste.page.columns.cycleStarted")}</TableCell><TableCell>{t("biologicalWaste.page.columns.autoclave")}</TableCell>
                <TableCell>{t("biologicalWaste.page.columns.program")}</TableCell><TableCell>{t("biologicalWaste.page.columns.cycleNo")}</TableCell><TableCell>{t("biologicalWaste.page.columns.cryovialItems")}</TableCell>
                <TableCell>{t("biologicalWaste.page.columns.otherItems")}</TableCell><TableCell>{t("biologicalWaste.page.columns.signedBy")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {records.length === 0 && (
                <TableRow><TableCell colSpan={8} sx={{ color: "text.secondary" }}>{t("biologicalWaste.page.empty")}</TableCell></TableRow>
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
        title={selected ? t("biologicalWaste.page.detailTitle", { reference: selected.reference }) : ""}
        actions={<Button onClick={() => setSelected(null)}>{t("biologicalWaste.page.close")}</Button>}
      >
        {selected && (
          <>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 1, mb: 2 }}>
              <Typography variant="body2">
                <Trans
                  i18nKey="biologicalWaste.page.autoclaveLine"
                  ns="labConfig"
                  values={{ name: selected.autoclaveName, code: selected.autoclaveCode }}
                  components={{ bold: <strong /> }}
                />
              </Typography>
              <Typography variant="body2">
                <Trans
                  i18nKey="biologicalWaste.page.programLine"
                  ns="labConfig"
                  values={{ code: selected.programCode, name: selected.programName }}
                  components={{ bold: <strong /> }}
                />
              </Typography>
              <Typography variant="body2">
                <Trans
                  i18nKey="biologicalWaste.page.parametersLine"
                  ns="labConfig"
                  values={{ temp: selected.temperature, minutes: selected.cycleTimeMinutes, loadType: selected.loadType }}
                  components={{ bold: <strong /> }}
                />
              </Typography>
              <Typography variant="body2">
                <Trans
                  i18nKey="biologicalWaste.page.cycleLine"
                  ns="labConfig"
                  values={{ number: selected.cycleNumber, date: dt(selected.cycleStartedAt) }}
                  components={{ bold: <strong /> }}
                />
              </Typography>
              <Typography variant="body2">
                <Trans
                  i18nKey="biologicalWaste.page.signedLine"
                  ns="labConfig"
                  values={{ name: selected.signedByName, role: selected.signedByRole, date: dt(selected.signedAt) }}
                  components={{ bold: <strong /> }}
                />
              </Typography>
              {selected.notes && (
                <Typography variant="body2">
                  <Trans
                    i18nKey="biologicalWaste.page.notesLine"
                    ns="labConfig"
                    values={{ notes: selected.notes }}
                    components={{ bold: <strong /> }}
                  />
                </Typography>
              )}
            </Box>
            <Table size="small">
              <TableHead>
                <TableRow sx={tableHeadSx}>
                  <TableCell>{t("biologicalWaste.page.itemColumns.type")}</TableCell><TableCell>{t("biologicalWaste.page.itemColumns.description")}</TableCell><TableCell>{t("biologicalWaste.page.itemColumns.quantity")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {selected.items.map((i) => (
                  <TableRow key={i.id}>
                    <TableCell>{t(`biologicalWaste.page.itemTypes.${i.itemType}`, { defaultValue: i.itemType })}</TableCell>
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
          setMessage({ text: t("biologicalWaste.page.recorded", { reference }), ok: true });
          load();
        }}
      />
    </>
  );
}
