import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ChromatographyColumnsPage, chromatographyTypeLabel } from "./ChromatographyColumnsPage";
import { EquipmentConfigurationService } from "./services/EquipmentConfigurationService";

vi.mock("./services/ChromatographyColumnService", () => ({
  ChromatographyColumnService: { getAll: vi.fn().mockResolvedValue([]) }
}));
vi.mock("./services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getEquipmentList: vi.fn(async (type?: string) =>
      type === "Hplc"
        ? [{ id: 1, code: "HPLC-01", name: "Shimadzu LC", type: "Hplc", sectionId: 5 }]
        : type === "Gc"
          ? [{ id: 2, code: "GC-01", name: "Agilent GC", type: "Gc", sectionId: 5 }]
          : [])
  }
}));
vi.mock("../../../hooks/useLaboratorySections", () => ({
  useLaboratorySections: () => ({ sections: [], sectionName: () => "FP" })
}));
vi.mock("../../../services/laboratorySectionService", () => ({
  getMySections: vi.fn().mockResolvedValue([{ sectionId: 5, sectionName: "FP" }])
}));

afterEach(cleanup);

describe("ChromatographyColumnsPage", () => {
  it("labels HPLC and GC instrument types", () => {
    expect(chromatographyTypeLabel("Gc")).toBe("GC");
    expect(chromatographyTypeLabel("Hplc")).toBe("HPLC");
    expect(chromatographyTypeLabel(undefined)).toBe("");
  });

  it("offers both HPLC and GC instruments as compatible equipment", async () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <ChromatographyColumnsPage />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const add = await screen.findAllByRole("button", { name: /add column/i });
    await userEvent.click(add[0]);

    expect(EquipmentConfigurationService.getEquipmentList).toHaveBeenCalledWith("Hplc");
    expect(EquipmentConfigurationService.getEquipmentList).toHaveBeenCalledWith("Gc");

    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("combobox", { name: /compatible instruments/i }));

    const listbox = await screen.findByRole("listbox");
    expect(within(listbox).getByText("[GC] GC-01 - Agilent GC")).toBeTruthy();
    expect(within(listbox).getByText("[HPLC] HPLC-01 - Shimadzu LC")).toBeTruthy();
  });
});
