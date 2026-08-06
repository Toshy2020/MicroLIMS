export type Role = "SystemAdministrator" | "SectionHead" | "Reviewer" | "Analyst";

export interface LoginResult {
  token: string;
  refreshToken: string;
  role: Role;
  mustChangePassword: boolean;
}

export interface CurrentUserInfo {
  userId: number;
  username: string;
  fullName: string;
  role: Role;
  lastLoginAt: string | null;
  passwordChangedAt: string | null;
  mustChangePassword: boolean;
}
