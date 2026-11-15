import type { MenuGroup } from "../../routes/menuConfig";

// A favourite is a pointer, never a copy: labels and routes are resolved at
// render from the user's own menu and saved-views list, so a withdrawn page or
// view cannot leak a stale name, and the stored value stays small.
export type Favourite = { t: "p"; path: string } | { t: "v"; table: string; view: number };

export interface ResolvedFavourite {
  fav: Favourite;
  label: string;
  to: string | null;
  available: boolean;
}

export const FAVOURITES_KEY = "nav.favourites";
export const MAX_FAVOURITES = 15;
// The interface-state store refuses values over 2 KB; stay under it.
export const MAX_FAVOURITES_BYTES = 2000;
export const FAVOURITES_FULL_MESSAGE = `You can have up to ${MAX_FAVOURITES} favourites`;
export const FAVOURITES_TOO_LONG_MESSAGE = "Your favourites no longer fit in the store. Remove one first.";

// Saved views live on a table; the favourite opens the page that hosts it.
// Only tables with their own plain route can be offered a star.
const TABLE_ROUTES: Record<string, string> = {
  "samples.receiving": "/receiving",
  "documentControl.library": "/document-control/library",
  "inventory.materials": "/inventory/materials",
  "inventory.equipment": "/inventory/equipment",
  "labConfig.cryovials": "/laboratory-configuration/cryovials",
  "labConfig.mediaLots": "/laboratory-configuration/media"
};

export function viewRoute(table: string): string | null {
  return TABLE_ROUTES[table] ?? null;
}

export function viewFavouriteTarget(table: string, view: number): string | null {
  const route = viewRoute(table);
  if (!route) return null;
  return `${route}${route.includes("?") ? "&" : "?"}view=${view}`;
}

export function sameFavourite(a: Favourite, b: Favourite): boolean {
  if (a.t === "p" && b.t === "p") return a.path === b.path;
  if (a.t === "v" && b.t === "v") return a.table === b.table && a.view === b.view;
  return false;
}

export function favouriteKey(f: Favourite): string {
  return f.t === "p" ? `p:${f.path}` : `v:${f.table}:${f.view}`;
}

export function serialiseFavourites(items: readonly Favourite[]): string {
  return JSON.stringify(items);
}

export function byteLength(value: string): number {
  return new TextEncoder().encode(value).length;
}

function isFavourite(x: unknown): x is Favourite {
  if (typeof x !== "object" || x === null) return false;
  const o = x as Record<string, unknown>;
  if (o.t === "p") return typeof o.path === "string" && o.path.length > 0;
  if (o.t === "v") return typeof o.table === "string" && typeof o.view === "number" && Number.isInteger(o.view);
  return false;
}

// Tolerant: a corrupt or hand-edited value yields what is valid, never an error page.
export function parseFavourites(value: string | null): Favourite[] {
  if (!value) return [];
  let raw: unknown;
  try {
    raw = JSON.parse(value);
  } catch {
    return [];
  }
  if (!Array.isArray(raw)) return [];
  const out: Favourite[] = [];
  for (const entry of raw) {
    if (isFavourite(entry) && !out.some((f) => sameFavourite(f, entry))) out.push(entry);
  }
  return out.slice(0, MAX_FAVOURITES);
}

export interface MenuPage {
  label: string;
  path: string;
}

// Every page the user's menu offers (parents with a landing page and children).
export function menuPages(groups: readonly MenuGroup[]): Map<string, MenuPage> {
  const pages = new Map<string, MenuPage>();
  for (const group of groups) {
    for (const item of group.items) {
      if (item.path) pages.set(item.path, { label: item.label, path: item.path });
      for (const child of item.children ?? []) {
        if (child.path) pages.set(child.path, { label: child.label, path: child.path });
      }
    }
  }
  return pages;
}
