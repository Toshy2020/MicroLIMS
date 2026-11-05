import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { ItemPreparationConfigurationSection } from "./ItemPreparationConfigurationSection";

const auth = vi.hoisted(() => ({ permissions: [] as string[] }));
vi.mock("../../../../contexts/AuthContext", () => ({ useAuth: () => auth }));
vi.mock("../../../testPreparation/PreparationStepsSummary", () => ({ PreparationStepsSummary: () => null }));
const svc = vi.hoisted(() => ({ get: vi.fn(), save: vi.fn() }));
vi.mock("../../../testPreparation/services/ItemPreparationConfigurationService", () => ({
  ItemPreparationConfigurationService: svc,
}));
vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getMediaProducts: vi.fn().mockResolvedValue([
      { id: 5, name: "Tryptic Soy Broth", code: "TSB", isActive: true },
      { id: 6, name: "Retired", code: "OLD", isActive: false },
    ]),
  },
}));
vi.mock("../../masterDataSimple/services/SolutionMasterService", () => ({
  SolutionMasterService: {
    getAll: vi.fn().mockResolvedValue([
      { id: 11, name: "Saline", type: "Diluent", isActive: true },
      { id: 12, name: "Mobile phase", type: "MobilePhase", isActive: true },
    ]),
  },
}));
vi.mock("../../masterDataSimple/services/MaterialMasterService", () => ({
  MaterialMasterService: {
    getAll: vi.fn().mockResolvedValue([{ id: 21, name: "Polysorbate 80", code: "RG-21", isActive: true }]),
  },
}));

const legacyConfig = {
  itemId: 1, amount: 10, technique: "PourPlate", diluent: "Peptone", neutralizer: "Tween", approvalStatus: "PendingReview",
  diluentSource: null, neutralizerSource: null, needsMaterialMapping: true,
};

beforeEach(() => {
  svc.get.mockReset().mockResolvedValue(legacyConfig);
  svc.save.mockReset().mockResolvedValue({ ...legacyConfig, needsMaterialMapping: false });
});
afterEach(cleanup);

async function renderSection(permissions: string[]) {
  auth.permissions = permissions;
  render(<ThemeProvider theme={lightTheme}><ItemPreparationConfigurationSection itemId={1} itemName="Item A" /></ThemeProvider>);
  await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
}

describe("ItemPreparationConfigurationSection", () => {
  it("hides Edit and Approve from a user who cannot manage items (the QC Manager)", async () => {
    await renderSection(["tests.not-applicable.final-approve"]);
    expect(screen.queryByText("Edit")).toBeNull();
    expect(screen.queryByText("Approve")).toBeNull();
  });

  it("shows Edit and Approve to a user holding Items.Manage", async () => {
    await renderSection(["Items.Manage"]);
    expect(screen.getByText("Edit")).toBeTruthy();
    expect(screen.getByText("Approve")).toBeTruthy();
  });

  it("warns when the diluent/neutralizer is not linked to master data", async () => {
    await renderSection(["Items.Manage"]);
    expect(screen.getByText(/not linked to master data — edit and pick them so the lot used can be recorded/)).toBeTruthy();
  });

  it("shows the resolved name and source in view mode without a warning when mapped", async () => {
    svc.get.mockResolvedValue({ ...legacyConfig, needsMaterialMapping: false, diluentSource: "MediaProduct" });
    await renderSection(["Items.Manage"]);
    expect(screen.queryByText(/not linked to master data/)).toBeNull();
  });

  async function pick(name: RegExp, option: string) {
    // The master list is disabled while it loads.
    await waitFor(() => expect(screen.getByRole("combobox", { name }).getAttribute("aria-disabled")).not.toBe("true"));
    fireEvent.mouseDown(screen.getByRole("combobox", { name }));
    fireEvent.click(await screen.findByRole("option", { name: option }));
  }

  async function configure(diluent: [string, string?], neutralizer: [string, string?]) {
    svc.get.mockResolvedValue(null);
    await renderSection(["Items.Manage"]);
    fireEvent.click(screen.getByText("Configure Preparation"));
    fireEvent.change(screen.getByLabelText(/Sample Amount/), { target: { value: "10" } });
    await pick(/Diluent source/, diluent[0]);
    if (diluent[1]) await pick(/^Diluent$/, diluent[1]);
    await pick(/Neutralizer source/, neutralizer[0]);
    if (neutralizer[1]) await pick(/^Neutralizer$/, neutralizer[1]);
    fireEvent.click(screen.getByText("Save Configuration"));
    await waitFor(() => expect(svc.save).toHaveBeenCalled());
    return svc.save.mock.calls[0][1];
  }

  it("saves a media product diluent and a neutralizer contained in the medium, without text fields", async () => {
    const payload = await configure(["Media product", "Tryptic Soy Broth (TSB)"], ["Contained in the medium"]);
    expect(payload).toMatchObject({
      amount: 10, technique: "PourPlate",
      diluentSource: "MediaProduct", diluentMediaProductId: 5, diluentSolutionMasterId: null, diluentMaterialMasterEntryId: null,
      neutralizerSource: "ContainedInMedium", neutralizerMediaProductId: null,
      neutralizerSolutionMasterId: null, neutralizerMaterialMasterEntryId: null,
    });
    expect(payload).not.toHaveProperty("diluent");
    expect(payload).not.toHaveProperty("neutralizer");
  });

  it("saves a solution recipe diluent and a material entry neutralizer", async () => {
    const payload = await configure(["Solution recipe", "Saline (Diluent)"], ["Material entry", "Polysorbate 80 (RG-21)"]);
    expect(payload).toMatchObject({
      diluentSource: "SolutionRecipe", diluentSolutionMasterId: 11, diluentMediaProductId: null,
      neutralizerSource: "MaterialEntry", neutralizerMaterialMasterEntryId: 21, neutralizerSolutionMasterId: null,
    });
  });

  it("offers only active media products and the Diluent/Reagent/Neutralizer solutions", async () => {
    svc.get.mockResolvedValue(null);
    await renderSection(["Items.Manage"]);
    fireEvent.click(screen.getByText("Configure Preparation"));
    await pick(/Diluent source/, "Media product");
    await waitFor(() => expect(screen.getByRole("combobox", { name: /^Diluent$/ }).getAttribute("aria-disabled")).not.toBe("true"));
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /^Diluent$/ }));
    expect(await screen.findByRole("option", { name: "Tryptic Soy Broth (TSB)" })).toBeTruthy();
    expect(screen.queryByRole("option", { name: /Retired/ })).toBeNull();
  });

  it("offers 'Contained in the medium' for the neutralizer only", async () => {
    svc.get.mockResolvedValue(null);
    await renderSection(["Items.Manage"]);
    fireEvent.click(screen.getByText("Configure Preparation"));
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Diluent source/ }));
    expect(screen.queryByRole("option", { name: "Contained in the medium" })).toBeNull();
    fireEvent.keyDown(screen.getByRole("listbox"), { key: "Escape" });
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Neutralizer source/ }));
    expect(await screen.findByRole("option", { name: "Contained in the medium" })).toBeTruthy();
  });
});
