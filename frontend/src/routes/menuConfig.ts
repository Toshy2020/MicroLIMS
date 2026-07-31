import { Role } from "../modules/authentication/types/authTypes";

export interface MenuItem {
  label: string;
  path?: string;
  children?: MenuItem[];
}

// Every module's nav entry, in one place. Sidebar (and eventually any
// other nav surface) reads this rather than hardcoding role checks -
// DashboardLayout -> Role -> menuConfig.ts -> Visible Modules.
const dashboardItem: MenuItem = { label: "Dashboard", path: "/dashboard" };
const receivingItem: MenuItem = { label: "Sample Receiving", path: "/receiving" };
const testingWorkspaceItem: MenuItem = { label: "Testing Workspace", path: "/testing-workspace" };
const emPrepItem: MenuItem = { label: "EM Preparation", path: "/em-preparation" };
const acPrepItem: MenuItem = { label: "After Cleaning Prep", path: "/aftercleaning-preparation" };
const testPrepItem: MenuItem = { label: "Test Preparation", path: "/test-preparation" };
const reviewItem: MenuItem = { label: "Review", path: "/review" };
const approvalItem: MenuItem = { label: "Approval", path: "/approval" };
const reportsItem: MenuItem = { label: "Reports", path: "/reports" };
const auditSearchItem: MenuItem = { label: "Audit Search", path: "/audit-search" };

const inventoryItem: MenuItem = {
  label: "Inventory",
  children: [
    { label: "Materials Stock", path: "/inventory/materials" },
    { label: "Equipment", path: "/inventory/equipment" },
    { label: "Approved Media List", path: "/inventory/approved-media" },
    { label: "Approved Cryovial List", path: "/inventory/approved-cryovials" }
  ]
};

// One collapsible menu instead of many separate top-level items.
const laboratoryConfigurationItem: MenuItem = {
  label: "Laboratory Configuration",
  children: [
    { label: "Items", path: "/laboratory-configuration/items" },
    { label: "Specifications", path: "/laboratory-configuration/specifications" },
    { label: "Media Types", path: "/laboratory-configuration/media-types" },
    { label: "Media Preparation", path: "/laboratory-configuration/media" },
    { label: "GPT", path: "/laboratory-configuration/gpt" },
    { label: "Reference Strains", path: "/laboratory-configuration/reference-strains" },
    { label: "Cryovials", path: "/laboratory-configuration/cryovials" },
    { label: "Water", path: "/laboratory-configuration/water" },
    { label: "Environmental Monitoring", path: "/laboratory-configuration/environmental-monitoring" },
    { label: "After Cleaning", path: "/laboratory-configuration/after-cleaning" },
    { label: "Cause of Testing", path: "/laboratory-configuration/cause-of-testing" },
    { label: "Diluents & Neutralizers", path: "/laboratory-configuration/diluents" },
    { label: "Equipment", path: "/laboratory-configuration/equipment" }
  ]
};

const usersItem: MenuItem = { label: "Users", path: "/users" };
const rolesItem: MenuItem = { label: "Roles", path: "/roles" };

const menuByRole: Record<Role, MenuItem[]> = {
  Analyst: [dashboardItem, receivingItem, testingWorkspaceItem, emPrepItem, acPrepItem, testPrepItem, inventoryItem, reportsItem],
  Reviewer: [dashboardItem, testingWorkspaceItem, reviewItem, reportsItem],
  SectionHead: [
    dashboardItem, receivingItem, testingWorkspaceItem, emPrepItem, acPrepItem, testPrepItem,
    reviewItem, approvalItem, laboratoryConfigurationItem, inventoryItem, reportsItem, auditSearchItem
  ],
  SystemAdministrator: [
    dashboardItem, receivingItem, testingWorkspaceItem, emPrepItem, acPrepItem, testPrepItem,
    reviewItem, approvalItem, laboratoryConfigurationItem, inventoryItem, usersItem, rolesItem, reportsItem, auditSearchItem
  ]
};

export function getMenuForRole(role: Role | null): MenuItem[] {
  if (!role) return [dashboardItem];
  return menuByRole[role] ?? [dashboardItem];
}
