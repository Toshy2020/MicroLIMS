import { createContext, useContext } from "react";

export type DashboardLabCode = "MICRO" | "FP";

export const LAB_LABELS: Record<DashboardLabCode, string> = {
  MICRO: "Microbiology Laboratory",
  FP: "Physicochemical Laboratory"
};

const WORKSPACE_PATHS: Record<DashboardLabCode, string> = {
  MICRO: "/microbiology/workspace",
  FP: "/physicochemical/workspace"
};

// Which laboratory a dashboard view is for. Every dashboard figure, panel
// and link reads this, so a Microbiology dashboard never shows or links to
// Physicochemical activity and vice versa. null = the user belongs to no
// laboratory (or labs have not loaded); dashboards then behave as before.
export interface DashboardLab {
  code: DashboardLabCode | null;
  // Workspace link for this laboratory. `query` is what used to follow
  // "/receiving-testing", e.g. "?status=UnderReview". /receiving-testing
  // itself always opened the first lab the user belongs to (Microbiology),
  // so a Physicochemical tile would have landed in the Microbiology
  // workspace.
  workspace: (query?: string) => string;
  isMicro: boolean;
  isPhyschem: boolean;
}

export function makeDashboardLab(code: DashboardLabCode | null): DashboardLab {
  const base = code ? WORKSPACE_PATHS[code] : "/receiving-testing";
  return {
    code,
    workspace: (query = "") => `${base}${query}`,
    isMicro: code === "MICRO",
    isPhyschem: code === "FP"
  };
}

const DashboardLabContext = createContext<DashboardLab>(makeDashboardLab(null));

export const DashboardLabProvider = DashboardLabContext.Provider;

export function useDashboardLab(): DashboardLab {
  return useContext(DashboardLabContext);
}

// Query-string suffix for the dashboard API calls ("?lab=FP" / "&lab=FP").
export function labQuery(code: DashboardLabCode | null, joiner: "?" | "&" = "?"): string {
  return code ? `${joiner}lab=${code}` : "";
}
