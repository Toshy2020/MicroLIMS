import { PERMISSIONS } from "../../routes/routes";

export interface AdminTab {
  label: string;
  path: string;
  // The permission the tab's page and endpoints check.
  permission: string;
}

// The tabs of the /admin shell (admin panel report section 9) that exist
// today. The panel controls the system, never GxP data, so every tab is
// guarded by its own permission and there are no lab-data tabs.
export const ADMIN_TABS: AdminTab[] = [
  { label: "Users", path: "/admin/users", permission: PERMISSIONS.USERS_MANAGE },
  { label: "Roles", path: "/admin/roles", permission: PERMISSIONS.ROLES_MANAGE },
  { label: "Audit Trail", path: "/admin/audit", permission: PERMISSIONS.AUDIT_VIEW },
  { label: "Error Log", path: "/admin/error-log", permission: PERMISSIONS.SYSTEM_VIEW_ERROR_LOG },
  { label: "Overview", path: "/admin/overview", permission: PERMISSIONS.SYSTEM_VIEW_ERROR_LOG },
  // The endpoint is Users.Manage (policies cannot express "or Roles.Manage").
  { label: "Security Policy", path: "/admin/security-policy", permission: PERMISSIONS.USERS_MANAGE }
];

export function visibleAdminTabs(permissions: string[]): AdminTab[] {
  return ADMIN_TABS.filter((t) => permissions.includes(t.permission));
}
