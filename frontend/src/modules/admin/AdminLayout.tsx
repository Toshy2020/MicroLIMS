import { useEffect } from "react";
import { Box, Tab, Tabs } from "@mui/material";
import { Link, Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { useDebouncedUiState } from "../../hooks/useDebouncedUiState";
import { useUiState } from "../../hooks/useUiState";
import { visibleAdminTabs } from "./adminTabs";

const ADMIN_TAB_KEY = "nav.tab.admin";

// The /admin shell: one tab per administration page the user may open.
// Each page keeps its own header; the tabs only switch between them.
export function AdminLayout() {
  const { permissions } = useAuth();
  const { pathname } = useLocation();
  const tabs = visibleAdminTabs(permissions);
  const active = tabs.find((t) => pathname === t.path || pathname.startsWith(`${t.path}/`));
  const { save } = useDebouncedUiState(ADMIN_TAB_KEY);
  const activePath = active?.path;

  // Remember the administration tab in use, whichever way the user got there.
  useEffect(() => {
    if (activePath) save(activePath);
  }, [activePath, save]);

  return (
    <>
      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}>
        <Tabs value={active?.path ?? false} variant="scrollable" scrollButtons="auto" aria-label="Administration sections">
          {tabs.map((t) => (
            <Tab key={t.path} value={t.path} label={t.label} component={Link} to={t.path} />
          ))}
        </Tabs>
      </Box>
      <Outlet />
    </>
  );
}

// /admin itself lands on the tab used last time when the user may still open
// it, otherwise on the first tab the user may open.
export function AdminIndexRedirect() {
  const { permissions } = useAuth();
  const { value, loaded } = useUiState(ADMIN_TAB_KEY);
  const tabs = visibleAdminTabs(permissions);
  if (tabs.length > 0 && !loaded) return null;
  const target = tabs.find((t) => t.path === value) ?? tabs[0];
  return <Navigate to={target ? target.path : "/dashboard"} replace />;
}
