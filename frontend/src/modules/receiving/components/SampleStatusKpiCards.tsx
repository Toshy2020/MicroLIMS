import { useMemo } from "react";
import { Box, ButtonBase, Tooltip, Typography, useTheme } from "@mui/material";
import { StatusTone } from "../../../theme/statusTokens";

// These tiles used to count sample categories (Product/RM/PM/Water/AC/EM),
// which duplicated the Item Type dropdown immediately below them. They now
// answer the question the workspace is actually opened to answer - what is
// outstanding, and what of it is mine.

export type WorkloadFilterKey =
  | "needsPreparation"
  | "readyToRead"
  | "awaitingReview"
  | "overdue"
  | "mine"
  | "unassigned";

export type DeepLinkWorkloadFilterKey =
  | "retestInProgress"
  | "reviewOverdue"
  | "approvalOverdue";

export type AllWorkloadFilterKey = WorkloadFilterKey | DeepLinkWorkloadFilterKey;

// The rules these tiles represent live on the server, in
// TestingWorkspaceService: one definition drives both GET /api/testorders/counts
// (the numbers on these tiles) and GET /api/testorders/page (the list beneath
// them), so a tile still cannot claim a number the register does not show.
//
// They used to be duplicated here as WORKLOAD_PREDICATES, evaluated over every
// sample the browser had downloaded. That copy is gone rather than left
// unreferenced: two definitions of one GMP rule drift, and the frontend is not
// where laboratory logic belongs. What stays below is presentation only -
// label, hint and tone.

interface TileConfig {
  key: WorkloadFilterKey;
  label: string;
  hint: string;
  tone: StatusTone;
  assignersOnly?: boolean;
}

const TILES: TileConfig[] = [
  {
    key: "needsPreparation",
    label: "Needs Preparation",
    hint: "Preparation not yet signed off",
    tone: "inconclusive"
  },
  {
    key: "readyToRead",
    label: "Ready to Read",
    hint: "Incubation complete, awaiting result entry",
    tone: "purple"
  },
  {
    key: "awaitingReview",
    label: "Awaiting Review",
    hint: "All tests complete, awaiting reviewer",
    tone: "info"
  },
  {
    key: "overdue",
    label: "Overdue",
    hint: "Open more than 24 hours after receipt",
    tone: "detected"
  },
  {
    key: "mine",
    label: "Assigned to Me",
    hint: "Samples with a test assigned to you",
    tone: "action"
  },
  {
    key: "unassigned",
    label: "Unassigned",
    hint: "No analyst assigned yet",
    tone: "pending",
    assignersOnly: true
  }
];

interface Props {
  counts?: Record<WorkloadFilterKey, number> | null;
  activeKey: AllWorkloadFilterKey | null;
  onSelect: (key: WorkloadFilterKey) => void;
  // "All samples" tab - clears the workload filter.
  onClear?: () => void;
  // Total for the All samples tab, when known (no queue filter active).
  allCount?: number | null;
  canAssignAnalyst: boolean;
}

// Work queues as a row of tabs above the register: one tap filters the list
// to that queue. Queues with work take their tone's colour; empty ones stay
// grey so the eye lands on what needs doing.
export function SampleStatusKpiCards({ counts, activeKey, onSelect, onClear, allCount, canAssignAnalyst }: Props) {
  const theme = useTheme();
  const tiles = useMemo(() => TILES.filter((t) => !t.assignersOnly || canAssignAnalyst), [canAssignAnalyst]);
  const isTileKey = (k: AllWorkloadFilterKey | null) => tiles.some((t) => t.key === k);

  const tab = (key: string, label: string, hint: string, count: number | null, tone: StatusTone | null, active: boolean, onClick: () => void) => {
    const toneText = tone && count ? theme.custom.status[tone].text : undefined;
    return (
      <Tooltip key={key} title={hint} disableInteractive>
        <ButtonBase
          onClick={onClick}
          aria-pressed={active}
          sx={{
            flex: "1 0 auto",
            minWidth: 120,
            display: "grid",
            justifyItems: "start",
            gap: 0.25,
            px: 2,
            pt: 1.25,
            pb: 1,
            textAlign: "left",
            borderBottom: "3px solid",
            borderColor: active ? "primary.main" : "transparent",
            bgcolor: active ? theme.custom.status.purple.bg : "transparent",
            "&:hover": { bgcolor: active ? theme.custom.status.purple.bg : "action.hover" },
            "&.Mui-focusVisible": { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: -2 }
          }}
        >
          <Typography sx={{ fontSize: 22, fontWeight: 700, lineHeight: 1.1, fontVariantNumeric: "tabular-nums", color: count === 0 ? "text.disabled" : toneText ?? "text.primary" }}>
            {count ?? <Box component="span" aria-hidden="true" sx={{ visibility: "hidden" }}>0</Box>}
          </Typography>
          <Typography sx={{ fontSize: 12, whiteSpace: "nowrap", color: active ? "text.primary" : "text.secondary", fontWeight: active ? 600 : 400 }}>
            {label}
          </Typography>
        </ButtonBase>
      </Tooltip>
    );
  };

  return (
    <Box
      component="nav"
      aria-label="Work queues"
      sx={{
        display: "flex",
        overflowX: "auto",
        mb: 2,
        border: "1px solid",
        borderColor: "divider",
        borderRadius: 2,
        bgcolor: "background.paper"
      }}
    >
      {onClear && tab("all", "All samples", "Every sample in this laboratory", allCount ?? null, null, !activeKey || !isTileKey(activeKey), onClear)}
      {tiles.map((t) =>
        tab(t.key, t.label, t.hint, counts?.[t.key] ?? 0, t.tone, activeKey === t.key, () => onSelect(t.key))
      )}
    </Box>
  );
}
