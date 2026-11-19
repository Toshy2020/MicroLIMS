import { vi } from "vitest";
import type { MenuGroup } from "../../routes/menuConfig";

// An in-memory stand-in for the server's interface-state store. It sits at the
// services/ boundary; every hook, component and context above it is real.
export const uiStateStore = new Map<string, string>();

export const fakeUiStateService = {
  getAll: vi.fn(async () => Object.fromEntries(uiStateStore)),
  set: vi.fn(async (key: string, value: string) => {
    uiStateStore.set(key, value);
  }),
  remove: vi.fn(async (key: string) => {
    uiStateStore.delete(key);
  })
};

export function resetFakeUiState() {
  uiStateStore.clear();
  fakeUiStateService.getAll.mockImplementation(async () => Object.fromEntries(uiStateStore));
  fakeUiStateService.set.mockImplementation(async (key: string, value: string) => {
    uiStateStore.set(key, value);
  });
  fakeUiStateService.remove.mockImplementation(async (key: string) => {
    uiStateStore.delete(key);
  });
  fakeUiStateService.getAll.mockClear();
  fakeUiStateService.set.mockClear();
  fakeUiStateService.remove.mockClear();
}

export const TEST_MENU: MenuGroup[] = [
  {
    groupName: "OVERVIEW",
    items: [
      { label: "My Work", path: "/my-work", badge: "myWork" },
      { label: "Dashboard", path: "/dashboard" }
    ]
  },
  {
    groupName: "QUALITY & COMPLIANCE",
    items: [{ label: "Reports", path: "/reports" }]
  }
];

export function seedFavourites(value: unknown) {
  uiStateStore.set("nav.favourites", JSON.stringify(value));
}

export function storedFavourites(): unknown {
  const raw = uiStateStore.get("nav.favourites");
  return raw === undefined ? undefined : JSON.parse(raw);
}
