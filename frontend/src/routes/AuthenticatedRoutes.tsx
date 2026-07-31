import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

// Any logged-in user, regardless of role (Dashboard, Profile, Reports).
// Replaces the old "PrivateRoutes" name for clarity against the
// role-specific guards below.
export function AuthenticatedRoutes() {
  const { token } = useAuth();
  return token ? <Outlet /> : <Navigate to="/login" replace />;
}
