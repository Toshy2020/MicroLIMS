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
import AcUnitOutlinedIcon from "@mui/icons-material/AcUnitOutlined";
import AirOutlinedIcon from "@mui/icons-material/AirOutlined";
import BlurOnOutlinedIcon from "@mui/icons-material/BlurOnOutlined";
import BubbleChartOutlinedIcon from "@mui/icons-material/BubbleChartOutlined";
import CleaningServicesOutlinedIcon from "@mui/icons-material/CleaningServicesOutlined";
import CoronavirusOutlinedIcon from "@mui/icons-material/CoronavirusOutlined";
import DashboardOutlinedIcon from "@mui/icons-material/DashboardOutlined";
import FunctionsOutlinedIcon from "@mui/icons-material/FunctionsOutlined";
import GrainOutlinedIcon from "@mui/icons-material/GrainOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import InsightsOutlinedIcon from "@mui/icons-material/InsightsOutlined";
import ListAltOutlinedIcon from "@mui/icons-material/ListAltOutlined";
import LocalLibraryOutlinedIcon from "@mui/icons-material/LocalLibraryOutlined";
import LocalShippingOutlinedIcon from "@mui/icons-material/LocalShippingOutlined";
import MemoryOutlinedIcon from "@mui/icons-material/MemoryOutlined";
import MenuBookOutlinedIcon from "@mui/icons-material/MenuBookOutlined";
import OpacityOutlinedIcon from "@mui/icons-material/OpacityOutlined";
import PrecisionManufacturingOutlinedIcon from "@mui/icons-material/PrecisionManufacturingOutlined";
import SchoolOutlinedIcon from "@mui/icons-material/SchoolOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import ThermostatOutlinedIcon from "@mui/icons-material/ThermostatOutlined";
import TuneOutlinedIcon from "@mui/icons-material/TuneOutlined";
import VerifiedOutlinedIcon from "@mui/icons-material/VerifiedOutlined";
import ViewColumnOutlinedIcon from "@mui/icons-material/ViewColumnOutlined";
import WaterDropOutlinedIcon from "@mui/icons-material/WaterDropOutlined";
import WaterOutlinedIcon from "@mui/icons-material/WaterOutlined";
import WhatshotOutlinedIcon from "@mui/icons-material/WhatshotOutlined";
import WorkspacePremiumOutlinedIcon from "@mui/icons-material/WorkspacePremiumOutlined";
import { Role } from "../modules/authentication/types/authTypes";
import { PERMISSIONS } from "./routes";

export interface MenuItem {
  label: string;
  // On a parent: its landing page listing the children as cards.
  path?: string;
  icon?: ComponentType<{ fontSize?: "small" | "inherit" | "medium" | "large"; sx?: SxProps<Theme> }>;
  group?: string;
  children?: MenuItem[];
  // One line on the section's landing page card (SectionPage).
  description?: string;
  // Drawn larger on the landing page: the section's daily sample workspace.
  featured?: boolean;
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
  physchemAreas?: ("fp" | "rmpm")[];
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
    path: "/sections/document-control",
    icon: DescriptionOutlinedIcon,
    group: "DOCUMENT CONTROL",
    children: [
      { label: "Dashboard", path: "/document-control", icon: DashboardOutlinedIcon, description: "Overview of documents due for review, approval and training." },
      { label: "My Reading List", path: "/document-control/my-reading-list", icon: MenuBookOutlinedIcon, description: "SOPs and documents you must read and acknowledge." },
      { label: "Training Matrix", path: "/document-control/training-matrix", icon: SchoolOutlinedIcon, description: "Who is trained on which document, and what is outstanding." },
      { label: "Compliance Dashboard", path: "/document-control/compliance-dashboard", icon: InsightsOutlinedIcon, description: "Training and document compliance across sections." },
      { label: "Document Library", path: "/document-control/library", icon: LocalLibraryOutlinedIcon, description: "Search and open every controlled document and its versions." },
      ...(canQueryDocumentAudit(role) ? [{ label: "Audit Trail", path: "/document-control/audit", icon: HistoryOutlinedIcon, description: "Audit trail of all document actions and signatures." }] : []),
      { label: "Configuration", path: "/document-control/configuration", icon: TuneOutlinedIcon, permission: PERMISSIONS.DOCUMENTS_CONFIG_MANAGE, description: "Document types, sections and workflow settings." }
    ]
  };
}

