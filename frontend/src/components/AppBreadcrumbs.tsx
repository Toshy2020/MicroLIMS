import { useEffect } from "react";
import { Breadcrumbs, Link as MuiLink, Typography } from "@mui/material";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import { Link as RouterLink } from "react-router-dom";
import { groupLabel, NavTrail } from "../routes/navigation";

const APP_NAME = "MicroLIMS";

// Module context above every page: group > laboratory area > page. The last
// crumb is the current page (not a link) unless the user is on a record or
// step below it, in which case it links back to that page.
//
// Also owns the browser tab title, so tabs, history entries and screen
// readers name the page instead of every tab reading "MicroLIMS".
export function AppBreadcrumbs({ trail }: { trail: NavTrail | null }) {
  const title = trail
    ? [trail.item.label, trail.parent?.label, APP_NAME].filter(Boolean).join(" · ")
    : APP_NAME;

  useEffect(() => {
    document.title = title;
  }, [title]);

  if (!trail) return null;

  const crumbSx = { fontSize: 13, lineHeight: 1.5 };

  return (
    <Breadcrumbs
      aria-label="Breadcrumb"
      className="no-print"
      separator={<NavigateNextIcon sx={{ fontSize: 16 }} />}
      sx={{ mb: 1.5, color: "text.secondary", "& .MuiBreadcrumbs-ol": { flexWrap: "wrap" } }}
    >
      <Typography component="span" sx={crumbSx}>{groupLabel(trail.group)}</Typography>
      {/* "Document Control > Document Control" says nothing twice. */}
      {trail.parent && trail.parent.label.toLowerCase() !== trail.group.toLowerCase() && (
        <Typography component="span" sx={crumbSx}>{trail.parent.label}</Typography>
      )}
      {trail.exact || !trail.item.path ? (
        <Typography component="span" aria-current="page" sx={{ ...crumbSx, color: "text.primary", fontWeight: 600 }}>
          {trail.item.label}
        </Typography>
      ) : (
        <MuiLink component={RouterLink} to={trail.item.path} underline="hover" sx={{ ...crumbSx, fontWeight: 600 }}>
          {trail.item.label}
        </MuiLink>
      )}
    </Breadcrumbs>
  );
}
