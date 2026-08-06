import { Paper, Typography } from "@mui/material";

// Placeholder for every Reports tab not built yet (Overview, Report
// Builder, Trending & Analysis, Saved Reports, KPI / Performance) -
// this prompt only makes Record Search functional.
export function ComingSoonPanel({ title }: { title: string }) {
  return (
    <Paper sx={{ p: 5, textAlign: "center" }}>
      <Typography sx={{ fontSize: 16, fontWeight: 600, color: "text.secondary", mb: 0.5 }}>{title}</Typography>
      <Typography sx={{ fontSize: 13, color: "#9ca3af" }}>Coming soon.</Typography>
    </Paper>
  );
}
