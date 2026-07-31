import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

// Sample Receiving + Testing Workspace - the day-to-day Analyst tools.
// Section Head and System Administrator can also reach them.
export function AnalystRoutes() {
  const { role } = useAuth();
  return role === "Analyst" || role === "SectionHead" || role === "SystemAdministrator"
    ? <Outlet />
    : <Navigate to="/dashboard" replace />;
}
