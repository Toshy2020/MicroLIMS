import { Routes, Route, Navigate } from "react-router-dom";
import { PublicRoutes } from "./PublicRoutes";
import { AuthenticatedRoutes } from "./AuthenticatedRoutes";
import { SystemAdministratorRoutes } from "./SystemAdministratorRoutes";
import { SectionHeadRoutes } from "./SectionHeadRoutes";
import { ReviewerRoutes } from "./ReviewerRoutes";
import { AnalystRoutes } from "./AnalystRoutes";
import { LoginPage } from "../pages/Login";
import { DashboardPage } from "../pages/Dashboard";
import { ProfilePage } from "../pages/Profile";
import { ReportsPage } from "../pages/Reports";
import { ReceiveSamplePage } from "../modules/receiving/ReceiveSamplePage";
import { TestingWorkspacePage } from "../modules/testingWorkspace/TestingWorkspacePage";
import { EMPreparationPage } from "../modules/laboratoryConfiguration/environmentalMonitoring/EMPreparationPage";
import { AfterCleaningPreparationPage } from "../modules/laboratoryConfiguration/afterCleaning/AfterCleaningPreparationPage";
import { TestPreparationPage } from "../modules/testPreparation/TestPreparationPage";
import { ReviewPage } from "../modules/review/ReviewPage";
import { ApprovalPage } from "../modules/approval/ApprovalPage";
import { ItemsPage } from "../modules/laboratoryConfiguration/items/ItemsPage";
import { SpecificationsPage } from "../modules/laboratoryConfiguration/specifications/SpecificationsPage";
import { MediaPage } from "../modules/laboratoryConfiguration/media/MediaPage";
import { MediaTypesPage } from "../modules/laboratoryConfiguration/media/MediaTypesPage";
import { GptPage } from "../modules/laboratoryConfiguration/gpt/GptPage";
import { WaterConfigPage } from "../modules/laboratoryConfiguration/water/WaterConfigPage";
import { EMConfigPage } from "../modules/laboratoryConfiguration/environmentalMonitoring/EMConfigPage";
import { AfterCleaningConfigPage } from "../modules/laboratoryConfiguration/afterCleaning/AfterCleaningConfigPage";
import { ReferenceStrainsPage } from "../modules/laboratoryConfiguration/referenceStrains/ReferenceStrainsPage";
import { CryovialsPage } from "../modules/laboratoryConfiguration/referenceStrains/CryovialsPage";
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
        <Route element={<MainLayout />}>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/reports" element={<ReportsPage />} />

          <Route element={<AnalystRoutes />}>
            <Route path="/receiving" element={<ReceiveSamplePage />} />
            <Route path="/testing-workspace" element={<TestingWorkspacePage />} />
            <Route path="/em-preparation" element={<EMPreparationPage />} />
            <Route path="/aftercleaning-preparation" element={<AfterCleaningPreparationPage />} />
            <Route path="/test-preparation" element={<TestPreparationPage />} />
          </Route>

          <Route element={<ReviewerRoutes />}>
            <Route path="/review" element={<ReviewPage />} />
          </Route>

          <Route element={<SectionHeadRoutes />}>
            <Route path="/approval" element={<ApprovalPage />} />
            <Route path="/audit-search" element={<AuditSearchPage />} />
            <Route path="/laboratory-configuration/items" element={<ItemsPage />} />
            <Route path="/laboratory-configuration/specifications" element={<SpecificationsPage />} />
            <Route path="/laboratory-configuration/media-types" element={<MediaTypesPage />} />
            <Route path="/laboratory-configuration/media" element={<MediaPage />} />
            <Route path="/laboratory-configuration/gpt" element={<GptPage />} />
            <Route path="/laboratory-configuration/reference-strains" element={<ReferenceStrainsPage />} />
            <Route path="/laboratory-configuration/cryovials" element={<CryovialsPage />} />
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
