import { useAuth } from "../../contexts/AuthContext";
import { AnalystDashboardPage } from "./AnalystDashboardPage";
import { ReviewerDashboardPage } from "./ReviewerDashboardPage";
import { SectionHeadDashboardPage } from "./SectionHeadDashboardPage";
import { AdminDashboardPage } from "./AdminDashboardPage";

export function DashboardPage() {
  const { role } = useAuth();

  // Role-specific operational dashboards
  if (role === "Analyst") {
    return <AnalystDashboardPage />;
  }

  if (role === "Reviewer") {
    return <ReviewerDashboardPage />;
  }

  if (role === "SystemAdministrator") {
    return <AdminDashboardPage />;
  }

  // SectionHead gets laboratory-wide operational oversight
  return <SectionHeadDashboardPage />;
}
