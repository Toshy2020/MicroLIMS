import { useTranslation } from "../../i18n";
import { useMemo } from "react";
import { Navigate } from "react-router-dom";
import { Tab, Tabs } from "@mui/material";
import { useAuth, useHasPermission } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { useMyLabs } from "../../hooks/useMyLabs";
import { usePersistedLab } from "../../hooks/usePersistedLab";
import { WorkspaceSkeleton } from "../../components/WorkspaceSkeleton";
import { AnalystDashboardPage } from "./AnalystDashboardPage";
import { ReviewerDashboardPage } from "./ReviewerDashboardPage";
import { SectionHeadDashboardPage } from "./SectionHeadDashboardPage";
import { AdminDashboardPage } from "./AdminDashboardPage";
import { QaDashboardPage } from "./QaDashboardPage";
import { DashboardLabCode, DashboardLabProvider, makeDashboardLab } from "./DashboardLabContext";

// Microbiology first, the same order the workspace redirect uses.
const LAB_ORDER: DashboardLabCode[] = ["MICRO", "FP"];

export function DashboardPage() {
  const { t } = useTranslation("dashboard");
  const { role } = useAuth();
  const { codes, physchemAreas, loading } = useMyLabs();
  const readsNoDashboard = !useHasPermission([PERMISSIONS.DASHBOARDS_LAB_OVERVIEW, PERMISSIONS.DASHBOARDS_REVIEW]);
  const canFinalApprove = useHasPermission(PERMISSIONS.NOT_APPLICABLE_TESTS_FINAL_APPROVE);

  // Each dashboard is for one laboratory: a Microbiology dashboard never
  // shows Physicochemical activity and vice versa. The laboratory lives in
  // the address (?lab=FP) so a reload, a bookmark or a shared link keeps it.
  const labs = LAB_ORDER.filter((c) => codes.includes(c));
  const [chosenLab, selectLab] = usePersistedLab(labs);
  const code = (chosenLab || null) as DashboardLabCode | null;
  const lab = useMemo(() => makeDashboardLab(code, physchemAreas), [code, physchemAreas]);

  // Whoever gives final approval to not-applicable tests but reads no dashboard works on the Items page. Decided by
  // permission, not role name, so the redirect never targets a page the user cannot open.
  if (canFinalApprove && readsNoDashboard) return <Navigate to="/laboratory-configuration/items" replace />;

  if (loading && role !== "Qa") return <WorkspaceSkeleton tiles={4} rows={5} />;

  // Role-specific operational dashboards. SectionHead (and any other role)
  // gets laboratory-wide operational oversight.
  const Page =
    role === "Analyst" ? AnalystDashboardPage
      : role === "Reviewer" ? ReviewerDashboardPage
        : role === "SystemAdministrator" ? AdminDashboardPage
          : role === "Qa" ? QaDashboardPage
            : SectionHeadDashboardPage;

  return (
    <DashboardLabProvider value={lab}>
      {labs.length > 1 && code && (
        <Tabs
          value={code}
          onChange={(_, next: DashboardLabCode) => selectLab(next)}
          aria-label={t("dashboardPage.label")}
          sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
        >
          {labs.map((c) => (
            <Tab key={c} value={c} label={t(`labName.${c}`)} />
          ))}
        </Tabs>
      )}
      {/* Keyed on the laboratory so switching remounts the page: no figure
          from the other laboratory survives the switch while new data loads. */}
      <Page key={code ?? "none"} />
    </DashboardLabProvider>
  );
}
