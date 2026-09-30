import { ComponentType } from "react";
import { SxProps, Theme } from "@mui/material";
import SpaceDashboardOutlinedIcon from "@mui/icons-material/SpaceDashboardOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import SearchOutlinedIcon from "@mui/icons-material/SearchOutlined";
import ReportProblemOutlinedIcon from "@mui/icons-material/ReportProblemOutlined";
import BugReportOutlinedIcon from "@mui/icons-material/BugReportOutlined";
import PeopleAltOutlinedIcon from "@mui/icons-material/PeopleAltOutlined";
import AdminPanelSettingsOutlinedIcon from "@mui/icons-material/AdminPanelSettingsOutlined";
import BiotechOutlinedIcon from "@mui/icons-material/BiotechOutlined";
import MedicationOutlinedIcon from "@mui/icons-material/MedicationOutlined";
import { Role } from "../modules/authentication/types/authTypes";
import { PERMISSIONS } from "./routes";

export interface MenuItem {
  label: string;
  path?: string;
  icon?: ComponentType<{ fontSize?: "small" | "inherit" | "medium" | "large"; sx?: SxProps<Theme> }>;
  group?: string;
  children?: MenuItem[];
  // Shown only to users holding this permission code (or any of the codes if an array) - the same code the
  // page's route guard and its main endpoint check, so a link never leads
  // to a page that bounces the user or answers 403.
  permission?: string | string[];
}

export interface MenuGroup {
  groupName: string;
  items: MenuItem[];
}

export interface MenuContext {
  role: Role | null;
  permissions: string[];
  labCodes: string[];
}

// Menu Items
const dashboardItem: MenuItem = { label: "Dashboard", path: "/dashboard", icon: SpaceDashboardOutlinedIcon, group: "OVERVIEW" };
const reportsItem: MenuItem = { label: "Reports", path: "/reports", icon: DescriptionOutlinedIcon, group: "REPORTS" };
const auditSearchItem: MenuItem = { label: "Audit Search", path: "/audit-search", icon: SearchOutlinedIcon, group: "AUDIT & COMPLIANCE", permission: PERMISSIONS.AUDIT_VIEW };
const oosTrackingItem: MenuItem = { label: "OOS Tracking", path: "/oos-tracking", icon: ReportProblemOutlinedIcon, group: "AUDIT & COMPLIANCE", permission: PERMISSIONS.OOS_MANAGE };
const errorMonitoringItem: MenuItem = { label: "Error Monitoring", path: "/error-monitoring", icon: BugReportOutlinedIcon, group: "SYSTEM", permission: PERMISSIONS.SYSTEM_VIEW_ERROR_LOG };

// Receiving area: the main receiving desk and the cross-lab tracking board,
// gated on privileges rather than role or lab membership - any lab may
// receive here (ReceiptLabGuard).
const receiveSampleItem: MenuItem = {
  label: "Receive Sample",
  path: "/receiving",
  icon: ScienceOutlinedIcon,
  group: "RECEIVING",
  permission: [PERMISSIONS.SAMPLES_RECEIVE, PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB]
};
const trackingBoardItem: MenuItem = { label: "Tracking Board", path: "/receiving/tracking", icon: FactCheckOutlinedIcon, group: "RECEIVING", permission: PERMISSIONS.SAMPLES_TRACK_ALL };

const usersItem: MenuItem = { label: "Users", path: "/users", icon: PeopleAltOutlinedIcon, group: "ADMINISTRATION", permission: PERMISSIONS.USERS_MANAGE };
const rolesItem: MenuItem = { label: "Roles", path: "/roles", icon: AdminPanelSettingsOutlinedIcon, group: "ADMINISTRATION", permission: PERMISSIONS.ROLES_MANAGE };

// The global document audit trail stays a fixed role rule on the server
// (DocumentAuthorizationService.CanQueryGlobalAuditAsync), so its link
// follows the role rather than a permission code.
function canQueryDocumentAudit(role: Role | null): boolean {
  return role === "Reviewer" || role === "SectionHead" || role === "SystemAdministrator";
}

function documentControlItem(role: Role | null): MenuItem {
  return {
    label: "Document Control",
    icon: DescriptionOutlinedIcon,
    group: "DOCUMENT CONTROL",
    children: [
      { label: "Dashboard", path: "/document-control" },
      { label: "My Reading List", path: "/document-control/my-reading-list" },
      { label: "Training Matrix", path: "/document-control/training-matrix" },
      { label: "Compliance Dashboard", path: "/document-control/compliance-dashboard" },
      { label: "Document Library", path: "/document-control/library" },
      ...(canQueryDocumentAudit(role) ? [{ label: "Audit Trail", path: "/document-control/audit" }] : []),
      { label: "Configuration", path: "/document-control/configuration", permission: PERMISSIONS.DOCUMENTS_CONFIG_MANAGE }
    ]
  };
}

// Laboratory areas: one collapsible section per laboratory, shown only to
// its members (useMyLabs). Each bundles that lab's workspace, its
// analyst-facing pages and its own materials/equipment inventory; each
// page link also needs the permission its endpoints check.
const microArea: MenuItem = {
  label: "Microbiology Laboratory",
  icon: BiotechOutlinedIcon,
  group: "LABORATORIES",
  children: [
    { label: "Workspace", path: "/microbiology/workspace" },
    { label: "Media Preparation & Evaluation", path: "/laboratory-configuration/media", permission: PERMISSIONS.MEDIA_PREPARE },
    { label: "Reference Cryovials", path: "/laboratory-configuration/cryovials", permission: PERMISSIONS.CRYOVIALS_MANAGE },
    { label: "Materials Stock", path: "/inventory/materials?lab=MICRO", permission: PERMISSIONS.MATERIALS_MANAGE },
    { label: "Equipment Inventory", path: "/inventory/equipment?lab=MICRO", permission: PERMISSIONS.EQUIPMENT_MANAGE },
    { label: "Approved Media List", path: "/inventory/approved-media", permission: PERMISSIONS.MEDIA_PREPARE },
    { label: "Approved Cryovial List", path: "/inventory/approved-cryovials", permission: PERMISSIONS.CRYOVIALS_MANAGE }
  ]
};

