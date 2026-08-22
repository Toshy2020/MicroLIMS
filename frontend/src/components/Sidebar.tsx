import { useRef, useState } from "react";
import {
  Drawer, Box, List, ListItemButton, ListItemIcon, ListItemText, Typography,
  Collapse, Tooltip, IconButton, useMediaQuery, useTheme, MenuItem, MenuList, Divider,
  Popper, Paper, ClickAwayListener
} from "@mui/material";
import ExpandLess from "@mui/icons-material/ExpandLess";
import ExpandMore from "@mui/icons-material/ExpandMore";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { getGroupedMenuForRole, MenuItem as MenuItemType } from "../routes/menuConfig";

export const EXPANDED_SIDEBAR_WIDTH = 250;
export const COLLAPSED_SIDEBAR_WIDTH = 68;

// How long to keep a flyout open after the pointer leaves it, so moving the
// mouse from the rail icon into the flyout panel itself doesn't close it
// mid-transit.
const FLYOUT_CLOSE_DELAY_MS = 200;

interface SidebarProps {
  mobileOpen: boolean;
  onMobileClose: () => void;
  collapsed: boolean;
  onToggleCollapse: () => void;
}

export function Sidebar({ mobileOpen, onMobileClose, collapsed, onToggleCollapse }: SidebarProps) {
  const theme = useTheme();
  const chrome = theme.custom.chrome;
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const navigate = useNavigate();
  const location = useLocation();
  const { role } = useAuth();
  const groups = getGroupedMenuForRole(role);

  // Rail (icon-only) mode only ever applies on desktop - the mobile temporary
  // Drawer always renders the full labeled nav regardless of the persisted
  // desktop collapse preference, since a narrow icon rail makes no sense
  // inside a full-width touch drawer.
  const effectiveCollapsed = collapsed && !isMobile;

  const [openSubmenus, setOpenSubmenus] = useState<Record<string, boolean>>({
    Inventory: false,
    "Laboratory Configuration": false
  });

  // Which parent item's flyout submenu is open in rail mode, and the icon
  // element it's anchored to. Only one can be open at a time.
  const [flyoutItem, setFlyoutItem] = useState<{ label: string; anchorEl: HTMLElement } | null>(null);
  const flyoutCloseTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const openFlyout = (label: string, el: HTMLElement) => {
    if (flyoutCloseTimer.current) {
      clearTimeout(flyoutCloseTimer.current);
      flyoutCloseTimer.current = null;
    }
    setFlyoutItem({ label, anchorEl: el });
  };
  const scheduleFlyoutClose = () => {
    flyoutCloseTimer.current = setTimeout(() => setFlyoutItem(null), FLYOUT_CLOSE_DELAY_MS);
  };
  const cancelFlyoutClose = () => {
    if (flyoutCloseTimer.current) {
      clearTimeout(flyoutCloseTimer.current);
      flyoutCloseTimer.current = null;
    }
  };

  const toggleSubmenu = (label: string) => {
    setOpenSubmenus((prev) => ({ ...prev, [label]: !prev[label] }));
  };

  const handleItemClick = (item: MenuItemType, anchorEl: HTMLElement) => {
    if (item.children) {
      if (effectiveCollapsed) {
        openFlyout(item.label, anchorEl);
      } else {
        toggleSubmenu(item.label);
      }
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
        bgcolor: chrome.sidebarBg,
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
            {!effectiveCollapsed && (
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
                const hasChildren = Boolean(item.children);
                const flyoutOpen = effectiveCollapsed && hasChildren && flyoutItem?.label === item.label;
                const IconComponent = item.icon;

                const button = (
                  <ListItemButton
                    onClick={(e) => handleItemClick(item, e.currentTarget)}
                    onMouseEnter={(e) => {
                      if (effectiveCollapsed && hasChildren) openFlyout(item.label, e.currentTarget);
                    }}
                    onMouseLeave={() => {
                      if (effectiveCollapsed && hasChildren) scheduleFlyoutClose();
                    }}
                    sx={{
                      minHeight: 40,
                      px: effectiveCollapsed ? 2.25 : 2,
                      py: 0.75,
                      mx: 1,
                      borderRadius: 1.5,
                      bgcolor: active ? chrome.sidebarActiveBg : "transparent",
                      color: active ? chrome.sidebarActiveText : chrome.sidebarText,
                      borderLeft: active ? `3px solid ${chrome.sidebarActiveBorder}` : "3px solid transparent",
                      "&:hover": {
                        bgcolor: "rgba(255, 255, 255, 0.1)",
                        color: "#fff"
                      },
                      justifyContent: effectiveCollapsed ? "center" : "flex-start"
                    }}
                  >
                    {IconComponent && (
                      <ListItemIcon
                        sx={{
                          minWidth: effectiveCollapsed ? 0 : 34,
                          color: active ? chrome.sidebarActiveText : "rgba(255, 255, 255, 0.75)",
                          justifyContent: "center"
                        }}
                      >
                        <IconComponent fontSize="small" />
                      </ListItemIcon>
                    )}
                    {!effectiveCollapsed && (
                      <ListItemText
                        primary={item.label}
                        primaryTypographyProps={{
                          fontSize: 13,
                          fontWeight: active ? 700 : 500,
                          noWrap: true
                        }}
                      />
                    )}
                    {!effectiveCollapsed && hasChildren && (
                      isSubOpen ? <ExpandLess sx={{ fontSize: 18 }} /> : <ExpandMore sx={{ fontSize: 18 }} />
                    )}
                  </ListItemButton>
                );

                return (
                  <Box key={item.label}>
                    {effectiveCollapsed && !hasChildren ? (
                      <Tooltip title={item.label} placement="right">
                        {button}
                      </Tooltip>
                    ) : (
                      button
                    )}

                    {/* Expanded mode: children render as an inline collapsible list. */}
                    {!effectiveCollapsed && hasChildren && (
                      <Collapse in={isSubOpen} timeout="auto" unmountOnExit>
                        <List disablePadding sx={{ pl: 2.5 }}>
                          {item.children!.map((child) => {
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
                                  bgcolor: childActive
                                    ? (theme.palette.mode === "dark" ? chrome.sidebarActiveBg : "rgba(255, 255, 255, 0.2)")
                                    : "transparent",
                                  color: childActive ? chrome.sidebarActiveText : "rgba(255, 255, 255, 0.8)",
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

                    {/* Rail mode: children render as a flyout panel to the right of the icon.
                        Popper (not Menu) deliberately - Menu's Popover/Modal base mounts an
                        invisible backdrop the instant it opens, which sits over the anchor
                        icon and steals its mouseleave, closing the flyout, which lets the
                        pointer "re-enter" the now-unhidden icon and reopen it - an open/close
                        loop that reads as flicker. Popper has no backdrop and no focus trap,
                        so hover state stays exactly where the mouse actually is. */}
                    {effectiveCollapsed && hasChildren && (
                      <Popper
                        open={flyoutOpen}
                        anchorEl={flyoutItem?.label === item.label ? flyoutItem.anchorEl : null}
                        placement="right-start"
                        sx={{ zIndex: theme.zIndex.modal }}
                        modifiers={[{ name: "offset", options: { offset: [0, 8] } }]}
                      >
                        <ClickAwayListener onClickAway={() => setFlyoutItem(null)}>
                          <Paper
                            onMouseEnter={cancelFlyoutClose}
                            onMouseLeave={scheduleFlyoutClose}
                            sx={{ minWidth: 210, py: 0.5 }}
                          >
                            <Typography
                              sx={{
                                px: 2, pt: 1, pb: 0.5,
                                fontSize: 10.5, fontWeight: 700, letterSpacing: 0.6,
                                color: "text.secondary", textTransform: "uppercase"
                              }}
                            >
                              {item.label}
                            </Typography>
                            <Divider sx={{ mb: 0.5 }} />
                            <MenuList dense>
                              {item.children!.map((child) => {
                                const childActive = location.pathname === child.path;
                                const ChildIcon = child.icon;
                                return (
                                  <MenuItem
                                    key={child.path}
                                    selected={childActive}
                                    onClick={() => {
                                      navigate(child.path!);
                                      setFlyoutItem(null);
                                    }}
                                    sx={{ fontSize: 13 }}
                                  >
                                    {ChildIcon && (
                                      <ListItemIcon sx={{ minWidth: 30 }}>
                                        <ChildIcon fontSize="small" />
                                      </ListItemIcon>
                                    )}
                                    <ListItemText primary={child.label} primaryTypographyProps={{ fontSize: 13 }} />
                                  </MenuItem>
                                );
                              })}
                            </MenuList>
                          </Paper>
                        </ClickAwayListener>
                      </Popper>
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
              bgcolor: chrome.sidebarBg
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
            bgcolor: chrome.sidebarBg,
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
