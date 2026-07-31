import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

// Inventory (Materials Stock, Equipment, Approved Media List, Approved
// Cryovial List) - day-to-day stock/equipment tracking, so Analysts get
// the same access as Section Head/System Administrator. Reviewer does
// not need it, matching its scope elsewhere in the app.
export function InventoryRoutes() {
  const { role } = useAuth();
  return role === "Analyst" || role === "SectionHead" || role === "SystemAdministrator"
    ? <Outlet />
    : <Navigate to="/dashboard" replace />;
}
