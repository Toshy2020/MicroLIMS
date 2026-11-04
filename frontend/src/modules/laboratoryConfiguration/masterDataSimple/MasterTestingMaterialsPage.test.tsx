import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { MasterTestingMaterialsPage } from "./MasterTestingMaterialsPage";

vi.mock("./MaterialMasterPage", () => ({
  MaterialMasterPage: ({ categories, embedded }: { categories?: string[]; embedded?: boolean }) => (
    <div data-testid="material-master" data-embedded={String(embedded)}>{categories?.join(",")}</div>
  )
}));
vi.mock("../media/MediaConfigurationPage", () => ({
  MediaConfigurationPage: () => <div data-testid="media-configuration" />
}));
vi.mock("./ReferenceOrganismsSection", () => ({
  ReferenceOrganismsSection: () => <div data-testid="reference-organisms" />
}));
vi.mock("./WorkingStandardCatalogSection", () => ({
  WorkingStandardCatalogSection: () => <div data-testid="working-standards" />
}));
vi.mock("../../../hooks/useOrganisms", () => ({ useOrganisms: () => ({ options: [] }) }));

afterEach(cleanup);

function SearchProbe() {
  return <div data-testid="search">{useLocation().search}</div>;
}

function renderAt(path: string) {
  render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[path]}>
        <MasterTestingMaterialsPage />
        <SearchProbe />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("MasterTestingMaterialsPage", () => {
  it("lists the five tabs in order", () => {
    renderAt("/laboratory-configuration/master-testing-materials");
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual([
      "Reagents & Indicators",
      "Reference & Primary Standards",
      "Culture Media",
      "Reference Organisms",
      "Other Materials"
    ]);
  });

  it("opens on Reagents & Indicators, embedded and limited to its categories", () => {
    renderAt("/laboratory-configuration/master-testing-materials");
    expect(screen.getByRole("tab", { name: "Reagents & Indicators" }).getAttribute("aria-selected")).toBe("true");
    expect(screen.getByTestId("material-master").textContent).toBe("Reagent,Indicator");
    expect(screen.getByTestId("material-master").getAttribute("data-embedded")).toBe("true");
  });

  it("selects the tab named by ?tab= and mounts the matching screen", () => {
    renderAt("/x?tab=standards");
    expect(screen.getByRole("tab", { name: "Reference & Primary Standards" }).getAttribute("aria-selected")).toBe("true");
    expect(screen.getByTestId("material-master").textContent).toBe("ReferenceStandard,PrimaryStandard");
    expect(screen.getByTestId("working-standards")).toBeTruthy();
  });

  it.each([
    ["media", "media-configuration"],
    ["organisms", "reference-organisms"],
    ["other", "material-master"]
  ])("?tab=%s mounts %s", (tab, testId) => {
    renderAt(`/x?tab=${tab}`);
    expect(screen.getByTestId(testId)).toBeTruthy();
  });

  it("falls back to the first tab for an unknown ?tab=", () => {
    renderAt("/x?tab=nope");
    expect(screen.getByRole("tab", { name: "Reagents & Indicators" }).getAttribute("aria-selected")).toBe("true");
  });

  it("keeps the selected tab in the URL and keeps species reachable", async () => {
    renderAt("/x");
    await userEvent.click(screen.getByRole("tab", { name: "Reference Organisms" }));
    expect(screen.getByTestId("search").textContent).toBe("?tab=organisms");
    expect(screen.getByTestId("reference-organisms")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Species list" }).getAttribute("href")).toBe("/laboratory-configuration/organisms");
  });

  it("the Other Materials tab limits the master to the Other category", () => {
    renderAt("/x?tab=other");
    expect(screen.getByTestId("material-master").textContent).toBe("Other");
  });
});
