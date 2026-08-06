import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

// System administration only - user/role management. Replaces the old
// "AdminRoutes" name to make the role hierarchy explicit and consistent
// with SectionHeadRoutes/ReviewerRoutes.
export function SystemAdministratorRoutes() {
  const { role } = useAuth();
  return role === "SystemAdministrator" ? <Outlet /> : <Navigate to="/dashboard" replace />;
}
