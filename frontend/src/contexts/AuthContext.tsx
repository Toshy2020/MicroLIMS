import { createContext, useContext, useState, ReactNode } from "react";

export type Role = "SystemAdministrator" | "SectionHead" | "Reviewer" | "Analyst";

interface AuthState {
  username: string | null;
  role: Role | null;
  token: string | null;
  login: (token: string, username: string, role: Role) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(localStorage.getItem("microlims_token"));
  const [username, setUsername] = useState<string | null>(localStorage.getItem("microlims_username"));
  const [role, setRole] = useState<Role | null>(localStorage.getItem("microlims_role") as Role | null);

  const login = (newToken: string, newUsername: string, newRole: Role) => {
    localStorage.setItem("microlims_token", newToken);
    localStorage.setItem("microlims_username", newUsername);
    localStorage.setItem("microlims_role", newRole);
    setToken(newToken);
    setUsername(newUsername);
    setRole(newRole);
  };

  const logout = () => {
    localStorage.removeItem("microlims_token");
    localStorage.removeItem("microlims_username");
    localStorage.removeItem("microlims_role");
    setToken(null);
    setUsername(null);
    setRole(null);
  };

  return (
    <AuthContext.Provider value={{ token, username, role, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
