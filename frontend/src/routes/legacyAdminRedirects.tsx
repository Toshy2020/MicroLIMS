import { generatePath, Navigate, useLocation, useParams } from "react-router-dom";

// The administration pages moved under /admin. Old bookmarks, emails and
// in-page links keep working through these; ":id" style params carry over.
export const LEGACY_ADMIN_REDIRECTS: { from: string; to: string }[] = [
  { from: "/users", to: "/admin/users" },
  { from: "/roles", to: "/admin/roles" },
  { from: "/roles/new", to: "/admin/roles/new" },
  { from: "/roles/:id", to: "/admin/roles/:id" },
  { from: "/audit-search", to: "/admin/audit" },
  { from: "/error-monitoring", to: "/admin/error-log" }
];

export function LegacyPathRedirect({ to }: { to: string }) {
  const location = useLocation();
  const params = useParams();
  return <Navigate to={`${generatePath(to, params)}${location.search}`} replace />;
}
