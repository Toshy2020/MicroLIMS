import { useState } from "react";
import { Link } from "react-router-dom";
import { Box, List, ListItemButton, ListItemText, Tooltip, Typography } from "@mui/material";
import StarIcon from "@mui/icons-material/Star";
import type { MenuGroup } from "../../routes/menuConfig";
import { favouriteKey } from "./favourites";
import { ManageFavouritesDialog } from "./ManageFavouritesDialog";
import { useFavourites } from "./useFavourites";

interface SidebarFavouritesProps {
  groups: MenuGroup[];
  collapsed: boolean;
  activePath: string | null;
  onNavigate: () => void;
}

// Rendered under My Work in stored order. Favourites the user can no longer
// open are not offered here; the manage dialog lists them so they can be removed.
export function SidebarFavourites({ groups, collapsed, activePath, onNavigate }: SidebarFavouritesProps) {
  const { items } = useFavourites(groups);
  const [manageOpen, setManageOpen] = useState(false);
  if (items.length === 0) return null;
  const usable = items.filter((i) => i.available && i.to);

  return (
    <Box>
      {!collapsed && (
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", px: 2.5, pt: 0.5 }}>
          <Typography
            component="span"
            sx={{ fontSize: 11, fontWeight: 600, letterSpacing: "0.08em", color: "rgba(255, 255, 255, 0.72)", textTransform: "uppercase" }}
          >
            Favourites
          </Typography>
          <Box
            component="button"
            type="button"
            onClick={() => setManageOpen(true)}
            sx={{
              bgcolor: "transparent",
              border: 0,
              p: 0.25,
              color: "rgba(255, 255, 255, 0.85)",
              fontSize: 11,
              cursor: "pointer",
              textDecoration: "underline",
              "&:hover": { color: "common.white" }
            }}
          >
            Manage
          </Box>
        </Box>
      )}
      <List disablePadding aria-label="Favourites">
        {usable.map((item) => {
          const active = item.to !== null && activePath === item.to;
          const link = (
            <ListItemButton
              component={Link}
              to={item.to as string}
              aria-label={collapsed ? item.label : undefined}
              aria-current={active ? "page" : undefined}
              onClick={onNavigate}
              sx={{
                minHeight: 34,
                py: 0.5,
                px: collapsed ? 2.25 : 2,
                mx: 1,
                my: 0.25,
                borderRadius: 1,
                color: "rgba(255, 255, 255, 0.85)",
                justifyContent: collapsed ? "center" : "flex-start",
                "&:hover": { bgcolor: "rgba(255, 255, 255, 0.1)", color: "common.white" }
              }}
            >
              <StarIcon sx={{ fontSize: 16, mr: collapsed ? 0 : 1.25, color: "rgba(255, 255, 255, 0.75)" }} />
              {!collapsed && (
                <ListItemText primary={item.label} slotProps={{ primary: { sx: { fontSize: 13, lineHeight: 1.3 } } }} />
              )}
            </ListItemButton>
          );
          return (
            <Box key={favouriteKey(item.fav)} component="li" sx={{ listStyle: "none" }}>
              {collapsed ? (
                <Tooltip title={item.label} placement="right">
                  {link}
                </Tooltip>
              ) : (
                link
              )}
            </Box>
          );
        })}
      </List>
      <ManageFavouritesDialog open={manageOpen} onClose={() => setManageOpen(false)} groups={groups} />
    </Box>
  );
}
