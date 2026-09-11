import React, { useMemo } from "react";
import { Box, Paper, Typography, Grid, useTheme } from "@mui/material";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import ScheduleOutlinedIcon from "@mui/icons-material/ScheduleOutlined";
import PersonOutlineIcon from "@mui/icons-material/PersonOutline";
import PersonOffOutlinedIcon from "@mui/icons-material/PersonOffOutlined";
import { SampleRecord } from "../types/receivingTypes";
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

export interface WorkloadContext {
  userId: number | null;
  isSectionHeadOrAdmin: boolean;
  now: number;
}

const OVERDUE_AFTER_MS = 24 * 60 * 60 * 1000;

// A sample past one of these is finished; it is not outstanding work no
// matter how long ago it arrived. Without this an "Overdue" count grows
// forever, because every approved sample eventually passes 24h.
const CLOSED_SAMPLE_STATUSES = new Set([
  "Approved",
  "Rejected",
  "RetestRequested",
  "Cancelled",
  "Voided"
]);

export function isClosedSample(r: SampleRecord): boolean {
  return CLOSED_SAMPLE_STATUSES.has(r.status);
}

// The single source of truth for what each tile means. The tiles count with
// these and the register filters with these, so a tile can never claim a
// number that the list underneath it does not show - which in a GMP system
// is a credibility problem, not just a cosmetic one.
export const WORKLOAD_PREDICATES: Record<
  WorkloadFilterKey,
  (r: SampleRecord, ctx: WorkloadContext) => boolean
> = {
  needsPreparation: (r) => r.preparationStatus === "NeedsPreparation" && !isClosedSample(r),

  readyToRead: (r) =>
    Boolean(r.assignedTests?.some((t) => t.workflowStatus === "ReadyToRead" || t.workflowStatus === "EnterResult")),

  awaitingReview: (r) =>
    r.status === "UnderReview" ||
    Boolean(r.assignedTests?.some((t) => t.status === "ResultEntered" || t.workflowStatus === "PendingReview")),

  overdue: (r, ctx) => !isClosedSample(r) && ctx.now - new Date(r.receivedAt).getTime() > OVERDUE_AFTER_MS,

  mine: (r, ctx) =>
    ctx.userId != null &&
    (r.assignedAnalystId === ctx.userId ||
      Boolean(r.assignedTests?.some((t) => t.assignedAnalystId === ctx.userId))),

  unassigned: (r) =>
    !isClosedSample(r) &&
    !r.assignedAnalystId &&
    !r.assignedTests?.some((t) => t.assignedAnalystId != null)
};

interface TileConfig {
  key: WorkloadFilterKey;
  label: string;
  hint: string;
  icon: React.ReactNode;
  tone: StatusTone;
  sectionHeadOnly?: boolean;
}

const TILES: TileConfig[] = [
  {
    key: "needsPreparation",
    label: "Needs Preparation",
    hint: "Preparation not yet signed off",
    icon: <ScienceOutlinedIcon sx={{ fontSize: 20 }} />,
    tone: "inconclusive"
  },
  {
    key: "readyToRead",
    label: "Ready to Read",
    hint: "Incubation complete, awaiting result entry",
    icon: <VisibilityOutlinedIcon sx={{ fontSize: 20 }} />,
    tone: "purple"
  },
  {
    key: "awaitingReview",
    label: "Awaiting Review",
    hint: "Results entered, awaiting reviewer",
    icon: <RateReviewOutlinedIcon sx={{ fontSize: 20 }} />,
    tone: "info"
  },
  {
    key: "overdue",
    label: "Overdue",
    hint: "Open more than 24 hours after receipt",
    icon: <ScheduleOutlinedIcon sx={{ fontSize: 20 }} />,
    tone: "detected"
  },
  {
    key: "mine",
    label: "Assigned to Me",
    hint: "Samples with a test assigned to you",
    icon: <PersonOutlineIcon sx={{ fontSize: 20 }} />,
    tone: "action"
  },
  {
    key: "unassigned",
    label: "Unassigned",
    hint: "No analyst assigned yet",
    icon: <PersonOffOutlinedIcon sx={{ fontSize: 20 }} />,
    tone: "pending",
    sectionHeadOnly: true
  }
];

