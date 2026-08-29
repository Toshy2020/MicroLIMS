import { createContext, useContext, useState, ReactNode } from "react";
import { apiClient } from "../services/apiClient";
import { authenticationService } from "../modules/authentication/services/authenticationService";

export type Role = "SystemAdministrator" | "SectionHead" | "Reviewer" | "Analyst";

export interface LoginData {
  token: string;
  refreshToken: string;
  username: string;
  role: Role;
  permissions: string[];
  fullName: string;
  jobTitle?: string | null;
  userId: number;
  mustChangePassword: boolean;
}

interface AuthState {
  username: string | null;
  role: Role | null;
  // Additive alongside role - same lifecycle (set at login, not synced on
  // apiClient's silent 401 token refresh, same as role itself isn't).
  permissions: string[];
  token: string | null;
  refreshToken: string | null;
  fullName: string | null;
  jobTitle: string | null;
  userId: number | null;
  mustChangePassword: boolean;
  login: (data: LoginData) => void;
  logout: () => void;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

function readStored<T>(key: string, fallback: T, parse: (raw: string) => T = (raw) => raw as unknown as T): T {
  const raw = localStorage.getItem(key);
  if (raw === null) return fallback;
  try {
    return parse(raw);
  } catch {
    return fallback;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(localStorage.getItem("microlims_token"));
  const [refreshToken, setRefreshToken] = useState<string | null>(localStorage.getItem("microlims_refresh_token"));
  const [username, setUsername] = useState<string | null>(localStorage.getItem("microlims_username"));
  const [role, setRole] = useState<Role | null>(localStorage.getItem("microlims_role") as Role | null);
  const [permissions, setPermissions] = useState<string[]>(readStored<string[]>("microlims_permissions", [], JSON.parse));
  const [fullName, setFullName] = useState<string | null>(localStorage.getItem("microlims_full_name"));
  const [jobTitle, setJobTitle] = useState<string | null>(localStorage.getItem("microlims_job_title"));
  const [userId, setUserId] = useState<number | null>(readStored<number | null>("microlims_user_id", null, Number));
  const [mustChangePassword, setMustChangePassword] = useState<boolean>(
    readStored<boolean>("microlims_must_change_password", false, (raw) => raw === "true")
  );

  const login = (data: LoginData) => {
    localStorage.setItem("microlims_token", data.token);
    localStorage.setItem("microlims_refresh_token", data.refreshToken);
    localStorage.setItem("microlims_username", data.username);
    localStorage.setItem("microlims_role", data.role);
    localStorage.setItem("microlims_permissions", JSON.stringify(data.permissions));
    localStorage.setItem("microlims_full_name", data.fullName);
    if (data.jobTitle) {
      localStorage.setItem("microlims_job_title", data.jobTitle);
    } else {
      localStorage.removeItem("microlims_job_title");
    }
    localStorage.setItem("microlims_user_id", String(data.userId));
    localStorage.setItem("microlims_must_change_password", String(data.mustChangePassword));

    setToken(data.token);
    setRefreshToken(data.refreshToken);
    setUsername(data.username);
    setRole(data.role);
    setPermissions(data.permissions);
    setFullName(data.fullName);
    setJobTitle(data.jobTitle ?? null);
    setUserId(data.userId);
    setMustChangePassword(data.mustChangePassword);
  };

  const clearLocalState = () => {
    localStorage.removeItem("microlims_token");
    localStorage.removeItem("microlims_refresh_token");
    localStorage.removeItem("microlims_username");
    localStorage.removeItem("microlims_role");
    localStorage.removeItem("microlims_permissions");
    localStorage.removeItem("microlims_full_name");
    localStorage.removeItem("microlims_job_title");
    localStorage.removeItem("microlims_user_id");
    localStorage.removeItem("microlims_must_change_password");
    setToken(null);
    setRefreshToken(null);
    setUsername(null);
    setRole(null);
    setPermissions([]);
    setFullName(null);
    setJobTitle(null);
    setUserId(null);
    setMustChangePassword(false);
  };

  const logout = () => {
    // Fire-and-forget: a network failure must never trap someone logged in.
    // The Authorization header is attached explicitly (rather than relying
    // on apiClient's request interceptor) because that interceptor reads
    // localStorage on a later microtask - by then clearLocalState() below
    // would already have removed the token, sending the call unauthenticated.
    if (token) {
      apiClient.post("/auth/logout", null, { headers: { Authorization: `Bearer ${token}` } }).catch(() => {});
    }
    clearLocalState();
  };

  const refresh = async () => {
    const info = await authenticationService.me();
    localStorage.setItem("microlims_full_name", info.fullName);
    if (info.jobTitle) {
      localStorage.setItem("microlims_job_title", info.jobTitle);
    } else {
      localStorage.removeItem("microlims_job_title");
    }
    localStorage.setItem("microlims_must_change_password", String(info.mustChangePassword));
    setFullName(info.fullName);
    setJobTitle(info.jobTitle ?? null);
    setMustChangePassword(info.mustChangePassword);
  };

  return (
    <AuthContext.Provider
      value={{ token, refreshToken, username, role, permissions, fullName, jobTitle, userId, mustChangePassword, login, logout, refresh }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

// Opt-in permission check for new/migrated features - existing role-based
// gates (role === "X") are untouched and keep working exactly as before.
export function useHasPermission(code: string): boolean {
  const { permissions } = useAuth();
  return permissions.includes(code);
}
