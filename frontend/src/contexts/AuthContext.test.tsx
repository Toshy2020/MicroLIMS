import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook } from "@testing-library/react";
import { ReactNode } from "react";
import { AuthProvider, useAuth } from "./AuthContext";
import { TOKEN_REFRESHED_EVENT } from "../modules/authentication/tokenClaims";

vi.mock("../services/apiClient", () => ({ apiClient: { post: vi.fn(() => Promise.resolve({})) } }));
vi.mock("../modules/authentication/services/authenticationService", () => ({ authenticationService: { me: vi.fn() } }));

const wrapper = ({ children }: { children: ReactNode }) => <AuthProvider>{children}</AuthProvider>;

// An unsigned JWT-shaped string is enough: only the payload is read.
function tokenFor(role: string): string {
  const payload = btoa(JSON.stringify({ "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": role, permission: "Samples.View" }));
  return `h.${payload}.s`;
}

const loginData = {
  token: tokenFor("Analyst"), refreshToken: "r", username: "sam", role: "Analyst" as const,
  roleName: "Senior Microbiologist", permissions: ["Samples.View"], fullName: "Sam Lab", userId: 5, mustChangePassword: false
};

afterEach(() => { cleanup(); localStorage.clear(); });

describe("AuthProvider role name", () => {
  it("stores the role name at login and clears it at logout", () => {
    const { result } = renderHook(() => useAuth(), { wrapper });

    act(() => result.current.login(loginData));
    expect(result.current.roleName).toBe("Senior Microbiologist");
    expect(localStorage.getItem("microlims_role_name")).toBe("Senior Microbiologist");

    act(() => result.current.logout());
    expect(result.current.roleName).toBeNull();
    expect(localStorage.getItem("microlims_role_name")).toBeNull();
  });

  it("picks up a renamed role from a silently refreshed token", () => {
    const { result } = renderHook(() => useAuth(), { wrapper });
    act(() => result.current.login(loginData));

    // What apiClient stores after /auth/refresh answers a 401.
    localStorage.setItem("microlims_token", tokenFor("Analyst"));
    localStorage.setItem("microlims_role_name", "Lead Microbiologist");
    act(() => { window.dispatchEvent(new Event(TOKEN_REFRESHED_EVENT)); });

    expect(result.current.roleName).toBe("Lead Microbiologist");
  });
});
