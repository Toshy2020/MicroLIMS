import { lazy, Suspense } from "react";
import { Routes, Route, Navigate, useLocation } from "react-router-dom";
import { PublicRoutes } from "./PublicRoutes";
import { AuthenticatedRoutes } from "./AuthenticatedRoutes";
import { SystemAdministratorRoutes } from "./SystemAdministratorRoutes";
import { PermissionRoute } from "./PermissionRoute";
import { PERMISSIONS } from "./routes";
import { SectionHeadRoutes } from "./SectionHeadRoutes";
import { InventoryRoutes } from "./InventoryRoutes";
import { MainLayout } from "../layouts/MainLayout";
import { LoadingSpinner } from "../components/LoadingSpinner";

// Every page-level route component below is lazy-loaded so the initial
// bundle only ships the shell (layout/auth guards + whichever page the
// user actually lands on) instead of every module in the app - see the
// bundle-size analysis in docs/superpowers if this changes again.
const LoginPage = lazy(() => import("../pages/Login").then((m) => ({ default: m.LoginPage })));
const AdminPasswordRecovery = lazy(() => import("../pages/AdminPasswordRecovery").then((m) => ({ default: m.AdminPasswordRecovery })));
const DashboardPage = lazy(() => import("../modules/dashboard/DashboardPage").then((m) => ({ default: m.DashboardPage })));
const ProfilePage = lazy(() => import("../pages/Profile").then((m) => ({ default: m.ProfilePage })));
const ChangePasswordPage = lazy(() => import("../pages/ChangePassword").then((m) => ({ default: m.ChangePasswordPage })));
const ReportsPage = lazy(() => import("../pages/Reports").then((m) => ({ default: m.ReportsPage })));
const DiscussionsFeedPage = lazy(() => import("../modules/discussions/DiscussionsFeedPage").then((m) => ({ default: m.DiscussionsFeedPage })));
const DiscussionDetailPage = lazy(() => import("../modules/discussions/DiscussionDetailPage").then((m) => ({ default: m.DiscussionDetailPage })));
const MessagesPage = lazy(() => import("../modules/messages/MessagesPage").then((m) => ({ default: m.MessagesPage })));
// ReceivingTestingWorkspacePage itself is only ever rendered from inside
// LabWorkspaceRoute now (it requires a resolved `lab` prop) - that module
// is the lazy chunk boundary, not the page component directly.
const LabWorkspaceRoute = lazy(() => import("../modules/receivingTesting/LabWorkspaceRoute").then((m) => ({ default: m.LabWorkspaceRoute })));
const FirstLabWorkspaceRedirect = lazy(() => import("../modules/receivingTesting/LabWorkspaceRoute").then((m) => ({ default: m.FirstLabWorkspaceRedirect })));
const ReceivingPage = lazy(() => import("../modules/receiving/ReceivingPage").then((m) => ({ default: m.ReceivingPage })));
const TrackingBoardPage = lazy(() => import("../modules/receiving/TrackingBoardPage").then((m) => ({ default: m.TrackingBoardPage })));
const SampleReportPage = lazy(() => import("../modules/testingWorkspace/SampleReportPage").then((m) => ({ default: m.SampleReportPage })));
const SampleCoaPage = lazy(() => import("../modules/testingWorkspace/SampleCoaPage").then((m) => ({ default: m.SampleCoaPage })));
const MediaReportPage = lazy(() => import("../modules/laboratoryConfiguration/media/MediaReportPage").then((m) => ({ default: m.MediaReportPage })));
const SuitabilityRunReportPage = lazy(() => import("../modules/systemSuitability/SuitabilityRunReportPage").then((m) => ({ default: m.SuitabilityRunReportPage })));
const CalibrationRunReportPage = lazy(() => import("../modules/calibrationRuns/CalibrationRunReportPage").then((m) => ({ default: m.CalibrationRunReportPage })));
const CryovialReportPage = lazy(() => import("../modules/laboratoryConfiguration/cryovials/CryovialReportPage").then((m) => ({ default: m.CryovialReportPage })));
const ItemsPage = lazy(() => import("../modules/laboratoryConfiguration/items/ItemsPage").then((m) => ({ default: m.ItemsPage })));
const TestMasterPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataSimple/TestMasterPage").then((m) => ({ default: m.TestMasterPage })));
const EquationTypesPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataSimple/EquationTypesPage").then((m) => ({ default: m.EquationTypesPage })));
const OrganismsPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataSimple/OrganismsPage").then((m) => ({ default: m.OrganismsPage })));
const SpecificationsPage = lazy(() => import("../modules/laboratoryConfiguration/specifications/SpecificationsPage").then((m) => ({ default: m.SpecificationsPage })));
const MediaPage = lazy(() => import("../modules/laboratoryConfiguration/media/MediaPage").then((m) => ({ default: m.MediaPage })));
const MediaConfigurationPage = lazy(() => import("../modules/laboratoryConfiguration/media/MediaConfigurationPage").then((m) => ({ default: m.MediaConfigurationPage })));
const MediaEvaluationPage = lazy(() => import("../modules/laboratoryConfiguration/mediaEvaluation/MediaEvaluationPage").then((m) => ({ default: m.MediaEvaluationPage })));
const WaterConfigPage = lazy(() => import("../modules/laboratoryConfiguration/water/WaterConfigPage").then((m) => ({ default: m.WaterConfigPage })));
const EMConfigPage = lazy(() => import("../modules/laboratoryConfiguration/environmentalMonitoring/EMConfigPage").then((m) => ({ default: m.EMConfigPage })));
const AfterCleaningConfigPage = lazy(() => import("../modules/laboratoryConfiguration/afterCleaning/AfterCleaningConfigPage").then((m) => ({ default: m.AfterCleaningConfigPage })));
const SystemSuitabilityRunsPage = lazy(() => import("../modules/systemSuitability/SystemSuitabilityRunsPage").then((m) => ({ default: m.SystemSuitabilityRunsPage })));
const CalibrationRunsPage = lazy(() => import("../modules/calibrationRuns/CalibrationRunsPage").then((m) => ({ default: m.CalibrationRunsPage })));
const CryovialsPage = lazy(() => import("../modules/laboratoryConfiguration/cryovials/CryovialsPage").then((m) => ({ default: m.CryovialsPage })));
const ReceivingConfigurationPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataSimple/ReceivingConfigurationPage").then((m) => ({ default: m.ReceivingConfigurationPage })));
const EquipmentPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataSimple/EquipmentPage").then((m) => ({ default: m.EquipmentPage })));
const FpInstrumentsPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataSimple/FpInstrumentsPage").then((m) => ({ default: m.FpInstrumentsPage })));
const ChromatographyColumnsPage = lazy(() => import("../modules/laboratoryConfiguration/masterDataSimple/ChromatographyColumnsPage").then((m) => ({ default: m.ChromatographyColumnsPage })));
const UsersPage = lazy(() => import("../modules/users/UsersPage").then((m) => ({ default: m.UsersPage })));
const RolesPage = lazy(() => import("../modules/roles/RolesPage").then((m) => ({ default: m.RolesPage })));
const RoleDetailPage = lazy(() => import("../modules/roles/RoleDetailPage").then((m) => ({ default: m.RoleDetailPage })));
const CreateRolePage = lazy(() => import("../modules/roles/CreateRolePage").then((m) => ({ default: m.CreateRolePage })));
const AuditSearchPage = lazy(() => import("../modules/auditSearch/AuditSearchPage").then((m) => ({ default: m.AuditSearchPage })));
const ErrorMonitoringPage = lazy(() => import("../modules/errorMonitoring/ErrorMonitoringPage").then((m) => ({ default: m.ErrorMonitoringPage })));
const OosTrackingPage = lazy(() => import("../modules/oosTracking/OosTrackingPage").then((m) => ({ default: m.OosTrackingPage })));
const MaterialsPage = lazy(() => import("../modules/inventory/materials/MaterialsPage").then((m) => ({ default: m.MaterialsPage })));
const EquipmentInventoryPage = lazy(() => import("../modules/inventory/equipment/EquipmentInventoryPage").then((m) => ({ default: m.EquipmentInventoryPage })));
const ApprovedMediaListPage = lazy(() => import("../modules/inventory/approvedLists/ApprovedMediaListPage").then((m) => ({ default: m.ApprovedMediaListPage })));
const ApprovedCryovialListPage = lazy(() => import("../modules/inventory/approvedLists/ApprovedCryovialListPage").then((m) => ({ default: m.ApprovedCryovialListPage })));
const DocumentControlDashboardPage = lazy(() => import("../modules/documentControl/pages/DocumentControlDashboardPage").then((m) => ({ default: m.DocumentControlDashboardPage })));
const DocumentLibraryPage = lazy(() => import("../modules/documentControl/pages/DocumentLibraryPage").then((m) => ({ default: m.DocumentLibraryPage })));
const MyReadingListPage = lazy(() => import("../modules/documentControl/pages/MyReadingListPage").then((m) => ({ default: m.MyReadingListPage })));
const TrainingMatrixPage = lazy(() => import("../modules/documentControl/pages/TrainingMatrixPage").then((m) => ({ default: m.TrainingMatrixPage })));
const ComplianceDashboardPage = lazy(() => import("../modules/documentControl/pages/ComplianceDashboardPage").then((m) => ({ default: m.ComplianceDashboardPage })));
const DocumentDetailPage = lazy(() => import("../modules/documentControl/pages/DocumentDetailPage").then((m) => ({ default: m.DocumentDetailPage })));
const DocumentConfigurationPage = lazy(() => import("../modules/documentControl/pages/DocumentConfigurationPage").then((m) => ({ default: m.DocumentConfigurationPage })));
const DocumentAuditPage = lazy(() => import("../modules/documentControl/pages/DocumentAuditPage").then((m) => ({ default: m.DocumentAuditPage })));

