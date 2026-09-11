import { Navigate, Outlet } from "react-router-dom";
import { useHasPermission } from "../contexts/AuthContext";

// Permission-based route guard, as opposed to SystemAdministratorRoutes'
// role-string check. New capabilities gate on a permission code so access
// can be granted through the Roles screen rather than by being one of the
// four built-in roles.
//
// The frontend guard only decides what to render - every gated endpoint
// carries its own [Authorize(Policy=...)] (Frozen Principle #3).
export function PermissionRoute({ code }: { code: string }) {
  return useHasPermission(code) ? <Outlet /> : <Navigate to="/dashboard" replace />;
}
