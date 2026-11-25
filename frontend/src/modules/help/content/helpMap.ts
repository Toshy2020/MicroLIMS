import { CHAPTERS } from "./guideLoader";

export interface HelpTarget {
  chapterId: string;
  heading?: string;
}

const INTRODUCTION = "01-introduction";

// Which guide chapter explains which page. Keys are menu paths exactly as in
// routes/menuConfig.ts (query string included); helpMap.test.ts fails with
// the missing paths when a menu page is added without an entry. Pages that
// are not in the menu (account pages) and bare variants of query-string menu
// paths are listed after the menu paths.
export const HELP_MAP: Record<string, HelpTarget> = {
  "/my-work": { chapterId: "45-my-work" },
  "/dashboard": { chapterId: "08-dashboard" },
  "/receiving": { chapterId: "12-sample-receiving" },
  "/receiving/tracking": { chapterId: "11-sample-management" },

  // Microbiology laboratory
  "/sections/microbiology-laboratory": { chapterId: "14-testing-workspace" },
  "/microbiology/workspace": { chapterId: "14-testing-workspace" },
  "/laboratory-configuration/media": { chapterId: "22-gpt-media" },
  "/preparation?lab=MICRO": { chapterId: "28-solution-standard-preparation" },
  "/laboratory-configuration/cryovials": { chapterId: "26-reference-strains" },
  "/laboratory-configuration/biological-waste": { chapterId: "26-reference-strains" },
  "/inventory/materials?lab=MICRO": { chapterId: "29-material-stock" },
  "/inventory/equipment?lab=MICRO": { chapterId: "30-equipment" },

  // Microbiology configuration
  "/sections/microbiology-configuration": { chapterId: "32-laboratory-configuration" },
  "/laboratory-configuration/test-master": { chapterId: "31-items-test-master-specifications" },
  "/laboratory-configuration/environmental-monitoring": { chapterId: "23-environmental-monitoring" },
  "/laboratory-configuration/solution-master?lab=MICRO": { chapterId: "28-solution-standard-preparation" },
  "/laboratory-configuration/equipment": { chapterId: "30-equipment" },
  "/laboratory-configuration/label-formats": { chapterId: "32-laboratory-configuration" },

  // Physicochemical laboratory
  "/sections/physicochemical-laboratory": { chapterId: "27-physicochemical-testing" },
  "/physicochemical/workspace": { chapterId: "14-testing-workspace" },
  "/physicochemical/rm-pm-workspace": { chapterId: "14-testing-workspace" },
  "/preparation": { chapterId: "28-solution-standard-preparation" },
  "/working-standards": { chapterId: "27-physicochemical-testing" },
  "/hplc-workspace": { chapterId: "27-physicochemical-testing" },
  "/gc-workspace": { chapterId: "27-physicochemical-testing" },
  "/icp-workspace": { chapterId: "27-physicochemical-testing" },
  "/inventory/materials?lab=FP": { chapterId: "29-material-stock" },
  "/inventory/equipment?lab=FP": { chapterId: "30-equipment" },

  // Physicochemical configuration
  "/sections/physicochemical-configuration": { chapterId: "32-laboratory-configuration" },
  "/laboratory-configuration/fp-test-master": { chapterId: "31-items-test-master-specifications" },
  "/laboratory-configuration/rm-pm-test-master": { chapterId: "31-items-test-master-specifications" },
  "/laboratory-configuration/equation-types": { chapterId: "27-physicochemical-testing" },
  "/laboratory-configuration/fp-instruments": { chapterId: "27-physicochemical-testing" },
  "/laboratory-configuration/columns": { chapterId: "27-physicochemical-testing" },
  "/laboratory-configuration/solution-master": { chapterId: "28-solution-standard-preparation" },
  "/laboratory-configuration/hplc-methods": { chapterId: "27-physicochemical-testing" },

  // General laboratory configuration
  "/laboratory-configuration/items": { chapterId: "31-items-test-master-specifications" },
  "/laboratory-configuration/master-testing-materials": { chapterId: "32-laboratory-configuration" },
  "/laboratory-configuration/receiving-configuration": { chapterId: "32-laboratory-configuration" },
  "/laboratory-configuration/master-data-workbook": { chapterId: "32-laboratory-configuration" },
  "/laboratory-configuration/water": { chapterId: "24-water-monitoring" },
  "/laboratory-configuration/after-cleaning": { chapterId: "25-after-cleaning" },
  "/laboratory-configuration/working-hours": { chapterId: "32-laboratory-configuration" },
  "/laboratory-configuration/escalation-rules": { chapterId: "32-laboratory-configuration" },

  // Quality and compliance
  "/sections/document-control": { chapterId: "38-document-control" },
  "/document-control": { chapterId: "38-document-control" },
  "/document-control/my-reading-list": { chapterId: "38-document-control" },
  "/document-control/training-matrix": { chapterId: "38-document-control" },
  "/document-control/compliance-dashboard": { chapterId: "38-document-control" },
  "/document-control/library": { chapterId: "38-document-control" },
  "/document-control/audit": { chapterId: "38-document-control" },
  "/document-control/reassignments": { chapterId: "38-document-control" },
  "/document-control/configuration": { chapterId: "38-document-control" },
  "/competency/matrix": { chapterId: "38-document-control", heading: "38.16 Qualification Matrix" },
  "/competency/my": { chapterId: "38-document-control", heading: "38.17 My Qualifications" },
  "/reports": { chapterId: "34-reports-kpis" },
  "/monitoring-plan": { chapterId: "23-environmental-monitoring" },
  "/oos-tracking": { chapterId: "20-oos-tracking" },

  // Administration
  "/admin/users": { chapterId: "39-users-roles-administration" },
  "/admin/roles": { chapterId: "39-users-roles-administration" },
  "/admin/audit": { chapterId: "35-audit-trail" },
  "/audit-trail-reviews": { chapterId: "35-audit-trail" },
  "/admin/error-log": { chapterId: "40-error-monitoring" },
  "/admin/settings": { chapterId: "41-integrations" },
  "/admin/organization": { chapterId: "39-users-roles-administration" },
  "/admin/jobs": { chapterId: "41-integrations" },
  "/admin/access-review": { chapterId: "39-users-roles-administration" },

  // Not in the menu: bare variants of query-string menu paths, account pages
  "/inventory/materials": { chapterId: "29-material-stock" },
  "/inventory/equipment": { chapterId: "30-equipment" },
  "/profile": { chapterId: "05-login-session-password" },
  "/change-password": { chapterId: "05-login-session-password" },
  "/set-signing-password": { chapterId: "37-electronic-signatures" },
  "/notifications": { chapterId: "09-notifications-messages" },
  "/messages": { chapterId: "09-notifications-messages" },
  "/discussions": { chapterId: "09-notifications-messages" },
  "/receiving-testing": { chapterId: "14-testing-workspace" }
};

