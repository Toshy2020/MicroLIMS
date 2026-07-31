import { useState, MouseEvent } from "react";
import { Box, Menu, MenuItem } from "@mui/material";
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
import { useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { getMenuForRole, MenuItem as MenuItemType } from "../routes/menuConfig";
import { brandColors } from "../theme";

// Dark-purple subnav tab strip, per the provided design. Content is
// entirely role-driven: DashboardLayout -> Role -> menuConfig.ts ->
// Visible Modules. "Laboratory Configuration" renders as a dropdown
// instead of flooding the bar with six separate top-level items.
export function Sidebar() {
  const navigate = useNavigate();
  const location = useLocation();
  const { role } = useAuth();
  const items = getMenuForRole(role);

  const [openMenu, setOpenMenu] = useState<{ anchor: HTMLElement; item: MenuItemType } | null>(null);

  const isActive = (item: MenuItemType): boolean =>
    item.path ? location.pathname === item.path : !!item.children?.some((c) => c.path === location.pathname);

  const handleClick = (e: MouseEvent<HTMLElement>, item: MenuItemType) => {
    if (item.children) {
      setOpenMenu({ anchor: e.currentTarget, item });
    } else if (item.path) {
      navigate(item.path);
    }
  };

  return (
    <Box className="no-print" sx={{ background: brandColors.subnavBg, display: "flex", gap: 3, px: 3, py: 1.25, flexWrap: "wrap" }}>
      {items.map((item) => {
        const active = isActive(item);
        return (
          <Box
            key={item.label}
            onClick={(e) => handleClick(e, item)}
            sx={{
              display: "flex", alignItems: "center",
              color: active ? "#fff" : brandColors.subnavText,
              fontSize: 14, fontWeight: 600, cursor: "pointer", pb: 0.5,
              borderBottom: active ? "2px solid #fff" : "2px solid transparent",
              "&:hover": { color: "#fff" }
            }}
          >
            {item.label}
            {item.children && <ArrowDropDownIcon fontSize="small" />}
          </Box>
        );
      })}

      <Menu
        anchorEl={openMenu?.anchor}
        open={!!openMenu}
        onClose={() => setOpenMenu(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
      >
        {openMenu?.item.children?.map((child) => (
          <MenuItem
            key={child.path}
            selected={location.pathname === child.path}
            onClick={() => { navigate(child.path!); setOpenMenu(null); }}
          >
            {child.label}
          </MenuItem>
        ))}
      </Menu>
    </Box>
  );
}
