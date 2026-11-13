export type Role = "SystemAdministrator" | "SectionHead" | "Reviewer" | "Analyst" | "QcManager" | "Qa";

export type SignInMethod = "Local" | "Directory";

export interface LoginResult {
  token?: string;
  refreshToken?: string;
  role?: Role;
  permissions?: string[];
  mustChangePassword?: boolean;
  signingPasswordRequired?: boolean;
  // The role's display name (Role.Name); `role` above is its base type.
  roleName?: string | null;
  mfaRequired?: boolean;
  mfaEnrolmentRequired?: boolean;
  mfaToken?: string;
  mfaEnrolmentDueAt?: string;
  deviceToken?: string;
  recoveryCodesRemaining?: number;
  recoveryCodes?: string[];
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
  signInMethod?: SignInMethod;
  signingPasswordSet?: boolean;
}
