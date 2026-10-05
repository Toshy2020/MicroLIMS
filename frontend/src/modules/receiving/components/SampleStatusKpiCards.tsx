import React, { useMemo } from "react";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import RateReviewOutlinedIcon from "@mui/icons-material/RateReviewOutlined";
import ScheduleOutlinedIcon from "@mui/icons-material/ScheduleOutlined";
import PersonOutlineIcon from "@mui/icons-material/PersonOutlined";
import PersonOffOutlinedIcon from "@mui/icons-material/PersonOffOutlined";
import { StatusTone } from "../../../theme/statusTokens";
import { SummaryTiles, type SummaryTile } from "../../../components/configHierarchy/SummaryTiles";

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
// label, hint, icon and tone.

interface TileConfig {
  key: WorkloadFilterKey;
  label: string;
  hint: string;
  icon: React.ReactNode;
  tone: StatusTone;
  assignersOnly?: boolean;
}

const TILES: TileConfig[] = [
  {
    key: "needsPreparation",
    label: "Needs Preparation",
    hint: "Preparation not yet signed off",
    icon: <ScienceOutlinedIcon />,
    tone: "inconclusive"
  },
  {
    key: "readyToRead",
    label: "Ready to Read",
    hint: "Incubation complete, awaiting result entry",
    icon: <VisibilityOutlinedIcon />,
    tone: "purple"
  },
  {
    key: "awaitingReview",
    label: "Awaiting Review",
    hint: "All tests complete, awaiting reviewer",
    icon: <RateReviewOutlinedIcon />,
    tone: "info"
  },
  {
    key: "overdue",
    label: "Overdue",
    hint: "Open more than 24 hours after receipt",
    icon: <ScheduleOutlinedIcon />,
    tone: "detected"
  },
  {
    key: "mine",
    label: "Assigned to Me",
    hint: "Samples with a test assigned to you",
    icon: <PersonOutlineIcon />,
    tone: "action"
  },
  {
    key: "unassigned",
    label: "Unassigned",
    hint: "No analyst assigned yet",
    icon: <PersonOffOutlinedIcon />,
    tone: "pending",
    assignersOnly: true
  }
];

interface Props {
  counts?: Record<WorkloadFilterKey, number> | null;
  activeKey: AllWorkloadFilterKey | null;
  onSelect: (key: WorkloadFilterKey) => void;
  canAssignAnalyst: boolean;
}

export function SampleStatusKpiCards({ counts, activeKey, onSelect, canAssignAnalyst }: Props) {
  const tiles = useMemo<SummaryTile[]>(
    () =>
      TILES.filter((t) => !t.assignersOnly || canAssignAnalyst).map((card) => {
        const count = counts?.[card.key] ?? 0;
        return {
          label: card.label,
          value: count,
          caption: count === 1 ? "sample" : "samples",
          hint: card.hint,
          icon: card.icon,
          tone: card.tone,
          active: activeKey === card.key,
          onClick: () => onSelect(card.key)
        };
      }),
    [counts, activeKey, onSelect, canAssignAnalyst]
  );

  return <SummaryTiles tiles={tiles} />;
}
