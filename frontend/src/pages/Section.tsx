import { Box, Card, CardActionArea, Typography } from "@mui/material";
import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import { Link, useLocation } from "react-router-dom";
import { PageHeader } from "../components/PageHeader";
import { useMenuGroups } from "../hooks/useMenuGroups";

// Landing page of a sidebar section: one card per page the user may open in
// it, read from the same permission-filtered menu as the sidebar.
export function SectionPage() {
  const { pathname } = useLocation();
  const section = useMenuGroups().flatMap((g) => g.items).find((i) => i.children && i.path === pathname);

  if (!section) return <PageHeader title="Section not available" subtitle="You have no pages in this section." />;

  const Icon = section.icon;
  const count = section.children!.length;

  return (
    <>
      <PageHeader
        title={
          <Box component="span" sx={{ display: "inline-flex", alignItems: "center", gap: 1 }}>
            {Icon && <Icon />}
            {section.label}
          </Box>
        }
        subtitle={`${count} ${count === 1 ? "page" : "pages"} in this section`}
      />
      <Box
        component="ul"
        sx={{ listStyle: "none", m: 0, p: 0, display: "grid", gap: 2, gridTemplateColumns: "repeat(auto-fill, minmax(min(100%, 280px), 1fr))" }}
      >
        {section.children!.map((child) => (
          <Card
            key={child.path}
            component="li"
            variant="outlined"
            sx={{
              transition: (t) => t.transitions.create(["border-color", "box-shadow"], { duration: 200 }),
              "&:hover, &:focus-within": { borderColor: "primary.main", boxShadow: 2 },
              "&:hover .section-card-arrow, &:focus-within .section-card-arrow": { color: "primary.main", transform: "translateX(2px)" }
            }}
          >
            <CardActionArea
              component={Link}
              to={child.path!}
              sx={{
                height: "100%",
                p: 2,
                display: "flex",
                alignItems: "flex-start",
                gap: 1.5,
                "&.Mui-focusVisible": { outline: (t) => `2px solid ${t.palette.primary.main}`, outlineOffset: -2 }
              }}
            >
              <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography component="h2" variant="subtitle1" sx={{ fontWeight: 600, lineHeight: 1.3 }}>
                  {child.label}
                </Typography>
                {child.description && (
                  <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5, lineHeight: 1.5 }}>
                    {child.description}
                  </Typography>
                )}
              </Box>
              <ArrowForwardIcon
                className="section-card-arrow"
                fontSize="small"
                aria-hidden
                sx={{ color: "text.disabled", mt: 0.25, transition: "transform 200ms, color 200ms" }}
              />
            </CardActionArea>
          </Card>
        ))}
      </Box>
    </>
  );
}
