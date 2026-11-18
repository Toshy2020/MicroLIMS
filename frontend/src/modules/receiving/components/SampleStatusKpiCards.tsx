import { useMemo } from "react";
import { Box, ButtonBase, Tooltip, Typography, useTheme } from "@mui/material";
import { StatusTone } from "../../../theme/statusTokens";
import { useTranslation } from "../../../i18n";

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
  tone: StatusTone;
  assignersOnly?: boolean;
}

const TILES: TileConfig[] = [
  { key: "needsPreparation", tone: "inconclusive" },
  { key: "readyToRead", tone: "purple" },
  { key: "awaitingReview", tone: "info" },
  { key: "overdue", tone: "detected" },
  { key: "mine", tone: "action" },
  { key: "unassigned", tone: "pending", assignersOnly: true }
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
  const { t } = useTranslation("receiving");
  const tiles = useMemo(() => TILES.filter((tile) => !tile.assignersOnly || canAssignAnalyst), [canAssignAnalyst]);
  const isTileKey = (k: AllWorkloadFilterKey | null) => tiles.some((tile) => tile.key === k);

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
            textAlign: "start",
            borderBottom: "3px solid",
            borderColor: active ? "primary.main" : "transparent",
            borderInlineEnd: "1px solid",
            borderInlineEndColor: "divider",
            "&:last-of-type": { borderInlineEnd: "none" },
            bgcolor: active ? theme.custom.status.purple.bg : "transparent",
            transition: "background-color 150ms cubic-bezier(0.4, 0, 0.2, 1), border-color 150ms cubic-bezier(0.4, 0, 0.2, 1)",
            "&:hover": { bgcolor: active ? theme.custom.status.purple.bg : "action.hover" },
            "&.Mui-focusVisible": { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: -2 }
          }}
        >
          <Typography
            sx={{
              fontSize: 22,
              fontWeight: 700,
              lineHeight: 1.1,
              letterSpacing: "-0.02em",
              fontVariantNumeric: "tabular-nums",
              color: count === 0 ? "text.disabled" : toneText ?? "text.primary"
            }}
          >
            {count ?? <Box component="span" aria-hidden="true" sx={{ visibility: "hidden" }}>0</Box>}
          </Typography>
          <Typography sx={{ fontSize: 12, whiteSpace: "nowrap", color: active ? "text.primary" : "text.secondary", fontWeight: active ? 600 : 400 }}>
            {label}
          </Typography>
        </ButtonBase>
      </Tooltip>
    );
  };

  const allTab = onClear
    ? tab("all", t("kpi.all.label"), t("kpi.all.hint"), allCount ?? null, null, !activeKey || !isTileKey(activeKey), onClear)
    : null;

  return (
    <Box
      component="nav"
      aria-label={t("kpi.navAria")}
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
      {allTab}
      {tiles.map((tile) =>
        tab(
          tile.key,
          t(`kpi.${tile.key}.label`),
          t(`kpi.${tile.key}.hint`),
          counts?.[tile.key] ?? 0,
          tile.tone,
          activeKey === tile.key,
          () => onSelect(tile.key)
        )
      )}
    </Box>
  );
}
