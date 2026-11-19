import { useEffect, useState } from "react";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";
import {
  Box,
  Typography,
  Button,
  Alert,
  Stack,
  Paper,
  Select,
  MenuItem,
  TextField,
  useTheme
} from "@mui/material";
import { StatusBadge } from "../../../../components/StatusBadge";
import { FloatingDialog } from "../../../../components/FloatingDialog";
import { MediaEvaluationService } from "../../mediaEvaluation/services/MediaEvaluationService";
import { serverMessage } from "../../../../utils/errorMessage";
import type {
  ChallengeFormState,
  MediaEvaluationChallenge,
  MediaEvaluationDetail,
  MediaLot,
  RecordResultPayload
} from "../../mediaEvaluation/types/mediaEvaluationTypes";
import type { CryovialItem } from "../../cryovials/types/cryovialTypes";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import type { IncubatorEquipmentItem } from "../../../../services/lookupCache";
import { CryovialService } from "../../cryovials/services/CryovialService";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import { masterDataOptions, evaluationTypeLabel } from "../../../../services/masterDataOptions";
import { LoadFailuresAlert } from "../../../../components/LoadErrorAlert";
import { useLoadFailures } from "../../../../hooks/useLoadFailures";
import { ChallengeOrganismSourcePicker } from "../../mediaEvaluation/components/ChallengeOrganismSourcePicker";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";

interface Props {
  open: boolean;
  evaluationId: number | null;
  onClose: () => void;
  onUpdated: () => void;
}

