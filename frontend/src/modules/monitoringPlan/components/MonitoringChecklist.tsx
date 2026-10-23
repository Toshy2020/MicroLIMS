import { useEffect, useRef, useState } from "react";
import { Box, Checkbox, FormControlLabel, Paper, Typography } from "@mui/material";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import { periodLabel } from "../statusDisplay";
import type { MonitoringChecklist as Checklist } from "../types";

// Shown on the prepare screens: what the plan expects from this session.
// The backend decides which occurrences are overdue, due now or due later
// this month and which are ticked to start with; this only reports the
// analyst's choices through onChange.
export function MonitoringChecklist({ sampleId, onChange }: { sampleId: number; onChange: (occurrenceIds: number[]) => void }) {
  const allowed = useHasPermission(PERMISSIONS.MONITORING_PLAN_VIEW);
  const [checklist, setChecklist] = useState<Checklist | null>(null);
  const [ticked, setTicked] = useState<Record<number, boolean>>({});
  const notify = useRef(onChange);
  notify.current = onChange;

  useEffect(() => {
    if (!allowed) return;
    let cancelled = false;
    MonitoringPlanService.getChecklist(sampleId)
      .then((c) => {
        if (cancelled) return;
        setChecklist(c);
        const initial: Record<number, boolean> = {};
        c.sections.forEach((s) => s.occurrences.forEach((o) => { if (o.preTicked) initial[o.occurrenceId] = true; }));
        setTicked(initial);
        notify.current(Object.keys(initial).map(Number));
      })
      .catch(() => { if (!cancelled) setChecklist(null); });
    return () => { cancelled = true; };
  }, [sampleId, allowed]);

  const toggle = (id: number) => {
    const next = { ...ticked, [id]: !ticked[id] };
    setTicked(next);
    notify.current(Object.keys(next).filter((k) => next[Number(k)]).map(Number));
  };

  const sections = checklist?.sections.filter((s) => s.occurrences.length > 0) ?? [];
  if (!allowed || sections.length === 0) return null;

  return (
    <Paper variant="outlined" sx={{ p: 1.5, mb: 2 }}>
      <Typography sx={{ fontWeight: 600, mb: 0.5 }}>Monitoring plan</Typography>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>
        Tick what this session fulfils. A ticked entry needs its tests selected below.
      </Typography>
      {sections.map((s) => (
        <Box key={s.key} sx={{ mb: 1 }}>
          <Typography sx={{ fontSize: 12, fontWeight: 700, color: s.key === "overdue" ? "error.main" : "text.secondary" }}>{s.title}</Typography>
          {s.occurrences.map((o) => (
            <FormControlLabel
              key={o.occurrenceId}
              sx={{ display: "flex", alignItems: "flex-start" }}
              control={<Checkbox size="small" checked={!!ticked[o.occurrenceId]} onChange={() => toggle(o.occurrenceId)} />}
              label={
                <Box>
                  <Typography sx={{ fontSize: 14 }}>{o.target} · {periodLabel(o.periodKey)} (window ends {o.windowEnd})</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                    {o.remainingItems.map((i) => i.label).join(", ")}
                  </Typography>
                </Box>
              }
            />
          ))}
        </Box>
      ))}
    </Paper>
  );
}
