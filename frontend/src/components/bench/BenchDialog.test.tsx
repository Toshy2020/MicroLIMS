import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { BenchDialog } from "./BenchDialog";
import { NumericEntryField } from "./NumericEntryField";
import { renderWithTheme } from "./benchTestUtils";

const release = vi.fn(() => Promise.resolve());
const request = vi.fn(() => Promise.resolve({ release }));

beforeEach(() => {
  release.mockClear();
  request.mockClear();
  Object.defineProperty(navigator, "wakeLock", { configurable: true, value: { request } });
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  Reflect.deleteProperty(navigator, "wakeLock");
});

function Host({ dirty, onClose, onSave = () => {} }: { dirty: boolean; onClose: () => void; onSave?: () => void }) {
  const [value, setValue] = useState("");
  return (
    <BenchDialog open title="Record counts" onClose={onClose} onSave={onSave} saving={false} dirty={dirty}>
      <NumericEntryField label="Plate 1" value={value} onChange={setValue} kind="count" />
    </BenchDialog>
  );
}

describe("BenchDialog", () => {
  it("is full-screen in bench mode and a normal dialog otherwise", () => {
    renderWithTheme(<Host dirty={false} onClose={() => {}} />, { bench: true });
    expect(screen.getByRole("dialog").className).toContain("MuiDialog-paperFullScreen");
    cleanup();
    renderWithTheme(<Host dirty={false} onClose={() => {}} />, { bench: false });
    expect(screen.getByRole("dialog").className).not.toContain("MuiDialog-paperFullScreen");
  });

  it("shows the keypad only in bench mode", () => {
    renderWithTheme(<Host dirty={false} onClose={() => {}} />, { bench: true });
    expect(screen.getByRole("group", { name: "Number keypad" })).toBeTruthy();
    cleanup();
    renderWithTheme(<Host dirty={false} onClose={() => {}} />, { bench: false });
    expect(screen.queryByRole("group", { name: "Number keypad" })).toBeNull();
  });

  it("Escape closes directly when clean", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    renderWithTheme(<Host dirty={false} onClose={onClose} />, { bench: true });
    await user.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("Escape asks before closing when dirty; Cancel is focused and keeps editing", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    renderWithTheme(<Host dirty onClose={onClose} />, { bench: true });
    await user.keyboard("{Escape}");
    const confirm = await screen.findByRole("alertdialog");
    expect(onClose).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(within(confirm).getByRole("button", { name: "Cancel" }));
    await user.click(within(confirm).getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("alertdialog")).toBeNull());
    expect(onClose).not.toHaveBeenCalled();
  });

  it("the Cancel button also asks when dirty, and confirming closes", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    renderWithTheme(<Host dirty onClose={onClose} />, { bench: true });
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    const confirm = await screen.findByRole("alertdialog");
    expect(onClose).not.toHaveBeenCalled();
    await user.click(within(confirm).getByRole("button", { name: "Discard" }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("Save calls onSave", async () => {
    const user = userEvent.setup();
    const onSave = vi.fn();
    renderWithTheme(<Host dirty onClose={() => {}} onSave={onSave} />, { bench: true });
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(onSave).toHaveBeenCalledTimes(1);
  });

  it("requests a screen wake lock while open and releases it on close", async () => {
    const { unmount } = renderWithTheme(<Host dirty={false} onClose={() => {}} />, { bench: true });
    await waitFor(() => expect(request).toHaveBeenCalledWith("screen"));
    unmount();
    await waitFor(() => expect(release).toHaveBeenCalled());
  });
});
