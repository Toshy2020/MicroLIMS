import { useEffect } from "react";
import { Box, Breadcrumbs, Link as MuiLink, Skeleton, Typography } from "@mui/material";
import NavigateNextIcon from "@mui/icons-material/NavigateNext";
import { DirectionalIcon } from "./DirectionalIcon";
import { Ltr } from "./Ltr";
import { Link as RouterLink } from "react-router-dom";
import { groupLabel, groupPath, NavTrail } from "../routes/navigation";
import type { MenuGroup } from "../routes/menuConfig";
import { usePageBreadcrumb } from "./breadcrumbContext";
import { useTranslation } from "../i18n";
import { useMenuText } from "../routes/menuText";

const APP_NAME = "MicroLIMS";
const MAX_CRUMB_CHARS = 48;
// A record's own name (a sample number, a lot) keeps its left-to-right order inside an
// Arabic page; a name that is itself Arabic is left to the browser.
const ARABIC_LETTER = /[؀-ۿ]/;
const isolated = (text: string) => (ARABIC_LETTER.test(text) ? text : <Ltr>{text}</Ltr>);

const visuallyHidden = {
  position: "absolute", width: 1, height: 1, overflow: "hidden", clip: "rect(0 0 0 0)", whiteSpace: "nowrap"
} as const;

// Module context above every page: group > laboratory area > page. The last
// crumb is the current page (not a link) unless the user is on a record or
// step below it, in which case it links back to that page.
//
// Also owns the browser tab title, so tabs, history entries and screen
// readers name the page instead of every tab reading "MicroLIMS".
export function AppBreadcrumbs({ trail, groups }: { trail: NavTrail | null; groups: MenuGroup[] }) {
  const { t } = useTranslation();
  const menu = useMenuText();
  const pageLabel = usePageBreadcrumb();
  // Same parts as the trail, most specific first; a label still loading is left out.
  const loadedLabel = typeof pageLabel === "string" ? pageLabel : null;
  const trailLabel = trail ? menu.label(trail.item.label) : undefined;
  const parentLabel = trail?.parent ? menu.label(trail.parent.label) : undefined;
  const title = [loadedLabel, trailLabel, parentLabel, APP_NAME].filter(Boolean).join(" · ");

  useEffect(() => {
    document.title = title;
  }, [title]);

  if (!trail && pageLabel === undefined) return null;

  const crumbSx = { fontSize: 13, lineHeight: 1.5 };
  const pageCrumb = pageLabel === undefined ? null : (
    <Typography
      component="span"
      aria-current="page"
      aria-busy={pageLabel === null ? true : undefined}
      title={pageLabel ?? undefined}
      sx={{ ...crumbSx, color: "text.primary", fontWeight: 600, display: "inline-flex", alignItems: "center" }}
    >
      {pageLabel === null ? (
        <>
          <Skeleton variant="text" width={120} aria-hidden sx={{ fontSize: "inherit" }} />
          <Box component="span" sx={visuallyHidden}>{t("breadcrumb.loading")}</Box>
        </>
      ) : pageLabel.length > MAX_CRUMB_CHARS ? (
        <>
          <span aria-hidden>{isolated(`${pageLabel.slice(0, MAX_CRUMB_CHARS - 1)}…`)}</span>
          <Box component="span" sx={visuallyHidden}>{pageLabel}</Box>
        </>
      ) : (
        isolated(pageLabel)
      )}
    </Typography>
  );

  if (!trail) {
    return (
      <Breadcrumbs aria-label={t("breadcrumb.label")} className="no-print" sx={{ mb: 1.5, color: "text.secondary" }}>
        {pageCrumb}
      </Breadcrumbs>
    );
  }

  // A group crumb opens the group's page of cards when it holds more than
  // one entry ("Overview > Dashboard" would lead to a single card).
  const groupLinked = (groups.find((g) => g.groupName === trail.group)?.items.length ?? 0) > 1;
  const parentShown = !!trail.parent && trail.parent.label.toLowerCase() !== trail.group.toLowerCase();
  const groupCrumb = groupLinked ? (
    <MuiLink component={RouterLink} to={groupPath(trail.group)} underline="hover" color="inherit" sx={crumbSx}>
      {groupLabel(menu.group(trail.group))}
    </MuiLink>
  ) : (
    <Typography component="span" sx={crumbSx}>{groupLabel(menu.group(trail.group))}</Typography>
  );
  // A section crumb opens the section's landing page of cards.
  const sectionCrumb = (label: string) =>
    trail.parent?.path ? (
      <MuiLink component={RouterLink} to={trail.parent.path} underline="hover" color="inherit" sx={crumbSx}>
        {label}
      </MuiLink>
    ) : (
      <Typography component="span" sx={crumbSx}>{label}</Typography>
    );

  return (
    <Breadcrumbs
      aria-label={t("breadcrumb.label")}
      className="no-print"
      separator={<DirectionalIcon><NavigateNextIcon sx={{ fontSize: 16 }} /></DirectionalIcon>}
      sx={{ mb: 1.5, color: "text.secondary", "& .MuiBreadcrumbs-ol": { flexWrap: "wrap" } }}
    >
      {/* "Document Control > Document Control" says nothing twice: the
          group crumb then stands in for the section and links to it. */}
      {!parentShown && trail.parent?.path ? sectionCrumb(groupLabel(menu.group(trail.group))) : groupCrumb}
      {parentShown && sectionCrumb(menu.label(trail.parent!.label))}
      {(trail.exact && pageLabel === undefined) || !trail.item.path ? (
        <Typography component="span" aria-current={pageLabel === undefined ? "page" : undefined} sx={{ ...crumbSx, color: "text.primary", fontWeight: 600 }}>
          {trailLabel}
        </Typography>
      ) : (
        <MuiLink component={RouterLink} to={trail.item.path} underline="hover" sx={{ ...crumbSx, fontWeight: 600 }}>
          {trailLabel}
        </MuiLink>
      )}
      {pageCrumb}
    </Breadcrumbs>
  );
}
