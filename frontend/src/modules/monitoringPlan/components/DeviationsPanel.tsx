import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, ButtonBase, Checkbox, FormControlLabel,
  Paper, Radio, RadioGroup, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography, useTheme
} from "@mui/material";
import { Trans } from "react-i18next";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { Ltr } from "../../../components/Ltr";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { useHasPermission } from "../../../contexts/AuthContext";
import { useTranslation } from "../../../i18n";
import { PERMISSIONS } from "../../../routes/routes";
import { serverMessage } from "../../../utils/errorMessage";
import { MonitoringDeviationApi } from "../services/MonitoringPlanService";
import { CATEGORY_KEY, formatShortDate } from "../statusDisplay";
import type { Deviation, DeviationClosure, DeviationList } from "../types";
import { StatusPill } from "./StatusPill";

const occurrenceDate = (d: Deviation) => d.targetDate ?? d.windowEnd;

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

const CLOSURE_KEY: Record<DeviationClosure, string> = {
  NotApplicableBeforeGoLive: "deviations.closure.NotApplicableBeforeGoLive",
  Justified: "deviations.closure.Justified",
  Investigated: "deviations.closure.Investigated"
};

const CLOSURE_ENGLISH: Record<DeviationClosure, string> = {
  NotApplicableBeforeGoLive: "Not applicable: it was before the plan went live",
  Justified: "Explained and accepted",
  Investigated: "Investigated: follow-up done or not needed"
};

