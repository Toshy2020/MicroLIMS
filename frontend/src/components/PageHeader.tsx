import { Typography, Box, useTheme } from "@mui/material";

// h1.page-title + p.subtitle from the design.
export function PageHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  const theme = useTheme();
  return (
    <Box sx={{ mb: 1 }}>
      <Typography sx={{ fontSize: 24, fontWeight: 700, color: theme.palette.primary.main, mb: 0.5 }}>{title}</Typography>
      {subtitle && <Typography sx={{ color: "text.secondary", mb: 1 }}>{subtitle}</Typography>}
    </Box>
  );
}
