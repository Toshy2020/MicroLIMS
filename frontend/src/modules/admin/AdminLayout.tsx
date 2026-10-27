import { Box, Tab, Tabs } from "@mui/material";
import { Link, Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { visibleAdminTabs } from "./adminTabs";

// The /admin shell: one tab per administration page the user may open.
// Each page keeps its own header; the tabs only switch between them.
export function AdminLayout() {
  const { permissions } = useAuth();
  const { pathname } = useLocation();
  const tabs = visibleAdminTabs(permissions);
  const active = tabs.find((t) => pathname === t.path || pathname.startsWith(`${t.path}/`));

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

// /admin itself lands on the first tab the user may open.
export function AdminIndexRedirect() {
  const { permissions } = useAuth();
  const first = visibleAdminTabs(permissions)[0];
  return <Navigate to={first ? first.path : "/dashboard"} replace />;
}
