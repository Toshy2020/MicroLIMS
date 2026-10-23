import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, ButtonBase, Checkbox, FormControlLabel,
  Paper, Radio, RadioGroup, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography, useTheme
} from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { MonitoringDeviationApi } from "../services/MonitoringPlanService";
import { CATEGORY_LABEL, MONTH_NAMES } from "../statusDisplay";
import type { Deviation, DeviationClosure, DeviationList } from "../types";
import { StatusPill } from "./StatusPill";

const occurrenceDate = (d: Deviation) => d.targetDate ?? d.windowEnd;

const shortDate = (iso: string, thisYear: string) =>
  `${Number(iso.slice(8))} ${MONTH_NAMES[Number(iso.slice(5, 7)) - 1].slice(0, 3)}${iso.startsWith(thisYear) ? "" : ` ${iso.slice(0, 4)}`}`;

interface MissedGroup { key: string; target: string; group: string | null; category: Deviation["category"]; items: Deviation[] }

// One entry per sampling point, so a point missed every week reads as one
// line with its dates. Most-missed first.
function groupMissed(items: Deviation[]): MissedGroup[] {
  const groups = new Map<string, MissedGroup>();
  for (const d of items) {
    const key = `${d.category}|${d.group ?? ""}|${d.target}`;
    const g = groups.get(key) ?? { key, target: d.target, group: d.group, category: d.category, items: [] };
    g.items.push(d);
    groups.set(key, g);
  }
  for (const g of groups.values()) g.items.sort((a, b) => occurrenceDate(a).localeCompare(occurrenceDate(b)));
  return [...groups.values()].sort((a, b) => b.items.length - a.items.length || a.target.localeCompare(b.target));
}

const CLOSURE_LABEL: Record<DeviationClosure, string> = {
  NotApplicableBeforeGoLive: "Not applicable: it was before the plan went live",
  Justified: "Explained and accepted",
  Investigated: "Investigated: follow-up done or not needed"
};

