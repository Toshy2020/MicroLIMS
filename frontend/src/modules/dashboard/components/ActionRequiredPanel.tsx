import { Paper, Box, Typography, Stack, Button, useTheme } from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { Link } from "react-router-dom";
import { MyTask } from "../types/dashboard";
import { StatusBadge } from "../../../components/StatusBadge";
import { SectionTitle } from "../../../components/SectionTitle";
import { LoadingSpinner } from "../../../components/LoadingSpinner";

const urgencyLabel: Record<MyTask["urgency"], (task: MyTask) => string> = {
  Overdue: (t) => `Overdue ${Math.round(Math.abs(Date.now() - new Date(t.dueAt).getTime()) / 3_600_000)}h`,
  DueSoon: (t) => `Due in ${Math.max(1, Math.round((new Date(t.dueAt).getTime() - Date.now()) / 3_600_000))}h`,
  DueToday: () => "Due today",
  DueTomorrow: () => "Due tomorrow"
};

function getTaskRoute(task: MyTask): string {
  if (task.mediaId) {
    return "/laboratory-configuration/media";
  } else if (task.sampleId && task.testOrderId) {
    return `/testing-workspace?sampleId=${task.sampleId}&testOrderId=${task.testOrderId}`;
  } else if (task.sampleId) {
    return `/testing-workspace?sampleId=${task.sampleId}`;
  } else {
    return "/testing-workspace?scope=mine";
  }
}

interface ActionRequiredPanelProps {
  tasks: MyTask[] | null;
  loading?: boolean;
}

export function ActionRequiredPanel({ tasks, loading }: ActionRequiredPanelProps) {
  const theme = useTheme();

  return (
    <Paper sx={{ p: 2.5, mb: 2 }}>
      <SectionTitle
        tabs={[
          {
            label: "Open Testing Workspace",
            to: "/testing-workspace"
          }
        ]}
      >
        Action Required (By Me)
      </SectionTitle>

      {loading || !tasks ? (
        <LoadingSpinner />
      ) : (
        <Stack spacing={1.5}>
          {tasks.map((t, i) => (
            <Box
              key={i}
              sx={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                gap: 1.5,
                p: 1.5,
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: theme.palette.divider,
                bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.01)",
                transition: "background 0.15s ease",
                "&:hover": {
                  bgcolor: theme.palette.mode === "dark" ? "rgba(255,255,255,0.05)" : "rgba(0,0,0,0.03)"
                }
              }}
            >
              <Box sx={{ minWidth: 0, flex: 1 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.25 }}>
                  <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.text.primary }}>
                    {t.title}
                  </Typography>
                  <StatusBadge status={t.urgency} label={urgencyLabel[t.urgency](t)} />
                  {t.isReturned && <StatusBadge status="Returned" label="Returned" />}
                </Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {t.subtitle}
                </Typography>
              </Box>

              <Box sx={{ flexShrink: 0 }}>
                <Button
                  component={Link}
                  to={getTaskRoute(t)}
                  size="small"
                  variant="contained"
                  endIcon={<ArrowForwardIcon sx={{ fontSize: 16 }} />}
                  sx={{
                    textTransform: "none",
                    fontWeight: 600,
                    fontSize: 12,
                    borderRadius: 1.5,
                    px: 2
                  }}
                >
                  {t.mediaId ? "Read GPT" : t.isReturned ? "Revise Result" : "Enter Result"}
                </Button>
              </Box>
            </Box>
          ))}

          {tasks.length === 0 && (
            <Box sx={{ py: 3, textAlign: "center" }}>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
                No actions required right now. All assigned testing is on schedule.
              </Typography>
            </Box>
          )}
        </Stack>
      )}
    </Paper>
  );
}
