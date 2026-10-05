import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MaterialFilterBar } from "./MaterialFilterBar";
import { MaterialService } from "../services/MaterialService";
import type { MaterialFilterState } from "../types/materialTypes";

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
});