// Laboratory areas: one collapsible section per laboratory, shown only to
// its members (useMyLabs). Each bundles that lab's workspace, its
// analyst-facing pages and its own materials/equipment inventory; each
// page link also needs the permission its endpoints check.
const microArea: MenuItem = {
  label: "Microbiology Laboratory",
  path: "/sections/microbiology-laboratory",
  icon: BiotechOutlinedIcon,
  group: "LABORATORIES",
  children: [
    { label: "Workspace", path: "/microbiology/workspace", icon: BiotechOutlinedIcon, featured: true, description: "Receive, test, review and approve microbiology samples." },
    { label: "Media Preparation & Evaluation", path: "/laboratory-configuration/media", icon: ScienceOutlinedIcon, permission: PERMISSIONS.MEDIA_PREPARE, description: "Prepare media lots and record growth promotion and sterility checks." },
    { label: "Reference Cryovials", path: "/laboratory-configuration/cryovials", icon: AcUnitOutlinedIcon, permission: PERMISSIONS.CRYOVIALS_MANAGE, description: "Reference strain cryovials: preparation, use and stock." },
    { label: "Materials Stock", path: "/inventory/materials?lab=MICRO", icon: Inventory2OutlinedIcon, permission: PERMISSIONS.MATERIALS_MANAGE, description: "Receive and track microbiology reagents and consumables." },
    { label: "Equipment Inventory", path: "/inventory/equipment?lab=MICRO", icon: PrecisionManufacturingOutlinedIcon, permission: PERMISSIONS.EQUIPMENT_MANAGE, description: "Microbiology instruments, calibration and maintenance status." },
    { label: "Approved Media List", path: "/inventory/approved-media", icon: VerifiedOutlinedIcon, permission: PERMISSIONS.MEDIA_PREPARE, description: "Media lots released for use in testing." },
    { label: "Approved Cryovial List", path: "/inventory/approved-cryovials", icon: FactCheckOutlinedIcon, permission: PERMISSIONS.CRYOVIALS_MANAGE, description: "Cryovials released for use in testing." }
  ]
};

const microConfigArea: MenuItem = {
  label: "Microbiology Configuration",
  path: "/sections/microbiology-configuration",
  icon: BiotechOutlinedIcon,
  group: "LABORATORIES",
  permission: PERMISSIONS.MASTER_DATA_MANAGE,
  children: [
    { label: "Test Master", path: "/laboratory-configuration/test-master", icon: ListAltOutlinedIcon, description: "Microbiology test definitions, steps, media and limits." },
    { label: "Organisms", path: "/laboratory-configuration/organisms", icon: CoronavirusOutlinedIcon, description: "Organism master list used in tests and identification." },
    { label: "Media Configurations", path: "/laboratory-configuration/media-configurations", icon: ThermostatOutlinedIcon, description: "Media products, incubation times and temperatures." },
    { label: "Water", path: "/laboratory-configuration/water", icon: WaterDropOutlinedIcon, description: "Water sampling points and their test plans." },
    { label: "Environmental Monitoring", path: "/laboratory-configuration/environmental-monitoring", icon: AirOutlinedIcon, description: "EM locations, sampling methods and alert/action limits." },
    { label: "After Cleaning", path: "/laboratory-configuration/after-cleaning", icon: CleaningServicesOutlinedIcon, description: "After-cleaning swab points and acceptance limits." },
    { label: "Equipment", path: "/laboratory-configuration/equipment", icon: SettingsOutlinedIcon, description: "Equipment types and settings used by microbiology tests." }
  ]
};

