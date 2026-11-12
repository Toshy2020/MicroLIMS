import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { OrganismPicker } from "./OrganismPicker";
import type { OrganismOption } from "../hooks/useOrganisms";
import { masterDataOptions } from "../services/masterDataOptions";

// Only the HTTP service layer is mocked; the real picker and useOrganisms
// hook run (CLAUDE.md Rule 4.5).
vi.mock("../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: { ...actual.masterDataOptions, getOrganisms: vi.fn(), createOrganism: vi.fn() }
  };
});

const NO_SUCH_ORGANISM = "No such organism. Add it in Master Testing Materials first.";

const organisms: OrganismOption[] = [
  { id: 1, scientificName: "Escherichia coli", atccNumber: "8739", commonName: null, description: null },
  { id: 2, scientificName: "Bacillus subtilis", atccNumber: null, commonName: null, description: null }
];

function renderPicker(props?: Partial<React.ComponentProps<typeof OrganismPicker>>) {
  const onChange = vi.fn();
  render(
    <PinnedLightTheme>
      <OrganismPicker value={null} onChange={onChange} {...props} />
    </PinnedLightTheme>
  );
  return { onChange, user: userEvent.setup() };
}

describe("OrganismPicker", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(masterDataOptions.getOrganisms).mockResolvedValue(organisms);
  });

  afterEach(cleanup);

  it("offers the existing organisms with their ATCC number", async () => {
    const { user } = renderPicker();

    await user.click(screen.getByRole("combobox", { name: "Organism" }));

    expect(await screen.findByRole("option", { name: "Escherichia coli (ATCC: 8739)" })).toBeTruthy();
    expect(screen.getByRole("option", { name: "Bacillus subtilis" })).toBeTruthy();
  });

  it("chooses an existing organism by its id", async () => {
    const { user, onChange } = renderPicker();

    await user.click(screen.getByRole("combobox", { name: "Organism" }));
    await user.click(await screen.findByRole("option", { name: "Bacillus subtilis" }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith(2);
  });

  it("shows the selected organism and clears it with null", async () => {
    const { user, onChange } = renderPicker({ value: 1 });

    const input = await screen.findByDisplayValue("Escherichia coli (ATCC: 8739)");
    expect(input).toBeTruthy();
    // Emptying the field is how a user clears a single-select picker.
    await user.clear(input);

    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("says so when typed text matches no organism, and offers no add option", async () => {
    const { user } = renderPicker();

    await user.type(screen.getByRole("combobox", { name: "Organism" }), "Zzz unknown");

    expect(await screen.findByText(NO_SUCH_ORGANISM)).toBeTruthy();
    expect(screen.queryByRole("option", { name: /Add/ })).toBeNull();
  });

  it("typed text plus Enter creates nothing and chooses nothing", async () => {
    const { user, onChange } = renderPicker();

    await user.type(screen.getByRole("combobox", { name: "Organism" }), "Zzz unknown{Enter}");

    expect(masterDataOptions.createOrganism).not.toHaveBeenCalled();
    expect(onChange).not.toHaveBeenCalled();
  });
});
