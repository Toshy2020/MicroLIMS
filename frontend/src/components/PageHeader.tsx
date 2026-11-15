import { Typography, Box, useTheme } from "@mui/material";
import type { ReactNode } from "react";
import { useInRouterContext, useLocation } from "react-router-dom";
import { FavouriteStar } from "../modules/navigation/FavouriteStar";
import { useProvidedMenuGroups } from "../modules/navigation/MenuGroupsContext";
import { findNavTrail } from "../routes/navigation";

/**
 * @component PageHeader
 * @status Stable
 * @purpose The one page title in the app: h1 + optional subtitle, with the page's primary action(s) on the right.
 * Actions wrap below the title on narrow screens instead of squeezing it, so they stay reachable on a phone.
 * LabPage composes this, so every module's header has the same anatomy.
 * @tokens Uses `theme.palette.primary.main` (`-0.015em` optical tracking) and `text.secondary` (`maxWidth: 72ch`).
 * @a11y Renders the single page-level `<h1>` landmark heading.
 */
// The star appears only when the current location is itself a menu page (not a
// record or wizard below one) in the user's own menu.
function PageHeaderStar() {
  const location = useLocation();
  const groups = useProvidedMenuGroups();
  if (!groups) return null;
  const trail = findNavTrail(groups, location.pathname, location.search);
  if (!trail || !trail.exact || !trail.item.path) return null;
  return <FavouriteStar favourite={{ t: "p", path: trail.item.path }} name={trail.item.label} />;
}

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
  const inRouter = useInRouterContext();
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
        <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
          <Typography
            component="h1"
            variant="h5"
            sx={{ color: theme.palette.primary.main, letterSpacing: "-0.015em", overflowWrap: "anywhere" }}
          >
            {title}
          </Typography>
          {inRouter && <PageHeaderStar />}
        </Box>
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
