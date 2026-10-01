import { useEffect, useRef, useState } from "react";
import {
  Drawer, Box, List, ListItemButton, ListItemIcon, ListItemText, Typography,
  Collapse, Tooltip, IconButton, useMediaQuery, useTheme, MenuItem, MenuList, Divider,
  Popper, Paper, ClickAwayListener
} from "@mui/material";
import ExpandLess from "@mui/icons-material/ExpandLess";
import ExpandMore from "@mui/icons-material/ExpandMore";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { Link } from "react-router-dom";
import { MenuGroup, MenuItem as MenuItemType } from "../routes/menuConfig";
import { NavTrail } from "../routes/navigation";

const EXPANDED_SIDEBAR_WIDTH = 250;
const COLLAPSED_SIDEBAR_WIDTH = 68;

// How long to keep a flyout open after the pointer leaves it, so moving the
// mouse from the rail icon into the flyout panel itself doesn't close it
// mid-transit.
const FLYOUT_CLOSE_DELAY_MS = 200;

interface SidebarProps {
  mobileOpen: boolean;
  onMobileClose: () => void;
  collapsed: boolean;
  onToggleCollapse: () => void;
  // Built once in MainLayout from the user's permissions and labs, shared
  // with the breadcrumb so both describe the same location.
  groups: MenuGroup[];
  activeTrail: NavTrail | null;
}

export function Sidebar({ mobileOpen, onMobileClose, collapsed, onToggleCollapse, groups, activeTrail }: SidebarProps) {
  const theme = useTheme();
  const chrome = theme.custom.chrome;
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));

  // Rail (icon-only) mode only ever applies on desktop - the mobile temporary
  // Drawer always renders the full labeled nav regardless of the persisted
  // desktop collapse preference, since a narrow icon rail makes no sense
  // inside a full-width touch drawer.
  const effectiveCollapsed = collapsed && !isMobile;

  const [openSubmenus, setOpenSubmenus] = useState<Record<string, boolean>>({});

  // Opening a deep link (or navigating from a notification) used to leave
  // the area holding the current page folded shut, so the active highlight
  // was invisible. Unfold it whenever the location moves into a new area;
  // areas the user opened or closed by hand are left as they are.
  const activeParentLabel = activeTrail?.parent?.label;
  useEffect(() => {
    if (!activeParentLabel) return;
    setOpenSubmenus((prev) => (prev[activeParentLabel] ? prev : { ...prev, [activeParentLabel]: true }));
  }, [activeParentLabel]);

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
    if (!item.children) return;
    if (effectiveCollapsed) {
      openFlyout(item.label, anchorEl);
    } else {
      toggleSubmenu(item.label);
    }
  };

  // Active = the menu link that owns the current location (see
  // findNavTrail), so query-string links and pages below a link (a run, a
  // record) highlight too; a parent is active when it holds that link.
  const isLinkActive = (item: MenuItemType): boolean => activeTrail?.item === item;
  const isItemActive = (item: MenuItemType): boolean =>
    isLinkActive(item) || (!!item.children && activeTrail?.parent === item);

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
                  fontSize: 11,
                  fontWeight: 600,
                  letterSpacing: "0.08em",
                  color: "rgba(255, 255, 255, 0.72)",
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
                    {...(hasChildren
                      ? {
                          onClick: (e: React.MouseEvent<HTMLElement>) => handleItemClick(item, e.currentTarget),
                          "aria-expanded": effectiveCollapsed ? flyoutOpen : isSubOpen,
                          "aria-haspopup": effectiveCollapsed ? true : undefined,
                          "aria-label": effectiveCollapsed ? item.label : undefined
                        }
                      : {
                          component: Link,
                          to: item.path!,
                          "aria-current": active ? "page" : undefined,
                          "aria-label": effectiveCollapsed ? item.label : undefined,
                          onClick: () => {
                            if (isMobile) onMobileClose();
                          }
                        })}
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
                        slotProps={{
                          // Wraps rather than truncating: "Physicochemical
                          // Configuration" clipped to "Physicochemical Conf…".
                          primary: {
                            sx: { fontSize: 13.5, fontWeight: active ? 600 : 500, lineHeight: 1.3 }
                          }
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
                            const childActive = isLinkActive(child);
                            return (
                              <ListItemButton
                                key={child.path}
                                component={Link}
                                to={child.path!}
                                aria-current={childActive ? "page" : undefined}
                                onClick={() => {
                                  if (isMobile) onMobileClose();
                                }}
                                sx={{
                                  minHeight: 34,
                                  py: 0.5,
                                  px: 1.75,
                                  my: 0.25,
                                  mr: 1,
                                  borderRadius: 1,
                                  bgcolor: childActive
                                    ? (theme.palette.mode === "dark" ? chrome.sidebarActiveBg : "rgba(255, 255, 255, 0.2)")
                                    : "transparent",
                                  color: childActive ? chrome.sidebarActiveText : "rgba(255, 255, 255, 0.85)",
                                  boxShadow: childActive ? `inset 3px 0 0 ${chrome.sidebarActiveBorder}` : "none",
                                  "&:hover": {
                                    bgcolor: "rgba(255, 255, 255, 0.1)",
                                    color: "#fff"
                                  }
                                }}
                              >
                                <ListItemText
                                  primary={child.label}
                                  slotProps={{
                                    primary: {
                                      sx: { fontSize: 13, fontWeight: childActive ? 600 : 400, lineHeight: 1.3 }
                                    }
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
                                const childActive = isLinkActive(child);
                                const ChildIcon = child.icon;
                                return (
                                  <MenuItem
                                    key={child.path}
                                    component={Link}
                                    to={child.path!}
                                    selected={childActive}
                                    aria-current={childActive ? "page" : undefined}
                                    onClick={() => {
                                      setFlyoutItem(null);
                                    }}
                                    sx={{ fontSize: 13 }}
                                  >
                                    {ChildIcon && (
                                      <ListItemIcon sx={{ minWidth: 30 }}>
                                        <ChildIcon fontSize="small" />
                                      </ListItemIcon>
                                    )}
                                    <ListItemText primary={child.label} slotProps={{
                                      primary: { sx: { fontSize: 13 } }
                                    }} />
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
            <IconButton
              onClick={onToggleCollapse}
              aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
              sx={{ color: "rgba(255, 255, 255, 0.8)", "&:hover": { color: "#fff" } }}
            >
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
          aria-label="Main navigation"
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
