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
const mediaPreparationItem: MenuItem = { label: "Media Preparation", path: "/laboratory-configuration/media" };
const mediaEvaluationItem: MenuItem = { label: "Media Evaluation", path: "/laboratory-configuration/media-evaluation" };
const cryovialsItem: MenuItem = { label: "Cryovials", path: "/laboratory-configuration/cryovials" };
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

// One collapsible menu instead of many separate top-level items. Only
// the admin/master-data pages live here - Media Preparation, Media
// Evaluation, and Cryovials are day-to-day workflow pages like Sample
// Receiving/Testing Workspace, so they're standalone items available to
// every role instead of nested under this Section-Head-only menu.
const laboratoryConfigurationItem: MenuItem = {
  label: "Laboratory Configuration",
  children: [
    { label: "Test Master", path: "/laboratory-configuration/test-master" },
    { label: "Organisms", path: "/laboratory-configuration/organisms" },
    { label: "Items", path: "/laboratory-configuration/items" },
    { label: "Specifications", path: "/laboratory-configuration/specifications" },
    { label: "Media Types", path: "/laboratory-configuration/media-types" },
    { label: "Media Challenge Specs", path: "/laboratory-configuration/media-challenge-specs" },
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

// Sample Receiving, Testing Workspace, Media Preparation, Media
// Evaluation, and Cryovials are shop-floor workflow pages available to
// every role, regardless of what else that role can reach.
const sharedWorkflowItems: MenuItem[] = [receivingItem, testingWorkspaceItem, mediaPreparationItem, mediaEvaluationItem, cryovialsItem];

const menuByRole: Record<Role, MenuItem[]> = {
  Analyst: [dashboardItem, ...sharedWorkflowItems, inventoryItem, reportsItem],
  // Reviewer's "Under Review" work happens by clicking a badge in the
  // Testing Workspace now - no separate Review menu item.
  Reviewer: [dashboardItem, ...sharedWorkflowItems, reportsItem],
  SectionHead: [
    dashboardItem, ...sharedWorkflowItems,
    laboratoryConfigurationItem, inventoryItem, reportsItem, auditSearchItem
  ],
  SystemAdministrator: [
    dashboardItem, ...sharedWorkflowItems,
    laboratoryConfigurationItem, inventoryItem, usersItem, rolesItem, reportsItem, auditSearchItem
  ]
};

export function getMenuForRole(role: Role | null): MenuItem[] {
  if (!role) return [dashboardItem];
  return menuByRole[role] ?? [dashboardItem];
}