const microConfigArea: MenuItem = {
  label: "Microbiology Configuration",
  icon: BiotechOutlinedIcon,
  group: "LABORATORIES",
  permission: PERMISSIONS.MASTER_DATA_MANAGE,
  children: [
    { label: "Test Master", path: "/laboratory-configuration/test-master" },
    { label: "Organisms", path: "/laboratory-configuration/organisms" },
    { label: "Media Configurations", path: "/laboratory-configuration/media-configurations" },
    { label: "Water", path: "/laboratory-configuration/water" },
    { label: "Environmental Monitoring", path: "/laboratory-configuration/environmental-monitoring" },
    { label: "After Cleaning", path: "/laboratory-configuration/after-cleaning" },
    { label: "Equipment", path: "/laboratory-configuration/equipment" }
  ]
};

const physchemArea: MenuItem = {
  label: "Physicochemical Laboratory",
  icon: MedicationOutlinedIcon,
  group: "LABORATORIES",
  children: [
    { label: "Workspace", path: "/physicochemical/workspace" },
    { label: "System Suitability (HPLC)", path: "/laboratory/system-suitability", permission: PERMISSIONS.TEST_WORKFLOW_EXECUTE },
    { label: "Calibration Runs (ICP-OES / AAS)", path: "/laboratory/calibration-runs", permission: PERMISSIONS.TEST_WORKFLOW_EXECUTE },
    { label: "Solution Preparation", path: "/preparation", permission: PERMISSIONS.SOLUTIONS_PREPARE },
    { label: "HPLC Workspace", path: "/hplc-workspace" },
    { label: "Materials Stock", path: "/inventory/materials?lab=FP", permission: PERMISSIONS.MATERIALS_MANAGE },
    { label: "Equipment Inventory", path: "/inventory/equipment?lab=FP", permission: PERMISSIONS.EQUIPMENT_MANAGE }
  ]
};

const physchemConfigArea: MenuItem = {
  label: "Physicochemical Configuration",
  icon: MedicationOutlinedIcon,
  group: "LABORATORIES",
  permission: PERMISSIONS.MASTER_DATA_MANAGE,
  children: [
    { label: "Physicochemical Test Master", path: "/laboratory-configuration/fp-test-master" },
    { label: "Equation Types", path: "/laboratory-configuration/equation-types" },
    { label: "Physicochemical Instruments", path: "/laboratory-configuration/fp-instruments" },
    { label: "Chromatography Columns", path: "/laboratory-configuration/columns" },
    { label: "Reagents & Standards", path: "/laboratory-configuration/material-master" },
    { label: "Solutions", path: "/laboratory-configuration/solution-master" },
    { label: "HPLC Methods", path: "/laboratory-configuration/hplc-methods" }
  ]
};

// Shared across both labs, not tied to either one's membership.
const itemsItem: MenuItem = { label: "Items", path: "/laboratory-configuration/items", icon: Inventory2OutlinedIcon, group: "GENERAL LABORATORY CONFIGURATION", permission: PERMISSIONS.ITEMS_MANAGE };
const receivingConfigItem: MenuItem = { label: "Receiving Configuration", path: "/laboratory-configuration/receiving-configuration", icon: FactCheckOutlinedIcon, group: "GENERAL LABORATORY CONFIGURATION", permission: PERMISSIONS.MASTER_DATA_MANAGE };

// Drops every item whose permission the user lacks, then any parent left
// with no children.
function visibleItems(items: MenuItem[], permissions: string[]): MenuItem[] {
  const result: MenuItem[] = [];
  for (const item of items) {
    if (item.permission) {
      const allowed = Array.isArray(item.permission)
        ? item.permission.some((p) => permissions.includes(p))
        : permissions.includes(item.permission);
      if (!allowed) continue;
    }
    if (item.children) {
      const children = visibleItems(item.children, permissions);
      if (children.length === 0) continue;
      result.push({ ...item, children });
    } else {
      result.push(item);
    }
  }
  return result;
}

function groupItems(items: MenuItem[]): MenuGroup[] {
  const groups: Record<string, MenuItem[]> = {};
  for (const item of items) {
    const g = item.group || "OTHER";
    if (!groups[g]) groups[g] = [];
    groups[g].push(item);
  }
  return Object.entries(groups).map(([groupName, groupItems]) => ({
    groupName,
    items: groupItems
  }));
}

// Builds the sidebar from the user's permissions and lab membership: each
// link needs the permission its page's route guard checks, and each
// laboratory area also needs membership in that lab (useMyLabs). The role
// only decides the document audit link, which the server still restricts
// by role.
export function getGroupedMenu({ role, permissions, labCodes }: MenuContext): MenuGroup[] {
  const items: MenuItem[] = [dashboardItem, receiveSampleItem, trackingBoardItem];
  if (labCodes.includes("MICRO")) items.push(microArea, microConfigArea);
  if (labCodes.includes("FP")) items.push(physchemArea, physchemConfigArea);
  items.push(
    itemsItem, receivingConfigItem,
    documentControlItem(role), usersItem, rolesItem, reportsItem, auditSearchItem, oosTrackingItem, errorMonitoringItem
  );
  return groupItems(visibleItems(items, permissions));
}
