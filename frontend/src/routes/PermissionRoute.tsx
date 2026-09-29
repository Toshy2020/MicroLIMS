import { Navigate, Outlet } from "react-router-dom";
import { useHasPermission } from "../contexts/AuthContext";

// Permission-based route guard. Pages gate on the permission code their
// endpoints check, so access follows what the Roles screen grants rather
// than which of the four built-in roles the user has.
//
// The frontend guard only decides what to render - every gated endpoint
// carries its own [Authorize(Policy=...)] (Frozen Principle #3).
export function PermissionRoute({ code }: { code: string }) {
  return useHasPermission(code) ? <Outlet /> : <Navigate to="/dashboard" replace />;
}
