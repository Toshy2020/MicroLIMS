import type { LoginData } from "../../contexts/AuthContext";
import { authenticationService, type LoginResult } from "./services/authenticationService";

/**
 * Persists final authentication tokens and loads `/auth/me` into `AuthContext`.
 * Used by both `LoginPage` and `MfaEnrolmentPage` once MFA checks succeed.
 */
export async function completeSignInSession(
  result: LoginResult,
  login: (data: LoginData) => void,
  fallbackUsername = ""
): Promise<void> {
  if (!result.token || !result.refreshToken || !result.role) {
    throw new Error("Incomplete authentication response.");
  }
  localStorage.setItem("microlims_token", result.token);
  const me = await authenticationService.me();
  login({
    token: result.token,
    refreshToken: result.refreshToken,
    username: me.username ?? fallbackUsername,
    role: result.role,
    roleName: result.roleName ?? me.roleName ?? null,
    permissions: result.permissions ?? [],
    fullName: me.fullName,
    jobTitle: me.jobTitle ?? null,
    userId: me.userId,
    mustChangePassword: Boolean(result.mustChangePassword)
  });
}
