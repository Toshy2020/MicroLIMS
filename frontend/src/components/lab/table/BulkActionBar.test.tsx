import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { BulkActionBar, type BulkAction } from "./BulkActionBar";
import type { RowSelection } from "./useRowSelection";

function makeSelection(ids: string[], allMatching = false, total = ids.length): RowSelection<string> {
  const set = new Set(ids);
  return {
    selected: set,
    allMatching,
    count: allMatching ? total : set.size,
    isSelected: (id) => allMatching || set.has(id),
    toggle: vi.fn(),
    togglePage: vi.fn(),
    selectAllMatching: vi.fn(),
    clear: vi.fn(),
  };
}

afterEach(() => {
  cleanup();
});

describe("BulkActionBar", () => {
  it('action bar shows "14 selected" and announces changes politely; Clear empties it', async () => {
    const user = userEvent.setup();
    const ids = Array.from({ length: 14 }, (_, i) => `ID-${i + 1}`);
    const selection = makeSelection(ids);
    const run = vi.fn();
    const actions: BulkAction<string>[] = [
      { key: "assign", label: "Assign analyst", run },
    ];

    localStorage.setItem("microlims_permissions", JSON.stringify([]));
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <BulkActionBar selection={selection} actions={actions} />
        </AuthProvider>
      </PinnedLightTheme>
    );

    const status = screen.getByRole("status");
    expect(status.getAttribute("aria-live")).toBe("polite");
    expect(status.textContent).toContain("14 selected");

    await user.click(screen.getByRole("button", { name: "Clear" }));
    expect(selection.clear).toHaveBeenCalledTimes(1);
  });

  it('ineligible rows are counted: "12 of 14 can be …"', async () => {
    const user = userEvent.setup();
    const ids = Array.from({ length: 14 }, (_, i) => `ID-${i + 1}`);
    const selection = makeSelection(ids);
    const run = vi.fn();
    const actions: BulkAction<string>[] = [
      {
        key: "assign",
        label: "Assign analyst",
        eligible: (id) => id !== "ID-13" && id !== "ID-14",
        ineligibleReason: "2 are already approved.",
        run,
      },
    ];

    localStorage.setItem("microlims_permissions", JSON.stringify([]));
    render(
      <PinnedLightTheme>
        <AuthProvider>
          <BulkActionBar selection={selection} actions={actions} />
        </AuthProvider>
      </PinnedLightTheme>
    );

    expect(screen.getByText(/12 of 14 can be assigned/i)).toBeTruthy();
    expect(screen.getByText(/2 are already approved\./i)).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Assign analyst" }));
    expect(run).toHaveBeenCalledWith(ids.slice(0, 12));
  });
});
