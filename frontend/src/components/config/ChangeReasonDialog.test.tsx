import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { ChangeReasonDialog } from "./ChangeReasonDialog";

function renderDialog(overrides: Partial<Parameters<typeof ChangeReasonDialog>[0]> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  render(
    <PinnedLightTheme>
      <ChangeReasonDialog
        open
        onConfirm={onConfirm}
        onCancel={onCancel}
        {...overrides}
      />
    </PinnedLightTheme>
  );
  return { onConfirm, onCancel };
}

describe("ChangeReasonDialog", () => {
  it("enforces 10-character minimum after trimming, shows a counter, and passes trimmed text", async () => {
    const user = userEvent.setup();
    const { onConfirm } = renderDialog();

    const confirmBtn = screen.getByRole("button", { name: "Confirm" }) as HTMLButtonElement;
    const input = screen.getByRole("textbox", { name: /Reason for change/ });

    expect(confirmBtn.disabled).toBe(true);
    expect(screen.getByText("0 / 10 characters minimum")).toBeTruthy();

    await user.type(input, "   short   ");
    expect(screen.getByText("5 / 10 characters minimum")).toBeTruthy();
    expect(confirmBtn.disabled).toBe(true);

    await user.clear(input);
    await user.type(input, "  123456789  ");
    expect(screen.getByText("9 / 10 characters minimum")).toBeTruthy();
    expect(confirmBtn.disabled).toBe(true);

    await user.clear(input);
    await user.type(input, "  Updated limit per CAPA-12  ");
    expect(screen.getByText("25 / 500 characters")).toBeTruthy();
    expect(confirmBtn.disabled).toBe(false);

    await user.click(confirmBtn);
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onConfirm).toHaveBeenCalledWith("Updated limit per CAPA-12");
  });

  it("calls onCancel on Cancel button or Escape", async () => {
    const user = userEvent.setup();
    const { onCancel } = renderDialog();

    await user.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onCancel).toHaveBeenCalledTimes(2);
  });
});
