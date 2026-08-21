import { Paper, Box, Typography, Stack } from "@mui/material";
import { MyTask } from "../types/dashboard";
import { StatusBadge } from "../../../components/StatusBadge";
import { SectionTitle } from "../../../components/SectionTitle";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { useMyTasks } from "../hooks/useMyTasks";

const urgencyLabel: Record<MyTask["urgency"], (task: MyTask) => string> = {
  Overdue: (t) => `Overdue ${Math.round(Math.abs(Date.now() - new Date(t.dueAt).getTime()) / 3_600_000)}h`,
  DueSoon: (t) => `Due in ${Math.max(1, Math.round((new Date(t.dueAt).getTime() - Date.now()) / 3_600_000))}h`,
  DueToday: () => "Due today",
  DueTomorrow: () => "Due tomorrow"
};

// Analyst-only - the backend rejects /dashboard/my-tasks for other
// roles, so only mount this when role === Analyst.
export function MyTasksPanel() {
  const { data: tasks, loading } = useMyTasks();

  return (
    <Paper sx={{ p: 2.5 }}>
      <SectionTitle>My Tasks</SectionTitle>
      {loading || !tasks ? <LoadingSpinner /> : (
        <Stack spacing={1.5}>
          {tasks.map((t, i) => (
            <Box key={i} sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 1, pb: 1.25, borderBottom: i < tasks.length - 1 ? "1px solid" : "none", borderColor: "divider" }}>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{t.title}</Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t.subtitle}</Typography>
              </Box>
              <Box sx={{ flexShrink: 0, textAlign: "right" }}>
                <StatusBadge status={t.urgency} label={urgencyLabel[t.urgency](t)} />
              </Box>
            </Box>
          ))}
          {tasks.length === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>No tasks due right now.</Typography>}
        </Stack>
      )}
    </Paper>
  );
}
