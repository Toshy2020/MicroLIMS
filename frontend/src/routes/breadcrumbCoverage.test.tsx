import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ThemeModeProvider } from "../theme/ThemeModeContext";
import { BenchModeProvider } from "../theme/BenchModeContext";
import { AuthProvider } from "../contexts/AuthContext";
import { PERMISSIONS } from "./routes";
import { AppRoutes } from "./AppRoutes";
import appRoutesSource from "./AppRoutes.tsx?raw";
import { CHAPTERS } from "../modules/help/content/guideLoader";

// Only the network boundary is replaced: every request stays pending, so each
// page is mounted for real and is in its loading state. A page that calls
// useBreadcrumb(null) while loading shows the skeleton crumb at once; a page that
// forgets the hook shows no page crumb at all.
vi.mock("../services/apiClient", () => {
  const pending = () => new Promise(() => {});
  return { apiClient: { get: vi.fn(pending), post: vi.fn(pending), put: vi.fn(pending), patch: vi.fn(pending), delete: vi.fn(pending) } };
});

vi.mock("../services/laboratorySectionService", () => ({
  laboratorySectionService: { getMySections: vi.fn().mockResolvedValue([]) }
}));

vi.mock("../modules/authentication/services/mfaService", () => ({
  mfaService: { getStatus: vi.fn(() => new Promise(() => {})) }
}));

// Routes with ":" that are deliberately not given a page crumb, with the reason.
const EXCLUDED: Record<string, string> = {
  "/samples/:id/report": "Printable report: rendered outside MainLayout, so it has no breadcrumb bar.",
  "/samples/:id/coa": "Printable certificate: rendered outside MainLayout, so it has no breadcrumb bar.",
  "/media/:id/report": "Printable report: rendered outside MainLayout, so it has no breadcrumb bar.",
  "/cryovials/:id/report": "Printable report: rendered outside MainLayout, so it has no breadcrumb bar.",
  "/sections/:slug": "A section is itself a menu item: the trail already ends on the page, so a page crumb would repeat it."
};

// Every path="..." in AppRoutes.tsx that holds a parameter. Paths nested under
// /admin are written relative ("roles/:id").
function parameterisedRoutes(): string[] {
  const found = new Set<string>();
  for (const m of appRoutesSource.matchAll(/path="([^"]*:[^"]*)"/g)) {
    found.add(m[1].startsWith("/") ? m[1] : `/admin/${m[1]}`);
  }
  return [...found];
}

const PARAM_VALUES: Record<string, string> = {
  slug: "overview",
  chapterId: CHAPTERS[0]?.id ?? "overview"
};

function concretePath(route: string): string {
  return route.replace(/:(\w+)/g, (_, name: string) => PARAM_VALUES[name] ?? "1");
}

function seedSession() {
  const payload = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead",
      permission: Object.values(PERMISSIONS)
    })
  );
  localStorage.setItem("microlims_token", `h.${payload}.s`);
  localStorage.setItem("microlims_username", "sec.head");
  localStorage.setItem("microlims_full_name", "Section Head");
}

// The last crumb is the page crumb: marked as the current page, and not the only
// crumb unless the trail has no menu item above it (a group page).
function lastCrumbIsPageCrumb(): boolean {
  const nav = document.querySelector('nav[aria-label="Breadcrumb"]');
  if (!nav) return false;
  const items = nav.querySelectorAll("li");
  const last = items[items.length - 1];
  return !!last && last.querySelector('[aria-current="page"]') !== null;
}

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("breadcrumb coverage", () => {
  it("lists excluded routes that really exist in AppRoutes.tsx", () => {
    const routes = parameterisedRoutes();
    const stale = Object.keys(EXCLUDED).filter((r) => !routes.includes(r));
    expect(stale, `Excluded routes no longer in AppRoutes.tsx: ${stale.join(", ")}`).toEqual([]);
    expect(routes.length).toBeGreaterThan(Object.keys(EXCLUDED).length);
  });

  it(
    "every route in AppRoutes.tsx whose path contains ':' renders a last crumb other than the menu item",
    async () => {
      const covered = parameterisedRoutes().filter((r) => !(r in EXCLUDED));
      const missing: string[] = [];

      for (const route of covered) {
        seedSession();
        const { unmount } = render(
          <BenchModeProvider>
            <ThemeModeProvider>
              <AuthProvider>
                <MemoryRouter initialEntries={[concretePath(route)]}>
                  <AppRoutes />
                </MemoryRouter>
              </AuthProvider>
            </ThemeModeProvider>
          </BenchModeProvider>
        );
        try {
          await waitFor(() => expect(lastCrumbIsPageCrumb()).toBe(true), { timeout: 15000 });
        } catch {
          missing.push(route);
        }
        unmount();
        cleanup();
      }

      expect(
        missing,
        `Routes whose page does not call useBreadcrumb (no last crumb): ${missing.join(", ")}`
      ).toEqual([]);
    },
    600_000
  );
});
