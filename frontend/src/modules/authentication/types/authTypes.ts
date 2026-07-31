export type Role = "SystemAdministrator" | "SectionHead" | "Reviewer" | "Analyst";

export interface LoginResult {
  token: string;
  refreshToken: string;
  role: Role;
}
