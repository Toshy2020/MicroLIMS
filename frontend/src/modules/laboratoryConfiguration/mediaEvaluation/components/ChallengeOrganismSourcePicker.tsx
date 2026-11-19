import { useEffect, useRef, useState } from "react";
import { Alert, Box, Button, MenuItem, Select, Stack, TextField, Typography } from "@mui/material";
import { MediaEvaluationService } from "../services/MediaEvaluationService";
import { ScanField } from "../../../../components/labels/ScanField";
import { serverMessage } from "../../../../utils/errorMessage";
import type { MediaEvaluationChallenge, ThawedVialOption } from "../types/mediaEvaluationTypes";
import type { CryovialItem } from "../../cryovials/types/cryovialTypes";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../../i18n";

interface Props {
  challenge: MediaEvaluationChallenge;
  // Approved, unexpired batches of this challenge's organism - the ones a
  // new vial can be thawed from.
  batches: CryovialItem[];
  // Lyophilized disks of this organism, offered alongside thawed vials.
  disks?: MaterialItem[];
  // Bumped by the parent after any source is assigned, so every challenge
  // re-reads the thawed vials (a vial thawed for one challenge is then
  // offered to all the others).
  refreshKey: number;
  onAssigned: (text: string) => void | Promise<void>;
  onError: (text: string) => void;
}

const dt = (v: string) => formatDateTimeWithTimeZone(v);

