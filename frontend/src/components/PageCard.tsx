import { Box, ButtonBase, Typography, alpha } from "@mui/material";
import { Link } from "react-router-dom";
import type { MenuItem } from "../routes/menuConfig";

// A link to a page, drawn as a card with its icon and one-line description.
// Section and group landing pages and the admin dashboard use it; a
// featured card (a lab's sample workspace) is filled and wider.
export function PageCard({ page }: { page: MenuItem }) {
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
