import { Typography, Box, useTheme } from "@mui/material";
import type { ReactNode } from "react";

/**
 * @component PageHeader
 * @status Stable
 * @purpose The one page title in the app: h1 + optional subtitle, with the page's primary action(s) on the right.
 * Actions wrap below the title on narrow screens instead of squeezing it, so they stay reachable on a phone.
 * LabPage composes this, so every module's header has the same anatomy.
 * @tokens Uses `theme.palette.primary.main` (`-0.015em` optical tracking) and `text.secondary` (`maxWidth: 72ch`).
 * @a11y Renders the single page-level `<h1>` landmark heading.
 */
export function PageHeader({
  title,
  subtitle,
  children
}: {
  title: ReactNode;
  subtitle?: ReactNode;
  // Primary/secondary page actions.
  children?: ReactNode;
}) {
  const theme = useTheme();
  return (
    <Box
      sx={{
        mb: 2,
        display: "flex",
        justifyContent: "space-between",
        alignItems: "flex-start",
        flexWrap: "wrap",
        columnGap: 2,
        rowGap: 1.5
      }}
    >
      <Box sx={{ minWidth: 0, flex: "1 1 320px" }}>
        <Typography
          component="h1"
          variant="h5"
          sx={{ color: theme.palette.primary.main, letterSpacing: "-0.015em", overflowWrap: "anywhere" }}
        >
          {title}
        </Typography>
        {subtitle && (
          <Typography
            variant="body2"
            sx={{ color: "text.secondary", mt: 0.5, maxWidth: "72ch", lineHeight: 1.55 }}
          >
            {subtitle}
          </Typography>
        )}
      </Box>
      {children && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>{children}</Box>
      )}
    </Box>
  );
}
