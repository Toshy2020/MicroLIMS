import { useEffect, useState } from "react";
import { Alert, Box, Button, MenuItem, Select, Stack, TextField, Typography } from "@mui/material";
import { MediaEvaluationService } from "../services/MediaEvaluationService";
import { serverMessage } from "../../../../utils/errorMessage";
import type { MediaEvaluationChallenge, ThawedVialOption } from "../types/mediaEvaluationTypes";
import type { CryovialItem } from "../../cryovials/types/cryovialTypes";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";

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

const dt = (v: string) => new Date(v).toLocaleString();

// Step 1 of a media evaluation challenge: the organism source. A cryovial
// source is always a specific thawed vial, so every result traces back to
// the vial used. One thawed vial can serve any media that needs the
// organism for 24 hours after thawing; when none is available the
// analyst thaws one here.
export function ChallengeOrganismSourcePicker({ challenge, batches, disks = [], refreshKey, onAssigned, onError }: Props) {
  const [thawedVials, setThawedVials] = useState<ThawedVialOption[] | null>(null);
  const [selection, setSelection] = useState("");
  const [thawOpen, setThawOpen] = useState(false);
  const [thawBatchId, setThawBatchId] = useState<number | "">("");
  const [thawNotes, setThawNotes] = useState("");
  const [busy, setBusy] = useState(false);

  const assigned = !!challenge.cryovial || !!challenge.lyophilizedDisk;

  useEffect(() => {
    if (assigned) return;
    MediaEvaluationService.getThawedVials(challenge.id)
      .then(setThawedVials)
      .catch((e) => {
        setThawedVials([]);
        onError(serverMessage(e) ?? "Could not load thawed vials.");
      });
  }, [challenge.id, assigned, refreshKey]);

  if (challenge.cryovial) {
    return (
      <Typography variant="body2" sx={{ mb: 1 }}>
        Cryovial: <strong>{challenge.cryovial.code}</strong>
        {challenge.thawEvent
          ? <>, vial thawed <strong>{dt(challenge.thawEvent.thawedAt)}</strong></>
          : " (thawed vial not recorded)"}
      </Typography>
    );
  }
  if (challenge.lyophilizedDisk) {
    return (
      <Typography variant="body2" sx={{ mb: 1 }}>
        Source: <strong>{challenge.lyophilizedDisk.materialName}, batch {challenge.lyophilizedDisk.batchNumber}</strong>
      </Typography>
    );
  }

  const thawableBatches = batches.filter((b) => b.vialsRemaining > 0);
  const noThawedVials = thawedVials !== null && thawedVials.length === 0;
  const showThawForm = thawOpen || (noThawedVials && disks.length === 0);
  const organism = challenge.organism?.scientificName ?? "this organism";

  // value is "thaw:<thawEventId>" or "disk:<materialId>".
  const assign = async () => {
    if (!selection) return;
    setBusy(true);
    try {
      if (selection.startsWith("thaw:")) {
        await MediaEvaluationService.selectThawedVial(challenge.id, Number(selection.slice(5)));
        await onAssigned("Thawed vial assigned.");
      } else if (selection.startsWith("disk:")) {
        await MediaEvaluationService.selectLyophilizedDisk(challenge.id, Number(selection.slice(5)));
        await onAssigned("Lyophilized disk assigned.");
      }
    } catch (e) {
      onError(serverMessage(e) ?? "Could not assign the organism source.");
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
      await onAssigned("Vial thawed and assigned. It is now available to every other challenge for this organism.");
    } catch (e) {
      onError(serverMessage(e) ?? "Could not thaw a vial.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Box sx={{ mb: 1.5, p: 1.5, bgcolor: "background.default", borderRadius: 1.5, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
        STEP 1: SELECT THAWED CRYOVIAL OR LYOPHILIZED DISK
      </Typography>

      {noThawedVials && (
        <Alert severity="info" sx={{ mb: 1 }}>
          No vial of {organism} thawed in the last 24 hours is available.
          {thawableBatches.length > 0 ? " Thaw a vial below to use it." : ""}
        </Alert>
      )}

      {((thawedVials?.length ?? 0) > 0 || disks.length > 0) && (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}>
          <Select
            size="small"
            displayEmpty
            value={selection}
            onChange={(e) => setSelection(e.target.value)}
            sx={{ minWidth: 320, maxWidth: "100%" }}
            inputProps={{ "aria-label": "Thawed vial or disk" }}
          >
            <MenuItem value="">
              <em>Select thawed vial or disk</em>
            </MenuItem>
            {(thawedVials ?? []).map((v) => (
              <MenuItem key={`thaw:${v.thawEventId}`} value={`thaw:${v.thawEventId}`}>
                {v.cryovialCode}, vial thawed {dt(v.thawedAt)} by {v.thawedByName}
                {v.usedInMediaLots.length > 0 ? ` (used in ${v.usedInMediaLots.join(", ")})` : ""}
              </MenuItem>
            ))}
            {disks.map((d) => (
              <MenuItem key={`disk:${d.id}`} value={`disk:${d.id}`}>
                {d.materialName}, batch {d.batchNumber} ({d.quantityRemaining} left), Lyophilized Disk
              </MenuItem>
            ))}
          </Select>
          <Button size="small" variant="contained" disabled={!selection || busy} onClick={assign}>
            Assign
          </Button>
          {!showThawForm && thawableBatches.length > 0 && (
            <Button size="small" variant="text" onClick={() => setThawOpen(true)}>
              Thaw a new vial
            </Button>
          )}
        </Stack>
      )}

      {showThawForm && (
        thawableBatches.length === 0 ? (
          <Alert severity="warning" sx={{ mt: 1 }}>
            No approved, unexpired cryovial batch of {organism} has vials remaining.
          </Alert>
        ) : (
          <Box sx={{ mt: 1 }}>
            <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", mb: 0.5 }}>
              THAW A VIAL
            </Typography>
            <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap", rowGap: 1 }}>
              <Select
                size="small"
                displayEmpty
                value={thawBatchId}
                onChange={(e) => setThawBatchId(e.target.value)}
                sx={{ minWidth: 260, maxWidth: "100%" }}
                inputProps={{ "aria-label": "Cryovial batch to thaw from" }}
              >
                <MenuItem value="">
                  <em>Cryovial batch</em>
                </MenuItem>
                {thawableBatches.map((b) => (
                  <MenuItem key={b.id} value={b.id}>
                    {b.code} ({b.vialsRemaining} of {b.numberOfVialsPrepared} vials left)
                  </MenuItem>
                ))}
              </Select>
              <TextField
                size="small"
                label="Notes (optional)"
                value={thawNotes}
                onChange={(e) => setThawNotes(e.target.value)}
                sx={{ minWidth: 200 }}
              />
              <Button size="small" variant="contained" disabled={thawBatchId === "" || busy} onClick={thawAndUse}>
                Thaw &amp; use
              </Button>
              {thawOpen && (
                <Button size="small" variant="text" onClick={() => setThawOpen(false)}>
                  Cancel
                </Button>
              )}
            </Stack>
          </Box>
        )
      )}
    </Box>
  );
}
