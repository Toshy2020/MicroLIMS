import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { ItemsPage } from "./ItemsPage";
import { ItemService, type Item } from "./services/ItemService";
import { ItemDocumentService } from "./services/ItemDocumentService";
import { ItemPreparationConfigurationService } from "../../testPreparation/services/ItemPreparationConfigurationService";

// Only the HTTP service layer is mocked; the real page, table, workspace and
// tabs run inside the real AuthProvider and router (CLAUDE.md Rule 4.5).
vi.mock("./services/ItemService", () => ({
  ItemService: { getAll: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), freeze: vi.fn(), unfreeze: vi.fn() }
}));

vi.mock("./services/ItemDocumentService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./services/ItemDocumentService")>();
  return { ...actual, ItemDocumentService: { getDocumentsForItem: vi.fn() } };
});

vi.mock("../../testPreparation/services/ItemPreparationConfigurationService", () => ({
  ItemPreparationConfigurationService: { get: vi.fn(), getUnmapped: vi.fn() }
}));

vi.mock("../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: new Proxy(actual.masterDataOptions, {
      get: (target, prop) => (typeof prop === "string" && prop.startsWith("get") ? vi.fn().mockResolvedValue([]) : Reflect.get(target, prop))
    })
  };
});

const makeItem = (id: number, name: string, code: string): Item => ({
  id,
  version: 1,
  name,
  code,
  category: "RawMaterial",
  sopNumber: "",
  isActive: true,
  assignedTests: [],
  specifications: [],
  minSampleQuantity: null,
  minRetestSampleQuantity: null,
  minSampleQuantityUnit: null
});

const riboflavin = makeItem(1, "Riboflavin", "RM-1");
const zinc = makeItem(2, "Zinc Oxide", "RM-2");

// Unsigned dummy token (test data only) so AuthProvider parses the Items.Manage claim.
function seedToken(permissions: string[]) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

function LocationProbe() {
  return <div data-testid="search">{useLocation().search}</div>;
}

function renderAt(url: string) {
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[url]}>
          <ItemsPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const search = () => new URLSearchParams(screen.getByTestId("search").textContent ?? "");
const tab = (name: RegExp) => screen.getByRole("tab", { name });

describe("ItemsPage selected item and tab in the URL", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedToken(["Items.Manage"]);
    vi.mocked(ItemService.getAll).mockResolvedValue([riboflavin, zinc]);
    vi.mocked(ItemDocumentService.getDocumentsForItem).mockResolvedValue([]);
    vi.mocked(ItemPreparationConfigurationService.get).mockResolvedValue(null);
    vi.mocked(ItemPreparationConfigurationService.getUnmapped).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("opens the workspace and writes ?item= when an item is clicked", async () => {
    const user = userEvent.setup();
    renderAt("/laboratory-configuration/items");

    await user.click(await screen.findByText("Riboflavin"));

    expect(await screen.findByRole("button", { name: "Close workspace" })).toBeTruthy();
    expect(search().get("item")).toBe("1");
    expect(search().has("tab")).toBe(false);
  });

  it("opens the workspace on the Specifications tab for a deep link", async () => {
    renderAt("/laboratory-configuration/items?item=2&tab=specifications");

    expect(await screen.findByRole("button", { name: "Close workspace" })).toBeTruthy();
    expect(tab(/^Specifications/).getAttribute("aria-selected")).toBe("true");
    expect(tab(/^Overview/).getAttribute("aria-selected")).toBe("false");
    expect(search().get("item")).toBe("2");
  });

  it("writes tab= when the tab changes and drops it when Overview is chosen", async () => {
    const user = userEvent.setup();
    renderAt("/laboratory-configuration/items?item=1");
    await screen.findByRole("button", { name: "Close workspace" });

    await user.click(tab(/^Audit History/));
    expect(search().get("tab")).toBe("audit");
    expect(tab(/^Audit History/).getAttribute("aria-selected")).toBe("true");

    await user.click(tab(/^Overview/));
    expect(search().has("tab")).toBe(false);
    expect(search().get("item")).toBe("1");
    expect(tab(/^Overview/).getAttribute("aria-selected")).toBe("true");
  });

  it("removes item and tab when the workspace is closed", async () => {
    const user = userEvent.setup();
    renderAt("/laboratory-configuration/items?item=1&tab=documents");

    await user.click(await screen.findByRole("button", { name: "Close workspace" }));

    expect(search().has("item")).toBe(false);
    expect(search().has("tab")).toBe(false);
    expect(screen.queryByRole("button", { name: "Close workspace" })).toBeNull();
  });

  it("shows the plain list and drops the parameter when ?item= names an unknown item", async () => {
    renderAt("/laboratory-configuration/items?item=999&tab=audit");

    expect(await screen.findByText("Riboflavin")).toBeTruthy();
    await waitFor(() => expect(search().has("item")).toBe(false));
    expect(search().has("tab")).toBe(false);
    expect(screen.queryByRole("button", { name: "Close workspace" })).toBeNull();
  });

  it("keeps the deep-linked item while the list is still loading", async () => {
    let resolveItems: (items: Item[]) => void = () => undefined;
    vi.mocked(ItemService.getAll).mockReturnValue(new Promise<Item[]>((resolve) => { resolveItems = resolve; }));
    renderAt("/laboratory-configuration/items?item=2");

    expect(search().get("item")).toBe("2");
    resolveItems([riboflavin, zinc]);

    expect(await screen.findByRole("button", { name: "Close workspace" })).toBeTruthy();
    expect(search().get("item")).toBe("2");
  });

  it("falls back to Overview for an unknown tab value", async () => {
    renderAt("/laboratory-configuration/items?item=1&tab=nonsense");

    await screen.findByRole("button", { name: "Close workspace" });
    expect(tab(/^Overview/).getAttribute("aria-selected")).toBe("true");
  });

  it("preserves unrelated parameters when selecting and closing", async () => {
    const user = userEvent.setup();
    renderAt("/laboratory-configuration/items?foo=1");

    await user.click(await screen.findByText("Zinc Oxide"));
    await screen.findByRole("button", { name: "Close workspace" });
    expect(search().get("foo")).toBe("1");
    expect(search().get("item")).toBe("2");

    await user.click(screen.getByRole("button", { name: "Close workspace" }));
    expect(search().get("foo")).toBe("1");
    expect(search().has("item")).toBe(false);
  });
});
