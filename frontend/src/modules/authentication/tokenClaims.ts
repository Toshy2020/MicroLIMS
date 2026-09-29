import { Role } from "./types/authTypes";

export interface TokenClaims {
  role: Role | null;
  permissions: string[];
}

// Reads the role and permission claims the backend puts in the access
// token. Used at login and again after every silent refresh, so the menus
// and page gates follow the permissions the server enforces now rather
// than the ones cached when the user logged in.
export function readTokenClaims(token: string | null): TokenClaims {
  if (!token) return { role: null, permissions: [] };
  try {
    const base64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    const payload = JSON.parse(atob(base64));
    const role = (payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? payload.role ?? null) as Role | null;
    // System.IdentityModel.Tokens.Jwt collapses a claim type down to a
    // plain string (not a 1-element array) when only one claim of that
    // type is present - a custom role granted exactly one permission
    // would hit this, so never assume the array shape.
    const raw = payload.permission;
    const permissions: string[] = Array.isArray(raw) ? raw : raw ? [raw] : [];
    return { role, permissions };
  } catch {
    return { role: null, permissions: [] };
  }
}

// Fired by apiClient after it stores a silently refreshed token.
export const TOKEN_REFRESHED_EVENT = "microlims:token-refreshed";
