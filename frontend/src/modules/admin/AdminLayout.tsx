import { useEffect } from "react";
import { Box, Tab, Tabs } from "@mui/material";
import { Link, Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { useDebouncedUiState } from "../../hooks/useDebouncedUiState";
import { useUiState } from "../../hooks/useUiState";
import { visibleAdminTabs } from "./adminTabs";
import { useTranslation } from "../../i18n";

const ADMIN_TAB_KEY = "nav.tab.admin";

// The /admin shell: one tab per administration page the user may open.
// Each page keeps its own header; the tabs only switch between them.
export function AdminLayout() {
  const { t } = useTranslation("admin");
  const { permissions } = useAuth();
  const { pathname } = useLocation();
  const tabs = visibleAdminTabs(permissions);
  const active = tabs.find((tab) => pathname === tab.path || pathname.startsWith(`${tab.path}/`));
  const { save } = useDebouncedUiState(ADMIN_TAB_KEY);
  const activePath = active?.path;

  // Remember the administration tab in use, whichever way the user got there.
  useEffect(() => {
    if (activePath) save(activePath);
  }, [activePath, save]);

  return (
    <>
      <Box sx={{ borderBottom: 1, borderColor: "divider", mb: 2 }}>
        <Tabs value={active?.path ?? false} variant="scrollable" scrollButtons="auto" aria-label={t("layout.tabsLabel")}>
          {tabs.map((tab) => (
            <Tab key={tab.path} value={tab.path} label={t(`tabs.${tab.path.split("/").pop()}`, { defaultValue: tab.label })} component={Link} to={tab.path} />
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