function buildPhyschemArea(physchemAreas: ("fp" | "rmpm")[]): MenuItem {
  const children: MenuItem[] = [];
  if (physchemAreas.includes("fp")) {
    children.push({ label: "FP Workspace", path: "/physicochemical/workspace", icon: MedicationOutlinedIcon, featured: true, description: "Receive and test finished product samples." });
  }
  if (physchemAreas.includes("rmpm")) {
    children.push({ label: "RM & PM Workspace", path: "/physicochemical/rm-pm-workspace", icon: LocalShippingOutlinedIcon, featured: true, description: "Receive and test raw material and packaging samples." });
  }
  children.push(
    { label: "Solution Preparation", path: "/preparation", icon: OpacityOutlinedIcon, permission: PERMISSIONS.SOLUTIONS_PREPARE, description: "Prepare and standardize reagent and volumetric solutions." },
    { label: "Working Standards", path: "/working-standards", icon: WorkspacePremiumOutlinedIcon, description: "Qualify and track in-house working standards." },
    { label: "HPLC Workspace", path: "/hplc-workspace", icon: ViewColumnOutlinedIcon, description: "HPLC runs: sequences, system suitability and results." },
    { label: "GC Workspace", path: "/gc-workspace", icon: WhatshotOutlinedIcon, description: "GC runs: sequences, system suitability and results." },
    { label: "ICP Workspace", path: "/icp-workspace", icon: BlurOnOutlinedIcon, description: "ICP runs: calibration, samples and results." },
    { label: "Materials Stock", path: "/inventory/materials?lab=FP", icon: Inventory2OutlinedIcon, permission: PERMISSIONS.MATERIALS_MANAGE, description: "Receive and track physicochemical reagents and standards." },
    { label: "Equipment Inventory", path: "/inventory/equipment?lab=FP", icon: PrecisionManufacturingOutlinedIcon, permission: PERMISSIONS.EQUIPMENT_MANAGE, description: "Physicochemical instruments, calibration and maintenance status." }
  );

  return {
    label: "Physicochemical Laboratory",
    path: "/sections/physicochemical-laboratory",
    icon: MedicationOutlinedIcon,
    group: "LABORATORIES",
    children
  };
}

const physchemConfigArea: MenuItem = {
  label: "Physicochemical Configuration",
  path: "/sections/physicochemical-configuration",
  icon: MedicationOutlinedIcon,
  group: "LABORATORIES",
  permission: PERMISSIONS.MASTER_DATA_MANAGE,
  children: [
    { label: "FP Test Master", path: "/laboratory-configuration/fp-test-master", icon: ListAltOutlinedIcon, description: "Finished product test definitions and specifications." },
    { label: "RM & PM Test Master", path: "/laboratory-configuration/rm-pm-test-master", icon: FactCheckOutlinedIcon, description: "Raw material and packaging test definitions." },
    { label: "Equation Types", path: "/laboratory-configuration/equation-types", icon: FunctionsOutlinedIcon, description: "Calculation equations used by physicochemical tests." },
    { label: "Physicochemical Instruments", path: "/laboratory-configuration/fp-instruments", icon: MemoryOutlinedIcon, description: "Instrument types available to physicochemical tests." },
    { label: "Chromatography Columns", path: "/laboratory-configuration/columns", icon: ViewColumnOutlinedIcon, description: "Chromatography columns and their usage history." },
    { label: "Reagents & Standards", path: "/laboratory-configuration/material-master", icon: GrainOutlinedIcon, description: "Reagent and reference standard master data." },
    { label: "Solutions", path: "/laboratory-configuration/solution-master", icon: WaterOutlinedIcon, description: "Solution recipes and standardization rules." },
    { label: "HPLC Methods", path: "/laboratory-configuration/hplc-methods", icon: BubbleChartOutlinedIcon, description: "HPLC methods: conditions, analytes and suitability criteria." },
    { label: "ICP Methods", path: "/laboratory-configuration/icp-methods", icon: BlurOnOutlinedIcon, description: "ICP methods: elements, wavelengths and limits." }
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
export function getGroupedMenu({ role, permissions, labCodes, physchemAreas }: MenuContext): MenuGroup[] {
  const items: MenuItem[] = [dashboardItem, receiveSampleItem, trackingBoardItem];
  if (labCodes.includes("MICRO")) items.push(microArea, microConfigArea);
  if (labCodes.includes("FP")) {
    const areas = physchemAreas ?? ["fp", "rmpm"];
    items.push(buildPhyschemArea(areas), physchemConfigArea);
  }
  items.push(
    itemsItem, receivingConfigItem,
    documentControlItem(role), usersItem, rolesItem, reportsItem, auditSearchItem, oosTrackingItem, errorMonitoringItem
  );
  return groupItems(visibleItems(items, permissions));
}
