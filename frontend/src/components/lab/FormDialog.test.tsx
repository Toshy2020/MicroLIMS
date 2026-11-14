import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { TextField } from "@mui/material";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { FormDialog } from "./FormDialog";

describe("FormDialog", () => {
  it("FormDialog focuses the first empty required field", async () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <FormDialog open title="New item" onClose={vi.fn()} onSubmit={vi.fn()} submitLabel="Save">
            <TextField label="Optional notes" />
            <TextField label="Filled required" required defaultValue="already here" />
            <TextField label="First empty required" required />
            <TextField label="Second empty required" required />
          </FormDialog>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const firstEmpty = screen.getByLabelText(/First empty required/);
    await waitFor(() => expect(document.activeElement).toBe(firstEmpty));
  });

  it("'none' leaves focus alone", async () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <FormDialog open title="New item" onClose={vi.fn()} onSubmit={vi.fn()} submitLabel="Save" initialFocus="none">
            <TextField label="First empty required" required />
          </FormDialog>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const field = screen.getByLabelText(/First empty required/);
    // The default moves focus one animation frame after opening; let several frames pass before checking.
    for (let i = 0; i < 3; i++) await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    expect(document.activeElement).not.toBe(field);
  });

  it("does not steal focus if a field inside already has autoFocus", async () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <FormDialog
            open
            title="New item"
            onClose={vi.fn()}
            onSubmit={vi.fn()}
            submitLabel="Save"
          >
            <TextField label="First empty required" required />
            <TextField label="Explicitly focused" autoFocus />
          </FormDialog>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    const explicit = screen.getByLabelText(/Explicitly focused/);
    await waitFor(() => expect(document.activeElement).toBe(explicit));
    // The dialog's own focus move runs a frame later; it must not take focus away.
    for (let i = 0; i < 3; i++) await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    expect(document.activeElement).toBe(explicit);
  });
});
