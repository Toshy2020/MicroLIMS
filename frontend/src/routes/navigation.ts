import type { MenuGroup, MenuItem } from "./menuConfig";

// Where the current location sits in the menu: its group, the collapsible
// area it belongs to (if any) and the menu link that owns it. The sidebar
// highlight, the breadcrumb and the browser tab title all read this one
// answer, so they can never disagree about where the user is.
export interface NavTrail {
  group: string;
  parent?: MenuItem;
  item: MenuItem;
  // False when the location is a page below the item (a run, a record, a
  // wizard step) rather than the item's own page.
  exact: boolean;
}

// Scores how well a menu path owns the location: -1 when it does not, else
// longer and exact paths win. A menu path may carry a query string
// ("/inventory/materials?lab=MICRO"); every parameter it names must match,
// so the Microbiology and Physicochemical stock links highlight separately.
function matchScore(menuPath: string, pathname: string, search: URLSearchParams): number {
  const [path, query] = menuPath.split("?");
  const exact = pathname === path;
  if (!exact && !pathname.startsWith(`${path}/`)) return -1;
  let paramCount = 0;
  if (query) {
    for (const [key, value] of new URLSearchParams(query)) {
      if (search.get(key) !== value) return -1;
      paramCount += 1;
    }
  }
  return path.length * 10 + paramCount + (exact ? 100_000 : 0);
}

export function findNavTrail(groups: MenuGroup[], pathname: string, searchString: string): NavTrail | null {
  const search = new URLSearchParams(searchString);
  const candidates: { group: string; parent?: MenuItem; item: MenuItem }[] = [];
  for (const group of groups) {
    for (const item of group.items) {
      candidates.push({ group: group.groupName, item });
      item.children?.forEach((child) => candidates.push({ group: group.groupName, parent: item, item: child }));
    }
  }
  let best: NavTrail | null = null;
  let bestScore = -1;
  for (const c of candidates) {
    if (!c.item.path) continue;
    const score = matchScore(c.item.path, pathname, search);
    if (score > bestScore) {
      bestScore = score;
      best = { ...c, exact: score >= 100_000 };
    }
  }
  return best;
}

// Pages reached from the profile menu rather than the sidebar - they still
// get a breadcrumb and a tab title.
const ACCOUNT_PAGES: Record<string, string> = {
  "/profile": "My Profile",
  "/change-password": "Change Password",
  "/messages": "Messages",
  "/discussions": "Discussions"
};

export function accountPageTrail(pathname: string): NavTrail | null {
  const match = Object.keys(ACCOUNT_PAGES).find((p) => pathname === p || pathname.startsWith(`${p}/`));
  if (!match) return null;
  return { group: "ACCOUNT", item: { label: ACCOUNT_PAGES[match], path: match }, exact: pathname === match };
}

// Menu groups are stored upper-case for the sidebar's overline labels;
// breadcrumbs and titles read better in title case.
export function groupLabel(groupName: string): string {
  return groupName
    .toLowerCase()
    .split(" ")
    .map((w) => (w === "&" ? w : w.charAt(0).toUpperCase() + w.slice(1)))
    .join(" ");
}
