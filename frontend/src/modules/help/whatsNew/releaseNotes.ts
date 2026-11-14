import type { RoleType } from "../tour/tourSteps";

export interface ReleaseNoteItem {
  roles: RoleType[] | "all";
  text: string;
  guide?: { chapterId: string; heading?: string };
}

export interface ReleaseNote {
  version: string;
  date: string;
  items: ReleaseNoteItem[];
}

export const WHATS_NEW_STATE_KEY = "whatsNew.lastSeen";

export const RELEASE_NOTES: ReleaseNote[] = [
  {
    version: "0.1.0",
    date: "2026-11-14",
    items: [
      {
        roles: "all",
        text: "My Work brings your assigned tests, reading tasks, and approvals into one queue grouped by urgency.",
        guide: { chapterId: "45-my-work" }
      },
      {
        roles: "all",
        text: "Choose visible columns, adjust row density, and save personal or shared table views across registers."
      },
      {
        roles: ["Analyst", "Reviewer", "SectionHead"],
        text: "Paste rows from a spreadsheet into receiving and location-count grids and review a preview before filling the form.",
        guide: { chapterId: "15-result-entry" }
      },
      {
        roles: ["Analyst", "SectionHead"],
        text: "Entry forms pre-fill your last chosen equipment and media lot, and ask you to confirm pre-filled lots before saving.",
        guide: { chapterId: "14-testing-workspace" }
      },
      {
        roles: "all",
        text: "Press F1 or open Help to read the guide chapter for the current page, search the guide, or replay your role tour.",
        guide: { chapterId: "06-user-interface" }
      }
    ]
  }
];

function parseVersionParts(version: string): number[] {
  const core = version.trim().replace(/^v/i, "").split(/[+-]/)[0] ?? "";
  if (!core) return [0];
  return core.split(".").map((segment) => {
    const parsed = Number.parseInt(segment, 10);
    return Number.isNaN(parsed) ? 0 : parsed;
  });
}

/**
 * Numeric dotted version comparison.
 * Returns a positive number when `a > b`, `0` when equal, and a negative number when `a < b`.
 */
export function compareVersions(a: string, b: string): number {
  const partsA = parseVersionParts(a);
  const partsB = parseVersionParts(b);
  const length = Math.max(partsA.length, partsB.length);
  for (let i = 0; i < length; i += 1) {
    const numA = partsA[i] ?? 0;
    const numB = partsB[i] ?? 0;
    if (numA !== numB) return numA - numB;
  }
  return 0;
}

/**
 * Returns true when `currentVersion` is newer than `lastSeenVersion`.
 * An absent (`null`, `undefined`, or blank) stored value counts as older.
 */
export function isNewerVersion(currentVersion: string, lastSeenVersion: string | null | undefined): boolean {
  if (!lastSeenVersion || !lastSeenVersion.trim()) return true;
  return compareVersions(currentVersion, lastSeenVersion) > 0;
}

/** Returns the items in a release note visible to the given role. */
export function itemsForRole(note: ReleaseNote, role: RoleType | null | undefined): ReleaseNoteItem[] {
  return note.items.filter(
    (item) => item.roles === "all" || (role != null && item.roles.includes(role))
  );
}