// Step 1 of a media evaluation challenge: the organism source. A cryovial
// source is always a specific thawed vial, so every result traces back to
// the vial used. One thawed vial can serve any media that needs the
// organism for 24 hours after thawing; when none is available the
// analyst thaws one here.
export function ChallengeOrganismSourcePicker({ challenge, batches, disks = [], refreshKey, onAssigned, onError }: Props) {
  const { t } = useTranslation("labConfig");
  const [thawedVials, setThawedVials] = useState<ThawedVialOption[] | null>(null);
  const [selection, setSelection] = useState("");
  const [thawOpen, setThawOpen] = useState(false);
  const [thawBatchId, setThawBatchId] = useState<number | "">("");
  const [thawNotes, setThawNotes] = useState("");
  const [busy, setBusy] = useState(false);
  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  const assigned = !!challenge.cryovial || !!challenge.lyophilizedDisk;

  useEffect(() => {
    if (assigned) return;
    MediaEvaluationService.getThawedVials(challenge.id)
      .then(setThawedVials)
      .catch((e) => {
        setThawedVials([]);
        onErrorRef.current(serverMessage(e) ?? t("mediaEvaluation.sourcePicker.errors.loadThawed"));
      });
  }, [challenge.id, assigned, refreshKey, t]);

  if (challenge.cryovial) {
    return (
      <Typography variant="body2" sx={{ mb: 1 }}>
        <Trans
          i18nKey="mediaEvaluation.sourcePicker.cryovialLabel"
          ns="labConfig"
          values={{ code: challenge.cryovial.code }}
          components={{ bold: <strong /> }}
        />
        {challenge.thawEvent
          ? <Trans
              i18nKey="mediaEvaluation.sourcePicker.vialThawed"
              ns="labConfig"
              values={{ dateTime: dt(challenge.thawEvent.thawedAt) }}
              components={{ bold: <strong /> }}
            />
          : t("mediaEvaluation.sourcePicker.vialNotRecorded")}
      </Typography>
    );
  }
  if (challenge.lyophilizedDisk) {
    return (
      <Typography variant="body2" sx={{ mb: 1 }}>
        <Trans
          i18nKey="mediaEvaluation.sourcePicker.diskSource"
          ns="labConfig"
          values={{ name: challenge.lyophilizedDisk.materialName, batch: challenge.lyophilizedDisk.batchNumber }}
          components={{ bold: <strong /> }}
        />
      </Typography>
    );
  }

  const thawableBatches = batches.filter((b) => b.vialsRemaining > 0);
  const noThawedVials = thawedVials !== null && thawedVials.length === 0;
  const showThawForm = thawOpen || (noThawedVials && disks.length === 0);
  const organism = challenge.organism?.scientificName ?? t("mediaEvaluation.sourcePicker.thisOrganism");

  // value is "thaw:<thawEventId>" or "disk:<materialId>".
  const assign = async () => {
    if (!selection) return;
    setBusy(true);
    try {
      if (selection.startsWith("thaw:")) {
        await MediaEvaluationService.selectThawedVial(challenge.id, Number(selection.slice(5)));
        await onAssigned(t("mediaEvaluation.sourcePicker.messages.thawedAssigned"));
      } else if (selection.startsWith("disk:")) {
        await MediaEvaluationService.selectLyophilizedDisk(challenge.id, Number(selection.slice(5)));
        await onAssigned(t("mediaEvaluation.sourcePicker.messages.diskAssigned"));
      }
    } catch (e) {
      onError(serverMessage(e) ?? t("mediaEvaluation.sourcePicker.errors.assign"));
    } finally {
      setBusy(false);
    }
  };

  const thawAndUse = async () => {
    if (thawBatchId === "") return;
    setBusy(true);
    try {
      await MediaEvaluationService.thawAndSelectVial(challenge.id, Number(thawBatchId), thawNotes.trim());
      setThawOpen(false);
      setThawBatchId("");
      setThawNotes("");
      await onAssigned(t("mediaEvaluation.sourcePicker.messages.thawedAndAssigned"));
    } catch (e) {
      onError(serverMessage(e) ?? t("mediaEvaluation.sourcePicker.errors.thaw"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Box sx={{ mb: 1.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
        {t("mediaEvaluation.sourcePicker.heading")}
      </Typography>

      {noThawedVials && (
        <Alert severity="info" sx={{ mb: 1 }}>
          {t("mediaEvaluation.sourcePicker.noThawedVials", { organism })}
          {thawableBatches.length > 0 ? t("mediaEvaluation.sourcePicker.thawBelowHint") : ""}
        </Alert>
      )}

      {((thawedVials?.length ?? 0) > 0 || disks.length > 0) && (
        <Stack spacing={1}>
          {disks.length > 0 && (
            <ScanField
              label={t("mediaEvaluation.sourcePicker.scanDiskLabel")}
              expectedType="MaterialStockLot"
              offeredIds={disks.map((d) => d.id)}
              onSelect={(id) => setSelection(`disk:${id}`)}
              disabled={busy}
            />
          )}
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}>
            <Select
              size="small"
              displayEmpty
              value={selection}
              onChange={(e) => setSelection(e.target.value)}
              sx={{ minWidth: 320, maxWidth: "100%" }}
              inputProps={{ "aria-label": t("mediaEvaluation.sourcePicker.selectAriaLabel") }}
            >
              <MenuItem value="">
                <em>{t("mediaEvaluation.sourcePicker.selectPlaceholder")}</em>
              </MenuItem>
              {(thawedVials ?? []).map((v) => (
                <MenuItem key={`thaw:${v.thawEventId}`} value={`thaw:${v.thawEventId}`}>
                  {t("mediaEvaluation.sourcePicker.thawedOption", { code: v.cryovialCode, dateTime: dt(v.thawedAt), by: v.thawedByName })}
                  {v.usedInMediaLots.length > 0 ? t("mediaEvaluation.sourcePicker.usedInSuffix", { lots: v.usedInMediaLots.join(", ") }) : ""}
                </MenuItem>
              ))}
              {disks.map((d) => (
                <MenuItem key={`disk:${d.id}`} value={`disk:${d.id}`}>
                  {t("mediaEvaluation.sourcePicker.diskOption", { name: d.materialName, batch: d.batchNumber, left: d.quantityRemaining })}
                </MenuItem>
              ))}
            </Select>
            <Button size="small" variant="contained" disabled={!selection || busy} onClick={assign}>
              {t("mediaEvaluation.sourcePicker.assign")}
            </Button>
            {!showThawForm && thawableBatches.length > 0 && (
              <Button size="small" variant="text" onClick={() => setThawOpen(true)}>
                {t("mediaEvaluation.sourcePicker.thawNewVial")}
              </Button>
            )}
          </Stack>
        </Stack>
      )}

      {showThawForm && (
        thawableBatches.length === 0 ? (
          <Alert severity="warning" sx={{ mt: 1 }}>
            {t("mediaEvaluation.sourcePicker.noThawableBatches", { organism })}
          </Alert>
        ) : (
          <Box sx={{ mt: 1 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
              {t("mediaEvaluation.sourcePicker.thawHeading")}
            </Typography>
            <Stack spacing={1}>
              <ScanField
                label={t("mediaEvaluation.sourcePicker.scanBatchLabel")}
                expectedType="Cryovial"
                offeredIds={thawableBatches.map((b) => b.id)}
                onSelect={(id) => setThawBatchId(id)}
                disabled={busy}
              />
              <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}>
                <Select
                  size="small"
                  displayEmpty
                  value={thawBatchId}
                  onChange={(e) => setThawBatchId(e.target.value)}
                  sx={{ minWidth: 260, maxWidth: "100%" }}
                  inputProps={{ "aria-label": t("mediaEvaluation.sourcePicker.batchAriaLabel") }}
                >
                  <MenuItem value="">
                    <em>{t("mediaEvaluation.sourcePicker.batchPlaceholder")}</em>
                  </MenuItem>
                  {thawableBatches.map((b) => (
                    <MenuItem key={b.id} value={b.id}>
                      {t("mediaEvaluation.sourcePicker.batchOption", { code: b.code, remaining: b.vialsRemaining, total: b.numberOfVialsPrepared })}
                    </MenuItem>
                  ))}
                </Select>
                <TextField
                  size="small"
                  label={t("mediaEvaluation.sourcePicker.notesLabel")}
                  value={thawNotes}
                  onChange={(e) => setThawNotes(e.target.value)}
                  sx={{ minWidth: 200 }}
                />
                <Button size="small" variant="contained" disabled={thawBatchId === "" || busy} onClick={thawAndUse}>
                  {t("mediaEvaluation.sourcePicker.thawAndUse")}
                </Button>
                {thawOpen && (
                  <Button size="small" variant="text" onClick={() => setThawOpen(false)}>
                    {t("mediaEvaluation.sourcePicker.cancel")}
                  </Button>
                )}
              </Stack>
            </Stack>
          </Box>
        )
      )}
    </Box>
  );
}
