import { Routes, Route, Navigate } from "react-router-dom";
import { PublicRoutes } from "./PublicRoutes";
import { AuthenticatedRoutes } from "./AuthenticatedRoutes";
import { SystemAdministratorRoutes } from "./SystemAdministratorRoutes";
import { SectionHeadRoutes } from "./SectionHeadRoutes";
import { LoginPage } from "../pages/Login";
import { DashboardPage } from "../modules/dashboard/DashboardPage";
import { ProfilePage } from "../pages/Profile";
import { ChangePasswordPage } from "../pages/ChangePassword";
import { ReportsPage } from "../pages/Reports";
import { ReceiveSamplePage } from "../modules/receiving/ReceiveSamplePage";
import { TestingWorkspacePage } from "../modules/testingWorkspace/TestingWorkspacePage";
import { SampleReportPage } from "../modules/testingWorkspace/SampleReportPage";
import { MediaReportPage } from "../modules/laboratoryConfiguration/media/MediaReportPage";
import { CryovialReportPage } from "../modules/laboratoryConfiguration/cryovials/CryovialReportPage";
import { ItemsPage } from "../modules/laboratoryConfiguration/items/ItemsPage";
import { TestMasterPage } from "../modules/laboratoryConfiguration/masterDataSimple/TestMasterPage";
import { OrganismsPage } from "../modules/laboratoryConfiguration/masterDataSimple/OrganismsPage";
import { SpecificationsPage } from "../modules/laboratoryConfiguration/specifications/SpecificationsPage";
import { MediaPage } from "../modules/laboratoryConfiguration/media/MediaPage";
import { MediaTypesPage } from "../modules/laboratoryConfiguration/media/MediaTypesPage";
import { MediaChallengeSpecsPage } from "../modules/laboratoryConfiguration/media/MediaChallengeSpecsPage";
import { MediaEvaluationPage } from "../modules/laboratoryConfiguration/mediaEvaluation/MediaEvaluationPage";
import { WaterConfigPage } from "../modules/laboratoryConfiguration/water/WaterConfigPage";
import { EMConfigPage } from "../modules/laboratoryConfiguration/environmentalMonitoring/EMConfigPage";
import { AfterCleaningConfigPage } from "../modules/laboratoryConfiguration/afterCleaning/AfterCleaningConfigPage";
import { CryovialsPage } from "../modules/laboratoryConfiguration/cryovials/CryovialsPage";
import { CauseOfTestingPage } from "../modules/laboratoryConfiguration/masterDataSimple/CauseOfTestingPage";
import { DiluentsPage } from "../modules/laboratoryConfiguration/masterDataSimple/DiluentsPage";
import { EquipmentPage } from "../modules/laboratoryConfiguration/masterDataSimple/EquipmentPage";
import { UsersPage } from "../modules/users/UsersPage";
import { RolesPage } from "../modules/roles/RolesPage";
import { AuditSearchPage } from "../modules/auditSearch/AuditSearchPage";
import { MaterialsPage } from "../modules/inventory/materials/MaterialsPage";
import { EquipmentInventoryPage } from "../modules/inventory/equipment/EquipmentInventoryPage";
import { ApprovedMediaListPage } from "../modules/inventory/approvedLists/ApprovedMediaListPage";
import { ApprovedCryovialListPage } from "../modules/inventory/approvedLists/ApprovedCryovialListPage";
import { InventoryRoutes } from "./InventoryRoutes";
import { MainLayout } from "../layouts/MainLayout";

// DashboardLayout -> Role -> menuConfig.ts -> Visible Modules.
export function AppRoutes() {
  return (
    <Routes>
      <Route element={<PublicRoutes />}>
        <Route path="/login" element={<LoginPage />} />
      </Route>

      <Route element={<AuthenticatedRoutes />}>
        {/* Deliberately outside MainLayout: the report is a printable
            controlled document, so it must render without the app's nav
            chrome. Opened in a new tab from the Sample Summary dialog. */}
        <Route path="/samples/:id/report" element={<SampleReportPage />} />
        <Route path="/media/:id/report" element={<MediaReportPage />} />
        <Route path="/cryovials/:id/report" element={<CryovialReportPage />} />

        <Route element={<MainLayout />}>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/change-password" element={<ChangePasswordPage />} />
          <Route path="/reports" element={<ReportsPage />} />

          {/* Available to every role - see menuConfig.ts */}
          <Route path="/receiving" element={<ReceiveSamplePage />} />
          <Route path="/testing-workspace" element={<TestingWorkspacePage />} />
          <Route path="/laboratory-configuration/media" element={<MediaPage />} />
          <Route path="/laboratory-configuration/media-evaluation" element={<MediaEvaluationPage />} />
          <Route path="/laboratory-configuration/cryovials" element={<CryovialsPage />} />

          <Route element={<SectionHeadRoutes />}>
            <Route path="/audit-search" element={<AuditSearchPage />} />
            <Route path="/laboratory-configuration/test-master" element={<TestMasterPage />} />
            <Route path="/laboratory-configuration/organisms" element={<OrganismsPage />} />
            <Route path="/laboratory-configuration/items" element={<ItemsPage />} />
            <Route path="/laboratory-configuration/specifications" element={<SpecificationsPage />} />
            <Route path="/laboratory-configuration/media-types" element={<MediaTypesPage />} />
            <Route path="/laboratory-configuration/media-challenge-specs" element={<MediaChallengeSpecsPage />} />
            <Route path="/laboratory-configuration/water" element={<WaterConfigPage />} />
            <Route path="/laboratory-configuration/environmental-monitoring" element={<EMConfigPage />} />
            <Route path="/laboratory-configuration/after-cleaning" element={<AfterCleaningConfigPage />} />
            <Route path="/laboratory-configuration/cause-of-testing" element={<CauseOfTestingPage />} />
            <Route path="/laboratory-configuration/diluents" element={<DiluentsPage />} />
            <Route path="/laboratory-configuration/equipment" element={<EquipmentPage />} />
          </Route>

          <Route element={<InventoryRoutes />}>
            <Route path="/inventory/materials" element={<MaterialsPage />} />
            <Route path="/inventory/equipment" element={<EquipmentInventoryPage />} />
            <Route path="/inventory/approved-media" element={<ApprovedMediaListPage />} />
            <Route path="/inventory/approved-cryovials" element={<ApprovedCryovialListPage />} />
          </Route>

          <Route element={<SystemAdministratorRoutes />}>
            <Route path="/users" element={<UsersPage />} />
            <Route path="/roles" element={<RolesPage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  );
}
