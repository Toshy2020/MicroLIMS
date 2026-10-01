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
    <Box sx={{ py: 5, px: 2, display: "flex", flexDirection: "column", alignItems: "center", gap: 1, textAlign: "center" }}>
      <Box sx={{ color: "text.disabled", display: "flex", "& svg": { fontSize: 40 } }}>{icon ?? <InboxOutlinedIcon />}</Box>
      <Typography sx={{ fontWeight: 600 }}>{title}</Typography>
      {description && <Typography variant="body2" sx={{ color: "text.secondary", maxWidth: 420 }}>{description}</Typography>}
      {action && <Box sx={{ mt: 1 }}>{action}</Box>}
    </Box>
  );
}
