import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

// Only reachable when logged OUT (e.g. /login) - redirects away if a
// token is already present.
export function PublicRoutes() {
  const { token } = useAuth();
  return token ? <Navigate to="/dashboard" replace /> : <Outlet />;
}
