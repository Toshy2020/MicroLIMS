import { Box, Paper, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import { StatusBadge } from "../StatusBadge";

interface ResultSectionProps {
  // Shown in a numbered circle when the section is one step of a sequence.
  step?: number;
  title: string;
  status?: string;
  actions?: ReactNode;
  children: ReactNode;
}

export function ResultSection({ step, title, status, actions, children }: ResultSectionProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", mb: 1.5 }}>
        {step !== undefined && (
          <Box
            aria-hidden
            sx={{
              width: 24, height: 24, borderRadius: "50%", flexShrink: 0,
              display: "flex", alignItems: "center", justifyContent: "center",
              fontSize: 12, fontWeight: 700, bgcolor: "primary.main", color: "primary.contrastText"
            }}
          >
            {step}
          </Box>
        )}
        <Typography sx={{ fontWeight: 700 }}>{title}</Typography>
        {status && <StatusBadge status={status} />}
        {actions && <Box sx={{ ml: "auto" }}>{actions}</Box>}
      </Stack>
      {children}
    </Paper>
  );
}
