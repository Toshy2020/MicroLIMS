import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import userEvent, { PointerEventsCheckLevel } from "@testing-library/user-event";
import { BenchActionBar } from "./BenchActionBar";
import { renderWithTheme } from "./benchTestUtils";

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("BenchActionBar", () => {
  it("Save is disabled with its reason shown as visible text, and Cancel works", async () => {
    // A disabled button has pointer-events: none; the click is attempted anyway to prove it does nothing.
    const user = userEvent.setup({ pointerEventsCheck: PointerEventsCheckLevel.Never });
    const onSave = vi.fn();
    const onCancel = vi.fn();
    renderWithTheme(<BenchActionBar onCancel={onCancel} onSave={onSave} saving={false} saveDisabledReason="Enter all plate counts first" />, { bench: true });
    const save = screen.getByRole("button", { name: "Save" });
    expect((save as HTMLButtonElement).disabled).toBe(true);
    const reason = screen.getByText("Enter all plate counts first");
    expect(save.getAttribute("aria-describedby")).toBe(reason.id);
    await user.click(save);
    expect(onSave).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it("saving shows a spinner and blocks a second submit", async () => {
    const user = userEvent.setup({ pointerEventsCheck: PointerEventsCheckLevel.Never });
    const onSave = vi.fn();
    const { rerender } = renderWithTheme(<BenchActionBar onCancel={() => {}} onSave={onSave} saving={false} />, { bench: true });
    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(onSave).toHaveBeenCalledTimes(1);
    rerender(<BenchActionBar onCancel={() => {}} onSave={onSave} saving />);
    expect(screen.getByRole("progressbar")).toBeTruthy();
    const save = screen.getByRole("button", { name: /Save/ });
    expect((save as HTMLButtonElement).disabled).toBe(true);
    await user.click(save);
    expect(onSave).toHaveBeenCalledTimes(1);
  });

  it("offers Save and next when provided, with a custom save label", async () => {
    const user = userEvent.setup();
    const onSaveAndNext = vi.fn();
    renderWithTheme(<BenchActionBar onCancel={() => {}} onSave={() => {}} onSaveAndNext={onSaveAndNext} saving={false} saveLabel="Record count" />, { bench: true });
    expect(screen.getByRole("button", { name: "Record count" })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Save and next" }));
    expect(onSaveAndNext).toHaveBeenCalledTimes(1);
  });

  it("renders the keypad slot", () => {
    renderWithTheme(<BenchActionBar onCancel={() => {}} onSave={() => {}} saving={false} keypad={<div>keypad here</div>} />, { bench: true });
    expect(screen.getByText("keypad here")).toBeTruthy();
  });
});
