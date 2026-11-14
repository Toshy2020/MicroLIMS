import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { ShortcutsDialog } from "./ShortcutsDialog";

describe("ShortcutsDialog", () => {
  it("lists all grid and global keyboard shortcuts with accessible structure", () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <ShortcutsDialog open onClose={vi.fn()} />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const dialog = screen.getByRole("dialog", { name: "Keyboard shortcuts" });
    const title = within(dialog).getByText("Keyboard shortcuts");
    expect(title.tagName).toBe("SPAN");

    // All key combinations from spec 5.1 + global shortcuts
    expect(within(dialog).getByText("Enter / Down")).toBeTruthy();
    expect(within(dialog).getByText("Ctrl+D")).toBeTruthy();
    expect(within(dialog).getByText("Ctrl+Enter")).toBeTruthy();
    expect(within(dialog).getByText("Escape")).toBeTruthy();
    expect(within(dialog).getByText("Ctrl+K")).toBeTruthy();
    expect(within(dialog).getByText("?")).toBeTruthy();
  });
});
