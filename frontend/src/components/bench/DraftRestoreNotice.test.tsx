import { describe, expect, it, vi } from "vitest";
import { screen, waitForElementToBeRemoved, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DraftRestoreNotice } from "./DraftRestoreNotice";
import { renderWithTheme } from "./benchTestUtils";

describe("DraftRestoreNotice", () => {
  it("shows nothing when there is no draft to offer", () => {
    renderWithTheme(<DraftRestoreNotice startedAt={null} onRestore={vi.fn()} onDiscard={vi.fn()} />, { bench: true });
    expect(screen.queryByRole("region", { name: "Unsaved entry found" })).toBeNull();
  });

  it("states the entry is only on the tablet and offers it back only on Restore", async () => {
    const user = userEvent.setup();
    const onRestore = vi.fn();
    renderWithTheme(<DraftRestoreNotice startedAt="2031-03-04T05:00:00.000Z" onRestore={onRestore} onDiscard={vi.fn()} />, { bench: true });

    const notice = screen.getByRole("region", { name: "Unsaved entry found" });
    expect(notice.textContent).toContain("has not been saved to MicroLIMS");
    expect(notice.textContent).toContain("(tablet clock)");
    expect(onRestore).not.toHaveBeenCalled();

    await user.click(within(notice).getByRole("button", { name: "Restore entry" }));
    expect(onRestore).toHaveBeenCalledTimes(1);
  });

  it("Discard asks for confirmation with Cancel focused and only discards when confirmed", async () => {
    const user = userEvent.setup();
    const onDiscard = vi.fn();
    renderWithTheme(<DraftRestoreNotice startedAt="2031-03-04T05:00:00.000Z" onRestore={vi.fn()} onDiscard={onDiscard} />, { bench: true });

    await user.click(screen.getByRole("button", { name: "Discard" }));
    const dialog = await screen.findByRole("alertdialog");
    expect(within(dialog).getByRole("button", { name: "Cancel" })).toBe(document.activeElement);
    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    await waitForElementToBeRemoved(() => screen.queryByRole("alertdialog"));
    expect(onDiscard).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Discard" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Discard" }));
    expect(onDiscard).toHaveBeenCalledTimes(1);
  });
});
