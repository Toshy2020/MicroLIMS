export type PhyschemArea = "fp" | "rmpm";

export const FP_WORKSPACE_PATH = "/physicochemical/workspace";
export const RMPM_WORKSPACE_PATH = "/physicochemical/rm-pm-workspace";

// Link about one sample: the workspace of that sample's category.
export function workspacePathForCategory(category?: string | null): string {
  return category === "RawMaterial" || category === "PackagingMaterial" ? RMPM_WORKSPACE_PATH : FP_WORKSPACE_PATH;
}

// Lab-level link: the first workspace the user has (fp first, else rmpm).
export function firstWorkspacePath(areas: readonly PhyschemArea[]): string {
  return !areas.includes("fp") && areas.includes("rmpm") ? RMPM_WORKSPACE_PATH : FP_WORKSPACE_PATH;
}
