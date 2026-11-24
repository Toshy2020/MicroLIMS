import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { MediaConfigurationDialog } from "./MediaConfigurationDialog";
import { masterDataOptions, type MediaProductOption } from "../../../../services/masterDataOptions";

const organism = { id: 7, scientificName: "Escherichia coli", atccNumber: "8739" };

vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: {
      ...actual.masterDataOptions,
      getOrganisms: vi.fn(async () => [{ id: 7, scientificName: "Escherichia coli", atccNumber: "8739" }]),
      createMediaConfiguration: vi.fn(),
      updateMediaConfiguration: vi.fn()
    }
  };
});

beforeEach(() => vi.clearAllMocks());

describe("MediaConfigurationDialog", () => {
  it("blocks Save while a picked organism has not been added, instead of dropping it", async () => {
    const user = userEvent.setup();
    const product = { id: 1, name: "TSA", code: "TSA", isActive: true, hasStock: false, configurationCount: 0 } as MediaProductOption;
    render(
      <ThemeProvider theme={lightTheme}>
        <MediaConfigurationDialog
          open
          product={product}
          configToEdit={null}
          conditions={[{ id: 1, mediaProductId: 1, configurationCount: 0, stepMediaCount: 0 }] as never}
          organisms={[organism] as never}
          onClose={vi.fn()}
          onSuccess={vi.fn()}
        />
      </ThemeProvider>
    );
    await user.click(await screen.findByRole("combobox", { name: "Organism" }));
    await user.click(await screen.findByRole("option", { name: /Escherichia coli/ }));
    await user.click(screen.getByRole("button", { name: "Save Configuration" }));

    expect((await screen.findByRole("alert")).textContent).toMatch(/Add Organism/);
    expect(masterDataOptions.createMediaConfiguration).not.toHaveBeenCalled();
  });
});
