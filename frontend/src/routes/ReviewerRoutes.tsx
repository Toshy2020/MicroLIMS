import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

// Review queue. Section Head and System Administrator can also reach
// it (escalation path), mirroring SectionHeadRoutes' pattern.
export function ReviewerRoutes() {
  const { role } = useAuth();
  return role === "Reviewer" || role === "SectionHead" || role === "SystemAdministrator"
    ? <Outlet />
    : <Navigate to="/dashboard" replace />;
}
