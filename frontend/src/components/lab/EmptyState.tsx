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

/**
 * @component EmptyState
 * @status Stable
 * @purpose Standardized zero-data / empty-filter state with a `48x48px` tinted icon badge and optional primary recovery CTA.
 * @tokens Uses `action.hover`, `divider`, `text.primary`, and `text.secondary`.
 * @a11y Exposes `role="status"` so assistive technologies announce zero-result filter transitions.
 */
export function EmptyState({ title, description, action, icon }: EmptyStateProps) {
  return (
    <Box
      role="status"
      sx={{
        py: 5,
        px: 2.5,
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: 1,
        textAlign: "center"
      }}
    >
      <Box
        aria-hidden
        sx={{
          width: 48,
          height: 48,
          borderRadius: 2.5,
          bgcolor: "action.hover",
          color: "text.secondary",
          display: "grid",
          placeItems: "center",
          mb: 0.5,
          "& svg": { fontSize: 26 }
        }}
      >
        {icon ?? <InboxOutlinedIcon />}
      </Box>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, letterSpacing: "-0.005em" }}>
        {title}
      </Typography>
      {description && (
        <Typography variant="body2" sx={{ color: "text.secondary", maxWidth: 420, lineHeight: 1.55 }}>
          {description}
        </Typography>
      )}
      {action && <Box sx={{ mt: 1.25 }}>{action}</Box>}
    </Box>
  );
}
