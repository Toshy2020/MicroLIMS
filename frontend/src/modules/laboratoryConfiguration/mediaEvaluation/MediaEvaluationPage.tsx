import { useEffect, useState } from "react";
import {
  Box, Paper, Table, TableHead, TableRow, TableCell, TableBody, Select, MenuItem, Button, Alert,
  TextField, Typography, Stack,
  TableContainer
} from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { MediaEvaluationService } from "./services/MediaEvaluationService";
import { serverMessage } from "../../../utils/errorMessage";
import { CryovialService } from "../cryovials/services/CryovialService";
import type { CryovialItem } from "../cryovials/types/cryovialTypes";
import type { IncubatorEquipmentItem } from "../../../services/lookupCache";
import type { ChallengeFormState, MediaEvaluationChallenge, MediaEvaluationDetail, RecordResultPayload } from "./types/mediaEvaluationTypes";
import { masterDataOptions, mediaClassLabel, evaluationTypeLabel } from "../../../services/masterDataOptions";
import { tableHeadSx } from "../../../theme";
import { ChallengeOrganismSourcePicker } from "./components/ChallengeOrganismSourcePicker";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";

const STATUSES = ["Assigned", "InProgress", "Completed"];

// Media preparation -> auto-assigned MediaEvaluation -> pick a thawed
// cryovial vial (or thaw one) per challenge -> record incubation -> record result(s) -> Conform/
// NonConform. Completing with Conform is the only thing that releases
// the Media lot for routine use (see MediaEvaluationEngine).
export function MediaEvaluationPage() {
  const { t } = useTranslation("labConfig");
  const [evaluations, setEvaluations] = useState<MediaEvaluationDetail[]>([]);
  const [statusFilter, setStatusFilter] = useState("");
  const [selected, setSelected] = useState<MediaEvaluationDetail | null>(null);
  const [cryovials, setCryovials] = useState<CryovialItem[]>([]);
  const [incubators, setIncubators] = useState<IncubatorEquipmentItem[]>([]);
  const [forms, setForms] = useState<Record<number, ChallengeFormState>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [sourceRefreshKey, setSourceRefreshKey] = useState(0);

  const loadList = () => MediaEvaluationService.getAll(statusFilter || undefined).then(setEvaluations);
  useEffect(() => { loadList(); }, [statusFilter]);
  useEffect(() => {
    CryovialService.getAll().then(setCryovials);
    masterDataOptions.getEquipment("Incubator").then(setIncubators);
  }, []);

  const openDetail = async (id: number) => {
    setMessage(null);
    setForms({});
    setSelected(await MediaEvaluationService.getById(id));
  };
  const refreshDetail = async () => {
    if (!selected) return;
    setSelected(await MediaEvaluationService.getById(selected.id));
    loadList();
  };
  const closeDetail = () => setSelected(null);

  const setField = (challengeId: number, k: keyof ChallengeFormState, v: ChallengeFormState[keyof ChallengeFormState]) =>
    setForms((f) => ({ ...f, [challengeId]: { ...f[challengeId], [k]: v } }));

  // Hard-filtered to Approved, non-destroyed, non-expired batches whose
  // organism matches this challenge.
  const cryovialOptionsFor = (organismId: number) => {
    const now = new Date();
    return cryovials.filter((c) =>
      c.approvalStatus === "Approved" && !c.isDestroyed && new Date(c.expiryDate) > now &&
      c.organismId === organismId);
  };

  // A thaw changes the batch's vials-left count and adds a thawed vial
  // every other challenge for the organism can now use - reload both.
  const sourceAssigned = async (text: string) => {
    setMessage({ text, ok: true });
    await refreshDetail();
    CryovialService.getAll().then(setCryovials);
    setSourceRefreshKey((k) => k + 1);
  };

  const recordIncubation = async (challenge: MediaEvaluationChallenge) => {
    const incubatorEquipmentId = forms[challenge.id]?.incubatorEquipmentId;
    if (!incubatorEquipmentId) return;
    try {
      await MediaEvaluationService.recordIncubation(challenge.id, Number(incubatorEquipmentId));
      setMessage({ text: t("mediaEvaluation.page.incubationRecorded"), ok: true });
      await refreshDetail();
    } catch (e) {
      setMessage({ text: serverMessage(e) ?? t("mediaEvaluation.page.incubationError"), ok: false });
    }
  };

  const recordResult = async (challenge: MediaEvaluationChallenge) => {
    const form = forms[challenge.id] ?? {};
    const payload: RecordResultPayload = {};
    if (selected?.evaluationType === "GrowthPromotion") {
      payload.oldMediaCount = Number(form.oldMediaCount);
      payload.newMediaCount = Number(form.newMediaCount);
    } else if (selected?.evaluationType === "IndicationInhibition" && challenge.challengeRole === "Inhibition") {
      payload.growthObserved = form.growthObserved === "yes";
    } else if (selected?.evaluationType === "IndicationInhibition" && challenge.challengeRole === "Indication") {
      payload.observedDescription = form.observedDescription ?? "";
      payload.manualConform = form.manualConform === "conform";
    } else if (selected?.evaluationType === "EnrichmentCharacteristics") {
      payload.isTurbid = form.isTurbid === "yes";
    }
    try {
      await MediaEvaluationService.recordResult(challenge.id, payload);
      setMessage({ text: t("mediaEvaluation.page.resultRecorded"), ok: true });
      await refreshDetail();
    } catch (e) {
      setMessage({ text: serverMessage(e) ?? t("mediaEvaluation.page.resultError"), ok: false });
    }
  };

  // The incubation duration is a hard gate, not a suggestion - matches
  // MediaEvaluationEngine.RecordResultAsync's server-side check.
  const isReadyToRead = (c: MediaEvaluationChallenge) => !!c.incubation && new Date(c.incubation.expectedReadingAt!) <= new Date();

  const previewRecovery = (form?: ChallengeFormState) => {
    const oldC = Number(form?.oldMediaCount), newC = Number(form?.newMediaCount);
    if (!oldC) return null;
    return Math.round((newC / oldC) * 1000) / 10;
  };

  return (
    <>
      <PageHeader title={t("mediaEvaluation.page.title")} subtitle={t("mediaEvaluation.page.subtitle")} />

      <SectionTitle>{t("mediaEvaluation.page.sectionTitle")}</SectionTitle>
      <Paper sx={{ p: 2.5 }}>
        <Select size="small" displayEmpty value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} sx={{ mb: 2, minWidth: 200 }} inputProps={{ "aria-label": t("mediaEvaluation.page.statusAriaLabel") }}>
          <MenuItem value=""><em>{t("mediaEvaluation.page.allStatuses")}</em></MenuItem>
          {STATUSES.map((s) => <MenuItem key={s} value={s}>{t(`mediaEvaluation.page.statuses.${s}`)}</MenuItem>)}
        </Select>
        <TableContainer>
          <Table size="small">
            <TableHead><TableRow sx={tableHeadSx}>
              <TableCell>{t("mediaEvaluation.page.columns.lot")}</TableCell><TableCell>{t("mediaEvaluation.page.columns.mediaType")}</TableCell><TableCell>{t("mediaEvaluation.page.columns.evaluationType")}</TableCell>
              <TableCell>{t("mediaEvaluation.page.columns.status")}</TableCell><TableCell>{t("mediaEvaluation.page.columns.outcome")}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {evaluations.map((e) => (
                <TableRow key={e.id} hover sx={{ cursor: "pointer" }} onClick={() => openDetail(e.id)}>
                  <TableCell>{e.media?.lotNumber}</TableCell>
                  <TableCell>{mediaClassLabel() /* MediaResponse has no mediaType: this cell has always rendered blank */}</TableCell>
                  <TableCell>{evaluationTypeLabel(e.evaluationType)}</TableCell>
                  <TableCell><StatusBadge status={e.status} /></TableCell>
                  <TableCell>{e.outcome ? <StatusBadge status={e.outcome} /> : "—"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      <FloatingDialog
        open={!!selected}
        onClose={closeDetail}
        maxWidth="md"
        title={
          selected ? (
            <Box>
              {selected.media?.lotNumber}: {evaluationTypeLabel(selected.evaluationType)}
              <Box sx={{ mt: 0.5, display: "flex", gap: 1 }}>
                <StatusBadge status={selected.status} />
                {selected.outcome && <StatusBadge status={selected.outcome} />}
              </Box>
            </Box>
          ) : null
        }
        actions={<Button onClick={closeDetail}>{t("mediaEvaluation.page.close")}</Button>}
      >
        {selected && (
          <>
              {message && <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>{message.text}</Alert>}
              <Stack spacing={2}>
                {selected.challenges.length === 0 && (
                  <Alert severity="warning">{t("mediaEvaluation.page.noChallenges")}</Alert>
                )}
                {selected.challenges.map((c) => {
                  const form = forms[c.id] ?? {};
                  const options = cryovialOptionsFor(c.organismId);
                  return (
                    <Paper key={c.id} variant="outlined" sx={{ p: 2 }}>
                      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                        <Typography sx={{ fontWeight: 700 }}>
                          {c.organism?.scientificName}{c.challengeRole ? ` (${c.challengeRole})` : ""}
                        </Typography>
                        {c.outcome ? <StatusBadge status={c.outcome} /> : <Typography variant="caption" sx={{
                          color: "text.secondary"
                        }}>{t("mediaEvaluation.page.pending")}</Typography>}
                      </Box>

                      <Typography variant="body2" sx={{ mb: 1 }}>
                        <Trans
                          i18nKey="mediaEvaluation.page.initialInoculum"
                          ns="labConfig"
                          values={{ value: c.initialInoculum }}
                          components={{ bold: <strong /> }}
                        />
                      </Typography>

                      {/* Organism source - a thawed cryovial vial */}
                      <ChallengeOrganismSourcePicker
                        challenge={c}
                        batches={options}
                        refreshKey={sourceRefreshKey}
                        onAssigned={sourceAssigned}
                        onError={(text) => setMessage({ text, ok: false })}
                      />

                      {/* Incubation */}
                      {c.incubation ? (
                        <Typography variant="body2" sx={{ mb: 1 }}>
                          <Trans
                            i18nKey="mediaEvaluation.page.incubationSummary"
                            ns="labConfig"
                            values={{
                              temperature: c.incubation.temperature,
                              duration: c.incubation.duration,
                              readingStatus: isReadyToRead(c)
                                ? t("mediaEvaluation.page.readyToRead")
                                : t("mediaEvaluation.page.earliestReading", { dateTime: formatDateTimeWithTimeZone(c.incubation.expectedReadingAt!) })
                            }}
                            components={{ bold: <strong /> }}
                          />
                        </Typography>
                      ) : (
                        <Stack
                          direction="row"
                          spacing={1}
                          sx={{
                            alignItems: "center",
                            mb: 1
                          }}>
                          <Select size="small" displayEmpty value={form.incubatorEquipmentId ?? ""} onChange={(e) => setField(c.id, "incubatorEquipmentId", e.target.value)} sx={{ minWidth: 200 }} inputProps={{ "aria-label": t("mediaEvaluation.page.incubatorAriaLabel") }}>
                            <MenuItem value=""><em>{t("mediaEvaluation.page.incubatorPlaceholder")}</em></MenuItem>
                            {incubators.map((i) => <MenuItem key={i.id} value={i.id}>{i.name}</MenuItem>)}
                          </Select>
                          <Button size="small" variant="outlined" disabled={!form.incubatorEquipmentId} onClick={() => recordIncubation(c)}>{t("mediaEvaluation.page.recordIncubation")}</Button>
                        </Stack>
                      )}

                      {/* Result */}
                      {c.outcome ? (
                        <Box sx={{ mt: 1 }}>
                          {selected.evaluationType === "GrowthPromotion" && (
                            <Typography variant="body2">{t("mediaEvaluation.page.resultSummary.growthPromotion", { old: c.oldMediaCount, new: c.newMediaCount, recovery: c.recoveryPercent })}</Typography>
                          )}
                          {c.challengeRole === "Inhibition" && (
                            <Typography variant="body2">{t("mediaEvaluation.page.resultSummary.growthObserved", { value: c.growthObserved ? t("mediaEvaluation.page.resultSummary.yes") : t("mediaEvaluation.page.resultSummary.no") })}</Typography>
                          )}
                          {c.challengeRole === "Indication" && (
                            <Typography variant="body2">{t("mediaEvaluation.page.resultSummary.observed", { value: c.observedDescription })}</Typography>
                          )}
                          {selected.evaluationType === "EnrichmentCharacteristics" && (
                            <Typography variant="body2">{c.isTurbid ? t("mediaEvaluation.page.resultSummary.turbid") : t("mediaEvaluation.page.resultSummary.clear")}</Typography>
                          )}
                        </Box>
                      ) : !isReadyToRead(c) ? (
                        <Alert severity="info" sx={{ mt: 1 }}>
                          {c.incubation
                            ? t("mediaEvaluation.page.incubationInProgress", { dateTime: formatDateTimeWithTimeZone(c.incubation.expectedReadingAt!) })
                            : t("mediaEvaluation.page.recordIncubationFirst")}
                        </Alert>
                      ) : (
                        <Box sx={{ mt: 1, display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 1.5 }}>
                          {selected.evaluationType === "GrowthPromotion" && (
                            <>
                              <TextField size="small" label={t("mediaEvaluation.page.oldMediaCountLabel")} value={form.oldMediaCount ?? ""} onChange={(e) => setField(c.id, "oldMediaCount", e.target.value)} />
                              <TextField size="small" label={t("mediaEvaluation.page.newMediaCountLabel")} value={form.newMediaCount ?? ""} onChange={(e) => setField(c.id, "newMediaCount", e.target.value)} />
                              {previewRecovery(form) !== null && <Typography variant="body2" sx={{ alignSelf: "center" }}>{t("mediaEvaluation.page.recoveryPreview", { value: previewRecovery(form) })}</Typography>}
                            </>
                          )}
                          {c.challengeRole === "Inhibition" && (
                            <Select size="small" displayEmpty value={form.growthObserved ?? ""} onChange={(e) => setField(c.id, "growthObserved", e.target.value)} inputProps={{ "aria-label": t("mediaEvaluation.page.growthObservedAriaLabel") }}>
                              <MenuItem value=""><em>{t("mediaEvaluation.page.growthObservedPlaceholder")}</em></MenuItem>
                              <MenuItem value="yes">{t("mediaEvaluation.page.resultSummary.yes")}</MenuItem>
                              <MenuItem value="no">{t("mediaEvaluation.page.resultSummary.no")}</MenuItem>
                            </Select>
                          )}
                          {c.challengeRole === "Indication" && (
                            <>
                              <TextField size="small" label={t("mediaEvaluation.page.observedDescriptionLabel")} value={form.observedDescription ?? ""} onChange={(e) => setField(c.id, "observedDescription", e.target.value)} sx={{ gridColumn: "span 2" }} />
                              <Typography
                                variant="caption"
                                sx={{
                                  color: "text.secondary",
                                  gridColumn: "span 2"
                                }}>{t("mediaEvaluation.page.expectedDescription", { value: c.expectedDescription ?? "—" })}</Typography>
                              <Select size="small" displayEmpty value={form.manualConform ?? ""} onChange={(e) => setField(c.id, "manualConform", e.target.value)} inputProps={{ "aria-label": t("mediaEvaluation.page.judgmentAriaLabel") }}>
                                <MenuItem value=""><em>{t("mediaEvaluation.page.judgmentPlaceholder")}</em></MenuItem>
                                <MenuItem value="conform">{t("mediaEvaluation.page.conform")}</MenuItem>
                                <MenuItem value="nonconform">{t("mediaEvaluation.page.nonConform")}</MenuItem>
                              </Select>
                            </>
                          )}
                          {selected.evaluationType === "EnrichmentCharacteristics" && (
                            <Select size="small" displayEmpty value={form.isTurbid ?? ""} onChange={(e) => setField(c.id, "isTurbid", e.target.value)} inputProps={{ "aria-label": t("mediaEvaluation.page.turbidOrClearAriaLabel") }}>
                              <MenuItem value=""><em>{t("mediaEvaluation.page.turbidOrClearPlaceholder")}</em></MenuItem>
                              <MenuItem value="yes">{t("mediaEvaluation.page.resultSummary.turbid")}</MenuItem>
                              <MenuItem value="no">{t("mediaEvaluation.page.resultSummary.clear")}</MenuItem>
                            </Select>
                          )}
                          <Button size="small" variant="contained" onClick={() => recordResult(c)}>{t("mediaEvaluation.page.recordResult")}</Button>
                        </Box>
                      )}
                    </Paper>
                  );
                })}
              </Stack>
          </>
        )}
      </FloatingDialog>
    </>
  );
}
