import { Box } from "@mui/material";
import { Link, useLocation } from "react-router-dom";
import { PageCard } from "../components/PageCard";
import { PageHeader } from "../components/PageHeader";
import { useMenuGroups } from "../hooks/useMenuGroups";
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
