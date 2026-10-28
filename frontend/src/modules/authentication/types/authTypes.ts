export type Role = "SystemAdministrator" | "SectionHead" | "Reviewer" | "Analyst";

export interface LoginResult {
  token: string;
  refreshToken: string;
  role: Role;
  permissions: string[];
  mustChangePassword: boolean;
  // The role's display name (Role.Name); `role` above is its base type.
  roleName?: string | null;
}

export interface CurrentUserInfo {
  userId: number;
  username: string;
  fullName: string;
  role: Role;
  roleName?: string | null;
  jobTitle?: string | null;
  lastLoginAt: string | null;
  passwordChangedAt: string | null;
  mustChangePassword: boolean;
}