// Parent sections for locations no key owns.
const SECTION_FALLBACKS: [string, string][] = [
  ["/oos-tracking", "20-oos-tracking"],
  ["/laboratory-configuration", "32-laboratory-configuration"],
  ["/sections", "07-navigation"],
  ["/admin", "39-users-roles-administration"],
  ["/document-control", "38-document-control"],
  ["/physicochemical", "27-physicochemical-testing"],
  ["/microbiology", "14-testing-workspace"]
];

// Same rule as findNavTrail (routes/navigation.ts): a key owns the location
// when it is the path or an ancestor of it and every query parameter the key
// names matches; exact beats deeper, longer beats shorter, more parameters
// beat fewer.
function matchScore(key: string, pathname: string, search: URLSearchParams): number {
  const [path, query] = key.split("?");
  const exact = pathname === path;
  if (!exact && !pathname.startsWith(`${path}/`)) return -1;
  let paramCount = 0;
  if (query) {
    for (const [name, value] of new URLSearchParams(query)) {
      if (search.get(name) !== value) return -1;
      paramCount += 1;
    }
  }
  return path.length * 10 + paramCount + (exact ? 100_000 : 0);
}

export function chapterForPath(pathname: string, search: string): HelpTarget {
  const params = new URLSearchParams(search);
  let best: HelpTarget | null = null;
  let bestScore = -1;
  for (const [key, target] of Object.entries(HELP_MAP)) {
    const score = matchScore(key, pathname, params);
    if (score > bestScore) {
      bestScore = score;
      best = target;
    }
  }
  if (best) return { ...best };
  for (const [prefix, chapterId] of SECTION_FALLBACKS) {
    if (pathname === prefix || pathname.startsWith(`${prefix}/`)) return { chapterId };
  }
  return { chapterId: CHAPTERS.find((c) => c.id === INTRODUCTION)?.id ?? INTRODUCTION };
}
