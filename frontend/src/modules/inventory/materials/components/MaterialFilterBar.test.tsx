import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MaterialFilterBar } from "./MaterialFilterBar";
import { MaterialService } from "../services/MaterialService";
import type { MaterialFilterState, MaterialItem } from "../types/materialTypes";

vi.mock("../services/MaterialService", () => ({
  MaterialService: {
    getTypeOptions: vi.fn()
  }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const defaultFilters: MaterialFilterState = {
  search: "",
  materialType: "",
  manufacturer: "",
  location: "",
  status: "",
  expiryRange: ""
};

describe("MaterialFilterBar", () => {
  it("shows 'Primary Standard' and not 'Dehydrated Media' when type-options returns the FP list", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({
      builtIn: [
        "Chemical",
        "Indicator",
        "ReferenceBuffer",
        "ReferenceStandard",
        "PrimaryStandard",
        "Other"
      ],
      custom: []
    });

    render(
      <MaterialFilterBar
        items={[]}
        filters={defaultFilters}
        onFilterChange={vi.fn()}
        onReset={vi.fn()}
        sectionId={2}
      />
    );

    await waitFor(() => {
      expect(MaterialService.getTypeOptions).toHaveBeenCalledWith(2);
    });

    // Open the Type select dropdown
    const typeSelect = screen.getByRole("combobox", { name: "Type" });
    await userEvent.click(typeSelect);

    await waitFor(() => {
      expect(screen.getByRole("option", { name: "Primary Standard" })).toBeTruthy();
    });

    expect(screen.queryByRole("option", { name: "Dehydrated Media" })).toBeNull();
  });

  it("lists Other material, and keeps legacy types and categories filterable while such lots are in stock", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["Chemical", "Other"], custom: [] });
    const items = [
      { id: 1, materialType: "Supplement", customType: null, materialCategoryId: null, categoryName: null },
      { id: 2, materialType: "Other", customType: null, materialCategoryId: 4, categoryName: "Filter Membrane" }
    ] as MaterialItem[];

    render(
      <MaterialFilterBar items={items} filters={defaultFilters} onFilterChange={vi.fn()} onReset={vi.fn()} sectionId={1} />
    );
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalledWith(1));
    await userEvent.click(screen.getByRole("combobox", { name: "Type" }));

    expect(screen.getByRole("option", { name: "Other material" })).toBeTruthy();
    expect(screen.getByRole("option", { name: "Supplement (earlier lots)" })).toBeTruthy();
    expect(screen.getByRole("option", { name: "Category: Filter Membrane" })).toBeTruthy();
    // No lot of this legacy type is in stock.
    expect(screen.queryByRole("option", { name: /Antibiotic Disc/ })).toBeNull();
  });

  it("filters by a legacy type", async () => {
    vi.mocked(MaterialService.getTypeOptions).mockResolvedValue({ builtIn: ["Chemical", "Other"], custom: [] });
    const onFilterChange = vi.fn();
    const items = [{ id: 1, materialType: "Supplement", customType: null }] as MaterialItem[];

    render(
      <MaterialFilterBar items={items} filters={defaultFilters} onFilterChange={onFilterChange} onReset={vi.fn()} />
    );
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalled());
    await userEvent.click(screen.getByRole("combobox", { name: "Type" }));
    await userEvent.click(screen.getByRole("option", { name: "Supplement (earlier lots)" }));

    expect(onFilterChange).toHaveBeenCalledWith({ ...defaultFilters, materialType: "Supplement" });
  });
});
