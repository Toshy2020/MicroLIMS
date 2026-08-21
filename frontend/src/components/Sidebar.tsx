import { useState } from "react";
import {
  Drawer, Box, List, ListItemButton, ListItemIcon, ListItemText, Typography,
  Collapse, Tooltip, IconButton, useMediaQuery, useTheme
} from "@mui/material";
import ExpandLess from "@mui/icons-material/ExpandLess";
import ExpandMore from "@mui/icons-material/ExpandMore";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { getGroupedMenuForRole, MenuItem as MenuItemType } from "../routes/menuConfig";
import { brandColors } from "../theme";

export const EXPANDED_SIDEBAR_WIDTH = 250;
export const COLLAPSED_SIDEBAR_WIDTH = 68;

interface SidebarProps {
  mobileOpen: boolean;
  onMobileClose: () => void;
  collapsed: boolean;
  onToggleCollapse: () => void;
}

export function Sidebar({ mobileOpen, onMobileClose, collapsed, onToggleCollapse }: SidebarProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const navigate = useNavigate();
  const location = useLocation();
  const { role } = useAuth();
  const groups = getGroupedMenuForRole(role);

  const [openSubmenus, setOpenSubmenus] = useState<Record<string, boolean>>({
    Inventory: false,
    "Laboratory Configuration": false
  });

  const toggleSubmenu = (label: string) => {
    setOpenSubmenus((prev) => ({ ...prev, [label]: !prev[label] }));
  };

  const handleItemClick = (item: MenuItemType) => {
    if (item.children) {
      toggleSubmenu(item.label);
    } else if (item.path) {
      navigate(item.path);
      if (isMobile) onMobileClose();
    }
  };

  const isItemActive = (item: MenuItemType): boolean =>
    item.path ? location.pathname === item.path : !!item.children?.some((c) => c.path === location.pathname);

  const drawerContent = (
    <Box
      sx={{
        height: "100%",
        display: "flex",
        flexDirection: "column",
        bgcolor: brandColors.subnavBg,
        color: "#fff",
        overflow: "hidden"
      }}
    >
      <Box
        sx={{
          flex: 1,
          overflowY: "auto",
          overflowX: "hidden",
          py: 1,
          "&::-webkit-scrollbar": { width: 5 },
          "&::-webkit-scrollbar-track": { bgcolor: "transparent" },
          "&::-webkit-scrollbar-thumb": { bgcolor: "rgba(255, 255, 255, 0.2)", borderRadius: 3 },
          "&::-webkit-scrollbar-thumb:hover": { bgcolor: "rgba(255, 255, 255, 0.4)" }
        }}
      >
        {groups.map((group, groupIdx) => (
          <Box key={group.groupName} sx={{ mb: 1 }}>
            {!collapsed && (
              <Typography
                sx={{
                  px: 2.5,
                  pt: groupIdx === 0 ? 0.75 : 1.75,
                  pb: 0.5,
                  fontSize: 10,
                  fontWeight: 700,
                  letterSpacing: 1.1,
                  color: "rgba(255, 255, 255, 0.6)",
                  textTransform: "uppercase"
                }}
              >
                {group.groupName}
              </Typography>
            )}

            <List disablePadding>
              {group.items.map((item) => {
                const active = isItemActive(item);
                const isSubOpen = Boolean(openSubmenus[item.label]);
                const IconComponent = item.icon;

                const button = (
                  <ListItemButton
                    onClick={() => handleItemClick(item)}
                    sx={{
                      minHeight: 40,
                      px: collapsed ? 2.25 : 2,
                      py: 0.75,
                      mx: 1,
                      borderRadius: 1.5,
                      bgcolor: active ? "rgba(255, 255, 255, 0.15)" : "transparent",
                      color: active ? "#fff" : brandColors.subnavText,
                      borderLeft: active ? "3px solid #fff" : "3px solid transparent",
                      "&:hover": {
                        bgcolor: "rgba(255, 255, 255, 0.1)",
                        color: "#fff"
                      },
                      justifyContent: collapsed ? "center" : "flex-start"
                    }}
                  >
                    {IconComponent && (
                      <ListItemIcon
                        sx={{
                          minWidth: collapsed ? 0 : 34,
                          color: active ? "#fff" : "rgba(255, 255, 255, 0.75)",
                          justifyContent: "center"
                        }}
                      >
                        <IconComponent fontSize="small" />
                      </ListItemIcon>
                    )}
                    {!collapsed && (
                      <ListItemText
                        primary={item.label}
                        primaryTypographyProps={{
                          fontSize: 13,
                          fontWeight: active ? 700 : 500,
                          noWrap: true
                        }}
                      />
                    )}
                    {!collapsed && item.children && (
                      isSubOpen ? <ExpandLess sx={{ fontSize: 18 }} /> : <ExpandMore sx={{ fontSize: 18 }} />
                    )}
                  </ListItemButton>
                );

                return (
                  <Box key={item.label}>
                    {collapsed ? (
                      <Tooltip title={item.label} placement="right">
                        {button}
                      </Tooltip>
                    ) : (
                      button
                    )}

                    {!collapsed && item.children && (
                      <Collapse in={isSubOpen} timeout="auto" unmountOnExit>
                        <List disablePadding sx={{ pl: 2.5 }}>
                          {item.children.map((child) => {
                            const childActive = location.pathname === child.path;
                            return (
                              <ListItemButton
                                key={child.path}
                                onClick={() => {
                                  navigate(child.path!);
                                  if (isMobile) onMobileClose();
                                }}
                                sx={{
                                  minHeight: 32,
                                  py: 0.5,
                                  px: 1.75,
                                  my: 0.25,
                                  borderRadius: 1,
                                  bgcolor: childActive ? "rgba(255, 255, 255, 0.2)" : "transparent",
                                  color: childActive ? "#fff" : "rgba(255, 255, 255, 0.8)",
                                  "&:hover": {
                                    bgcolor: "rgba(255, 255, 255, 0.1)",
                                    color: "#fff"
                                  }
                                }}
                              >
                                <ListItemText
                                  primary={child.label}
                                  primaryTypographyProps={{
                                    fontSize: 12,
                                    fontWeight: childActive ? 700 : 400,
                                    noWrap: true
                                  }}
                                />
                              </ListItemButton>
                            );
                          })}
                        </List>
                      </Collapse>
                    )}
                  </Box>
                );
              })}
            </List>
          </Box>
        ))}
      </Box>

      {/* Collapse/Expand Toggle on Desktop */}
      {!isMobile && (
        <Box sx={{ p: 1, borderTop: "1px solid rgba(255, 255, 255, 0.12)", textAlign: "center", flexShrink: 0 }}>
          <Tooltip title={collapsed ? "Expand sidebar" : "Collapse sidebar"}>
            <IconButton onClick={onToggleCollapse} sx={{ color: "rgba(255, 255, 255, 0.8)", "&:hover": { color: "#fff" } }}>
              {collapsed ? <ChevronRightIcon /> : <ChevronLeftIcon />}
            </IconButton>
          </Tooltip>
        </Box>
      )}
    </Box>
  );

  return (
    <>
      {isMobile ? (
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={onMobileClose}
          ModalProps={{ keepMounted: true }}
          sx={{
            display: { xs: "block", md: "none" },
            "& .MuiDrawer-paper": {
              width: EXPANDED_SIDEBAR_WIDTH,
              boxSizing: "border-box",
              borderRight: "none",
              bgcolor: brandColors.subnavBg
            }
          }}
        >
          {drawerContent}
        </Drawer>
      ) : (
        <Box
          component="nav"
          className="no-print"
          sx={{
            width: collapsed ? COLLAPSED_SIDEBAR_WIDTH : EXPANDED_SIDEBAR_WIDTH,
            flexShrink: 0,
            height: "100%",
            bgcolor: brandColors.subnavBg,
            transition: theme.transitions.create("width", {
              easing: theme.transitions.easing.sharp,
              duration: theme.transitions.duration.enteringScreen
            }),
            overflow: "hidden",
            display: { xs: "none", md: "flex" },
            flexDirection: "column"
          }}
        >
          {drawerContent}
        </Box>
      )}
    </>
  );
}