// Missed occurrences and results over the action limit, with the closing of
// them. The plan opens them by itself; a person closes them with a reason and
// two signatures.
export function DeviationsPanel({ onPick }: { onPick: (occurrenceId: number) => void }) {
  const { t } = useTranslation("monitoring");
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
    } catch (e: unknown) {
      setList(null);
      setError(serverMessage(e) ?? t("errors.loadDeviations"));
    }
  }, [show, t]);

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
    } catch (e: unknown) {
      setError(serverMessage(e) ?? failure);
    } finally {
      setBusy(false);
    }
  };

  const scan = () => run(async () => {
    const r = await MonitoringDeviationApi.scan();
    return r.missedOpened + r.excursionsOpened === 0
      ? t("deviations.scanNothingNew")
      : t("deviations.scanOpened", { missed: r.missedOpened, excursions: r.excursionsOpened });
  }, t("errors.scanFailed"));

  const trimmedReason = reason.trim();
  // Recorded statement stays English; Arabic translation is shown alongside it in SignatureDialog.
  const closureMeaning = `I am closing ${chosen.length} monitoring deviation(s) as "${CLOSURE_ENGLISH[closure]}". Reason: ${trimmedReason}`;
  const closureMeaningTranslation = t("deviations.signatureMeaning", {
    count: chosen.length,
    closure: t(CLOSURE_KEY[closure]),
    reason: trimmedReason
  });

  // Errors propagate to SignatureDialog, which shows them and stays open.
  const sign = async (password: string, second?: { username: string; password: string }) => {
    if (!second) return;
    const n = await MonitoringDeviationApi.close([...selected], closure, {
      reason: trimmedReason, password, secondSignerUsername: second.username, secondSignerPassword: second.password
    });
    setSigning(false);
    setClosing(false);
    setReason("");
    setMessage(t("deviations.closedCount", { count: n }));
    await load();
  };

  const openClose = () => {
    setClosure(closureOptions[0] === "NotApplicableBeforeGoLive" ? "Justified" : closureOptions[0]);
    setReason("");
    setClosing(true);
  };

  const renderDueText = (items: Deviation[]) => {
    const first = items.reduce((a, b) => (a.dueDate <= b.dueDate ? a : b));
    return (
      <Trans
        i18nKey={first.pastDue ? "deviations.pastDueSince" : "deviations.dueDate"}
        ns="monitoring"
        values={{ date: first.dueDate }}
        components={{ ltr: <Ltr>{null}</Ltr> }}
      />
    );
  };

  return (
    <Stack spacing={2}>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="success">{message}</Alert>}
      <Paper variant="outlined" sx={{ p: 1.5 }}>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap", mb: 1 }}>
          <Typography sx={{ fontWeight: 600 }}>{t("deviations.title")}</Typography>
          {list && (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              <Trans i18nKey="deviations.openCount" ns="monitoring" values={{ count: list.openCount }} components={{ ltr: <Ltr>{null}</Ltr> }} />
              {list.pastDueCount > 0 && (
                <Trans i18nKey="deviations.pastDueCount" ns="monitoring" values={{ count: list.pastDueCount }} components={{ ltr: <Ltr>{null}</Ltr> }} />
              )}
            </Typography>
          )}
          <ToggleButtonGroup exclusive size="small" value={show} aria-label={t("deviations.showAria")} onChange={(_, v) => { if (v) setShow(v); }} sx={{ ml: "auto" }}>
            <ToggleButton value="Open">{t("deviations.open")}</ToggleButton>
            <ToggleButton value="Closed">{t("deviations.closed")}</ToggleButton>
          </ToggleButtonGroup>
          {canManage && <Button variant="outlined" size="small" disabled={busy} onClick={() => void scan()}>{t("deviations.scanNow")}</Button>}
          {canApprove && show === "Open" && (
            <Button variant="contained" size="small" disabled={busy || chosen.length === 0 || chosenKinds.size > 1} onClick={openClose}>
              <Trans i18nKey="deviations.closeSelected" ns="monitoring" values={{ count: chosen.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />
            </Button>
          )}
        </Box>
        {chosenKinds.size > 1 && (
          <Alert severity="info" sx={{ mb: 1 }}>{t("deviations.mixedKindsInfo")}</Alert>
        )}
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          {t("deviations.subtitle")}
        </Typography>
      </Paper>

      {list && list.items.length === 0 && (
        <Typography sx={{ color: "text.secondary" }}>{show === "Open" ? t("deviations.noOpen") : t("deviations.noClosed")}</Typography>
      )}

      {excursions.length > 0 && (
        <Paper variant="outlined" sx={{ p: 1.5 }}>
          <Typography sx={{ fontWeight: 600, mb: 0.5 }}>
            <Trans i18nKey="deviations.excursionsHeading" ns="monitoring" values={{ count: excursions.length }} components={{ ltr: <Ltr>{null}</Ltr> }} />
          </Typography>
          {excursions.map((d) => (
            <Box key={d.id} sx={{ display: "flex", gap: 1, alignItems: "flex-start", py: 0.75, borderBottom: 1, borderColor: "divider" }}>
              {canApprove && show === "Open" && (
                <Checkbox size="small" checked={selected.has(d.id)} onChange={(e) => toggle([d.id], e.target.checked)} slotProps={{ input: { "aria-label": t("deviations.selectItem", { name: d.number }) } }} />
              )}
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography sx={{ fontSize: 14, fontWeight: 600 }}><Ltr>{d.number}</Ltr></Typography>
                <Typography sx={{ fontSize: 13 }}>{d.description}</Typography>
                <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap", mt: 0.5, alignItems: "center" }}>
                  <Button size="small" onClick={() => onPick(d.occurrenceId)}>{t("deviations.openOccurrence")}</Button>
                  {d.followUpOccurrenceId && <Button size="small" onClick={() => onPick(d.followUpOccurrenceId!)}>{t("deviations.openFollowUp")}</Button>}
                  <Typography sx={{ fontSize: 12, color: d.pastDue ? "error.main" : "text.secondary" }}>
                    {d.status === "Open" ? renderDueText([d]) : (
                      <Trans
                        i18nKey="deviations.closedInline"
                        ns="monitoring"
                        values={{ date: d.closedAt?.slice(0, 10) ?? "", reason: d.closureReason ?? "" }}
                        components={{ ltr: <Ltr>{null}</Ltr> }}
                      />
                    )}
                  </Typography>
                </Box>
              </Box>
              <StatusPill tone={d.status === "Open" ? "detected" : "notDetected"} symbol={d.status === "Open" ? "▲" : "✓"}>
                {d.status === "Open" ? t("deviations.open") : t("deviations.closed")}
              </StatusPill>
            </Box>
          ))}
        </Paper>
      )}

      {missed.length > 0 && (
        <Paper variant="outlined" sx={{ p: 1.5 }}>
          <Typography sx={{ fontWeight: 600, mb: 0.5 }}>
            <Trans
              i18nKey="deviations.missedHeading"
              ns="monitoring"
              count={missed.length}
              values={{ count: missed.length }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          {missed.map((g) => {
            const ids = g.items.map((d) => d.id);
            const picked = ids.filter((id) => selected.has(id)).length;
            return (
              <Box key={g.key} sx={{ display: "flex", gap: 1, alignItems: "flex-start", py: 1, borderBottom: 1, borderColor: "divider" }}>
                {canApprove && show === "Open" && (
                  <Checkbox
                    size="small" checked={picked === ids.length} indeterminate={picked > 0 && picked < ids.length}
                    onChange={(e) => toggle(ids, e.target.checked)} slotProps={{ input: { "aria-label": t("deviations.selectItem", { name: g.target }) } }}
                  />
                )}
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography sx={{ fontSize: 14, fontWeight: 600 }}><Ltr>{g.target}</Ltr></Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {g.group ? `${g.group} · ` : ""}{t(CATEGORY_KEY[g.category])}{show === "Open" ? <> · {renderDueText(g.items)}</> : ""}
                  </Typography>
                  <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 0.5, mt: 0.5 }}>
                    <Typography sx={{ fontSize: 12, color: "text.secondary", mr: 0.5 }}>{t("deviations.missedOn")}</Typography>
                    {g.items.map((d) => {
                      const short = formatShortDate(occurrenceDate(d), thisYear, t);
                      return (
                        <ButtonBase
                          key={d.id} onClick={() => onPick(d.occurrenceId)} aria-label={`${g.target}, ${short}`}
                          sx={{ px: 0.75, py: 0.125, borderRadius: 1, fontSize: 12, color: tokens.text, bgcolor: tokens.bg, border: `1px solid ${tokens.border}`, "&:hover": { filter: "brightness(0.96)" } }}
                        >
                          <Ltr>{short}</Ltr>
                        </ButtonBase>
                      );
                    })}
                  </Box>
                  {show === "Closed" && g.items[0].closureReason && (
                    <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.5 }}>
                      <Trans
                        i18nKey="deviations.closedLine"
                        ns="monitoring"
                        values={{ date: g.items[0].closedAt?.slice(0, 10) ?? "", reason: g.items[0].closureReason }}
                        components={{ ltr: <Ltr>{null}</Ltr> }}
                      />
                    </Typography>
                  )}
                </Box>
                <StatusPill tone={show === "Open" ? "detected" : "notDetected"} symbol={show === "Open" ? "▲" : "✓"}>
                  <Trans
                    i18nKey={show === "Open" ? "deviations.pillOpenCount" : "deviations.pillClosedCount"}
                    ns="monitoring"
                    values={{ count: g.items.length }}
                    components={{ ltr: <Ltr>{null}</Ltr> }}
                  />
                </StatusPill>
              </Box>
            );
          })}
        </Paper>
      )}

      <FloatingDialog
        open={closing && !signing}
        title={
          <Trans
            i18nKey="deviations.closeDialogTitle"
            ns="monitoring"
            count={chosen.length}
            values={{ count: chosen.length }}
            components={{ ltr: <Ltr>{null}</Ltr> }}
          />
        }
        onClose={() => setClosing(false)}
        maxWidth="sm"
        actions={
          <>
            <Button onClick={() => setClosing(false)}>{t("commonFields.cancel")}</Button>
            <Button variant="contained" disabled={trimmedReason.length < 5} onClick={() => setSigning(true)}>{t("commonFields.continueToSign")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <RadioGroup value={closure} onChange={(e) => setClosure(e.target.value as DeviationClosure)}>
            {closureOptions.map((c) => <FormControlLabel key={c} value={c} control={<Radio />} label={t(CLOSURE_KEY[c])} />)}
          </RadioGroup>
          {closure === "NotApplicableBeforeGoLive" && (
            <Alert severity="warning">{t("deviations.skipWarning")}</Alert>
          )}
          <TextField
            label={t("commonFields.reason")} required multiline minRows={2} value={reason}
            onChange={(e) => setReason(e.target.value)} helperText={t("commonFields.minChars", { min: 5 })}
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={closureMeaning}
        meaningStatementTranslation={closureMeaningTranslation}
        requireSecondSigner
        secondSignerHint={t("commonFields.secondSignerHint")}
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </Stack>
  );
}
