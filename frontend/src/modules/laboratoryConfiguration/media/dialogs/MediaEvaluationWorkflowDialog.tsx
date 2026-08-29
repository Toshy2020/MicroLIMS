import { useEffect, useState } from "react";
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
import { CryovialService } from "../../cryovials/services/CryovialService";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import { masterDataOptions, evaluationTypeLabel } from "../../../../services/masterDataOptions";
import { brandColors } from "../../../../theme";

interface Props {
  open: boolean;
  evaluationId: number | null;
  onClose: () => void;
  onUpdated: () => void;
}

export function MediaEvaluationWorkflowDialog({ open, evaluationId, onClose, onUpdated }: Props) {
  const theme = useTheme();
  const [evaluation, setEvaluation] = useState<any | null>(null);
  const [cryovials, setCryovials] = useState<any[]>([]);
  const [disks, setDisks] = useState<any[]>([]);
  const [incubators, setIncubators] = useState<any[]>([]);
  const [referenceLotOptions, setReferenceLotOptions] = useState<any[]>([]);
  const [forms, setForms] = useState<Record<number, any>>({});
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const loadData = async () => {
    if (!evaluationId) return;
    setMessage(null);
    setForms({});
    try {
      const data = await MediaEvaluationService.getById(evaluationId);
      setEvaluation(data);
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not load evaluation details.", ok: false });
    }
  };

  useEffect(() => {
    if (open && evaluationId) {
      loadData();
      CryovialService.getAll().then(setCryovials).catch(() => setCryovials([]));
      MaterialService.getAll("LyophilizedMicroorganism").then(setDisks).catch(() => setDisks([]));
      masterDataOptions.getEquipment("Incubator").then(setIncubators).catch(() => setIncubators([]));
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
        .catch(() => setReferenceLotOptions([]));
    } else {
      setReferenceLotOptions([]);
    }
  }, [evaluation?.id]);

  // Undefined (untouched) defaults to the auto-suggestion when one
  // exists, or straight to free text when no prior lot of this material
  // has ever been released.
  const referenceModeFor = (form: any): "linked" | "freetext" =>
    form.referenceFreeText === true ? "freetext" : form.referenceFreeText === false ? "linked" : referenceLotOptions.length > 0 ? "linked" : "freetext";
  const referenceMediaIdFor = (form: any) => (form.referenceMediaId !== undefined ? form.referenceMediaId : referenceLotOptions[0]?.id ?? "");

  const refreshDetail = async () => {
    if (!evaluationId) return;
    const data = await MediaEvaluationService.getById(evaluationId);
    setEvaluation(data);
    onUpdated();
  };

  const setField = (challengeId: number, k: string, v: any) =>
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

  // Step 1's picker merges both organism-source types into one Select -
  // value is "cv:<id>" or "disk:<id>" so a single selection unambiguously
  // identifies which backend action to call.
  const pickOrganismSource = async (challenge: any) => {
    const selection = forms[challenge.id]?.sourceSelection as string | undefined;
    if (!selection) return;
    try {
      if (selection.startsWith("cv:")) {
        await MediaEvaluationService.selectCryovial(challenge.id, Number(selection.slice(3)));
      } else if (selection.startsWith("disk:")) {
        await MediaEvaluationService.selectLyophilizedDisk(challenge.id, Number(selection.slice(5)));
      }
      setMessage({ text: "Organism source selected.", ok: true });
      await refreshDetail();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not select organism source.", ok: false });
    }
  };

  const recordIncubation = async (challenge: any) => {
    const incubatorEquipmentId = forms[challenge.id]?.incubatorEquipmentId;
    if (!incubatorEquipmentId) return;
    try {
      await MediaEvaluationService.recordIncubation(challenge.id, Number(incubatorEquipmentId));
      setMessage({ text: "Incubation recorded.", ok: true });
      await refreshDetail();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not record incubation.", ok: false });
    }
  };

  const recordResult = async (challenge: any) => {
    const form = forms[challenge.id] ?? {};
    const payload: any = {};
    if (evaluation.evaluationType === "GrowthPromotion") {
      payload.oldMediaCount = Number(form.oldMediaCount);
      payload.newMediaCount = Number(form.newMediaCount);
      if (referenceModeFor(form) === "linked") {
        payload.referenceMediaId = Number(referenceMediaIdFor(form)) || null;
        payload.referenceMediaLabel = null;
      } else {
        payload.referenceMediaId = null;
        payload.referenceMediaLabel = (form.referenceMediaLabel ?? "").trim();
      }
    } else if (evaluation.evaluationType === "IndicationInhibition" && challenge.challengeRole === "Inhibition") {
      payload.growthObserved = form.growthObserved === "yes";
    } else if (evaluation.evaluationType === "IndicationInhibition" && challenge.challengeRole === "Indication") {
      payload.observedDescription = form.observedDescription ?? "";
      payload.manualConform = form.manualConform === "conform";
    } else if (evaluation.evaluationType === "EnrichmentCharacteristics") {
      payload.isTurbid = form.isTurbid === "yes";
    }
    try {
      await MediaEvaluationService.recordResult(challenge.id, payload);
      setMessage({ text: "Result recorded successfully.", ok: true });
      await refreshDetail();
    } catch (e: any) {
      setMessage({ text: e?.response?.data?.message ?? "Could not record result.", ok: false });
    }
  };

  const isReadyToRead = (c: any) => !!c.incubation && new Date(c.incubation.expectedReadingAt) <= new Date();

  const previewRecovery = (form: any) => {
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
                {evaluation.media?.lotNumber} — {evaluationTypeLabel(evaluation.evaluationType)}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                Evaluation ID #{evaluation.id} · Status: {evaluation.status}
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
          Close
        </Button>
      }
    >
      {evaluation && (
        <>
            {message && (
              <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }}>
                {message.text}
              </Alert>
            )}

            <Stack spacing={2}>
              {evaluation.challenges.length === 0 && (
                <Alert severity="warning">
                  No challenges assigned yet - add Media Challenge Specs for this material under Laboratory Configuration.
                </Alert>
              )}

              {evaluation.challenges.map((c: any) => {
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
                          Pending
                        </Typography>
                      )}
                    </Box>

                    <Typography variant="body2" sx={{ mb: 1 }}>
                      Initial Inoculum: <strong>{c.initialInoculum}</strong>
                    </Typography>

                    {/* Reference Lot (GrowthPromotion only) - a setup choice
                        like cryovial/incubator, not a measured result, so it's
                        decided up front rather than gated behind incubation
                        completing. */}
                    {evaluation.evaluationType === "GrowthPromotion" && !c.outcome && (
                      <Box sx={{ mb: 1.5 }}>
                        <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
                          REFERENCE LOT (FOR RECOVERY% COMPARISON)
                        </Typography>
                        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap">
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
                            >
                              {referenceLotOptions.map((m: any) => (
                                <MenuItem key={m.id} value={m.id}>
                                  {m.lotNumber} — prepared {new Date(m.preparedAt).toLocaleDateString()}
                                </MenuItem>
                              ))}
                              <MenuItem value="freetext">
                                <em>Enter manually…</em>
                              </MenuItem>
                            </Select>
                          )}
                          {(referenceLotOptions.length === 0 || referenceModeFor(form) === "freetext") && (
                            <TextField
                              size="small"
                              label="Reference Lot (manual entry)"
                              placeholder="e.g. prior lot code or description"
                              value={form.referenceMediaLabel ?? ""}
                              onChange={(e) => setField(c.id, "referenceMediaLabel", e.target.value)}
                              sx={{ minWidth: 320 }}
                            />
                          )}
                        </Stack>
                      </Box>
                    )}

                    {/* Step 1: Organism Source Selection (Cryovial or Lyophilized Disk) */}
                    {c.cryovial ? (
                      <Typography variant="body2" sx={{ mb: 1 }}>
                        Cryovial: <strong>{c.cryovial.code}</strong>
                      </Typography>
                    ) : c.lyophilizedDisk ? (
                      <Typography variant="body2" sx={{ mb: 1 }}>
                        Source: <strong>{c.lyophilizedDisk.materialName} — batch {c.lyophilizedDisk.batchNumber}</strong>
                      </Typography>
                    ) : (
                      <Box sx={{ mb: 1.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
                        <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
                          STEP 1: SELECT WORKING CRYOVIAL OR LYOPHILIZED DISK
                        </Typography>
                        {options.length === 0 && diskOpts.length === 0 ? (
                          <Alert severity="warning" sx={{ mb: 1 }}>
                            No approved, unexpired cryovial batches or lyophilized disks available for {c.organism?.scientificName}.
                          </Alert>
                        ) : (
                          <Stack direction="row" spacing={1} alignItems="center">
                            <Select
                              size="small"
                              displayEmpty
                              value={form.sourceSelection ?? ""}
                              onChange={(e) => setField(c.id, "sourceSelection", e.target.value)}
                              sx={{ minWidth: 320 }}
                            >
                              <MenuItem value="">
                                <em>Select Cryovial or Disk</em>
                              </MenuItem>
                              {options.map((o: any) => (
                                <MenuItem key={`cv:${o.id}`} value={`cv:${o.id}`}>
                                  {o.code} ({o.vialsRemaining} of {o.numberOfVialsPrepared} vials left) — Cryovial
                                </MenuItem>
                              ))}
                              {diskOpts.map((d: any) => (
                                <MenuItem key={`disk:${d.id}`} value={`disk:${d.id}`}>
                                  {d.materialName} — batch {d.batchNumber} ({d.quantityRemaining} left) — Lyophilized Disk
                                </MenuItem>
                              ))}
                            </Select>
                            <Button
                              size="small"
                              variant="contained"
                              disabled={!form.sourceSelection}
                              onClick={() => pickOrganismSource(c)}
                              sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
                            >
                              Assign
                            </Button>
                          </Stack>
                        )}
                      </Box>
                    )}

                    {/* Step 2: Incubation Recording */}
                    {c.incubation ? (
                      <Typography variant="body2" sx={{ mb: 1 }}>
                        Incubation: <strong>{c.incubation.temperature}°C, {c.incubation.duration}h</strong>
                        {" — "}
                        {isReadyToRead(c) ? (
                          <span style={{ color: theme.custom.status.notDetected.text, fontWeight: 600 }}>Ready to read</span>
                        ) : (
                          <span>Earliest reading: {new Date(c.incubation.expectedReadingAt).toLocaleString()}</span>
                        )}
                      </Typography>
                    ) : (
                      <Box sx={{ mb: 1.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
                        <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
                          STEP 2: RECORD INCUBATION
                        </Typography>
                        <Stack direction="row" spacing={1} alignItems="center">
                          <Select
                            size="small"
                            displayEmpty
                            value={form.incubatorEquipmentId ?? ""}
                            onChange={(e) => setField(c.id, "incubatorEquipmentId", e.target.value)}
                            sx={{ minWidth: 220 }}
                          >
                            <MenuItem value="">
                              <em>Select Incubator</em>
                            </MenuItem>
                            {incubators.map((i: any) => (
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
                            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
                          >
                            Record Incubation
                          </Button>
                        </Stack>
                      </Box>
                    )}

                    {/* Step 3: Result Entry */}
                    {c.outcome ? (
                      <Box sx={{ mt: 1, p: 1.5, bgcolor: theme.custom.status.notDetected.bg, borderRadius: 1.5, border: "1px solid", borderColor: theme.custom.status.notDetected.border }}>
                        <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.custom.status.notDetected.text, mb: 0.5 }}>
                          EVALUATION RESULT RECORDED
                        </Typography>
                        {evaluation.evaluationType === "GrowthPromotion" && (
                          <Typography variant="body2">
                            Old Count: <strong>{c.oldMediaCount}</strong> / New Count: <strong>{c.newMediaCount}</strong> — Recovery: <strong>{c.recoveryPercent}%</strong>
                          </Typography>
                        )}
                        {evaluation.evaluationType === "GrowthPromotion" && (
                          <Typography variant="body2">
                            Reference Lot: <strong>{c.referenceMedia?.lotNumber ?? c.referenceMediaLabel ?? "—"}</strong>
                          </Typography>
                        )}
                        {c.challengeRole === "Inhibition" && (
                          <Typography variant="body2">
                            Growth Observed: <strong>{c.growthObserved ? "Yes" : "No"}</strong>
                          </Typography>
                        )}
                        {c.challengeRole === "Indication" && (
                          <Typography variant="body2">
                            Observed: <strong>{c.observedDescription}</strong>
                          </Typography>
                        )}
                        {evaluation.evaluationType === "EnrichmentCharacteristics" && (
                          <Typography variant="body2">
                            State: <strong>{c.isTurbid ? "Turbid" : "Clear"}</strong>
                          </Typography>
                        )}
                      </Box>
                    ) : !isReadyToRead(c) ? (
                      <Alert severity="info" sx={{ mt: 1 }}>
                        {c.incubation
                          ? `Incubation still in progress — result entry opens at ${new Date(c.incubation.expectedReadingAt).toLocaleString()}.`
                          : "Record incubation before a result can be entered."}
                      </Alert>
                    ) : (
                      <Box sx={{ mt: 1, p: 1.5, bgcolor: theme.custom.status.purple.bg, borderRadius: 1.5, border: "1px solid", borderColor: theme.custom.status.purple.border }}>
                        <Typography sx={{ fontSize: 11, fontWeight: 700, color: theme.palette.primary.main, mb: 1 }}>
                          STEP 3: RECORD OBSERVATION / RESULT
                        </Typography>
                        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))", gap: 1.5, alignItems: "center" }}>
                          {evaluation.evaluationType === "GrowthPromotion" && (
                            <>
                              <TextField
                                size="small"
                                label="Old Media Count"
                                type="number"
                                value={form.oldMediaCount ?? ""}
                                onChange={(e) => setField(c.id, "oldMediaCount", e.target.value)}
                              />
                              <TextField
                                size="small"
                                label="New Media Count"
                                type="number"
                                value={form.newMediaCount ?? ""}
                                onChange={(e) => setField(c.id, "newMediaCount", e.target.value)}
                              />
                              {previewRecovery(form) !== null && (
                                <Typography variant="body2" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
                                  Recovery: {previewRecovery(form)}%
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
                            >
                              <MenuItem value="">
                                <em>Growth Observed?</em>
                              </MenuItem>
                              <MenuItem value="yes">Yes</MenuItem>
                              <MenuItem value="no">No</MenuItem>
                            </Select>
                          )}

                          {c.challengeRole === "Indication" && (
                            <>
                              <TextField
                                size="small"
                                label="Observed Description"
                                value={form.observedDescription ?? ""}
                                onChange={(e) => setField(c.id, "observedDescription", e.target.value)}
                                sx={{ gridColumn: "span 2" }}
                              />
                              <Typography variant="caption" color="text.secondary" sx={{ gridColumn: "span 2" }}>
                                Expected: {c.expectedDescription ?? "—"}
                              </Typography>
                              <Select
                                size="small"
                                displayEmpty
                                value={form.manualConform ?? ""}
                                onChange={(e) => setField(c.id, "manualConform", e.target.value)}
                              >
                                <MenuItem value="">
                                  <em>Judgment</em>
                                </MenuItem>
                                <MenuItem value="conform">Conform</MenuItem>
                                <MenuItem value="nonconform">NonConform</MenuItem>
                              </Select>
                            </>
                          )}

                          {evaluation.evaluationType === "EnrichmentCharacteristics" && (
                            <Select
                              size="small"
                              displayEmpty
                              value={form.isTurbid ?? ""}
                              onChange={(e) => setField(c.id, "isTurbid", e.target.value)}
                            >
                              <MenuItem value="">
                                <em>Turbid or Clear?</em>
                              </MenuItem>
                              <MenuItem value="yes">Turbid</MenuItem>
                              <MenuItem value="no">Clear</MenuItem>
                            </Select>
                          )}

                          <Button
                            size="small"
                            variant="contained"
                            onClick={() => recordResult(c)}
                            sx={{ bgcolor: brandColors.sectionTitle, "&:hover": { bgcolor: "#632273" } }}
                          >
                            Record Result
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