interface Props {
  counts?: Record<WorkloadFilterKey, number> | null;
  activeKey: WorkloadFilterKey | null;
  onSelect: (key: WorkloadFilterKey) => void;
  isSectionHeadOrAdmin: boolean;
  // Optional legacy props maintained for backward compatibility:
  samples?: SampleRecord[];
  userId?: number | null;
}

export function SampleStatusKpiCards({ counts, activeKey, onSelect, isSectionHeadOrAdmin }: Props) {
  const theme = useTheme();

  const visibleTiles = useMemo(
    () => TILES.filter((t) => !t.sectionHeadOnly || isSectionHeadOrAdmin),
    [isSectionHeadOrAdmin]
  );

  return (
    <Grid container spacing={1.5} sx={{ mb: 2.5 }}>
      {visibleTiles.map((card) => {
        const isActive = activeKey === card.key;
        const count = counts?.[card.key] ?? 0;
        const iconTokens = theme.custom.status[card.tone];
        const activeTokens = theme.custom.status.purple;

        return (
          <Grid item xs={6} sm={4} md={2} key={card.key}>
            <Paper
              elevation={isActive ? 2 : 0}
              role="button"
              tabIndex={0}
              aria-pressed={isActive}
              aria-label={`${card.label}: ${count} ${count === 1 ? "sample" : "samples"}. ${card.hint}.`}
              title={card.hint}
              onClick={() => onSelect(card.key)}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  onSelect(card.key);
                }
              }}
              sx={{
                p: 1.75,
                borderRadius: 2,
                cursor: "pointer",
                border: isActive ? `2px solid ${activeTokens.border}` : "1px solid",
                borderColor: isActive ? activeTokens.border : "divider",
                bgcolor: isActive ? activeTokens.bg : "background.paper",
                transition: "all 0.15s ease-in-out",
                display: "flex",
                flexDirection: "column",
                justifyContent: "space-between",
                minHeight: 88,
                position: "relative",
                overflow: "hidden",
                // A zero count is not worth pulling the eye toward, but the
                // tile stays clickable so the filter is still reachable.
                opacity: count === 0 && !isActive ? 0.65 : 1,
                "&:hover": {
                  borderColor: activeTokens.border,
                  boxShadow: "0 2px 8px rgba(0,0,0,0.08)",
                  transform: "translateY(-1px)"
                },
                // These tiles are the page's primary filter, so the keyboard
                // path needs the same visible affordance the pointer one has.
                // .text, not .border: the border token is a pale tint that
                // would leave the ring under the 3:1 an indicator needs.
                "&:focus-visible": {
                  outline: `2px solid ${activeTokens.text}`,
                  outlineOffset: 2
                }
              }}
            >
              <Box sx={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 0.5, mb: 0.75 }}>
                <Typography
                  sx={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: isActive ? activeTokens.text : "text.secondary",
                    lineHeight: 1.2
                  }}
                >
                  {card.label}
                </Typography>
                <Box
                  sx={{
                    color: iconTokens.text,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    width: 28,
                    height: 28,
                    borderRadius: 1.5,
                    bgcolor: iconTokens.bg,
                    flexShrink: 0
                  }}
                >
                  {card.icon}
                </Box>
              </Box>

              <Box sx={{ display: "flex", alignItems: "baseline", gap: 0.5 }}>
                <Typography
                  sx={{
                    fontSize: 22,
                    fontWeight: 700,
                    color: isActive ? activeTokens.text : "text.primary",
                    lineHeight: 1
                  }}
                >
                  {count}
                </Typography>
                {isActive && (
                  <Typography sx={{ fontSize: 11, color: activeTokens.text, fontWeight: 600 }}>
                    Filtering
                  </Typography>
                )}
              </Box>
            </Paper>
          </Grid>
        );
      })}
    </Grid>
  );
}