function LegacyRedirect({ to }: { to: string }) {
  const location = useLocation();
  return <Navigate to={`${to}${location.search}`} replace />;
}

export function AppRoutes() {
  return (
    <Suspense fallback={<LoadingSpinner />}>
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
          <Route path="/laboratory/system-suitability/:id/report" element={<SuitabilityRunReportPage />} />
          <Route path="/laboratory/calibration-runs/:id/report" element={<CalibrationRunReportPage />} />

          <Route element={<MainLayout />}>
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/profile" element={<ProfilePage />} />
            <Route path="/change-password" element={<ChangePasswordPage />} />
            <Route path="/discussions" element={<DiscussionsFeedPage />} />
            <Route path="/discussions/:id" element={<DiscussionDetailPage />} />
            <Route path="/messages" element={<MessagesPage />} />
            <Route path="/reports" element={<ReportsPage />} />

            {/* Lab Workspaces (Task 13a): each laboratory gets its own scoped
                Receiving & Testing Workspace instance. /receiving-testing has
                no lab of its own any more - it redirects to the first lab
                workspace the caller belongs to (Microbiology first). */}
            {/* Keyed on lab code so switching between the two workspaces
                forces a full remount (LabWorkspaceRoute -> ReceivingTesting-
                WorkspacePage) instead of React reusing the same component
                instance in place - otherwise the previous lab's records,
                selection and filters stay on screen under the new lab's
                title until some unrelated state change happens to refetch
                them (review finding, design.md §3.2 lab isolation). */}
            <Route path="/microbiology/workspace" element={<LabWorkspaceRoute key="MICRO" code="MICRO" />} />
            <Route path="/physicochemical/workspace" element={<LabWorkspaceRoute key="FP" code="FP" />} />
            <Route path="/receiving-testing" element={<FirstLabWorkspaceRedirect />} />

            {/* Receiving area: the main receiving desk and the cross-lab
                tracking board (design.md §3.1, §3.4) - gated on the
                Samples.Receive / Samples.TrackAll privileges, not role or
                lab membership. /receiving used to redirect into the
                workspace; it now owns the Receiving page itself. */}
            <Route element={<PermissionRoute code={PERMISSIONS.SAMPLES_RECEIVE} />}>
              <Route path="/receiving" element={<ReceivingPage />} />
            </Route>
            <Route element={<PermissionRoute code={PERMISSIONS.SAMPLES_TRACK_ALL} />}>
              <Route path="/receiving/tracking" element={<TrackingBoardPage />} />
            </Route>

            {/* Backward-Compatible Query-Preserving Legacy Redirects */}
            {/* Chains through FirstLabWorkspaceRedirect above rather than
                pointing at a lab workspace directly - one place decides
                "first lab the user belongs to". */}
            <Route path="/testing-workspace" element={<LegacyRedirect to="/receiving-testing" />} />
            <Route path="/laboratory-configuration/media" element={<MediaPage />} />
            <Route path="/laboratory-configuration/media-evaluation" element={<MediaEvaluationPage />} />
            <Route path="/laboratory-configuration/cryovials" element={<CryovialsPage />} />
            <Route path="/laboratory/system-suitability" element={<SystemSuitabilityRunsPage />} />
            <Route path="/laboratory/calibration-runs" element={<CalibrationRunsPage />} />

            <Route element={<SectionHeadRoutes />}>
              <Route path="/audit-search" element={<AuditSearchPage />} />
              <Route path="/oos-tracking" element={<OosTrackingPage />} />
              <Route path="/laboratory-configuration/test-master" element={<TestMasterPage lab="micro" />} />
              <Route path="/laboratory-configuration/fp-test-master" element={<TestMasterPage key="fp" lab="fp" />} />
              <Route path="/laboratory-configuration/equation-types" element={<EquationTypesPage />} />
              <Route path="/laboratory-configuration/organisms" element={<OrganismsPage />} />
              <Route path="/laboratory-configuration/items" element={<ItemsPage />} />
              <Route path="/laboratory-configuration/specifications" element={<SpecificationsPage />} />
              <Route path="/laboratory-configuration/media-configurations" element={<MediaConfigurationPage />} />
              <Route path="/laboratory-configuration/water" element={<WaterConfigPage />} />
              <Route path="/laboratory-configuration/environmental-monitoring" element={<EMConfigPage />} />
              <Route path="/laboratory-configuration/after-cleaning" element={<AfterCleaningConfigPage />} />
              <Route path="/laboratory-configuration/receiving-configuration" element={<ReceivingConfigurationPage />} />
              <Route path="/laboratory-configuration/cause-of-testing" element={<LegacyRedirect to="/laboratory-configuration/receiving-configuration" />} />
              <Route path="/laboratory-configuration/equipment" element={<EquipmentPage />} />
              <Route path="/laboratory-configuration/fp-instruments" element={<FpInstrumentsPage />} />
              <Route path="/laboratory-configuration/columns" element={<ChromatographyColumnsPage />} />
            </Route>

            {/* Document Control Module (Release 1a) */}
            <Route path="/document-control" element={<DocumentControlDashboardPage />} />
            <Route path="/document-control/my-reading-list" element={<MyReadingListPage />} />
            <Route path="/document-control/training-matrix" element={<TrainingMatrixPage />} />
            <Route path="/document-control/compliance-dashboard" element={<ComplianceDashboardPage />} />
            <Route path="/document-control/library" element={<DocumentLibraryPage />} />
            <Route path="/document-control/documents/:id" element={<DocumentDetailPage />} />
            <Route path="/document-control/audit" element={<DocumentAuditPage />} />

            <Route element={<InventoryRoutes />}>
              <Route path="/inventory/materials" element={<MaterialsPage />} />
              <Route path="/inventory/equipment" element={<EquipmentInventoryPage />} />
              <Route path="/inventory/approved-media" element={<ApprovedMediaListPage />} />
              <Route path="/inventory/approved-cryovials" element={<ApprovedCryovialListPage />} />
            </Route>

            {/* Permission-gated rather than role-gated - a new capability
                with no legacy role equivalent to reproduce. */}
            <Route element={<PermissionRoute code={PERMISSIONS.SYSTEM_VIEW_ERROR_LOG} />}>
              <Route path="/error-monitoring" element={<ErrorMonitoringPage />} />
            </Route>

            <Route element={<SystemAdministratorRoutes />}>
              <Route path="/document-control/configuration" element={<DocumentConfigurationPage />} />
              <Route path="/users" element={<UsersPage />} />
              <Route path="/roles" element={<RolesPage />} />
              <Route path="/roles/new" element={<CreateRolePage />} />
              <Route path="/roles/:id" element={<RoleDetailPage />} />
            </Route>
          </Route>
        </Route>
      </Routes>
    </Suspense>
  );
}
