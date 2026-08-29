import { Routes, Route, Navigate, useLocation } from "react-router-dom";
import { PublicRoutes } from "./PublicRoutes";
import { AuthenticatedRoutes } from "./AuthenticatedRoutes";
import { SystemAdministratorRoutes } from "./SystemAdministratorRoutes";
import { SectionHeadRoutes } from "./SectionHeadRoutes";
import { LoginPage } from "../pages/Login";
import { AdminPasswordRecovery } from "../pages/AdminPasswordRecovery";
import { DashboardPage } from "../modules/dashboard/DashboardPage";
import { ProfilePage } from "../pages/Profile";
import { ChangePasswordPage } from "../pages/ChangePassword";
import { ReportsPage } from "../pages/Reports";
import { ReceivingTestingWorkspacePage } from "../modules/receivingTesting/ReceivingTestingWorkspacePage";
import { SampleReportPage } from "../modules/testingWorkspace/SampleReportPage";
import { SampleCoaPage } from "../modules/testingWorkspace/SampleCoaPage";
import { MediaReportPage } from "../modules/laboratoryConfiguration/media/MediaReportPage";
import { CryovialReportPage } from "../modules/laboratoryConfiguration/cryovials/CryovialReportPage";
import { ItemsPage } from "../modules/laboratoryConfiguration/items/ItemsPage";
import { TestMasterPage } from "../modules/laboratoryConfiguration/masterDataSimple/TestMasterPage";
import { OrganismsPage } from "../modules/laboratoryConfiguration/masterDataSimple/OrganismsPage";
import { SpecificationsPage } from "../modules/laboratoryConfiguration/specifications/SpecificationsPage";
import { MediaPage } from "../modules/laboratoryConfiguration/media/MediaPage";
import { MediaConfigurationPage } from "../modules/laboratoryConfiguration/media/MediaConfigurationPage";
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
import { RoleDetailPage } from "../modules/roles/RoleDetailPage";
import { CreateRolePage } from "../modules/roles/CreateRolePage";
import { AuditSearchPage } from "../modules/auditSearch/AuditSearchPage";
import { OosTrackingPage } from "../modules/oosTracking/OosTrackingPage";
import { MaterialsPage } from "../modules/inventory/materials/MaterialsPage";
import { EquipmentInventoryPage } from "../modules/inventory/equipment/EquipmentInventoryPage";
import { ApprovedMediaListPage } from "../modules/inventory/approvedLists/ApprovedMediaListPage";
import { ApprovedCryovialListPage } from "../modules/inventory/approvedLists/ApprovedCryovialListPage";
import { InventoryRoutes } from "./InventoryRoutes";
import { MainLayout } from "../layouts/MainLayout";

function LegacyRedirect({ to }: { to: string }) {
  const location = useLocation();
  return <Navigate to={`${to}${location.search}`} replace />;
}

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<PublicRoutes />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/admin-recovery" element={<AdminPasswordRecovery />} />
      </Route>

      <Route element={<AuthenticatedRoutes />}>
        <Route path="/samples/:id/report" element={<SampleReportPage />} />
        <Route path="/samples/:id/coa" element={<SampleCoaPage />} />
        <Route path="/media/:id/report" element={<MediaReportPage />} />
        <Route path="/cryovials/:id/report" element={<CryovialReportPage />} />

        <Route element={<MainLayout />}>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/change-password" element={<ChangePasswordPage />} />
          <Route path="/reports" element={<ReportsPage />} />

          {/* Canonical Unified Receiving & Testing Workspace */}
          <Route path="/receiving-testing" element={<ReceivingTestingWorkspacePage />} />

          {/* Backward-Compatible Query-Preserving Legacy Redirects */}
          <Route path="/receiving" element={<LegacyRedirect to="/receiving-testing" />} />
          <Route path="/testing-workspace" element={<LegacyRedirect to="/receiving-testing" />} />
          <Route path="/laboratory-configuration/media" element={<MediaPage />} />
          <Route path="/laboratory-configuration/media-evaluation" element={<MediaEvaluationPage />} />
          <Route path="/laboratory-configuration/cryovials" element={<CryovialsPage />} />

          <Route element={<SectionHeadRoutes />}>
            <Route path="/audit-search" element={<AuditSearchPage />} />
            <Route path="/oos-tracking" element={<OosTrackingPage />} />
            <Route path="/laboratory-configuration/test-master" element={<TestMasterPage />} />
            <Route path="/laboratory-configuration/organisms" element={<OrganismsPage />} />
            <Route path="/laboratory-configuration/items" element={<ItemsPage />} />
            <Route path="/laboratory-configuration/specifications" element={<SpecificationsPage />} />
            <Route path="/laboratory-configuration/media-configurations" element={<MediaConfigurationPage />} />
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
            <Route path="/roles/new" element={<CreateRolePage />} />
            <Route path="/roles/:id" element={<RoleDetailPage />} />
          </Route>
        </Route>
      </Route>
    </Routes>
  );
}
