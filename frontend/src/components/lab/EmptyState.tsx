import { Box, Typography } from "@mui/material";
import InboxOutlinedIcon from "@mui/icons-material/InboxOutlined";
import type { ReactNode } from "react";

interface EmptyStateProps {
  title: string;
  description?: string;
  // Usually the page's primary action, so an empty register is never a dead end.
  action?: ReactNode;
  icon?: ReactNode;
}

export function EmptyState({ title, description, action, icon }: EmptyStateProps) {
  return (
    <Box role="status" sx={{ py: 4, px: 2, display: "flex", flexDirection: "column", alignItems: "center", gap: 0.75, textAlign: "center" }}>
      <Box aria-hidden sx={{ color: "text.disabled", display: "flex", "& svg": { fontSize: 36 } }}>{icon ?? <InboxOutlinedIcon />}</Box>
      <Typography variant="subtitle1">{title}</Typography>
      {description && <Typography variant="body2" sx={{ color: "text.secondary", maxWidth: 420 }}>{description}</Typography>}
      {action && <Box sx={{ mt: 1 }}>{action}</Box>}
    </Box>
  );
}