export function MediaEvaluationWorkflowDialog({ open, evaluationId, onClose, onUpdated }: Props) {
  const { t } = useTranslation("labConfig");
  const theme = useTheme();
  const [evaluation, setEvaluation] = useState<MediaEvaluationDetail | null>(null);
  const [cryovials, setCryovials] = useState<CryovialItem[]>([]);
  const [disks, setDisks] = useState<MaterialItem[]>([]);
  const [incubators, setIncubators] = useState<IncubatorEquipmentItem[]>([]);
  const [referenceLotOptions, setReferenceLotOptions] = useState<MediaLot[]>([]);
  const [forms, setForms] = useState<Record<number, ChallengeFormState>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [sourceRefreshKey, setSourceRefreshKey] = useState(0);
  const { failed: failedLists, fail, reset: resetFailures } = useLoadFailures();

  const loadData = async () => {
    if (!evaluationId) return;
    setMessage(null);
    setForms({});
    try {
      const data = await MediaEvaluationService.getById(evaluationId);
      setEvaluation(data);
    } catch (e) {
      setMessage({ text: serverMessage(e) ?? t("media.evaluationWorkflowDialog.loadError"), ok: false });
    }
  };

  useEffect(() => {
    if (open && evaluationId) {
      loadData();
      resetFailures();
      CryovialService.getAll().then(setCryovials).catch(fail("reference cryovials", () => setCryovials([])));
      MaterialService.getAll("LyophilizedMicroorganism").then(setDisks).catch(fail("lyophilized microorganism disks", () => setDisks([])));
      masterDataOptions.getEquipment("Incubator").then(setIncubators).catch(fail("incubators", () => setIncubators([])));
    } else {
      setEvaluation(null);
      setMessage(null);
      setForms({});
      setReferenceLotOptions([]);
    }
  }, [open, evaluationId]);

  // Same-material prior released lots, for the GrowthPromotion reference-
  // lot suggestion - loaded once the evaluation (and so its Media/
  // MaterialId) is known. includeExpired: true because this is citing a
  // historical count, not asking what's usable right now (see
  // MediaPreparationService.GetReleasedAsync).
  useEffect(() => {
    if (evaluation?.evaluationType === "GrowthPromotion" && evaluation.media?.materialId) {
      masterDataOptions
        .getReleasedMedia(evaluation.media.materialId, { includeExpired: true, excludeId: evaluation.media.id })
        .then(setReferenceLotOptions)
        .catch(fail("prior released lots for the reference-lot suggestion", () => setReferenceLotOptions([])));
    } else {
      setReferenceLotOptions([]);
    }
  }, [evaluation?.id]);

  // Undefined (untouched) defaults to the auto-suggestion when one
  // exists, or straight to free text when no prior lot of this material
  // has ever been released.
  const referenceModeFor = (form: ChallengeFormState): "linked" | "freetext" =>
    form.referenceFreeText === true ? "freetext" : form.referenceFreeText === false ? "linked" : referenceLotOptions.length > 0 ? "linked" : "freetext";
  const referenceMediaIdFor = (form: ChallengeFormState) => (form.referenceMediaId !== undefined ? form.referenceMediaId : referenceLotOptions[0]?.id ?? "");

  const refreshDetail = async () => {
    if (!evaluationId) return;
    const data = await MediaEvaluationService.getById(evaluationId);
    setEvaluation(data);
    onUpdated();
  };

  const setField = (challengeId: number, k: keyof ChallengeFormState, v: ChallengeFormState[keyof ChallengeFormState]) =>
    setForms((f) => ({ ...f, [challengeId]: { ...f[challengeId], [k]: v } }));

  const cryovialOptionsFor = (organismId: number) => {
    const now = new Date();
    return cryovials.filter((c) =>
      c.approvalStatus === "Approved" && !c.isDestroyed && new Date(c.expiryDate) > now &&
      c.organismId === organismId);
  };

  // Lyophilized disks have no approval-gate workflow of their own (unlike
  // Cryovials) - organism match, not expired, and quantity remaining are
  // the only checks, mirroring MediaEvaluationEngine.SelectLyophilizedDiskAsync.
  const diskOptionsFor = (organismId: number) => {
    const now = new Date();
    return disks.filter((d) =>
      d.organismId === organismId && d.quantityRemaining > 0 && (!d.expiryDate || new Date(d.expiryDate) > now));
  };

  // A thaw changes the batch's vials-left count and adds a thawed vial
  // every other challenge for the organism can now use - reload both.
  const sourceAssigned = async (text: string) => {
    setMessage({ text, ok: true });
    await refreshDetail();
    CryovialService.getAll().then(setCryovials).catch(fail("reference cryovials", () => setCryovials([])));
    setSourceRefreshKey((k) => k + 1);
  };

  const recordIncubation = async (challenge: MediaEvaluationChallenge) => {
    const incubatorEquipmentId = forms[challenge.id]?.incubatorEquipmentId;
    if (!incubatorEquipmentId) return;
    try {
      await MediaEvaluationService.recordIncubation(challenge.id, Number(incubatorEquipmentId));
      setMessage({ text: t("media.evaluationWorkflowDialog.incubationRecorded"), ok: true });
      await refreshDetail();
    } catch (e) {
      setMessage({ text: serverMessage(e) ?? t("media.evaluationWorkflowDialog.incubationError"), ok: false });
    }
  };

  const recordResult = async (challenge: MediaEvaluationChallenge) => {
    const form = forms[challenge.id] ?? {};
    const payload: RecordResultPayload = {};
    if (evaluation?.evaluationType === "GrowthPromotion") {
      payload.oldMediaCount = Number(form.oldMediaCount);
      payload.newMediaCount = Number(form.newMediaCount);
      if (referenceModeFor(form) === "linked") {
        payload.referenceMediaId = Number(referenceMediaIdFor(form)) || null;
        payload.referenceMediaLabel = null;
      } else {
        payload.referenceMediaId = null;
        payload.referenceMediaLabel = (form.referenceMediaLabel ?? "").trim();
      }
    } else if (evaluation?.evaluationType === "IndicationInhibition" && challenge.challengeRole === "Inhibition") {
      payload.growthObserved = form.growthObserved === "yes";
    } else if (evaluation?.evaluationType === "IndicationInhibition" && challenge.challengeRole === "Indication") {
      payload.observedDescription = form.observedDescription ?? "";
      payload.manualConform = form.manualConform === "conform";
    } else if (evaluation?.evaluationType === "EnrichmentCharacteristics") {
      payload.isTurbid = form.isTurbid === "yes";
    }
    try {
      await MediaEvaluationService.recordResult(challenge.id, payload);
      setMessage({ text: t("media.evaluationWorkflowDialog.resultRecorded"), ok: true });
      await refreshDetail();
    } catch (e) {
      setMessage({ text: serverMessage(e) ?? t("media.evaluationWorkflowDialog.resultError"), ok: false });
    }
  };

  const isReadyToRead = (c: MediaEvaluationChallenge) => !!c.incubation && new Date(c.incubation.expectedReadingAt!) <= new Date();

  const previewRecovery = (form?: ChallengeFormState) => {
    const oldC = Number(form?.oldMediaCount), newC = Number(form?.newMediaCount);
    if (!oldC) return null;
    return Math.round((newC / oldC) * 1000) / 10;
  };

  if (!open) return null;

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      titleSx={{ pb: 1 }}
      title={
        evaluation ? (
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: 1, flex: 1, minWidth: 0 }}>
            <Box>
              <Typography sx={{ fontSize: 18, fontWeight: 700, color: theme.palette.primary.main }}>
                {evaluation.media?.lotNumber}: {evaluationTypeLabel(evaluation.evaluationType)}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("media.evaluationWorkflowDialog.evaluationMeta", { id: evaluation.id, status: evaluation.status })}
              </Typography>
            </Box>

            <Box sx={{ display: "flex", gap: 1, mr: 1 }}>
              <StatusBadge status={evaluation.status} />
              {evaluation.outcome && <StatusBadge status={evaluation.outcome} />}
            </Box>
          </Box>
        ) : null
      }
      actions={
        <Button onClick={onClose} variant="outlined">
          {t("media.evaluationWorkflowDialog.close")}
        </Button>
      }
    >
      {evaluation && (
        <>
            <LoadFailuresAlert failed={failedLists} sx={{ mb: 2 }} />
            {message && (
              <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>
                {message.text}
              </Alert>
            )}

            <Stack spacing={2}>
              {evaluation.challenges.length === 0 && (
                <Alert severity="warning">
                  {t("media.evaluationWorkflowDialog.noChallenges")}
                </Alert>
              )}

              {evaluation.challenges.map((c) => {
                const form = forms[c.id] ?? {};
                const options = cryovialOptionsFor(c.organismId);
                const diskOpts = diskOptionsFor(c.organismId);

                return (
                  <Paper key={c.id} variant="outlined" sx={{ p: 2, borderRadius: 2 }}>
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
                      <Typography sx={{ fontWeight: 700, fontSize: 14, color: "text.primary" }}>
                        {c.organism?.scientificName}{c.challengeRole ? ` (${c.challengeRole})` : ""}
                      </Typography>
                      {c.outcome ? (
                        <StatusBadge status={c.outcome} />
                      ) : (
                        <Typography variant="caption" sx={{ color: "text.secondary", fontWeight: 600 }}>
                          {t("media.evaluationWorkflowDialog.pending")}
                        </Typography>
                      )}
                    </Box>

                    <Typography variant="body2" sx={{ mb: 1 }}>
                      <Trans
                        i18nKey="media.evaluationWorkflowDialog.initialInoculum"
                        ns="labConfig"
                        values={{ value: c.initialInoculum }}
                        components={{ bold: <strong /> }}
                      />
                    </Typography>

                    {/* Reference Lot (GrowthPromotion only) - a setup choice
                        like cryovial/incubator, not a measured result, so it's
                        decided up front rather than gated behind incubation
                        completing. */}
                    {evaluation.evaluationType === "GrowthPromotion" && !c.outcome && (
                      <Box sx={{ mb: 1.5 }}>
                        <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
                          {t("media.evaluationWorkflowDialog.referenceLotHeading")}
                        </Typography>
                        <Stack useFlexGap
                          direction="row"
                          spacing={1}
                          sx={{
                            alignItems: "center",
                            flexWrap: "wrap"
                          }}>
                          {referenceLotOptions.length > 0 && (
                            <Select
                              size="small"
                              value={referenceModeFor(form) === "freetext" ? "freetext" : referenceMediaIdFor(form)}
                              onChange={(e) => {
                                const v = e.target.value;
                                if (v === "freetext") {
                                  setField(c.id, "referenceFreeText", true);
                                } else {
                                  setField(c.id, "referenceFreeText", false);
                                  setField(c.id, "referenceMediaId", v);
                                }
                              }}
                              sx={{ minWidth: 320 }}
                              inputProps={{ "aria-label": t("media.evaluationWorkflowDialog.referenceLotAria") }}
                            >
                              {referenceLotOptions.map((m) => (
                                <MenuItem key={m.id} value={m.id}>
                                  {t("media.evaluationWorkflowDialog.referenceLotOption", {
                                    lot: m.lotNumber,
                                    date: new Date(m.preparedAt).toLocaleDateString()
                                  })}
                                </MenuItem>
                              ))}
                              <MenuItem value="freetext">
                                <em>{t("media.evaluationWorkflowDialog.enterManually")}</em>
                              </MenuItem>
                            </Select>
                          )}
                          {(referenceLotOptions.length === 0 || referenceModeFor(form) === "freetext") && (
                            <TextField
                              size="small"
                              label={t("media.evaluationWorkflowDialog.referenceLotManualLabel")}
                              placeholder={t("media.evaluationWorkflowDialog.referenceLotManualPlaceholder")}
                              value={form.referenceMediaLabel ?? ""}
                              onChange={(e) => setField(c.id, "referenceMediaLabel", e.target.value)}
                              sx={{ minWidth: 320 }}
                            />
                          )}
                        </Stack>
                      </Box>
                    )}

                    {/* Step 1: Organism Source Selection (thawed cryovial vial or Lyophilized Disk) */}
                    <ChallengeOrganismSourcePicker
                      challenge={c}
                      batches={options}
                      disks={diskOpts}
                      refreshKey={sourceRefreshKey}
                      onAssigned={sourceAssigned}
                      onError={(text) => setMessage({ text, ok: false })}
                    />

                    {/* Step 2: Incubation Recording */}
                    {c.incubation ? (
                      <Typography variant="body2" sx={{ mb: 1 }}>
                        {t("media.evaluationWorkflowDialog.incubationLabel")}{" "}
                        <strong>
                          {t("media.evaluationWorkflowDialog.incubationValue", {
                            temp: c.incubation.temperature,
                            duration: c.incubation.duration
                          })}
                        </strong>
                        {", "}
                        {isReadyToRead(c) ? (
                          <span style={{ color: theme.custom.status.notDetected.text, fontWeight: 600 }}>{t("media.evaluationWorkflowDialog.readyToRead")}</span>
                        ) : (
                          <span>{t("media.evaluationWorkflowDialog.earliestReading", { date: formatDateTimeWithTimeZone(c.incubation.expectedReadingAt!) })}</span>
                        )}
                      </Typography>
                    ) : (
                      <Box sx={{ mb: 1.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
                        <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
                          {t("media.evaluationWorkflowDialog.step2Heading")}
                        </Typography>
                        <Stack direction="row" spacing={1} sx={{
                          alignItems: "center"
                        }}>
                          <Select
                            size="small"
                            displayEmpty
                            value={form.incubatorEquipmentId ?? ""}
                            onChange={(e) => setField(c.id, "incubatorEquipmentId", e.target.value)}
                            sx={{ minWidth: 220 }}
                            inputProps={{ "aria-label": t("media.evaluationWorkflowDialog.incubatorAria") }}
                          >
                            <MenuItem value="">
                              <em>{t("media.evaluationWorkflowDialog.selectIncubator")}</em>
                            </MenuItem>
                            {incubators.map((i) => (
                              <MenuItem key={i.id} value={i.id}>
                                {i.name}
                              </MenuItem>
                            ))}
                          </Select>
                          <Button
                            size="small"
                            variant="contained"
                            disabled={!form.incubatorEquipmentId}
                            onClick={() => recordIncubation(c)}
                            color="primary"
                          >
                            {t("media.evaluationWorkflowDialog.recordIncubation")}
                          </Button>
                        </Stack>
                      </Box>
                    )}

                    {/* Step 3: Result Entry */}
                    {c.outcome ? (
                      <Box sx={{ mt: 1, p: 1.5, bgcolor: theme.custom.status.notDetected.bg, borderRadius: 1.5, border: "1px solid", borderColor: theme.custom.status.notDetected.border }}>
                        <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.custom.status.notDetected.text, mb: 0.5 }}>
                          {t("media.evaluationWorkflowDialog.resultRecordedHeading")}
                        </Typography>
                        {evaluation.evaluationType === "GrowthPromotion" && (
                          <Typography variant="body2">
                            <Trans
                              i18nKey="media.evaluationWorkflowDialog.countsLine"
                              ns="labConfig"
                              values={{ old: c.oldMediaCount, new: c.newMediaCount, recovery: c.recoveryPercent }}
                              components={{ bold: <strong /> }}
                            />
                          </Typography>
                        )}
                        {evaluation.evaluationType === "GrowthPromotion" && (
                          <Typography variant="body2">
                            <Trans
                              i18nKey="media.evaluationWorkflowDialog.referenceLotLine"
                              ns="labConfig"
                              values={{ lot: c.referenceMedia?.lotNumber ?? c.referenceMediaLabel ?? "—" }}
                              components={{ bold: <strong /> }}
                            />
                          </Typography>
                        )}
                        {c.challengeRole === "Inhibition" && (
                          <Typography variant="body2">
                            <Trans
                              i18nKey="media.evaluationWorkflowDialog.growthObservedLine"
                              ns="labConfig"
                              values={{ value: c.growthObserved ? t("media.evaluationWorkflowDialog.yes") : t("media.evaluationWorkflowDialog.no") }}
                              components={{ bold: <strong /> }}
                            />
                          </Typography>
                        )}
                        {c.challengeRole === "Indication" && (
                          <Typography variant="body2">
                            <Trans
                              i18nKey="media.evaluationWorkflowDialog.observedLine"
                              ns="labConfig"
                              values={{ value: c.observedDescription }}
                              components={{ bold: <strong /> }}
                            />
                          </Typography>
                        )}
                        {evaluation.evaluationType === "EnrichmentCharacteristics" && (
                          <Typography variant="body2">
                            <Trans
                              i18nKey="media.evaluationWorkflowDialog.stateLine"
                              ns="labConfig"
                              values={{ value: c.isTurbid ? t("media.evaluationWorkflowDialog.turbid") : t("media.evaluationWorkflowDialog.clear") }}
                              components={{ bold: <strong /> }}
                            />
                          </Typography>
                        )}
                      </Box>
                    ) : !isReadyToRead(c) ? (
                      <Alert severity="info" sx={{ mt: 1 }}>
                        {c.incubation
                          ? t("media.evaluationWorkflowDialog.incubationInProgress", { date: formatDateTimeWithTimeZone(c.incubation.expectedReadingAt!) })
                          : t("media.evaluationWorkflowDialog.recordIncubationFirst")}
                      </Alert>
                    ) : (
                      <Box sx={{ mt: 1, p: 1.5, bgcolor: theme.custom.status.purple.bg, borderRadius: 1.5, border: "1px solid", borderColor: theme.custom.status.purple.border }}>
                        <Typography sx={{ fontSize: 12, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
                          {t("media.evaluationWorkflowDialog.step3Heading")}
                        </Typography>
                        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))", gap: 1.5, alignItems: "center" }}>
                          {evaluation.evaluationType === "GrowthPromotion" && (
                            <>
                              <TextField
                                size="small"
                                label={t("media.evaluationWorkflowDialog.oldMediaCount")}
                                type="number"
                                value={form.oldMediaCount ?? ""}
                                onChange={(e) => setField(c.id, "oldMediaCount", e.target.value)}
                              />
                              <TextField
                                size="small"
                                label={t("media.evaluationWorkflowDialog.newMediaCount")}
                                type="number"
                                value={form.newMediaCount ?? ""}
                                onChange={(e) => setField(c.id, "newMediaCount", e.target.value)}
                              />
                              {previewRecovery(form) !== null && (
                                <Typography variant="body2" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
                                  {t("media.evaluationWorkflowDialog.recoveryPreview", { value: previewRecovery(form) })}
                                </Typography>
                              )}
                            </>
                          )}

                          {c.challengeRole === "Inhibition" && (
                            <Select
                              size="small"
                              displayEmpty
                              value={form.growthObserved ?? ""}
                              onChange={(e) => setField(c.id, "growthObserved", e.target.value)}
                              inputProps={{ "aria-label": t("media.evaluationWorkflowDialog.growthObservedQuestion") }}
                            >
                              <MenuItem value="">
                                <em>{t("media.evaluationWorkflowDialog.growthObservedQuestion")}</em>
                              </MenuItem>
                              <MenuItem value="yes">{t("media.evaluationWorkflowDialog.yes")}</MenuItem>
                              <MenuItem value="no">{t("media.evaluationWorkflowDialog.no")}</MenuItem>
                            </Select>
                          )}

                          {c.challengeRole === "Indication" && (
                            <>
                              <TextField
                                size="small"
                                label={t("media.evaluationWorkflowDialog.observedDescription")}
                                value={form.observedDescription ?? ""}
                                onChange={(e) => setField(c.id, "observedDescription", e.target.value)}
                                sx={{ gridColumn: "span 2" }}
                              />
                              <Typography
                                variant="caption"
                                sx={{
                                  color: "text.secondary",
                                  gridColumn: "span 2"
                                }}>
                                {t("media.evaluationWorkflowDialog.expected", { value: c.expectedDescription ?? "—" })}
                              </Typography>
                              <Select
                                size="small"
                                displayEmpty
                                value={form.manualConform ?? ""}
                                onChange={(e) => setField(c.id, "manualConform", e.target.value)}
                                inputProps={{ "aria-label": t("media.evaluationWorkflowDialog.judgment") }}
                              >
                                <MenuItem value="">
                                  <em>{t("media.evaluationWorkflowDialog.judgment")}</em>
                                </MenuItem>
                                <MenuItem value="conform">{t("media.evaluationWorkflowDialog.conform")}</MenuItem>
                                <MenuItem value="nonconform">{t("media.evaluationWorkflowDialog.nonConform")}</MenuItem>
                              </Select>
                            </>
                          )}

                          {evaluation.evaluationType === "EnrichmentCharacteristics" && (
                            <Select
                              size="small"
                              displayEmpty
                              value={form.isTurbid ?? ""}
                              onChange={(e) => setField(c.id, "isTurbid", e.target.value)}
                              inputProps={{ "aria-label": t("media.evaluationWorkflowDialog.turbidOrClear") }}
                            >
                              <MenuItem value="">
                                <em>{t("media.evaluationWorkflowDialog.turbidOrClear")}</em>
                              </MenuItem>
                              <MenuItem value="yes">{t("media.evaluationWorkflowDialog.turbid")}</MenuItem>
                              <MenuItem value="no">{t("media.evaluationWorkflowDialog.clear")}</MenuItem>
                            </Select>
                          )}

                          <Button
                            size="small"
                            variant="contained"
                            onClick={() => recordResult(c)}
                            color="primary"
                          >
                            {t("media.evaluationWorkflowDialog.recordResult")}
                          </Button>
                        </Box>
                      </Box>
                    )}
                  </Paper>
                );
              })}
            </Stack>
        </>
      )}
    </FloatingDialog>
  );
}
