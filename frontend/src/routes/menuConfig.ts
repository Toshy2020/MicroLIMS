import { ComponentType } from "react";
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

export interface MenuItem {
  label: string;
  path?: string;
  icon?: ComponentType<{ fontSize?: "small" | "inherit" | "medium" | "large"; sx?: any }>;
  group?: string;
  children?: MenuItem[];
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
const auditSearchItem: MenuItem = { label: "Audit Search", path: "/audit-search", icon: SearchOutlinedIcon, group: "AUDIT & COMPLIANCE" };
const oosTrackingItem: MenuItem = { label: "OOS Tracking", path: "/oos-tracking", icon: ReportProblemOutlinedIcon, group: "AUDIT & COMPLIANCE" };
// Listed for System Administrators, but the route itself is gated on the
// System.ViewErrorLog permission - a role granted that code reaches the
// page directly even though the menu does not offer it.
const errorMonitoringItem: MenuItem = { label: "Error Monitoring", path: "/error-monitoring", icon: BugReportOutlinedIcon, group: "SYSTEM" };

// Receiving area: the main receiving desk and the cross-lab tracking board,
// gated on the Samples.Receive / Samples.TrackAll privileges rather than
// role or lab membership - any lab may receive here (ReceiptLabGuard).
const receivingAreaItems: MenuItem[] = [
  { label: "Receive Sample", path: "/receiving", icon: ScienceOutlinedIcon, group: "RECEIVING" },
  { label: "Tracking Board", path: "/receiving/tracking", icon: FactCheckOutlinedIcon, group: "RECEIVING" }
];

const usersItem: MenuItem = { label: "Users", path: "/users", icon: PeopleAltOutlinedIcon, group: "ADMINISTRATION" };
const rolesItem: MenuItem = { label: "Roles", path: "/roles", icon: AdminPanelSettingsOutlinedIcon, group: "ADMINISTRATION" };

const documentControlUserItem: MenuItem = {
  label: "Document Control",
  icon: DescriptionOutlinedIcon,
  group: "DOCUMENT CONTROL",
  children: [
    { label: "Dashboard", path: "/document-control" },
    { label: "My Reading List", path: "/document-control/my-reading-list" },
    { label: "Training Matrix", path: "/document-control/training-matrix" },
    { label: "Compliance Dashboard", path: "/document-control/compliance-dashboard" },
    { label: "Document Library", path: "/document-control/library" }
  ]
};

const documentControlAuditorItem: MenuItem = {
  label: "Document Control",
  icon: DescriptionOutlinedIcon,
  group: "DOCUMENT CONTROL",
  children: [
    { label: "Dashboard", path: "/document-control" },
    { label: "My Reading List", path: "/document-control/my-reading-list" },
    { label: "Training Matrix", path: "/document-control/training-matrix" },
    { label: "Compliance Dashboard", path: "/document-control/compliance-dashboard" },
    { label: "Document Library", path: "/document-control/library" },
    { label: "Audit Trail", path: "/document-control/audit" }
  ]
};

const documentControlAdminItem: MenuItem = {
  label: "Document Control",
  icon: DescriptionOutlinedIcon,
  group: "DOCUMENT CONTROL",
  children: [
    { label: "Dashboard", path: "/document-control" },
    { label: "My Reading List", path: "/document-control/my-reading-list" },
    { label: "Training Matrix", path: "/document-control/training-matrix" },
    { label: "Compliance Dashboard", path: "/document-control/compliance-dashboard" },
    { label: "Document Library", path: "/document-control/library" },
    { label: "Audit Trail", path: "/document-control/audit" },
    { label: "Configuration", path: "/document-control/configuration" }
  ]
};

// Materials Stock, Equipment Inventory and the two approved lists all sit
// behind InventoryRoutes.tsx's role gate (Analyst/SectionHead/SystemAdministrator
// only - "Reviewer does not need it") and the matching [Authorize] on
// MaterialController/EquipmentInventoryController. The menu must mirror that
// gate exactly, or a Reviewer lab member gets a link that bounces them
// straight back to /dashboard.
function canUseInventory(role: Role | null): boolean {
  return role === "Analyst" || role === "SectionHead" || role === "SystemAdministrator";
}

const materialsStockMicroItem: MenuItem = { label: "Materials Stock", path: "/inventory/materials?lab=MICRO" };
const equipmentInventoryMicroItem: MenuItem = { label: "Equipment Inventory", path: "/inventory/equipment?lab=MICRO" };
const approvedMediaListItem: MenuItem = { label: "Approved Media List", path: "/inventory/approved-media" };
const approvedCryovialListItem: MenuItem = { label: "Approved Cryovial List", path: "/inventory/approved-cryovials" };
const materialsStockFpItem: MenuItem = { label: "Materials Stock", path: "/inventory/materials?lab=FP" };
const equipmentInventoryFpItem: MenuItem = { label: "Equipment Inventory", path: "/inventory/equipment?lab=FP" };

// Identifies the inventory-gated children above by reference, so filtering
// them out doesn't depend on matching their label text.
const INVENTORY_GUARDED_ITEMS = new Set<MenuItem>([
  materialsStockMicroItem, equipmentInventoryMicroItem, approvedMediaListItem, approvedCryovialListItem,
  materialsStockFpItem, equipmentInventoryFpItem
]);

// Returns the lab area with its inventory-gated children stripped for a
// role InventoryRoutes.tsx would bounce (e.g. Reviewer).
function labAreaFor(area: MenuItem, role: Role | null): MenuItem {
  if (canUseInventory(role)) return area;
  return { ...area, children: area.children?.filter((c) => !INVENTORY_GUARDED_ITEMS.has(c)) };
}

// Laboratory areas: one collapsible section per laboratory, shown only to
// its members (useMyLabs). Each bundles that lab's workspace (built in
// Tasks 12-13), its analyst-facing configuration pages, and its own
// materials/equipment inventory. Water/EM/after-cleaning configuration
// stays inside the Microbiology configuration group, not split out.
const microArea: MenuItem = {
  label: "Microbiology Laboratory",
  icon: BiotechOutlinedIcon,
  group: "LABORATORIES",
  children: [
    { label: "Workspace", path: "/microbiology/workspace" },
    { label: "Media Preparation & Evaluation", path: "/laboratory-configuration/media" },
    { label: "Reference Cryovials", path: "/laboratory-configuration/cryovials" },
    materialsStockMicroItem,
    equipmentInventoryMicroItem,
    approvedMediaListItem,
    approvedCryovialListItem
  ]
};

// Section Head / System Administrator only.
const microConfigArea: MenuItem = {
  label: "Microbiology Configuration",
  icon: BiotechOutlinedIcon,
  group: "LABORATORIES",
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
    { label: "System Suitability (HPLC)", path: "/laboratory/system-suitability" },
    { label: "Calibration Runs (ICP-OES / AAS)", path: "/laboratory/calibration-runs" },
    materialsStockFpItem,
    equipmentInventoryFpItem
  ]
};

