import { Grid, Paper, Typography, Box, useTheme } from "@mui/material";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlined";
import AccessTimeIcon from "@mui/icons-material/AccessTime";
import TodayIcon from "@mui/icons-material/Today";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlined";
import { SvgIconComponent } from "@mui/icons-material";
import { Link } from "react-router-dom";
import { MyTask } from "../types/dashboard";
import { useDashboardLab } from "../DashboardLabContext";

interface SummaryCardProps {
  label: string;
  count: number;
  icon: SvgIconComponent;
  color: string;
  bgWash: string;
  to: string;
}

function SummaryCard({ label, count, icon: Icon, color, bgWash, to }: SummaryCardProps) {
  return (
    <Paper
      component={Link}
      to={to}
      sx={{
        p: 2,
        display: "flex",
        alignItems: "center",
        gap: 1.5,
        cursor: "pointer",
        textDecoration: "none",
        color: "inherit",
        transition: "transform 0.15s ease, box-shadow 0.15s ease",
        "&:hover": { transform: "translateY(-2px)", boxShadow: 3 },
        "&:focus-visible": { outline: `2px solid ${color}`, outlineOffset: 2 }
      }}
    >
      <Box
        sx={{
          width: 44,
          height: 44,
          borderRadius: "12px",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          bgcolor: bgWash,
          color,
          flexShrink: 0
        }}
      >
        <Icon fontSize="medium" />
      </Box>
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontSize: 24, fontWeight: 700, lineHeight: 1.1, color }}>
          {count}
        </Typography>
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.secondary" }} noWrap>
          {label}
        </Typography>
      </Box>
    </Paper>
  );
}

// Workspace filters per tile; the path is the dashboard's own laboratory.
const CATEGORY_QUERIES: Record<string, string> = {
  Overdue: "?scope=mine&urgency=overdue",
  DueNow: "?scope=mine",
  DueToday: "?scope=mine",
  ReadyToRead: "?scope=mine&testStatus=ReadyToRead"
};

interface AnalystWorkSummaryProps {
  tasks: MyTask[];
  readyToReadCount: number;
}

export function AnalystWorkSummary({ tasks, readyToReadCount }: AnalystWorkSummaryProps) {
  const theme = useTheme();
  const lab = useDashboardLab();

  const overdueCount = tasks.filter((t) => t.urgency === "Overdue").length;
  const dueNowCount = tasks.filter((t) => t.urgency === "DueSoon").length;
  const dueTodayCount = tasks.filter((t) => t.urgency === "DueToday").length;

  const allCards = [
    {
      label: "Overdue",
      count: overdueCount,
      icon: ErrorOutlineIcon,
      color: theme.custom.status.detected.text,
      bgWash: theme.custom.status.detected.bg,
      category: "Overdue" as const
    },
    {
      label: "Due Now",
      count: dueNowCount,
      icon: AccessTimeIcon,
      color: theme.custom.status.action.text,
      bgWash: theme.custom.status.action.bg,
      category: "DueNow" as const
    },
    {
      label: "Due Today",
      count: dueTodayCount,
      icon: TodayIcon,
      color: theme.custom.status.purple.text,
      bgWash: theme.custom.status.purple.bg,
      category: "DueToday" as const
    },
    {
      label: "Ready to Read",
      count: readyToReadCount,
      icon: CheckCircleOutlineIcon,
      color: theme.custom.status.notDetected.text,
      bgWash: theme.custom.status.notDetected.bg,
      category: "ReadyToRead" as const
    }
  ];
  // "Ready to Read" is the end of an incubation - Microbiology only.
  const cards = lab.isPhyschem ? allCards.filter((c) => c.category !== "ReadyToRead") : allCards;

  return (
    <Grid container spacing={2} sx={{ mb: 2 }}>
      {cards.map((c) => (
        <Grid
          key={c.label}
          size={{
            xs: 12,
            sm: 6,
            md: 12 / cards.length
          }}>
          <SummaryCard
            label={c.label}
            count={c.count}
            icon={c.icon}
            color={c.color}
            bgWash={c.bgWash}
            to={lab.workspace(CATEGORY_QUERIES[c.category])}
          />
        </Grid>
      ))}
    </Grid>
  );
}