// Missed occurrences and results over the action limit, with the closing of
// them. The plan opens them by itself; a person closes them with a reason and
// two signatures.
export function DeviationsPanel({ onPick }: { onPick: (occurrenceId: number) => void }) {
  const canManage = useHasPermission(PERMISSIONS.MONITORING_PLAN_MANAGE);
  const canApprove = useHasPermission(PERMISSIONS.MONITORING_PLAN_APPROVE);
  const tokens = useTheme().custom.status.detected;
  const [show, setShow] = useState<"Open" | "Closed">("Open");
  const [list, setList] = useState<DeviationList | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [closing, setClosing] = useState(false);
  const [closure, setClosure] = useState<DeviationClosure>("Justified");
  const [reason, setReason] = useState("");
  const [signing, setSigning] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      setList(await MonitoringDeviationApi.list(show));
      setSelected(new Set());
    } catch (e: any) {
      setList(null);
      setError(e?.response?.data?.message ?? "Could not load the deviations.");
    }
  }, [show]);

  useEffect(() => { void load(); }, [load]);

  const thisYear = String(new Date().getFullYear());
  const missed = useMemo(() => groupMissed((list?.items ?? []).filter((d) => d.kind === "MissedOccurrence")), [list]);
  const excursions = useMemo(() => (list?.items ?? []).filter((d) => d.kind === "ActionLimitExceeded"), [list]);

  const chosen = (list?.items ?? []).filter((d) => selected.has(d.id));
  const chosenKinds = new Set(chosen.map((d) => d.kind));
  const closureOptions: DeviationClosure[] = chosenKinds.has("ActionLimitExceeded") ? ["Investigated"] : ["NotApplicableBeforeGoLive", "Justified"];

  const toggle = (ids: number[], on: boolean) => {
    const next = new Set(selected);
    ids.forEach((id) => (on ? next.add(id) : next.delete(id)));
    setSelected(next);
  };

  const run = async (action: () => Promise<string>, failure: string) => {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      setMessage(await action());
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.message ?? failure);
    } finally {
      setBusy(false);
    }
  };

  const scan = () => run(async () => {
    const r = await MonitoringDeviationApi.scan();
    return r.missedOpened + r.excursionsOpened === 0
      ? "Nothing new to record."
      : `Opened ${r.missedOpened} for missed occurrences and ${r.excursionsOpened} for results over the action limit.`;
  }, "The scan failed.");

  const trimmedReason = reason.trim();
  const closureMeaning = `I am closing ${chosen.length} monitoring deviation(s) as "${CLOSURE_LABEL[closure]}". Reason: ${trimmedReason}`;

  // Errors propagate to SignatureDialog, which shows them and stays open.
  const sign = async (password: string, second?: { username: string; password: string }) => {
    if (!second) return;
    const n = await MonitoringDeviationApi.close([...selected], closure, {
      reason: trimmedReason, password, secondSignerUsername: second.username, secondSignerPassword: second.password
    });
    setSigning(false);
    setClosing(false);
    setReason("");
    setMessage(`Closed ${n} deviation(s).`);
    await load();
  };

  const openClose = () => {
    setClosure(closureOptions[0] === "NotApplicableBeforeGoLive" ? "Justified" : closureOptions[0]);
    setReason("");
    setClosing(true);
  };

  const dueText = (items: Deviation[]) => {
    const first = items.reduce((a, b) => (a.dueDate <= b.dueDate ? a : b));
    return first.pastDue ? `past due since ${first.dueDate}` : `due ${first.dueDate}`;
  };

  return (
    <Stack spacing={2}>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      <Paper variant="outlined" sx={{ p: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap", mb: 1 }}>
          <Typography sx={{ fontWeight: 600 }}>Deviations</Typography>
          {list && (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {list.openCount} open{list.pastDueCount > 0 ? `, ${list.pastDueCount} past due` : ""}
            </Typography>
          )}
          <ToggleButtonGroup exclusive size="small" value={show} aria-label="Show deviations" onChange={(_, v) => { if (v) setShow(v); }} sx={{ ml: "auto" }}>
            <ToggleButton value="Open">Open</ToggleButton>
            <ToggleButton value="Closed">Closed</ToggleButton>
          </ToggleButtonGroup>
          {canManage && <Button variant="outlined" size="small" disabled={busy} onClick={() => void scan()}>Scan now</Button>}
          {canApprove && show === "Open" && (
            <Button variant="contained" size="small" disabled={busy || chosen.length === 0 || chosenKinds.size > 1} onClick={openClose}>
              Close selected ({chosen.length})
            </Button>
          )}
        </Box>
        {chosenKinds.size > 1 && (
          <Alert severity="info" sx={{ mb: 1 }}>Select missed occurrences or results over the action limit, not both, to close them together.</Alert>
        )}
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          The plan opens these by itself: an occurrence missed after its grace days, or an approved result over its action limit (which also opens follow-up sampling).
        </Typography>
      </Paper>

      {list && list.items.length === 0 && (
        <Typography sx={{ color: "text.secondary" }}>{show === "Open" ? "No open deviations." : "No closed deviations."}</Typography>
      )}

      {excursions.length > 0 && (
        <Paper variant="outlined" sx={{ p: 1.5 }}>
          <Typography sx={{ fontWeight: 600, mb: 0.5 }}>Results over the action limit · {excursions.length}</Typography>
          {excursions.map((d) => (
            <Box key={d.id} sx={{ display: "flex", gap: 1, alignItems: "flex-start", py: 0.75, borderBottom: 1, borderColor: "divider" }}>
              {canApprove && show === "Open" && (
                <Checkbox size="small" checked={selected.has(d.id)} onChange={(e) => toggle([d.id], e.target.checked)} slotProps={{ input: { "aria-label": `Select ${d.number}` } }} />
              )}
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{d.number}</Typography>
                <Typography sx={{ fontSize: 13 }}>{d.description}</Typography>
                <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap", mt: 0.5, alignItems: "center" }}>
                  <Button size="small" onClick={() => onPick(d.occurrenceId)}>Open the occurrence</Button>
                  {d.followUpOccurrenceId && <Button size="small" onClick={() => onPick(d.followUpOccurrenceId!)}>Open the follow-up sampling</Button>}
                  <Typography sx={{ fontSize: 12, color: d.pastDue ? "error.main" : "text.secondary" }}>
                    {d.status === "Open" ? dueText([d]) : `closed ${d.closedAt?.slice(0, 10)}: ${d.closureReason}`}
                  </Typography>
                </Box>
              </Box>
              <StatusPill tone={d.status === "Open" ? "detected" : "notDetected"} symbol={d.status === "Open" ? "▲" : "✓"}>
                {d.status === "Open" ? "Open" : "Closed"}
              </StatusPill>
            </Box>
          ))}
        </Paper>
      )}

      {missed.length > 0 && (
        <Paper variant="outlined" sx={{ p: 1.5 }}>
          <Typography sx={{ fontWeight: 600, mb: 0.5 }}>Missed occurrences · {missed.length} sampling point(s)</Typography>
          {missed.map((g) => {
            const ids = g.items.map((d) => d.id);
            const picked = ids.filter((id) => selected.has(id)).length;
            return (
              <Box key={g.key} sx={{ display: "flex", gap: 1, alignItems: "flex-start", py: 1, borderBottom: 1, borderColor: "divider" }}>
                {canApprove && show === "Open" && (
                  <Checkbox
                    size="small" checked={picked === ids.length} indeterminate={picked > 0 && picked < ids.length}
                    onChange={(e) => toggle(ids, e.target.checked)} slotProps={{ input: { "aria-label": `Select ${g.target}` } }}
                  />
                )}
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{g.target}</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {g.group ? `${g.group} · ` : ""}{CATEGORY_LABEL[g.category]}{show === "Open" ? ` · ${dueText(g.items)}` : ""}
                  </Typography>
                  <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 0.5, mt: 0.5 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary", mr: 0.5 }}>Missed on</Typography>
                    {g.items.map((d) => (
                      <ButtonBase
                        key={d.id} onClick={() => onPick(d.occurrenceId)} aria-label={`${g.target}, ${shortDate(occurrenceDate(d), thisYear)}`}
                        sx={{ px: 0.75, py: 0.125, borderRadius: 1, fontSize: 12, color: tokens.text, bgcolor: tokens.bg, border: `1px solid ${tokens.border}`, "&:hover": { filter: "brightness(0.96)" } }}
                      >
                        {shortDate(occurrenceDate(d), thisYear)}
                      </ButtonBase>
                    ))}
                  </Box>
                  {show === "Closed" && g.items[0].closureReason && (
                    <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
                      Closed {g.items[0].closedAt?.slice(0, 10)}: {g.items[0].closureReason}
                    </Typography>
                  )}
                </Box>
                <StatusPill tone={show === "Open" ? "detected" : "notDetected"} symbol={show === "Open" ? "▲" : "✓"}>
                  {show === "Open" ? "Open" : "Closed"} ×{g.items.length}
                </StatusPill>
              </Box>
            );
          })}
        </Paper>
      )}

      <FloatingDialog
        open={closing && !signing}
        title={`Close ${chosen.length} deviation(s)`}
        onClose={() => setClosing(false)}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={() => setClosing(false)}>Cancel</Button>
            <Button variant="contained" disabled={trimmedReason.length < 5} onClick={() => setSigning(true)}>Continue to sign</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <RadioGroup value={closure} onChange={(e) => setClosure(e.target.value as DeviationClosure)}>
            {closureOptions.map((c) => <FormControlLabel key={c} value={c} control={<Radio />} label={CLOSURE_LABEL[c]} />)}
          </RadioGroup>
          {closure === "NotApplicableBeforeGoLive" && (
            <Alert severity="warning">The missed occurrences are skipped, so they stop counting against the plan. This cannot be undone.</Alert>
          )}
          <TextField
            label="Reason" required multiline minRows={2} value={reason}
            onChange={(e) => setReason(e.target.value)} helperText="At least 5 characters."
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={closureMeaning}
        requireSecondSigner
        secondSignerHint="A different person who is allowed to approve monitoring plan changes signs as well."
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </Stack>
  );
}