// Section Head / System Administrator only.
const physchemConfigArea: MenuItem = {
  label: "Physicochemical Configuration",
  icon: MedicationOutlinedIcon,
  group: "LABORATORIES",
  children: [
    { label: "Physicochemical Test Master", path: "/laboratory-configuration/fp-test-master" },
    { label: "Equation Types", path: "/laboratory-configuration/equation-types" },
    { label: "Physicochemical Instruments", path: "/laboratory-configuration/fp-instruments" },
    { label: "Chromatography Columns", path: "/laboratory-configuration/columns" }
  ]
};

// Shared across both labs, not tied to either one's membership - Section
// Head / System Administrator only.
const generalConfigArea: MenuItem[] = [
  { label: "Items", path: "/laboratory-configuration/items", icon: Inventory2OutlinedIcon, group: "GENERAL LABORATORY CONFIGURATION" },
  { label: "Receiving Configuration", path: "/laboratory-configuration/receiving-configuration", icon: FactCheckOutlinedIcon, group: "GENERAL LABORATORY CONFIGURATION" }
];

// The items unaffected by lab separation - exactly today's per-role lists,
// minus the ones that moved into the receiving area or the lab areas above
// (Receiving & Testing, media/cryovial workspaces, suitability/calibration
// runs, inventory, laboratory configuration).
function sharedItemsFor(role: Role | null): MenuItem[] {
  switch (role) {
    case "Analyst":
      return [documentControlUserItem, reportsItem];
    case "Reviewer":
      return [documentControlAuditorItem, reportsItem];
    case "SectionHead":
      return [documentControlAuditorItem, reportsItem, auditSearchItem, oosTrackingItem];
    case "SystemAdministrator":
      return [documentControlAdminItem, usersItem, rolesItem, reportsItem, auditSearchItem, oosTrackingItem, errorMonitoringItem];
    default:
      return [];
  }
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

// Builds the sidebar menu from privilege and lab membership rather than
// role alone: the receiving area needs Samples.Receive/Samples.TrackAll,
// each laboratory area needs membership in that lab (useMyLabs), and each
// lab's configuration plus the general configuration group are additionally
// restricted to Section Head / System Administrator.
export function getGroupedMenu({ role, permissions, labCodes }: MenuContext): MenuGroup[] {
  const isHead = role === "SectionHead" || role === "SystemAdministrator";
  const items: MenuItem[] = [dashboardItem];
  if (permissions.includes("Samples.Receive")) items.push(receivingAreaItems[0]);
  if (permissions.includes("Samples.TrackAll")) items.push(receivingAreaItems[1]);
  if (labCodes.includes("MICRO")) items.push(labAreaFor(microArea, role), ...(isHead ? [microConfigArea] : []));
  if (labCodes.includes("FP")) items.push(labAreaFor(physchemArea, role), ...(isHead ? [physchemConfigArea] : []));
  if (isHead) items.push(...generalConfigArea);
  items.push(...sharedItemsFor(role));
  return groupItems(items);
}
