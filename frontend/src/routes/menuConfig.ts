import { ComponentType } from "react";
import SpaceDashboardOutlinedIcon from "@mui/icons-material/SpaceDashboardOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import MoveToInboxOutlinedIcon from "@mui/icons-material/MoveToInboxOutlined";
import MedicationLiquidOutlinedIcon from "@mui/icons-material/MedicationLiquidOutlined";
import AcUnitOutlinedIcon from "@mui/icons-material/AcUnitOutlined";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import PrecisionManufacturingOutlinedIcon from "@mui/icons-material/PrecisionManufacturingOutlined";
import RuleOutlinedIcon from "@mui/icons-material/RuleOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import SearchOutlinedIcon from "@mui/icons-material/SearchOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import PeopleAltOutlinedIcon from "@mui/icons-material/PeopleAltOutlined";
import AdminPanelSettingsOutlinedIcon from "@mui/icons-material/AdminPanelSettingsOutlined";
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

// Menu Items
const dashboardItem: MenuItem = { label: "Dashboard", path: "/dashboard", icon: SpaceDashboardOutlinedIcon, group: "OVERVIEW" };
const testingWorkspaceItem: MenuItem = { label: "Testing Workspace", path: "/testing-workspace", icon: ScienceOutlinedIcon, group: "MY WORK" };
const mediaWorkspaceItem: MenuItem = { label: "Media Preparation & Evaluation", path: "/laboratory-configuration/media", icon: MedicationLiquidOutlinedIcon, group: "LABORATORY" };
const cryovialsItem: MenuItem = { label: "Reference Cryovials", path: "/laboratory-configuration/cryovials", icon: AcUnitOutlinedIcon, group: "LABORATORY" };
const receivingItem: MenuItem = { label: "Sample Receiving", path: "/receiving", icon: MoveToInboxOutlinedIcon, group: "OPERATIONS" };
const reportsItem: MenuItem = { label: "Reports", path: "/reports", icon: DescriptionOutlinedIcon, group: "REPORTS" };
const auditSearchItem: MenuItem = { label: "Audit Search", path: "/audit-search", icon: SearchOutlinedIcon, group: "AUDIT & COMPLIANCE" };

const inventoryItem: MenuItem = {
  label: "Inventory",
  icon: Inventory2OutlinedIcon,
  group: "INVENTORY",
  children: [
    { label: "Materials Stock", path: "/inventory/materials", icon: Inventory2OutlinedIcon },
    { label: "Equipment Master", path: "/inventory/equipment", icon: PrecisionManufacturingOutlinedIcon },
    { label: "Approved Media List", path: "/inventory/approved-media", icon: RuleOutlinedIcon },
    { label: "Approved Cryovial List", path: "/inventory/approved-cryovials", icon: FactCheckOutlinedIcon }
  ]
};

const laboratoryConfigurationItem: MenuItem = {
  label: "Laboratory Configuration",
  icon: SettingsOutlinedIcon,
  group: "LAB CONFIGURATION",
  children: [
    { label: "Test Master", path: "/laboratory-configuration/test-master" },
    { label: "Organisms", path: "/laboratory-configuration/organisms" },
    { label: "Items", path: "/laboratory-configuration/items" },
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

const usersItem: MenuItem = { label: "Users", path: "/users", icon: PeopleAltOutlinedIcon, group: "ADMINISTRATION" };
const rolesItem: MenuItem = { label: "Roles", path: "/roles", icon: AdminPanelSettingsOutlinedIcon, group: "ADMINISTRATION" };

const menuByRole: Record<Role, MenuItem[]> = {
  Analyst: [
    dashboardItem,
    testingWorkspaceItem,
    mediaWorkspaceItem,
    cryovialsItem,
    inventoryItem,
    receivingItem,
    reportsItem
  ],
  Reviewer: [
    dashboardItem,
    testingWorkspaceItem,
    mediaWorkspaceItem,
    cryovialsItem,
    receivingItem,
    reportsItem
  ],
  SectionHead: [
    dashboardItem,
    testingWorkspaceItem,
    mediaWorkspaceItem,
    cryovialsItem,
    receivingItem,
    inventoryItem,
    laboratoryConfigurationItem,
    reportsItem,
    auditSearchItem
  ],
  SystemAdministrator: [
    dashboardItem,
    testingWorkspaceItem,
    mediaWorkspaceItem,
    cryovialsItem,
    receivingItem,
    inventoryItem,
    laboratoryConfigurationItem,
    usersItem,
    rolesItem,
    reportsItem,
    auditSearchItem
  ]
};

export const getMenuForRole = (role: Role | null): MenuItem[] => (role ? menuByRole[role] ?? [] : []);
export const getMenuItems = getMenuForRole;

export function getGroupedMenuForRole(role: Role | null): MenuGroup[] {
  const items = getMenuForRole(role);
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
