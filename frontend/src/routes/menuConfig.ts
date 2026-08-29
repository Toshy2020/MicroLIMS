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
import ReportProblemOutlinedIcon from "@mui/icons-material/ReportProblemOutlined";
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
const receivingTestingItem: MenuItem = { label: "Receiving & Testing", path: "/receiving-testing", icon: ScienceOutlinedIcon, group: "MY WORK" };
const mediaWorkspaceItem: MenuItem = { label: "Media Preparation & Evaluation", path: "/laboratory-configuration/media", icon: MedicationLiquidOutlinedIcon, group: "LABORATORY" };
const cryovialsItem: MenuItem = { label: "Reference Cryovials", path: "/laboratory-configuration/cryovials", icon: AcUnitOutlinedIcon, group: "LABORATORY" };
const reportsItem: MenuItem = { label: "Reports", path: "/reports", icon: DescriptionOutlinedIcon, group: "REPORTS" };
const auditSearchItem: MenuItem = { label: "Audit Search", path: "/audit-search", icon: SearchOutlinedIcon, group: "AUDIT & COMPLIANCE" };
const oosTrackingItem: MenuItem = { label: "OOS Tracking", path: "/oos-tracking", icon: ReportProblemOutlinedIcon, group: "AUDIT & COMPLIANCE" };

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
    { label: "Media Configurations", path: "/laboratory-configuration/media-configurations" },
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
    receivingTestingItem,
    mediaWorkspaceItem,
    cryovialsItem,
    inventoryItem,
    reportsItem
  ],
  Reviewer: [
    dashboardItem,
    receivingTestingItem,
    mediaWorkspaceItem,
    cryovialsItem,
    reportsItem
  ],
  SectionHead: [
    dashboardItem,
    receivingTestingItem,
    mediaWorkspaceItem,
    cryovialsItem,
    inventoryItem,
    laboratoryConfigurationItem,
    reportsItem,
    auditSearchItem,
    oosTrackingItem
  ],
  SystemAdministrator: [
    dashboardItem,
    receivingTestingItem,
    mediaWorkspaceItem,
    cryovialsItem,
    inventoryItem,
    laboratoryConfigurationItem,
    usersItem,
    rolesItem,
    reportsItem,
    auditSearchItem,
    oosTrackingItem
  ]
};

export function getGroupedMenuForRole(role: Role | null): MenuGroup[] {
  const items = role ? menuByRole[role] ?? [] : [];
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
