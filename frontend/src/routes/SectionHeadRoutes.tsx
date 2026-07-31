import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

// Laboratory Configuration + Approval - Section Head owns the Master
// Configuration (Frozen Principle #1). SystemAdministrator can always
// reach anything a Section Head can.
export function SectionHeadRoutes() {
  const { role } = useAuth();
  return role === "SectionHead" || role === "SystemAdministrator"
    ? <Outlet />
    : <Navigate to="/dashboard" replace />;
}
