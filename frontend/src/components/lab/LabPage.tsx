import { Box, Stack, Typography, useTheme } from "@mui/material";
import type { ReactNode } from "react";

interface LabPageProps {
  title: string;
  subtitle?: string;
  // Primary action(s), right-aligned in the header.
  actions?: ReactNode;
  kpis?: ReactNode;
  filters?: ReactNode;
  children: ReactNode;
}

// Page anatomy for every physicochemical page: header -> KPIs -> filters ->
// content. Padding and gaps live here so pages never set their own.
export function LabPage({ title, subtitle, actions, kpis, filters, children }: LabPageProps) {
  const theme = useTheme();
  return (
    <Box sx={{ p: 3 }}>
      <Stack spacing={2}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 2, flexWrap: "wrap" }}>
          <Box>
            <Typography component="h1" variant="h5" sx={{ fontWeight: 700, color: theme.palette.primary.main }}>{title}</Typography>
            {subtitle && <Typography sx={{ color: "text.secondary", mt: 0.5, fontSize: 14 }}>{subtitle}</Typography>}
          </Box>
          {actions && <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>{actions}</Stack>}
        </Box>
        {kpis}
        {filters}
        {children}
      </Stack>
    </Box>
  );
}
