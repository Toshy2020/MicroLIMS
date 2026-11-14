import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { BenchModeProvider } from "../../../../theme/BenchModeContext";
import { HelpPanelProvider } from "../../../help/HelpPanelContext";
import { AddMediaProductDialog } from "./AddMediaProductDialog";
import { hints } from "../hints";

vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: { createMediaProduct: vi.fn() }
}));

describe("AddMediaProductDialog field hint", () => {
  it("describes the product code by both its helper text and its hint", () => {
    render(
      <BenchModeProvider>
        <PinnedLightTheme>
          <MemoryRouter>
            <HelpPanelProvider>
              <AddMediaProductDialog open onClose={() => {}} onSuccess={() => {}} />
            </HelpPanelProvider>
          </MemoryRouter>
        </PinnedLightTheme>
      </BenchModeProvider>
    );
    const input = screen.getByRole("textbox", { name: /Product Code/ });
    const description = (input.getAttribute("aria-describedby") ?? "")
      .split(/\s+/)
      .map((id) => document.getElementById(id)?.textContent ?? "")
      .join(" ");
    expect(description).toContain("2-10 letters, digits, dot or hyphen");
    expect(description).toContain(hints.productCode.text);
  });
});
