import { Box, ButtonBase, Typography, alpha } from "@mui/material";
import { Link, useLocation } from "react-router-dom";
import { PageHeader } from "../components/PageHeader";
import { useMenuGroups } from "../hooks/useMenuGroups";
import type { MenuItem } from "../routes/menuConfig";
import { groupLabel, groupPath } from "../routes/navigation";

// Landing page of a sidebar section or menu group: one card per page the user may open in
// it, read from the same permission-filtered menu as the sidebar. The lab's
// sample workspaces (featured) lead, drawn wider and filled.
export function SectionPage() {
  const { pathname } = useLocation();
  const groups = useMenuGroups();
  const section = groups.flatMap((g) => g.items).find((i) => i.children && i.path === pathname);
  const group = section ? undefined : groups.find((g) => groupPath(g.groupName) === pathname);

  if (!section && !group) return <PageHeader title="Section not available" subtitle="You have no pages in this section." />;

  const Icon = section?.icon;
  const title = section ? section.label : groupLabel(group!.groupName);
  // On a group page a section card says how much it holds.
  const pages = (section ? [...section.children!] : group!.items.map((i) =>
    i.children && !i.description ? { ...i, description: `${i.children.length} pages` } : i
  )).sort((a, b) => Number(!!b.featured) - Number(!!a.featured));

  return (
    <>
      <PageHeader
        title={
          <Box component="span" sx={{ display: "inline-flex", alignItems: "center", gap: 1.25 }}>
            {Icon && <Icon />}
            {title}
          </Box>
        }
        subtitle={`${pages.length} ${pages.length === 1 ? "page" : "pages"} you can open here`}
      />
      <Box
        component="ul"
        sx={{
          listStyle: "none",
          m: 0,
          p: 0,
          display: "grid",
          gap: 2,
          gridTemplateColumns: "repeat(auto-fill, minmax(min(100%, 250px), 1fr))"
        }}
      >
        {pages.map((page) => (
          <Box component="li" key={page.path} sx={{ display: "flex", gridColumn: page.featured ? { sm: "span 2" } : undefined }}>
            <PageCard page={page} />
          </Box>
        ))}
      </Box>
    </>
  );
}

function PageCard({ page }: { page: MenuItem }) {
  const Icon = page.icon;
  const featured = !!page.featured;

  return (
    <ButtonBase
      component={Link}
      to={page.path!}
      focusRipple
      sx={(t) => ({
        flex: 1,
        display: "flex",
        alignItems: featured ? "center" : "flex-start",
        justifyContent: "flex-start",
        textAlign: "left",
        gap: 2,
        p: featured ? 2.75 : 2.25,
        borderRadius: 3,
        border: "1px solid",
        borderColor: featured ? "primary.main" : "divider",
        bgcolor: featured ? "primary.main" : "background.paper",
        color: featured ? "primary.contrastText" : "text.primary",
        transition: t.transitions.create(["border-color", "background-color", "box-shadow"], { duration: 180 }),
        "& .page-card-tile": {
          transition: t.transitions.create(["background-color", "color"], { duration: 180 })
        },
        "&:hover, &.Mui-focusVisible": featured
          ? { bgcolor: "primary.dark", boxShadow: `0 6px 18px ${alpha(t.palette.primary.main, 0.3)}` }
          : {
              borderColor: alpha(t.palette.primary.main, 0.55),
              boxShadow: `0 4px 14px ${alpha(t.palette.primary.main, 0.12)}`,
              "& .page-card-tile": { bgcolor: "primary.main", color: "primary.contrastText" }
            },
        "&.Mui-focusVisible": { outline: `2px solid ${t.palette.primary.main}`, outlineOffset: 2 },
        "@media (prefers-reduced-motion: reduce)": { transition: "none", "& .page-card-tile": { transition: "none" } }
      })}
    >
      {Icon && (
        <Box
          className="page-card-tile"
          aria-hidden
          sx={(t) => ({
            flexShrink: 0,
            width: featured ? 52 : 44,
            height: featured ? 52 : 44,
            borderRadius: 2,
            display: "grid",
            placeItems: "center",
            bgcolor: featured ? alpha(t.palette.common.white, 0.16) : alpha(t.palette.primary.main, 0.09),
            color: featured ? "common.white" : "primary.main"
          })}
        >
          <Icon fontSize={featured ? "large" : "medium"} />
        </Box>
      )}
      <Box sx={{ minWidth: 0 }}>
        <Typography component="h2" sx={{ fontSize: featured ? 18 : 15.5, fontWeight: 600, lineHeight: 1.3 }}>
          {page.label}
        </Typography>
        {page.description && (
          <Typography
            variant="body2"
            sx={{ mt: 0.5, lineHeight: 1.5, color: featured ? alpha("#fff", 0.85) : "text.secondary" }}
          >
            {page.description}
          </Typography>
        )}
      </Box>
    </ButtonBase>
  );
}
