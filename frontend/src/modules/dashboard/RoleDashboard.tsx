import { Grid, Paper, Typography } from "@mui/material";

// Composes the summary tiles returned by GET /api/dashboard - content
// differs per role, decided entirely by the backend DashboardService.
export function RoleDashboard({ summary }: { summary: Record<string, number> }) {
  return (
    <Grid container spacing={2}>
      {Object.entries(summary).map(([key, value]) => (
        <Grid item xs={12} sm={4} key={key}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h4">{value}</Typography>
            <Typography variant="body2" color="text.secondary">{key}</Typography>
          </Paper>
        </Grid>
      ))}
    </Grid>
  );
}
